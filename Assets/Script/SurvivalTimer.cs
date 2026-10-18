using TMPro;
using UnityEngine;

public class SurvivalTimer : MonoBehaviour
{
    [Header("Sobrevivencia")]
    [SerializeField, Min(1f)] private float startingTime = 120f;
    [SerializeField] private CandleManager gameManager;

    [Header("Interface")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private TMP_Text timeText;

    private float remainingTime;
    private float survivedTime;
    private bool hasExpired;
    private int lastDisplayedSeconds = -1;

    public float RemainingTime => remainingTime;
    public float SurvivedTime => survivedTime;
    public bool HasExpired => hasExpired;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindAnyObjectByType<CandleManager>();

        ResetTimer();
    }

    private void OnEnable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged += OnGameStateChanged;

        SetHudVisible(GameFlow.CanPlay);
    }

    private void OnDisable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged -= OnGameStateChanged;

        SetHudVisible(false);
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
            ResetTimer();

        SetHudVisible(state == GameState.Playing);
    }

    private void Update()
    {
        if (!GameFlow.CanPlay || hasExpired || (gameManager != null && gameManager.IsGameOver))
            return;

        float elapsed = Mathf.Min(Time.deltaTime, remainingTime);
        survivedTime += elapsed;
        remainingTime = Mathf.Max(0f, remainingTime - elapsed);
        RefreshText();

        if (remainingTime <= 0f)
        {
            hasExpired = true;
            if (gameManager != null)
                gameManager.LoseGame();
            else if (GameFlow.Instance != null)
                GameFlow.Instance.ShowGameOver();
        }
    }

    public void AddTime(float seconds)
    {
        if (!GameFlow.CanPlay || hasExpired || seconds <= 0f ||
            float.IsNaN(seconds) || float.IsInfinity(seconds) ||
            (gameManager != null && gameManager.IsGameOver))
            return;

        float total = remainingTime + seconds;
        if (float.IsInfinity(total))
            return;

        remainingTime = total;
        RefreshText();
    }

    private void ResetTimer()
    {
        remainingTime = Mathf.Max(0f, startingTime);
        survivedTime = 0f;
        hasExpired = false;
        lastDisplayedSeconds = -1;
        RefreshText();
    }

    private void RefreshText()
    {
        int seconds = Mathf.CeilToInt(remainingTime);
        if (seconds == lastDisplayedSeconds)
            return;

        lastDisplayedSeconds = seconds;
        if (timeText != null)
            timeText.text = $"<size=45%>TEMPO</size>\n{seconds / 60:00}:{seconds % 60:00}";
    }

    private void SetHudVisible(bool visible)
    {
        if (hudPanel != null)
            hudPanel.SetActive(visible);
    }
}
