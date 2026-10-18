using TMPro;
using UnityEngine;

public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    [SerializeField] private CandleManager gameManager;
    [SerializeField] private TMP_Text hudText;   // opcional: cronômetro na tela

    public float TimeSurvived { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<CandleManager>();
    }

    private void Update()
    {
        // o tempo para sozinho quando o jogo acaba
        if (gameManager != null && gameManager.IsGameOver) return;

        TimeSurvived += Time.deltaTime;
        if (hudText != null) hudText.text = Format(TimeSurvived);
    }

    public static string Format(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}