using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
/// <summary>
/// Server-authoritative game loop for online play.
/// Mirrors the local GameManager logic but uses NetworkBehaviour lifecycle
/// and NetworkVariables to sync round state across clients.
/// Only the server drives round transitions; clients receive state via RPC/NetworkVariable.
/// </summary>
public class OnlineGameManager : NetworkBehaviour
{
    [Header("Game Rules")]
    public int m_NumRoundsToWin = 5;
    public float m_StartDelay = 3f;
    public float m_EndDelay = 3f;

    [Header("References")]
    public CameraControl m_CameraControl;
    public Text m_MessageText;
    public GameObject m_NetworkTankPrefab;

    [Header("Spawn Points")]
    public Transform[] m_SpawnPoints;

    [Header("Tank Colors")]
    public Color m_Player1Color = Color.red;
    public Color m_Player2Color = Color.blue;

    private const string MainMenuScene = "MainMenu";

    private NetworkVariable<int> m_RoundNumber = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> m_Player1Wins = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> m_Player2Wins = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<NetworkedGameState> m_GameState = new NetworkVariable<NetworkedGameState>(
        NetworkedGameState.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GameObject m_Tank1;
    private GameObject m_Tank2;

    private WaitForSeconds m_StartWait;
    private WaitForSeconds m_EndWait;

    public override void OnNetworkSpawn()
    {
        m_StartWait = new WaitForSeconds(m_StartDelay);
        m_EndWait = new WaitForSeconds(m_EndDelay);

        m_GameState.OnValueChanged += OnGameStateChanged;
        m_RoundNumber.OnValueChanged += OnRoundNumberChanged;
        m_Player1Wins.OnValueChanged += (_, _) => RefreshUI();
        m_Player2Wins.OnValueChanged += (_, _) => RefreshUI();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        m_GameState.OnValueChanged -= OnGameStateChanged;
        m_RoundNumber.OnValueChanged -= OnRoundNumberChanged;
        m_Player1Wins.OnValueChanged -= (_, _) => RefreshUI();
        m_Player2Wins.OnValueChanged -= (_, _) => RefreshUI();

        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    // -------------------------------------------------------------------------
    // Connection callbacks (server only)
    // -------------------------------------------------------------------------

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer)
            return;

        // Start the game once both players are connected.
        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
            StartCoroutine(RunGameLoop());
    }

    // -------------------------------------------------------------------------
    // Game loop (server only)
    // -------------------------------------------------------------------------

    private IEnumerator RunGameLoop()
    {
        yield return StartCoroutine(RoundStarting());
        yield return StartCoroutine(RoundPlaying());
        yield return StartCoroutine(RoundEnding());

        if (GetGameWinner() != -1)
        {
            // Game is over — return to the main menu for all clients.
            NetworkManager.Singleton.SceneManager.LoadScene(MainMenuScene, LoadSceneMode.Single);
        }
        else
        {
            StartCoroutine(RunGameLoop());
        }
    }

    private IEnumerator RoundStarting()
    {
        m_GameState.Value = NetworkedGameState.RoundStarting;
        m_RoundNumber.Value++;

        SpawnTanks();
        SetTankControlEnabled(false);

        m_CameraControl.SetStartPositionAndSize();

        yield return m_StartWait;
    }

    private IEnumerator RoundPlaying()
    {
        m_GameState.Value = NetworkedGameState.RoundPlaying;
        SetTankControlEnabled(true);

        while (!OneTankLeft())
            yield return null;
    }

    private IEnumerator RoundEnding()
    {
        m_GameState.Value = NetworkedGameState.RoundEnding;
        SetTankControlEnabled(false);

        int winner = GetRoundWinner();
        if (winner == 1) m_Player1Wins.Value++;
        else if (winner == 2) m_Player2Wins.Value++;

        yield return m_EndWait;
    }

    // -------------------------------------------------------------------------
    // Tank spawning (server only)
    // -------------------------------------------------------------------------

    private void SpawnTanks()
    {
        if (!IsServer)
            return;

        // Despawn previous instances if they exist.
        DespawnTank(ref m_Tank1);
        DespawnTank(ref m_Tank2);

        IReadOnlyList<ulong> connectedIds = NetworkManager.Singleton.ConnectedClientsIds;
        ulong[] clientIds = new ulong[connectedIds.Count];
        for (int i = 0; i < connectedIds.Count; i++)
            clientIds[i] = connectedIds[i];

        if (m_SpawnPoints.Length < 2 || clientIds.Length < 2)
        {
            Debug.LogError("[OnlineGameManager] Not enough spawn points or clients.");
            return;
        }

        m_Tank1 = SpawnTank(m_SpawnPoints[0], clientIds[0], m_Player1Color);
        m_Tank2 = SpawnTank(m_SpawnPoints[1], clientIds[1], m_Player2Color);

        // Update camera targets.
        m_CameraControl.m_Targets = new Transform[] { m_Tank1.transform, m_Tank2.transform };
    }

    private GameObject SpawnTank(Transform spawnPoint, ulong ownerClientId, Color tankColor)
    {
        GameObject tank = Instantiate(m_NetworkTankPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkObject networkObject = tank.GetComponent<NetworkObject>();
        networkObject.SpawnAsPlayerObject(ownerClientId, true);

        // Apply color via ClientRpc after spawn.
        OnlineColorApplier colorApplier = tank.GetComponent<OnlineColorApplier>();
        if (colorApplier != null)
            colorApplier.ApplyColorClientRpc(tankColor.r, tankColor.g, tankColor.b);

        return tank;
    }

    private void DespawnTank(ref GameObject tank)
    {
        if (tank == null)
            return;

        NetworkObject netObj = tank.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);

        tank = null;
    }

    // -------------------------------------------------------------------------
    // Control helpers (server sends RPC to all clients)
    // -------------------------------------------------------------------------

    private void SetTankControlEnabled(bool enabled)
    {
        SetTankControl(m_Tank1, enabled);
        SetTankControl(m_Tank2, enabled);
    }

    private void SetTankControl(GameObject tank, bool enabled)
    {
        if (tank == null)
            return;

        NetworkTankMovement movement = tank.GetComponent<NetworkTankMovement>();
        if (movement != null) movement.SetControlEnabled(enabled);

        NetworkTankShooting shooting = tank.GetComponent<NetworkTankShooting>();
        if (shooting != null) shooting.SetControlEnabled(enabled);
    }

    // -------------------------------------------------------------------------
    // Round/game outcome helpers (server only)
    // -------------------------------------------------------------------------

    private bool OneTankLeft()
    {
        int alive = 0;
        if (m_Tank1 != null && m_Tank1.activeSelf) alive++;
        if (m_Tank2 != null && m_Tank2.activeSelf) alive++;
        return alive <= 1;
    }

    /// <returns>1, 2, or 0 for draw, -1 if still playing.</returns>
    private int GetRoundWinner()
    {
        bool tank1Alive = m_Tank1 != null && m_Tank1.activeSelf;
        bool tank2Alive = m_Tank2 != null && m_Tank2.activeSelf;

        if (tank1Alive && !tank2Alive) return 1;
        if (!tank1Alive && tank2Alive) return 2;
        return 0;
    }

    /// <returns>1, 2, or -1 if no winner yet.</returns>
    private int GetGameWinner()
    {
        if (m_Player1Wins.Value >= m_NumRoundsToWin) return 1;
        if (m_Player2Wins.Value >= m_NumRoundsToWin) return 2;
        return -1;
    }

    // -------------------------------------------------------------------------
    // UI updates (runs on all clients via NetworkVariable callbacks)
    // -------------------------------------------------------------------------

    private void OnGameStateChanged(NetworkedGameState previous, NetworkedGameState current)
    {
        RefreshUI();
    }

    private void OnRoundNumberChanged(int previous, int current)
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (m_MessageText == null)
            return;

        switch (m_GameState.Value)
        {
            case NetworkedGameState.WaitingForPlayers:
                m_MessageText.text = "Waiting for players...";
                break;

            case NetworkedGameState.RoundStarting:
                m_MessageText.text = $"ROUND {m_RoundNumber.Value}";
                break;

            case NetworkedGameState.RoundPlaying:
                m_MessageText.text = string.Empty;
                break;

            case NetworkedGameState.RoundEnding:
                m_MessageText.text = BuildEndRoundMessage();
                break;
        }
    }

    private string BuildEndRoundMessage()
    {
        int winner = GetRoundWinner();
        string header = winner == 0 ? "DRAW!" :
                        winner == 1 ? $"<color=#{ColorUtility.ToHtmlStringRGB(m_Player1Color)}>PLAYER 1</color> WINS THE ROUND!" :
                                      $"<color=#{ColorUtility.ToHtmlStringRGB(m_Player2Color)}>PLAYER 2</color> WINS THE ROUND!";

        string scores = $"\n\n\n\n" +
                        $"<color=#{ColorUtility.ToHtmlStringRGB(m_Player1Color)}>PLAYER 1</color>: {m_Player1Wins.Value} WINS\n" +
                        $"<color=#{ColorUtility.ToHtmlStringRGB(m_Player2Color)}>PLAYER 2</color>: {m_Player2Wins.Value} WINS\n";

        int gameWinner = GetGameWinner();
        if (gameWinner == 1)
            return $"<color=#{ColorUtility.ToHtmlStringRGB(m_Player1Color)}>PLAYER 1</color> WINS THE GAME!";
        if (gameWinner == 2)
            return $"<color=#{ColorUtility.ToHtmlStringRGB(m_Player2Color)}>PLAYER 2</color> WINS THE GAME!";

        return header + scores;
    }
}

/// <summary>State machine for the online game round flow.</summary>
public enum NetworkedGameState
{
    WaitingForPlayers,
    RoundStarting,
    RoundPlaying,
    RoundEnding
}
