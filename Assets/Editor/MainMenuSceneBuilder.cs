using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor utility that creates the MainMenu scene programmatically.
/// Run it from the menu: Tools > Tanks > Build Main Menu Scene
/// </summary>
public static class MainMenuSceneBuilder
{
    [MenuItem("Tools/Tanks/Build Main Menu Scene")]
    public static void BuildMainMenuScene()
    {
        // Prompt to save current scene.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Create and open a new scene.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // -----------------------------------------------------------------------
        // Camera
        // -----------------------------------------------------------------------
        GameObject cameraGo = new GameObject("Main Camera");
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        camera.orthographic = true;
        cameraGo.AddComponent<AudioListener>();
        cameraGo.tag = "MainCamera";

        // -----------------------------------------------------------------------
        // NetworkGameManager (persistent singleton for online session)
        // -----------------------------------------------------------------------
        GameObject networkManagerGo = new GameObject("NetworkGameManager");
        networkManagerGo.AddComponent<NetworkGameManager>();

        // -----------------------------------------------------------------------
        // Canvas root
        // -----------------------------------------------------------------------
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // -----------------------------------------------------------------------
        // EventSystem
        // -----------------------------------------------------------------------
        GameObject eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // -----------------------------------------------------------------------
        // Background panel (full-screen dark tint)
        // -----------------------------------------------------------------------
        GameObject bgGo = CreatePanel(canvasGo.transform, "Background",
            new Color(0.08f, 0.08f, 0.12f, 1f));
        SetStretchFull(bgGo.GetComponent<RectTransform>());

        // -----------------------------------------------------------------------
        // Title text
        // -----------------------------------------------------------------------
        GameObject titleGo = CreateText(canvasGo.transform, "Title", "TANKS!",
            60, FontStyle.Bold, Color.white);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -80f);
        titleRect.sizeDelta = new Vector2(600f, 80f);

        // -----------------------------------------------------------------------
        // MAIN PANEL
        // -----------------------------------------------------------------------
        GameObject mainPanel = CreatePanel(canvasGo.transform, "MainPanel", Color.clear);
        RectTransform mainRect = mainPanel.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0.5f, 0.5f);
        mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.pivot = new Vector2(0.5f, 0.5f);
        mainRect.anchoredPosition = Vector2.zero;
        mainRect.sizeDelta = new Vector2(400f, 320f);

        VerticalLayoutGroup mainLayout = mainPanel.AddComponent<VerticalLayoutGroup>();
        mainLayout.spacing = 20f;
        mainLayout.childAlignment = TextAnchor.MiddleCenter;
        mainLayout.childForceExpandWidth = true;
        mainLayout.childForceExpandHeight = false;

        Button localBtn = CreateButton(mainPanel.transform, "LocalButton", "LOCAL MULTIPLAYER",
            new Color(0.2f, 0.6f, 0.9f));
        Button onlineBtn = CreateButton(mainPanel.transform, "OnlineButton", "ONLINE MULTIPLAYER",
            new Color(0.2f, 0.75f, 0.4f));
        Button quitBtn = CreateButton(mainPanel.transform, "QuitButton", "QUIT",
            new Color(0.75f, 0.2f, 0.2f));

        // -----------------------------------------------------------------------
        // ONLINE PANEL
        // -----------------------------------------------------------------------
        GameObject onlinePanel = CreatePanel(canvasGo.transform, "OnlinePanel", Color.clear);
        RectTransform onlineRect = onlinePanel.GetComponent<RectTransform>();
        onlineRect.anchorMin = new Vector2(0.5f, 0.5f);
        onlineRect.anchorMax = new Vector2(0.5f, 0.5f);
        onlineRect.pivot = new Vector2(0.5f, 0.5f);
        onlineRect.anchoredPosition = Vector2.zero;
        onlineRect.sizeDelta = new Vector2(500f, 480f);

        VerticalLayoutGroup onlineLayout = onlinePanel.AddComponent<VerticalLayoutGroup>();
        onlineLayout.spacing = 16f;
        onlineLayout.childAlignment = TextAnchor.MiddleCenter;
        onlineLayout.childForceExpandWidth = true;
        onlineLayout.childForceExpandHeight = false;

        Button hostBtn = CreateButton(onlinePanel.transform, "HostButton", "CREATE GAME (HOST)",
            new Color(0.2f, 0.75f, 0.4f));

        // Join code input field
        GameObject inputGo = new GameObject("JoinCodeInput");
        inputGo.transform.SetParent(onlinePanel.transform, false);
        RectTransform inputRect = inputGo.AddComponent<RectTransform>();
        inputRect.sizeDelta = new Vector2(400f, 50f);
        Image inputImage = inputGo.AddComponent<Image>();
        inputImage.color = new Color(0.15f, 0.15f, 0.2f);
        InputField inputField = inputGo.AddComponent<InputField>();

        GameObject inputTextGo = CreateText(inputGo.transform, "Text", "Enter join code...",
            24, FontStyle.Normal, new Color(0.7f, 0.7f, 0.7f));
        inputTextGo.GetComponent<RectTransform>().sizeDelta = new Vector2(380f, 40f);
        inputField.textComponent = inputTextGo.GetComponent<Text>();
        inputField.placeholder = inputTextGo.GetComponent<Text>();

        Button joinBtn = CreateButton(onlinePanel.transform, "JoinButton", "JOIN GAME",
            new Color(0.2f, 0.6f, 0.9f));

        // Join code display (shown after hosting)
        GameObject codeDisplayGo = CreateText(onlinePanel.transform, "JoinCodeDisplay", "",
            28, FontStyle.Bold, Color.yellow);
        codeDisplayGo.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 50f);

        // Status text (errors / waiting messages)
        GameObject statusGo = CreateText(onlinePanel.transform, "StatusText", "",
            20, FontStyle.Normal, Color.white);
        statusGo.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 50f);

        Button backBtn = CreateButton(onlinePanel.transform, "BackButton", "BACK",
            new Color(0.5f, 0.5f, 0.5f));

        // -----------------------------------------------------------------------
        // LOADING PANEL
        // -----------------------------------------------------------------------
        GameObject loadingPanel = CreatePanel(canvasGo.transform, "LoadingPanel",
            new Color(0f, 0f, 0f, 0.7f));
        SetStretchFull(loadingPanel.GetComponent<RectTransform>());

        GameObject loadingTextGo = CreateText(loadingPanel.transform, "LoadingText",
            "Connecting...", 40, FontStyle.Bold, Color.white);
        RectTransform loadingTextRect = loadingTextGo.GetComponent<RectTransform>();
        loadingTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        loadingTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        loadingTextRect.anchoredPosition = Vector2.zero;
        loadingTextRect.sizeDelta = new Vector2(600f, 60f);

        // -----------------------------------------------------------------------
        // Wire up MainMenuController
        // -----------------------------------------------------------------------
        MainMenuController controller = canvasGo.AddComponent<MainMenuController>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("m_MainPanel").objectReferenceValue = mainPanel;
        so.FindProperty("m_OnlinePanel").objectReferenceValue = onlinePanel;
        so.FindProperty("m_LoadingPanel").objectReferenceValue = loadingPanel;
        so.FindProperty("m_LocalButton").objectReferenceValue = localBtn;
        so.FindProperty("m_OnlineButton").objectReferenceValue = onlineBtn;
        so.FindProperty("m_QuitButton").objectReferenceValue = quitBtn;
        so.FindProperty("m_HostButton").objectReferenceValue = hostBtn;
        so.FindProperty("m_JoinButton").objectReferenceValue = joinBtn;
        so.FindProperty("m_BackButton").objectReferenceValue = backBtn;
        so.FindProperty("m_JoinCodeInput").objectReferenceValue = inputField;
        so.FindProperty("m_JoinCodeDisplayText").objectReferenceValue = codeDisplayGo.GetComponent<Text>();
        so.FindProperty("m_StatusText").objectReferenceValue = statusGo.GetComponent<Text>();
        so.ApplyModifiedPropertiesWithoutUndo();

        // Start with main panel active, others off.
        onlinePanel.SetActive(false);
        loadingPanel.SetActive(false);

        // -----------------------------------------------------------------------
        // Save the scene
        // -----------------------------------------------------------------------
        string scenePath = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        // Add to Build Settings if not already there.
        AddSceneToBuildSettings(scenePath);
        AddSceneToBuildSettings("Assets/Scenes/_Complete-Game.unity");

        Debug.Log("[MainMenuSceneBuilder] MainMenu scene created at: " + scenePath);
        EditorUtility.DisplayDialog("Done", "MainMenu scene created at:\n" + scenePath, "OK");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static Button CreateButton(Transform parent, string name, string label, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(380f, 60f);

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        Button btn = go.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.2f);
        btn.colors = colors;

        // Label
        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        Text text = labelGo.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    private static GameObject CreateText(Transform parent, string name, string content,
        int fontSize, FontStyle style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        Text text = go.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        return go;
    }

    private static void SetStretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene s in existing)
        {
            if (s.path == scenePath)
                return;
        }

        EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
        for (int i = 0; i < existing.Length; i++)
            updated[i] = existing[i];

        updated[existing.Length] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = updated;
        Debug.Log($"[MainMenuSceneBuilder] Added '{scenePath}' to Build Settings.");
    }
}
