using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameState
{
    Menu,
    Playing,
    GameOver
}

[DefaultExecutionOrder(-100)]
public class GameFlow : MonoBehaviour
{
    [Header("Paineis do Canvas")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Sequencia de derrota")]
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
    public bool IsPlaying => State == GameState.Playing && !IsLoading;
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
            gameOverSequence.Completed += ShowGameOverOptions;
        startPlaying = playAfterReload;
        playAfterReload = false;
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
        if (IsLoading || IsPlaying || eventSystem == null || !eventSystem.sendNavigationEvents ||
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
        if (IsLoading || IsPlaying)
            return;

        if (hasStarted)
            RestartGameplay();
        else
            BeginGameplay();
    }

    public void ShowMainMenu()
    {
        if (IsLoading)
            return;

        if (gameOverSequence != null)
            gameOverSequence.StopSequence();

        SetState(GameState.Menu);
    }

    public void ShowGameOver()
    {
        if (IsLoading || !IsPlaying)
            return;

        SetState(GameState.GameOver);

        if (gameOverSequence != null && gameOverSequence.isActiveAndEnabled)
            gameOverSequence.Play();
        else
            ShowGameOverOptions();
    }

    private void ShowGameOverOptions()
    {
        if (IsLoading || State != GameState.GameOver)
            return;

        if (gameOverSequence != null)
            gameOverSequence.HidePanel();

        SetPanels(false, true);
    }

    public void QuitGame()
    {
        if (IsLoading)
            return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginGameplay()
    {
        hasStarted = true;
        if (gameOverSequence != null)
            gameOverSequence.StopSequence();

        SetState(GameState.Playing);
    }

    private void RestartGameplay()
    {
        Scene scene = gameObject.scene;

        IsLoading = true;
        foreach (Button button in new[] { playButton, quitButton, retryButton, mainMenuButton })
            if (button != null)
                button.interactable = false;

        playAfterReload = true;
        Time.timeScale = 1f;
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

    private void SetState(GameState state)
    {
        bool changed = State != state;
        State = state;
        Time.timeScale = state == GameState.Playing ? 1f : 0f;
        SetPanels(state == GameState.Menu, false);

        if (changed)
            StateChanged?.Invoke(state);
    }

    private void OnDestroy()
    {
        if (gameOverSequence != null)
            gameOverSequence.Completed -= ShowGameOverOptions;

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
