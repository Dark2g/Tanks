using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Complete
{
    /// <summary>
    /// Manages online session lifecycle: UGS authentication, Relay allocation,
    /// Lobby creation/joining, and NGO host/client startup.
    /// Acts as a persistent singleton across scenes.
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        // Key used to store the Relay join code inside the Lobby data.
        private const string RelayJoinCodeKey = "RelayJoinCode";
        private const string GameSceneName = "_Online-Game";
        private const int MaxPlayers = 2;

        // Lobby heartbeat interval in seconds to keep it alive.
        private const float LobbyHeartbeatInterval = 15f;

        public string JoinCode { get; private set; }
        public bool IsInitialized { get; private set; }

        private Lobby m_CurrentLobby;
        private float m_HeartbeatTimer;
        private bool m_GameSceneLoaded;

        public event Action<string> OnError;
        public event Action OnSessionStarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            await InitializeUnityServicesAsync();
        }

        private void Update()
        {
            HandleLobbyHeartbeat();
        }

        // -------------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------------

        /// <summary>Initializes Unity Gaming Services and signs in anonymously.</summary>
        public async Task InitializeUnityServicesAsync()
        {
            if (IsInitialized)
                return;

            try
            {
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                IsInitialized = true;
                Debug.Log($"[NetworkGameManager] Signed in as {AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkGameManager] UGS initialization failed: {e.Message}");
                OnError?.Invoke($"Failed to connect to online services: {e.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // Host flow
        // -------------------------------------------------------------------------

        /// <summary>
        /// Creates a Relay allocation, gets a join code, creates a Lobby with that
        /// code embedded, configures the transport and starts the NGO host.
        /// </summary>
        public async Task CreateOnlineGameAsync()
        {
            if (!IsInitialized)
            {
                OnError?.Invoke("Online services not initialized. Please wait.");
                return;
            }

            try
            {
                // 1. Relay allocation (MaxPlayers - 1 slots; the host itself occupies 1).
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log($"[NetworkGameManager] Relay join code: {JoinCode}");

                // 2. Configure the Unity Transport with the Relay data.
                RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
                UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(relayServerData);

                // 3. Create a Lobby and embed the join code so clients can find it.
                CreateLobbyOptions lobbyOptions = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            RelayJoinCodeKey,
                            new DataObject(DataObject.VisibilityOptions.Public, JoinCode)
                        }
                    }
                };

                m_CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(
                    "TanksGame",
                    MaxPlayers,
                    lobbyOptions
                );

                Debug.Log($"[NetworkGameManager] Lobby created: {m_CurrentLobby.Id}");

                // 4. Start NGO as host. The game scene will be loaded once a second
                //    player connects (handled by OnClientConnected below).
                m_GameSceneLoaded = false;
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.StartHost();

                OnSessionStarted?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkGameManager] Failed to create online game: {e.Message}");
                OnError?.Invoke($"Failed to create game: {e.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // Client connected callback (host only, before game scene loads)
        // -------------------------------------------------------------------------

        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsHost || m_GameSceneLoaded)
                return;

            // ConnectedClientsIds includes the host itself, so >= 2 means one remote player joined.
            if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
            {
                m_GameSceneLoaded = true;
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
            }
        }

        // -------------------------------------------------------------------------
        // Client flow
        // -------------------------------------------------------------------------

        /// <summary>
        /// Joins a Relay session using the provided join code, configures the
        /// transport and starts the NGO client.
        /// </summary>
        public async Task JoinOnlineGameAsync(string joinCode)
        {
            if (!IsInitialized)
            {
                OnError?.Invoke("Online services not initialized. Please wait.");
                return;
            }

            try
            {
                JoinCode = joinCode.Trim().ToUpper();

                // 1. Join the Relay allocation via the join code.
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(JoinCode);

                // 2. Configure the Unity Transport.
                RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
                UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(relayServerData);

                // 3. Start NGO as client; the host will trigger scene load once connected.
                NetworkManager.Singleton.StartClient();

                OnSessionStarted?.Invoke();
                Debug.Log($"[NetworkGameManager] Joined game with code: {JoinCode}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkGameManager] Failed to join online game: {e.Message}");
                OnError?.Invoke($"Failed to join game: {e.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // Session management
        // -------------------------------------------------------------------------

        /// <summary>Cleanly shuts down the NGO session and deletes or leaves the Lobby.</summary>
        public async Task LeaveSessionAsync()
        {
            try
            {
                if (NetworkManager.Singleton != null)
                    NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

                if (m_CurrentLobby != null)
                {
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
                        await LobbyService.Instance.DeleteLobbyAsync(m_CurrentLobby.Id);
                    else
                        await LobbyService.Instance.RemovePlayerAsync(
                            m_CurrentLobby.Id,
                            AuthenticationService.Instance.PlayerId
                        );

                    m_CurrentLobby = null;
                }

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetworkGameManager] LeaveSession error (non-critical): {e.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // Lobby heartbeat
        // -------------------------------------------------------------------------

        private void HandleLobbyHeartbeat()
        {
            if (m_CurrentLobby == null)
                return;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
                return;

            m_HeartbeatTimer += Time.deltaTime;
            if (m_HeartbeatTimer < LobbyHeartbeatInterval)
                return;

            m_HeartbeatTimer = 0f;
            LobbyService.Instance.SendHeartbeatPingAsync(m_CurrentLobby.Id);
        }

        private async void OnApplicationQuit()
        {
            await LeaveSessionAsync();
        }
    }
}
