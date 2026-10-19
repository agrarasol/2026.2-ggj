using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameState
{
    Menu,
    Playing,
    GameOver,
    Credits,
    Paused
}

[DefaultExecutionOrder(-100)]
public class GameFlow : MonoBehaviour
{
    [Header("Paineis do Canvas")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Transicao de telas")]
    [SerializeField] private PixelScreenTransition screenTransition;

    [Header("Creditos e audio de derrota")]
    [SerializeField] private GameOverSequence gameOverSequence;

    [Header("Botoes")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button fullscreenButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button pauseExitButton;

    private const int DefaultWindowWidth = 1280;
    private const int DefaultWindowHeight = 720;
    private static int windowedWidth = DefaultWindowWidth;
    private static int windowedHeight = DefaultWindowHeight;
    private static bool playAfterReload;
    private TMP_Text fullscreenButtonLabel;
    private bool startPlaying;
    private bool hasStarted;

    public static GameFlow Instance { get; private set; }
    public static bool CanPlay => Instance != null && Instance.IsPlaying;
    public static bool IsPaused => Instance != null && Instance.State == GameState.Paused;

    public GameState State { get; private set; } = GameState.Menu;
    public GameState PreviousState { get; private set; } = GameState.Menu;
    public bool IsLoading { get; private set; }
    public bool IsScreenTransitioning { get; private set; }
    public bool IsPlaying => State == GameState.Playing && !IsLoading && !IsScreenTransitioning;
    public event Action<GameState> StateChanged;
    public CollectedItemsHUD CollectedItems { get; private set; }
    internal Canvas InterfaceCanvas => mainMenuPanel != null ? mainMenuPanel.GetComponentInParent<Canvas>(true) : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStartupState()
    {
        playAfterReload = false;
        windowedWidth = DefaultWindowWidth;
        windowedHeight = DefaultWindowHeight;
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartInWindowedMode()
    {
#if UNITY_STANDALONE && !UNITY_EDITOR
        Screen.SetResolution(DefaultWindowWidth, DefaultWindowHeight, FullScreenMode.Windowed);
#endif
    }

    private void Awake()
    {
        Instance = this;
        ConnectExistingUI();
        if (gameOverSequence != null)
            gameOverSequence.Completed += FinishCredits;
        startPlaying = playAfterReload;
        playAfterReload = false;
        if (screenTransition == null && mainMenuPanel != null)
            screenTransition = mainMenuPanel.GetComponentInParent<Canvas>(true)?.GetComponentInChildren<PixelScreenTransition>(true);
        if (screenTransition != null)
        {
            if (startPlaying && screenTransition.enabled)
            {
                IsScreenTransitioning = true;
                screenTransition.ShowCovered();
            }
            else screenTransition.Clear();
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        SetPanels(!startPlaying, false);
    }

    private void ConnectExistingUI()
    {
        Canvas canvas = InterfaceCanvas;
        if (creditsButton == null && mainMenuPanel != null)
            creditsButton = FindUIElement(mainMenuPanel.transform, "CreditsButton")?.GetComponent<Button>();
        ConnectButton(creditsButton, ShowCredits);
        if (fullscreenButton == null && mainMenuPanel != null)
            fullscreenButton = FindUIElement(mainMenuPanel.transform, "FullscreenButton")?.GetComponent<Button>();
        if (fullscreenButton != null)
            fullscreenButtonLabel = fullscreenButton.GetComponentInChildren<TMP_Text>(true);
        ConnectButton(fullscreenButton, ToggleFullscreen);
        UpdateFullscreenButtonLabel();
        if (pausePanel == null && canvas != null)
            pausePanel = FindUIElement(canvas.transform, "PauseMenu")?.gameObject;
        if (pausePanel != null)
        {
            if (resumeButton == null) resumeButton = FindUIElement(pausePanel.transform, "ResumeButton")?.GetComponent<Button>();
            if (restartButton == null) restartButton = FindUIElement(pausePanel.transform, "RestartButton")?.GetComponent<Button>();
            if (pauseExitButton == null) pauseExitButton = FindUIElement(pausePanel.transform, "ExitButton")?.GetComponent<Button>();
        }
        ConnectButton(resumeButton, ResumeGame);
        ConnectButton(restartButton, RestartGame);
        ConnectButton(pauseExitButton, ShowMainMenu);
        CollectedItems = GetComponent<CollectedItemsHUD>();
        if (CollectedItems == null && canvas != null && FindUIElement(canvas.transform, "ItensHUD") != null)
            CollectedItems = gameObject.AddComponent<CollectedItemsHUD>();
    }

    private void ConnectButton(Button button, UnityAction action)
    {
        if (button == null) return;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == action.Method.Name &&
                button.onClick.GetPersistentListenerState(i) != UnityEventCallState.Off)
                return;
        button.onClick.AddListener(action);
    }

    internal static Transform FindUIElement(Transform root, string elementName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == elementName) return child;
        return null;
    }

    private void Start()
    {
        if (hasStarted)
            return;

        if (startPlaying)
            BeginGameplay();
        else
            ShowMainMenu();
    }

    private void Update()
    {
        UpdateFullscreenButtonLabel();
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void ToggleFullscreen()
    {
        if (IsLoading || IsScreenTransitioning) return;

        if (Screen.fullScreen)
            Screen.SetResolution(windowedWidth, windowedHeight, FullScreenMode.Windowed);
        else
        {
            windowedWidth = Screen.width;
            windowedHeight = Screen.height;
            Resolution displayResolution = Screen.currentResolution;
            Screen.SetResolution(displayResolution.width, displayResolution.height, FullScreenMode.FullScreenWindow);
        }
    }

    private void UpdateFullscreenButtonLabel()
    {
        if (fullscreenButtonLabel == null) return;
        string label = Screen.fullScreen ? "Modo janela" : "Tela cheia";
        if (fullscreenButtonLabel.text != label)
            fullscreenButtonLabel.text = label;
    }

    public void TogglePause()
    {
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        if (!IsPlaying || pausePanel == null) return;
        SetState(GameState.Paused);
    }

    public void ResumeGame()
    {
        if (!IsPaused || IsLoading || IsScreenTransitioning) return;
        SetState(GameState.Playing);
    }

    public void RestartGame()
    {
        if (IsLoading || IsScreenTransitioning || (State != GameState.Paused && State != GameState.GameOver)) return;
        RestartGameplay();
    }

    private void LateUpdate()
    {
        EventSystem eventSystem = EventSystem.current;
        if (IsLoading || IsScreenTransitioning || (State != GameState.Menu && State != GameState.GameOver && State != GameState.Paused) || eventSystem == null || !eventSystem.sendNavigationEvents ||
            eventSystem.currentSelectedGameObject != null)
            return;

        InputSystemUIInputModule inputModule = eventSystem.currentInputModule as InputSystemUIInputModule;
        var navigation = inputModule != null ? inputModule.move?.action : null;
        if (navigation != null && navigation.WasPerformedThisFrame() &&
            navigation.ReadValue<Vector2>().sqrMagnitude > 0f)
        {
            SelectButton(State == GameState.Menu ? playButton : State == GameState.Paused ? resumeButton : retryButton);
        }
    }

    public void PlayGame()
    {
        if (IsLoading || IsScreenTransitioning || IsPlaying || IsPaused || State == GameState.Credits)
            return;

        if (hasStarted)
            RestartGameplay();
        else
            BeginGameplay();
    }

    public void ShowMainMenu()
    {
        if (IsLoading || IsScreenTransitioning || State == GameState.Credits)
            return;

        if (State == GameState.GameOver && gameOverSequence != null && gameOverSequence.isActiveAndEnabled)
        {
            ShowCredits();
            return;
        }

        if (gameOverSequence != null)
            gameOverSequence.StopSequence();

        SetState(GameState.Menu);
    }

    public void ShowCredits()
    {
        if (IsLoading || IsScreenTransitioning || (State != GameState.Menu && State != GameState.GameOver) ||
            gameOverSequence == null || !gameOverSequence.isActiveAndEnabled)
            return;

        bool keepMenuVisible = State == GameState.Menu;
        SetState(GameState.Credits, !keepMenuVisible);
        gameOverSequence.Play();
    }

    public void ShowGameOver()
    {
        if (IsLoading || !IsPlaying)
            return;

        bool animate = screenTransition != null && screenTransition.enabled;
        IsScreenTransitioning = animate;
        SetState(GameState.GameOver, !animate);
        if (gameOverSequence != null)
        {
            gameOverSequence.StopSequence();
            gameOverSequence.PlayDeathSound();
        }
        if (animate) StartCoroutine(RevealGameOver());
    }

    private void FinishCredits()
    {
        if (IsLoading || IsScreenTransitioning || State != GameState.Credits)
            return;

        if (gameOverSequence != null)
            gameOverSequence.StopSequence();

        SetState(GameState.Menu);
    }

    public void QuitGame()
    {
        if (IsLoading || IsScreenTransitioning)
            return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginGameplay()
    {
        if (screenTransition != null && screenTransition.enabled)
            StartCoroutine(RevealGameplay());
        else
            ActivateGameplay();
    }

    private IEnumerator RevealGameplay()
    {
        IsScreenTransitioning = true;
        Time.timeScale = 0f;
        if (!startPlaying) yield return screenTransition.Cover();
        if (gameOverSequence != null) gameOverSequence.StopSequence();
        SetPanels(false, false);
        yield return screenTransition.Reveal();
        screenTransition.Clear();
        IsScreenTransitioning = false;
        ActivateGameplay();
    }

    private IEnumerator RevealGameOver()
    {
        yield return screenTransition.Cover();
        SetPanels(false, true);
        yield return screenTransition.Reveal();
        screenTransition.Clear();
        IsScreenTransitioning = false;
    }

    private void ActivateGameplay()
    {
        IsScreenTransitioning = false;
        hasStarted = true;
        if (gameOverSequence != null)
            gameOverSequence.StopSequence();

        SetState(GameState.Playing);
    }

    private void RestartGameplay()
    {
        if (screenTransition != null && screenTransition.enabled)
            StartCoroutine(CoverAndRestart());
        else
            LoadGameplayScene();
    }

    private IEnumerator CoverAndRestart()
    {
        IsScreenTransitioning = true;
        Time.timeScale = 0f;
        yield return screenTransition.Cover();
        LoadGameplayScene();
    }

    private void LoadGameplayScene()
    {
        Scene scene = gameObject.scene;

        IsLoading = true;
        foreach (Button button in new[] { playButton, creditsButton, fullscreenButton, quitButton, retryButton, mainMenuButton, resumeButton, restartButton, pauseExitButton })
            if (button != null)
                button.interactable = false;

        playAfterReload = true;
        if (screenTransition != null) screenTransition.PreserveForReload();
        Time.timeScale = 0f;
        SceneManager.LoadSceneAsync(scene.buildIndex, LoadSceneMode.Single);
    }

    private void SetPanels(bool showMainMenu, bool showGameOver)
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(showMainMenu);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(showGameOver);
        if (pausePanel != null)
        {
            pausePanel.SetActive(State == GameState.Paused);
            if (pausePanel.activeSelf && !IsScreenTransitioning)
                pausePanel.transform.SetAsLastSibling();
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void SetState(GameState state, bool updatePanels = true)
    {
        bool changed = State != state;
        PreviousState = State;
        State = state;
        Time.timeScale = state == GameState.Playing && !IsScreenTransitioning ? 1f : 0f;
        if (updatePanels) SetPanels(state == GameState.Menu, state == GameState.GameOver);
        else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        if (changed)
            StateChanged?.Invoke(state);
    }

    private void OnDestroy()
    {
        if (gameOverSequence != null)
            gameOverSequence.Completed -= FinishCredits;

        if (Instance == this)
            Instance = null;
    }

    private static void SelectButton(Button button)
    {
        if (EventSystem.current == null || button == null || !button.isActiveAndEnabled)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
