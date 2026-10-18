using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameState
{
    Menu,
    Playing,
    GameOver,
    Credits
}

[DefaultExecutionOrder(-100)]
public class GameFlow : MonoBehaviour
{
    [Header("Paineis do Canvas")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Transicao de telas")]
    [SerializeField] private PixelScreenTransition screenTransition;

    [Header("Creditos e audio de derrota")]
    [SerializeField] private GameOverSequence gameOverSequence;

    [Header("Botoes")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    private static bool playAfterReload;
    private bool startPlaying;
    private bool hasStarted;

    public static GameFlow Instance { get; private set; }
    public static bool CanPlay => Instance != null && Instance.IsPlaying;

    public GameState State { get; private set; } = GameState.Menu;
    public bool IsLoading { get; private set; }
    public bool IsScreenTransitioning { get; private set; }
    public bool IsPlaying => State == GameState.Playing && !IsLoading && !IsScreenTransitioning;
    public event Action<GameState> StateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStartupState()
    {
        playAfterReload = false;
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
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

    private void Start()
    {
        if (hasStarted)
            return;

        if (startPlaying)
            BeginGameplay();
        else
            ShowMainMenu();
    }

    private void LateUpdate()
    {
        EventSystem eventSystem = EventSystem.current;
        if (IsLoading || IsScreenTransitioning || (State != GameState.Menu && State != GameState.GameOver) || eventSystem == null || !eventSystem.sendNavigationEvents ||
            eventSystem.currentSelectedGameObject != null)
            return;

        InputSystemUIInputModule inputModule = eventSystem.currentInputModule as InputSystemUIInputModule;
        var navigation = inputModule != null ? inputModule.move?.action : null;
        if (navigation != null && navigation.WasPerformedThisFrame() &&
            navigation.ReadValue<Vector2>().sqrMagnitude > 0f)
        {
            SelectButton(State == GameState.Menu ? playButton : retryButton);
        }
    }

    public void PlayGame()
    {
        if (IsLoading || IsScreenTransitioning || IsPlaying || State == GameState.Credits)
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
            SetState(GameState.Credits);
            gameOverSequence.Play();
            return;
        }

        if (gameOverSequence != null)
            gameOverSequence.StopSequence();

        SetState(GameState.Menu);
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
        foreach (Button button in new[] { playButton, quitButton, retryButton, mainMenuButton })
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

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void SetState(GameState state, bool updatePanels = true)
    {
        bool changed = State != state;
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
