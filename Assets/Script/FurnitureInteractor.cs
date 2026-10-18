using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Resolve os cliques de interacao antes do movimento do jogador.
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMove))]
public class FurnitureInteractor : MonoBehaviour
{
    [SerializeField] private SurvivalTimer survivalTimer;
    [SerializeField] private FurnitureLootTable lootTable;
    [SerializeField] private FurnitureInteractionUI feedback;

    private PlayerMove player;
    private PlayerRoomTracker roomTracker;
    private Collider2D playerCollider;
    private Camera worldCamera;
    private CandleManager candleManager;
    private FurnitureSearchTarget currentTarget;
    private float elapsed;

    public bool IsSearching => currentTarget != null;
    public float Progress => currentTarget != null ? Mathf.Clamp01(elapsed / currentTarget.SearchDuration) : 0f;
    public FurnitureSearchTarget CurrentTarget => currentTarget;

    private void Awake()
    {
        player = GetComponent<PlayerMove>();
        roomTracker = GetComponent<PlayerRoomTracker>();
        playerCollider = GetComponent<Collider2D>();
        worldCamera = Camera.main;
        candleManager = FindAnyObjectByType<CandleManager>();
    }

    private void OnEnable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged -= OnGameStateChanged;
        CancelSearch();
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state != GameState.Playing)
            CancelSearch();
    }

    private void Update()
    {
        if (!GameFlow.CanPlay || player.IsDefeated || player.IsTransitioning)
        {
            CancelSearch();
            return;
        }

        if (currentTarget != null)
        {
            if (!currentTarget.isActiveAndEnabled || roomTracker == null ||
                roomTracker.CurrentRoom != currentTarget.Room || !currentTarget.IsInReach(playerCollider))
            {
                CancelSearch();
                return;
            }

            elapsed += Time.deltaTime;
            if (feedback != null) feedback.ShowProgress(Progress);
            if (elapsed >= currentTarget.SearchDuration)
                FinishSearch();
            return;
        }

        // Um alvo destruido durante a busca tambem precisa liberar o jogador.
        if (player.IsInteracting)
            CancelSearch();

        Mouse mouse = Mouse.current;
        if (mouse == null || worldCamera == null || roomTracker == null ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            if (feedback != null) feedback.ClearPointer();
            return;
        }

        Vector2 pointerPosition = worldCamera.ScreenToWorldPoint(mouse.position.ReadValue());
        Candle candle = candleManager != null ? candleManager.ActiveCandle : null;
        if (candle != null && candle.Room == roomTracker.CurrentRoom && candle.ContainsPointer(pointerPosition))
        {
            bool canRelight = candleManager.CanRelight(candle);
            if (feedback != null) feedback.ShowCandleHover(canRelight);
            if (mouse.leftButton.wasPressedThisFrame && candleManager.TryRelight(candle))
            {
                player.ConsumeMovementClick();
                if (feedback != null) feedback.ClearPointer();
            }
            return;
        }

        FurnitureSearchTarget hovered = FindAt(pointerPosition);
        bool inReach = hovered != null && hovered.IsInReach(playerCollider);
        if (feedback != null) feedback.ShowHover(hovered, inReach);
        if (mouse.leftButton.wasPressedThisFrame && hovered != null && hovered.IsAvailable && inReach)
            TryStartSearch(hovered);
    }

    public bool TryStartSearch(FurnitureSearchTarget target)
    {
        if (!isActiveAndEnabled || !GameFlow.CanPlay || currentTarget != null || target == null ||
            survivalTimer == null || survivalTimer.HasExpired || lootTable == null ||
            roomTracker == null || roomTracker.CurrentRoom == null || roomTracker.CurrentRoom != target.Room ||
            !target.IsInReach(playerCollider) || !target.IsAvailable || !player.TryBeginInteraction())
            return false;

        if (!target.TryReserve())
        {
            player.EndInteraction();
            return false;
        }
        currentTarget = target;
        elapsed = 0f;
        if (feedback != null) feedback.ShowProgress(0f);
        return true;
    }

    private FurnitureSearchTarget FindAt(Vector2 position)
    {
        FurnitureSearchTarget selected = null;
        foreach (FurnitureSearchTarget candidate in FurnitureSearchTarget.Targets)
        {
            if (candidate == null || candidate.Room == null || candidate.Room != roomTracker.CurrentRoom ||
                !candidate.ContainsPointer(position))
                continue;
            if (selected == null || SortingLayer.GetLayerValueFromID(candidate.Renderer.sortingLayerID) >
                SortingLayer.GetLayerValueFromID(selected.Renderer.sortingLayerID) ||
                (candidate.Renderer.sortingLayerID == selected.Renderer.sortingLayerID &&
                 candidate.Renderer.sortingOrder > selected.Renderer.sortingOrder))
                selected = candidate;
        }
        return selected;
    }

    private void FinishSearch()
    {
        FurnitureSearchTarget target = currentTarget;
        currentTarget = null;
        elapsed = 0f;
        player.EndInteraction();
        if (target == null || !target.CompleteSearch())
        {
            if (feedback != null) feedback.ClearPointer();
            return;
        }

        FurnitureLootEntry result = lootTable.Roll();
        if (result != null) survivalTimer.AddTime(result.timeBonus);
        if (feedback != null) feedback.ShowResult(result);
    }

    public void CancelSearch()
    {
        if (currentTarget != null) currentTarget.CancelSearch();
        currentTarget = null;
        elapsed = 0f;
        if (player != null && player.IsInteracting) player.EndInteraction();
        if (feedback != null) feedback.ClearPointer();
    }
}
