using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Complete
{
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

        // 0 = draw, 1 = player 1, 2 = player 2. Replicated so clients can build
        // the correct end-of-round message without relying on local death flags.
        private NetworkVariable<int> m_RoundWinner = new NetworkVariable<int>(
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
        private bool m_Tank1Dead;
        private bool m_Tank2Dead;

        private WaitForSeconds m_StartWait;
        private WaitForSeconds m_EndWait;

        public override void OnNetworkSpawn()
        {
            m_StartWait = new WaitForSeconds(m_StartDelay);
            m_EndWait = new WaitForSeconds(m_EndDelay);

            m_GameState.OnValueChanged += OnGameStateChanged;
            m_RoundNumber.OnValueChanged += OnRoundNumberChanged;
            m_Player1Wins.OnValueChanged += OnWinsChanged;
            m_Player2Wins.OnValueChanged += OnWinsChanged;

            if (IsServer)
                StartCoroutine(RunGameLoop());

            // Defer the initial UI refresh by one frame so NGO has time to replicate
            // the current NetworkVariable values to this client before we read them.
            StartCoroutine(RefreshUINextFrame());
        }

        public override void OnNetworkDespawn()
        {
            m_GameState.OnValueChanged -= OnGameStateChanged;
            m_RoundNumber.OnValueChanged -= OnRoundNumberChanged;
            m_Player1Wins.OnValueChanged -= OnWinsChanged;
            m_Player2Wins.OnValueChanged -= OnWinsChanged;
        }

        private IEnumerator RefreshUINextFrame()
        {
            yield return null;
            RefreshUI();
        }

        private void OnWinsChanged(int previous, int current) => RefreshUI();

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
                // Despawn tanks before leaving so NGO can clean up cleanly.
                // destroyWithScene is false, so they won't auto-destroy on scene unload.
                DespawnTank(ref m_Tank1);
                DespawnTank(ref m_Tank2);
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
            m_RoundWinner.Value = winner;

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

            m_Tank1Dead = false;
            m_Tank2Dead = false;

            IReadOnlyList<ulong> connectedIds = NetworkManager.Singleton.ConnectedClientsIds;
            ulong[] clientIds = new ulong[connectedIds.Count];
            for (int i = 0; i < connectedIds.Count; i++)
                clientIds[i] = connectedIds[i];

            if (m_SpawnPoints.Length < 2 || clientIds.Length < 2)
            {
                Debug.LogError("[OnlineGameManager] Not enough spawn points or clients.");
                return;
            }

            m_Tank1 = SpawnTank(m_SpawnPoints[0], clientIds[0], m_Player1Color, playerNumber: 1);
            m_Tank2 = SpawnTank(m_SpawnPoints[1], clientIds[1], m_Player2Color, playerNumber: 2);

            // Update camera targets on server and propagate NetworkObjectIds to all clients
            // so each client can resolve the local Transform references independently.
            ulong id1 = m_Tank1.GetComponent<NetworkObject>().NetworkObjectId;
            ulong id2 = m_Tank2.GetComponent<NetworkObject>().NetworkObjectId;
            m_CameraControl.m_Targets = new Transform[] { m_Tank1.transform, m_Tank2.transform };
            SetCameraTargetsClientRpc(id1, id2);
        }

        private GameObject SpawnTank(Transform spawnPoint, ulong ownerClientId, Color tankColor, int playerNumber)
        {
            GameObject tank = Instantiate(m_NetworkTankPrefab, spawnPoint.position, spawnPoint.rotation);
            NetworkObject networkObject = tank.GetComponent<NetworkObject>();
            // destroyWithScene: false — NGO manages the lifecycle during scene transitions.
            // Passing true would let Unity destroy the NetworkObject locally on the client
            // before NGO sends the despawn message, causing [Invalid Destroy] errors.
            networkObject.SpawnAsPlayerObject(ownerClientId, false);

            // Track death events to avoid querying activeSelf on potentially destroyed objects.
            NetworkTankHealth health = tank.GetComponent<NetworkTankHealth>();
            if (health != null)
            {
                if (playerNumber == 1)
                    health.OnTankDied += () => m_Tank1Dead = true;
                else
                    health.OnTankDied += () => m_Tank2Dead = true;
            }

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
        // Camera sync (runs on all clients via ClientRpc)
        // -------------------------------------------------------------------------

        [ClientRpc]
        private void SetCameraTargetsClientRpc(ulong tankId1, ulong tankId2)
        {
            // The server already set its own targets above; skip to avoid double-assign.
            if (IsServer)
                return;

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tankId1, out NetworkObject obj1) ||
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tankId2, out NetworkObject obj2))
            {
                Debug.LogError("[OnlineGameManager] Could not resolve tank NetworkObjects on client.");
                return;
            }

            m_CameraControl.m_Targets = new Transform[] { obj1.transform, obj2.transform };
            m_CameraControl.SetStartPositionAndSize();
        }

        // -------------------------------------------------------------------------
        // Control helpers
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
            // Use death flags instead of activeSelf to avoid querying potentially
            // destroyed or null GameObjects after despawn.
            int alive = 0;
            if (!m_Tank1Dead) alive++;
            if (!m_Tank2Dead) alive++;
            return alive <= 1;
        }

        /// <returns>1, 2, or 0 for draw, -1 if still playing.</returns>
        private int GetRoundWinner()
        {
            if (!m_Tank1Dead && m_Tank2Dead) return 1;
            if (m_Tank1Dead && !m_Tank2Dead) return 2;
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
            int winner = m_RoundWinner.Value;
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
}
