using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the main menu UI: local multiplayer, online (host/join) and quit.
/// Communicates with NetworkGameManager for the online flow.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const string LocalGameScene = "_Complete-Game";

    [Header("Panels")]
    [SerializeField] private GameObject m_MainPanel;
    [SerializeField] private GameObject m_OnlinePanel;
    [SerializeField] private GameObject m_LoadingPanel;

    [Header("Main Panel Buttons")]
    [SerializeField] private Button m_LocalButton;
    [SerializeField] private Button m_OnlineButton;
    [SerializeField] private Button m_QuitButton;

    [Header("Online Panel")]
    [SerializeField] private Button m_HostButton;
    [SerializeField] private Button m_JoinButton;
    [SerializeField] private Button m_BackButton;
    [SerializeField] private InputField m_JoinCodeInput;
    [SerializeField] private Text m_JoinCodeDisplayText;
    [SerializeField] private Text m_StatusText;

    private void Awake()
    {
        m_LocalButton.onClick.AddListener(OnLocalClicked);
        m_OnlineButton.onClick.AddListener(OnOnlineClicked);
        m_QuitButton.onClick.AddListener(OnQuitClicked);

        m_HostButton.onClick.AddListener(OnHostClicked);
        m_JoinButton.onClick.AddListener(OnJoinClicked);
        m_BackButton.onClick.AddListener(OnBackClicked);

        ShowMainPanel();
    }

    private void Start()
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.OnError += ShowError;
    }

    private void OnDestroy()
    {
        if (NetworkGameManager.Instance != null)
            NetworkGameManager.Instance.OnError -= ShowError;
    }

    // -------------------------------------------------------------------------
    // Button handlers
    // -------------------------------------------------------------------------

    private void OnLocalClicked()
    {
        SceneManager.LoadScene(LocalGameScene);
    }

    private void OnOnlineClicked()
    {
        ShowOnlinePanel();
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnHostClicked()
    {
        _ = HostGameAsync();
    }

    private void OnJoinClicked()
    {
        string code = m_JoinCodeInput != null ? m_JoinCodeInput.text : string.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            ShowError("Please enter a join code.");
            return;
        }

        _ = JoinGameAsync(code);
    }

    private void OnBackClicked()
    {
        ShowMainPanel();
    }

    // -------------------------------------------------------------------------
    // Online flow
    // -------------------------------------------------------------------------

    private async Task HostGameAsync()
    {
        ShowLoading("Creating game...");

        if (NetworkGameManager.Instance == null)
        {
            ShowError("NetworkGameManager not found in scene.");
            return;
        }

        if (!NetworkGameManager.Instance.IsInitialized)
            await NetworkGameManager.Instance.InitializeUnityServicesAsync();

        await NetworkGameManager.Instance.CreateOnlineGameAsync();

        // Show the generated join code so the host can share it.
        if (!string.IsNullOrEmpty(NetworkGameManager.Instance.JoinCode))
        {
            HideLoading();
            ShowOnlinePanel();

            if (m_JoinCodeDisplayText != null)
                m_JoinCodeDisplayText.text = $"Your code: {NetworkGameManager.Instance.JoinCode}";

            SetStatus("Waiting for second player...");
            SetInteractable(false);
        }
    }

    private async Task JoinGameAsync(string code)
    {
        ShowLoading($"Joining game {code}...");

        if (NetworkGameManager.Instance == null)
        {
            ShowError("NetworkGameManager not found in scene.");
            return;
        }

        if (!NetworkGameManager.Instance.IsInitialized)
            await NetworkGameManager.Instance.InitializeUnityServicesAsync();

        await NetworkGameManager.Instance.JoinOnlineGameAsync(code);
    }

    // -------------------------------------------------------------------------
    // UI helpers
    // -------------------------------------------------------------------------

    private void ShowMainPanel()
    {
        m_MainPanel.SetActive(true);
        m_OnlinePanel.SetActive(false);
        m_LoadingPanel.SetActive(false);
    }

    private void ShowOnlinePanel()
    {
        m_MainPanel.SetActive(false);
        m_OnlinePanel.SetActive(true);
        m_LoadingPanel.SetActive(false);

        if (m_JoinCodeDisplayText != null)
            m_JoinCodeDisplayText.text = string.Empty;

        SetStatus(string.Empty);
        SetInteractable(true);
    }

    private void ShowLoading(string message)
    {
        m_MainPanel.SetActive(false);
        m_OnlinePanel.SetActive(false);
        m_LoadingPanel.SetActive(true);

        if (m_StatusText != null)
            m_StatusText.text = message;
    }

    private void HideLoading()
    {
        m_LoadingPanel.SetActive(false);
    }

    private void ShowError(string message)
    {
        HideLoading();
        ShowOnlinePanel();
        SetStatus($"<color=red>{message}</color>");
    }

    private void SetStatus(string message)
    {
        if (m_StatusText != null)
            m_StatusText.text = message;
    }

    private void SetInteractable(bool interactable)
    {
        m_HostButton.interactable = interactable;
        m_JoinButton.interactable = interactable;
        m_BackButton.interactable = interactable;

        if (m_JoinCodeInput != null)
            m_JoinCodeInput.interactable = interactable;
    }
}
