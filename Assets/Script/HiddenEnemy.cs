using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HiddenEnemy : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CandleManager gameManager;
    [SerializeField] private PlayerMove player;
    [SerializeField] private PlayerRoomTracker roomTracker;
    [SerializeField] private EnemyHidingSpot[] hidingSpots;
    [SerializeField] private GameObject enemyPrefab;

    [Header("Tempo entre aparicoes")]
    [SerializeField, Min(0.1f)] private float minWaitTime = 15f;
    [SerializeField, Min(0.1f)] private float maxWaitTime = 25f;
    [SerializeField, Min(0.1f)] private float visibleDuration = 8f;

    [Header("Distancia segura ao aparecer")]
    [Tooltip("Distancia minima ate o jogador, medida a partir do centro do perigo.")]
    [SerializeField, Min(0f)] private float minSpawnDistance = 3f;

    private readonly List<EnemyHidingSpot> availableSpots = new List<EnemyHidingSpot>();
    private EnemyHidingSpot activeSpot;
    private EnemyHidingSpot previousSpot;
    private GameObject enemyInstance;
    private SortingGroup enemySorting;
    private Collider2D playerCollider;
    private float nextAppearanceTime;
    private float disappearTime;

    public EnemyHidingSpot ActiveSpot => activeSpot;
    public bool IsVisible => activeSpot != null && enemyInstance != null && enemyInstance.activeSelf;

    private void Start()
    {
        if (gameManager == null || player == null || roomTracker == null || enemyPrefab == null)
            Debug.LogWarning("HiddenEnemy: preencha as referencias.", this);
    }

    private void OnEnable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged += OnGameStateChanged;

        ScheduleNextAppearance();
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
            ScheduleNextAppearance();
        else
            Disappear();
    }

    private void LateUpdate()
    {
        if (!GameFlow.CanPlay || gameManager == null || gameManager.IsGameOver || player == null || roomTracker == null)
            return;

        if (activeSpot != null)
        {
            if (!activeSpot.isActiveAndEnabled || activeSpot.Room == null ||
                !activeSpot.Room.isActiveAndEnabled || enemyInstance == null ||
                !enemyInstance.activeInHierarchy || Time.time >= disappearTime)
            {
                Disappear();
                return;
            }

            if (!player.IsDefeated && !player.IsTransitioning &&
                roomTracker.CurrentRoom == activeSpot.Room &&
                activeSpot.DistanceToPlayer(player.transform, playerCollider) <= activeSpot.DangerRadius)
            {
                Debug.Log("O inimigo te encontrou!", this);
                gameManager.LoseGame();
            }

            return;
        }

        if (enemyInstance != null && enemyInstance.activeSelf)
        {
            Disappear();
            return;
        }

        if (Time.time >= nextAppearanceTime && !TryAppear())
            nextAppearanceTime = Time.time + 1f;
    }

    public bool TryAppear()
    {
        if (!GameFlow.CanPlay || !Application.isPlaying || !isActiveAndEnabled || activeSpot != null ||
            gameManager == null || gameManager.IsGameOver || player == null ||
            player.IsDefeated || player.IsTransitioning || roomTracker == null ||
            roomTracker.CurrentRoom == null || enemyPrefab == null || hidingSpots == null)
            return false;

        if (playerCollider == null)
            playerCollider = player.GetComponent<Collider2D>();

        availableSpots.Clear();
        foreach (EnemyHidingSpot spot in hidingSpots)
        {
            if (spot == null || !spot.isActiveAndEnabled || spot.Room == null ||
                !spot.Room.isActiveAndEnabled)
                continue;

            float safeDistance = Mathf.Max(minSpawnDistance, spot.DangerRadius + 0.5f);
            if (spot.DistanceToPlayer(player.transform, playerCollider) <= safeDistance)
                continue;

            if (!availableSpots.Contains(spot))
                availableSpots.Add(spot);
        }

        if (availableSpots.Count > 1)
            availableSpots.Remove(previousSpot);

        if (availableSpots.Count == 0)
            return false;

        activeSpot = availableSpots[Random.Range(0, availableSpots.Count)];
        previousSpot = activeSpot;

        if (enemyInstance == null)
        {
            enemyInstance = Instantiate(enemyPrefab, transform);
            enemySorting = enemyInstance.GetComponent<SortingGroup>();
            if (enemySorting == null)
                enemySorting = enemyInstance.AddComponent<SortingGroup>();
        }

        Transform point = activeSpot.AppearancePoint;
        enemyInstance.transform.SetPositionAndRotation(point.position, point.rotation);

        SpriteRenderer furniture = activeSpot.FurnitureRenderer;
        enemySorting.sortingLayerID = furniture != null ? furniture.sortingLayerID : 0;
        enemySorting.sortingOrder = furniture != null ? furniture.sortingOrder - 1 : 1;

        enemyInstance.SetActive(true);
        disappearTime = Time.time + Mathf.Max(visibleDuration, 0.1f);
        return true;
    }

    [ContextMenu("DEBUG/Aparecer agora")]
    private void DebugAppear()
    {
        if (TryAppear())
            Debug.Log("Inimigo escondido em: " + activeSpot.name, this);
        else if (Application.isPlaying)
            Debug.Log("Sem esconderijo disponivel, ou o inimigo ja aparecendo.", this);
    }

    [ContextMenu("DEBUG/Esconder agora")]
    public void Disappear()
    {
        activeSpot = null;
        if (enemyInstance != null)
            enemyInstance.SetActive(false);

        ScheduleNextAppearance();
    }

    private void ScheduleNextAppearance()
    {
        float minimum = Mathf.Max(minWaitTime, 0.1f);
        nextAppearanceTime = Time.time + Random.Range(minimum, Mathf.Max(maxWaitTime, minimum));
    }

    private void OnDisable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged -= OnGameStateChanged;

        Disappear();
    }
}
