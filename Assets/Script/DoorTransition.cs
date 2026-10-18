using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RoomPassageType { Door, OpenBoundary, Stairs }

public class DoorTransition : MonoBehaviour
{
    private static readonly List<DoorTransition> activeDoors = new List<DoorTransition>();
    public static IReadOnlyList<DoorTransition> ActivePassages => activeDoors;

    public RoomZone SourceRoom => sourceRoom;
    public RoomZone DestinationRoom => destinationRoom;
    public Vector2 ApproachPosition => approachPoint != null ? (Vector2)approachPoint.position : (Vector2)transform.position;
    public Vector2 EntryPosition => entryPoint != null ? (Vector2)entryPoint.position : (Vector2)transform.position;
    public RoomPassageType PassageType => passageType;
    public float StairDuration => Mathf.Max(0.1f, stairTravelDuration);
    public Vector2 PassageDirection => passageType == RoomPassageType.Stairs ?
        (stairClimbPoint != null ? ((Vector2)stairClimbPoint.position - ApproachPosition).normalized : Vector2.up) :
        (EntryPosition - ApproachPosition).normalized;
    public Vector2 ArrivalDirection => passageType == RoomPassageType.Stairs && entryPoint != null && entryPoint.parent != null ?
        ((Vector2)(entryPoint.position - entryPoint.parent.position)).normalized : PassageDirection;
    public string InteractionHint => sourceRoom != null && destinationRoom != null &&
        destinationRoom.transform.position.y > sourceRoom.transform.position.y ? "Clique para subir a escada" : "Clique para descer a escada";

// Devolve a primeira porta do caminho mais curto entre duas salas (null se não houver caminho)
    public static DoorTransition NextDoorTowards(RoomZone from, RoomZone to)
    {
        if (from == null || to == null || from == to)
            return null;

        var firstDoor = new Dictionary<RoomZone, DoorTransition>();
        var queue = new Queue<RoomZone>();
        firstDoor[from] = null;
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            RoomZone room = queue.Dequeue();
            foreach (DoorTransition door in activeDoors)
            {
                if (door == null || door.sourceRoom != room || door.destinationRoom == null)
                    continue;
                if (firstDoor.ContainsKey(door.destinationRoom))
                    continue;

                firstDoor[door.destinationRoom] = room == from ? door : firstDoor[room];
                    if (door.destinationRoom == to)
                        return firstDoor[to];

                queue.Enqueue(door.destinationRoom);
            }
    }
        return null;
    }

    [SerializeField] private RoomZone sourceRoom;
    [SerializeField] private RoomZone destinationRoom;

    [SerializeField] private Transform entryPoint;
    [SerializeField] private Transform cameraPoint;
    [SerializeField] private Transform approachPoint;

    [SerializeField, Min(0.01f)] private float transitionDuration = 0.5f;
    [SerializeField, Min(0.1f)] private float movementTimeout = 5f;
    [Header("Tipo de passagem")]
    [SerializeField] private RoomPassageType passageType;
    [Header("Escada")]
    [SerializeField] private Transform stairClimbPoint;
    [SerializeField] private SpriteRenderer stairRenderer;
    [SerializeField] private CanvasGroup transitionFade;
    [SerializeField, Min(0.1f)] private float stairTravelDuration = 0.5f;

    private PlayerMove transitioningPlayer;
    private PlayerRoomTracker transitioningTracker;
    private Transform transitioningCamera;
    private Vector2 playerStartPosition;
    private Vector3 cameraStartPosition;
    private bool reportedMissingReferences;
    private readonly RaycastHit2D[] openPassageHits = new RaycastHit2D[16];


    private void OnEnable()
    {
        if (!activeDoors.Contains(this))
            activeDoors.Add(this);
        if (GameFlow.Instance != null) GameFlow.Instance.StateChanged += OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing || state == GameState.Paused || transitioningPlayer == null) return;
        StopAllCoroutines();
        FinishTransition(false);
    }

    public static bool TryHandleCrossing(PlayerMove playerMove, Rigidbody2D body, Vector2 nextPosition)
    {
        if (!GameFlow.CanPlay)
            return false;

        PlayerRoomTracker tracker = playerMove.GetComponent<PlayerRoomTracker>();
        if (tracker == null)
            return false;

        DoorTransition nearestDoor = null;
        float nearestFraction = float.PositiveInfinity;

        foreach (DoorTransition door in activeDoors)
        {
            if (door == null || door.passageType == RoomPassageType.Stairs || !door.CanStartTransition(playerMove, tracker))
                continue;

            if (door.TryGetCrossing(body, nextPosition, out float fraction) && fraction < nearestFraction)
            {
                nearestDoor = door;
                nearestFraction = fraction;
            }
        }

        if (nearestDoor == null)
            return false;

        return nearestDoor.TryBeginTransition(playerMove, tracker);
    }

    private bool TryGetCrossing(Rigidbody2D body, Vector2 nextPosition, out float fraction)
    {
        fraction = 0f;
        if (approachPoint == null || entryPoint == null)
            return false;

        Collider2D doorway = GetComponent<Collider2D>();
        Vector2 direction = PassageDirection;
        if (doorway == null || !doorway.enabled || direction.sqrMagnitude < 0.0001f)
            return false;

        Vector2 normal = direction.normalized;
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        Bounds bounds = doorway.bounds;
        Vector2 center = bounds.center;
        float startSide = Vector2.Dot(body.position - center, normal);
        float endSide = Vector2.Dot(nextPosition - center, normal);
        float advance = endSide - startSide;

        if (startSide > 0.01f || endSide < 0f || advance <= 0f)
            return false;

        fraction = Mathf.Clamp01(-startSide / advance);
        Vector2 crossingPoint = Vector2.Lerp(body.position, nextPosition, fraction);
        float halfWidth = Mathf.Abs(tangent.x) * bounds.extents.x +
            Mathf.Abs(tangent.y) * bounds.extents.y;

        // Considera tambem o tamanho do jogador nas bordas da abertura.
        float playerHalfWidth = 0f;
        foreach (Collider2D collider in body.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != body)
                continue;

            Bounds playerBounds = collider.bounds;
            float extent = Mathf.Abs(Vector2.Dot((Vector2)playerBounds.center - body.position, tangent)) +
                Mathf.Abs(tangent.x) * playerBounds.extents.x +
                Mathf.Abs(tangent.y) * playerBounds.extents.y;
            playerHalfWidth = Mathf.Max(playerHalfWidth, extent);
        }

        return Mathf.Abs(Vector2.Dot(crossingPoint - center, tangent)) <= halfWidth + playerHalfWidth;
    }

    private bool CanStartTransition(PlayerMove playerMove, PlayerRoomTracker tracker)
    {
        return GameFlow.CanPlay && isActiveAndEnabled && transitioningPlayer == null &&
            playerMove != null && playerMove.isActiveAndEnabled && !playerMove.IsTransitioning &&
            !playerMove.IsDefeated && !playerMove.IsInteracting &&
            tracker != null && tracker.CurrentRoom == sourceRoom;
    }

    private bool HasReferences()
    {
        if (sourceRoom != null && destinationRoom != null && entryPoint != null &&
            cameraPoint != null && approachPoint != null && Camera.main != null)
        {
            reportedMissingReferences = false;
            return true;
        }

        if (!reportedMissingReferences)
        {
            Debug.LogWarning("DoorTransition is missing references.", this);
            reportedMissingReferences = true;
        }
        return false;
    }

    private bool TryBeginTransition(PlayerMove playerMove, PlayerRoomTracker tracker)
    {
        if (!CanStartTransition(playerMove, tracker) || !HasReferences())
            return false;

        if (passageType == RoomPassageType.OpenBoundary)
            StartCoroutine(OpenTransitionCoroutine(playerMove, tracker));
        else if (passageType == RoomPassageType.Stairs)
            StartCoroutine(StairTransitionCoroutine(playerMove, tracker));
        else
            StartCoroutine(TransitionCoroutine(playerMove, tracker));
        return true;
    }

    public bool TryUseStairs(PlayerMove playerMove)
    {
        return passageType == RoomPassageType.Stairs && playerMove != null &&
            TryBeginTransition(playerMove, playerMove.GetComponent<PlayerRoomTracker>());
    }

    public bool ContainsStairPointer(Vector2 position)
    {
        if (passageType != RoomPassageType.Stairs || !isActiveAndEnabled) return false;
        if (stairRenderer != null && stairRenderer.enabled && stairRenderer.gameObject.activeInHierarchy)
        {
            Bounds bounds = stairRenderer.bounds;
            return position.x >= bounds.min.x && position.x <= bounds.max.x &&
                position.y >= bounds.min.y && position.y <= bounds.max.y;
        }
        Collider2D area = GetComponent<Collider2D>();
        return area != null && area.enabled && area.OverlapPoint(position);
    }

    public Vector2 GetApproachPosition(Vector2 from)
    {
        if (passageType != RoomPassageType.OpenBoundary) return ApproachPosition;
        Vector2 tangent = new Vector2(-PassageDirection.y, PassageDirection.x);
        return ApproachPosition + tangent * Vector2.Dot(from - ApproachPosition, tangent);
    }

    public Vector2 GetEntryPosition(Vector2 from)
    {
        if (passageType != RoomPassageType.OpenBoundary) return EntryPosition;
        Vector2 tangent = new Vector2(-PassageDirection.y, PassageDirection.x);
        return EntryPosition + tangent * Vector2.Dot(from - ApproachPosition, tangent);
    }

    public Vector2 GetOpenArrival(Vector2 start, Vector2 movementDirection)
    {
        Vector2 normal = PassageDirection;
        Vector2 direction = movementDirection.sqrMagnitude > 0.0001f ? movementDirection.normalized : normal;
        float forward = Vector2.Dot(direction, normal);
        if (forward <= 0f) direction = normal;
        forward = Mathf.Max(Vector2.Dot(direction, normal), 0.0001f);
        float distance = Mathf.Max(0.1f, Vector2.Dot(EntryPosition - start, normal));
        Vector2 arrival = start + direction * (distance / forward);

        // Um clique quase paralelo a passagem nao pode apontar para fora do andar.
        BoxCollider2D roomArea = destinationRoom != null ? destinationRoom.GetComponent<BoxCollider2D>() : null;
        if (roomArea != null)
        {
            Vector2 tangent = new Vector2(-normal.y, normal.x);
            Bounds bounds = roomArea.bounds;
            float halfWidth = Mathf.Abs(tangent.x) * bounds.extents.x + Mathf.Abs(tangent.y) * bounds.extents.y;
            float offset = Vector2.Dot(arrival - (Vector2)bounds.center, tangent);
            float limit = Mathf.Max(0f, halfWidth - 0.05f);
            arrival += tangent * (Mathf.Clamp(offset, -limit, limit) - offset);
        }
        return arrival;
    }

    private Vector2 GetClearOpenArrival(Rigidbody2D body, Vector2 arrival)
    {
        Vector2 start = body.position;
        Vector2 normal = PassageDirection;
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(body.gameObject.layer));
        filter.useTriggers = false;

        // Usa o tamanho real do jogador para limitar a diagonal antes de uma parede ou movel.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Vector2 travel = arrival - start;
            float distance = travel.magnitude;
            if (distance < 0.001f) break;
            int count = body.Cast(travel / distance, filter, openPassageHits, distance);
            float freeDistance = distance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = openPassageHits[i];
                if (hit.rigidbody == body || hit.collider.GetComponentInParent<BossDoorChase>() != null)
                    continue;
                freeDistance = Mathf.Min(freeDistance, Mathf.Max(0f, hit.distance - 0.03f));
            }
            if (freeDistance >= distance) break;

            Vector2 stop = start + travel / distance * freeDistance;
            arrival = start + normal * Vector2.Dot(arrival - start, normal) +
                tangent * Vector2.Dot(stop - start, tangent);
        }
        return arrival;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartTransition(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartTransition(other);
    }

    private void TryStartTransition(Collider2D other)
    {
        if (passageType != RoomPassageType.Door || !isActiveAndEnabled || transitioningPlayer != null)
            return;

        Rigidbody2D body = other.attachedRigidbody;

        if(body == null) return;

        PlayerMove playerMove = body.GetComponent<PlayerMove>();
        PlayerRoomTracker playerTracker = body.GetComponent<PlayerRoomTracker>();

        if (!CanStartTransition(playerMove, playerTracker) || !HasReferences())
            return;

        Vector2 crossingDirection = entryPoint.position - approachPoint.position;
        Collider2D doorway = GetComponent<Collider2D>();
        if (doorway == null || !playerMove.WantsToCrossDoor(crossingDirection, doorway.bounds))
            return;

        TryBeginTransition(playerMove, playerTracker);
    }

    private IEnumerator TransitionCoroutine(PlayerMove playerMove, PlayerRoomTracker playerTracker)
    {
        TrackTransition(playerMove, playerTracker);
        bool completed = false;

        try
        {
            playerMove.BeginTransition(approachPoint.position);

            float elapsed = 0f;
            float timeout = Mathf.Max(movementTimeout, 0.1f);

            while (CanContinueTransition && (GameFlow.IsPaused || (!playerMove.ReachedTarget && elapsed < timeout)))
            {
                if (GameFlow.IsPaused) { yield return null; continue; }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!GameFlow.CanPlay || playerMove == null || playerMove.IsDefeated)
                yield break;

            if (!playerMove.isActiveAndEnabled || !playerMove.ReachedTarget)
            {
                Debug.LogWarning("Nao foi possivel alinhar o jogador com a porta.", this);
                yield break;
            }

            playerMove.BeginTransition(entryPoint.position);

            Vector3 destination = cameraPoint.position;
            destination.z = cameraStartPosition.z;

            elapsed = 0f;
            float duration = Mathf.Max(transitionDuration, 0.01f);
            timeout = Mathf.Max(timeout, duration);

            while (CanContinueTransition && (GameFlow.IsPaused || elapsed < duration || !playerMove.ReachedTarget))
            {
                if (GameFlow.IsPaused) { yield return null; continue; }
                if (elapsed >= timeout && !playerMove.ReachedTarget)
                {
                    Debug.LogWarning("Nao foi possivel atravessar a porta.", this);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

                transitioningCamera.position = Vector3.Lerp(cameraStartPosition, destination, smoothProgress);
                yield return null;
            }

            completed = GameFlow.CanPlay && playerMove != null && playerMove.isActiveAndEnabled && !playerMove.IsDefeated &&
                playerMove.ReachedTarget && transitioningCamera != null;

            if (completed)
                transitioningCamera.position = destination;
        }
        finally
        {
            FinishTransition(completed);
        }
    }

    private void TrackTransition(PlayerMove playerMove, PlayerRoomTracker playerTracker)
    {
        transitioningPlayer = playerMove;
        transitioningTracker = playerTracker;
        transitioningCamera = Camera.main.transform;
        playerStartPosition = playerMove.GetComponent<Rigidbody2D>().position;
        cameraStartPosition = transitioningCamera.position;
    }

    private bool CanContinueTransition => (GameFlow.CanPlay || GameFlow.IsPaused) && transitioningPlayer != null &&
        transitioningPlayer.isActiveAndEnabled && !transitioningPlayer.IsDefeated && transitioningCamera != null;

    private IEnumerator OpenTransitionCoroutine(PlayerMove playerMove, PlayerRoomTracker playerTracker)
    {
        Rigidbody2D body = playerMove.GetComponent<Rigidbody2D>();
        Vector2 arrival = GetClearOpenArrival(body, GetOpenArrival(body.position, playerMove.MovementDirection));
        TrackTransition(playerMove, playerTracker);
        bool completed = false;
        try
        {
            playerMove.BeginTransition(arrival);
            float elapsed = 0f;
            float bestDistance = Vector2.Distance(playerStartPosition, arrival);
            float lastProgressTime = 0f;
            float duration = Mathf.Max(transitionDuration, 0.01f);
            float travelDistance = Mathf.Max(Vector2.Distance(playerStartPosition, arrival), 0.001f);
            Vector3 destination = cameraPoint.position;
            destination.z = cameraStartPosition.z;
            while (CanContinueTransition && (GameFlow.IsPaused || elapsed < duration || !playerMove.ReachedTarget))
            {
                if (GameFlow.IsPaused) { yield return null; continue; }
                if (elapsed > Mathf.Max(movementTimeout, duration)) yield break;
                elapsed += Time.deltaTime;
                float distance = Vector2.Distance(body.position, arrival);
                if (distance < bestDistance - 0.001f)
                {
                    bestDistance = distance;
                    lastProgressTime = elapsed;
                }
                else if (!playerMove.ReachedTarget && elapsed - lastProgressTime > 0.6f)
                {
                    Vector2 stoppedAt = body.position;
                    if (!destinationRoom.Contains(stoppedAt)) yield break;
                    playerMove.BeginTransition(stoppedAt);
                }
                float progress = Mathf.Min(Mathf.Clamp01(elapsed / duration), Mathf.Clamp01(1f - distance / travelDistance));
                transitioningCamera.position = Vector3.Lerp(cameraStartPosition, destination,
                    Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }
            completed = CanContinueTransition && playerMove.ReachedTarget &&
                destinationRoom.Contains(playerMove.GetComponent<Rigidbody2D>().position);
            if (completed) transitioningCamera.position = destination;
        }
        finally { FinishTransition(completed); }
    }

    private IEnumerator StairTransitionCoroutine(PlayerMove playerMove, PlayerRoomTracker playerTracker)
    {
        TrackTransition(playerMove, playerTracker);
        bool completed = false;
        try
        {
            yield return MoveStairStep(ApproachPosition);
            yield return WaitForResume();
            if (!CanContinueTransition || !playerMove.ReachedTarget) yield break;
            Vector2 climb = stairClimbPoint != null ? (Vector2)stairClimbPoint.position : ApproachPosition + Vector2.up * 0.8f;
            yield return MoveStairStep(climb);
            yield return WaitForResume();
            if (!CanContinueTransition || !playerMove.ReachedTarget) yield break;
            yield return FadeStairs(0f, 1f);
            yield return WaitForResume();
            if (!CanContinueTransition) yield break;

            Rigidbody2D body = playerMove.GetComponent<Rigidbody2D>();
            body.position = EntryPosition;
            playerMove.transform.position = new Vector3(EntryPosition.x, EntryPosition.y, playerMove.transform.position.z);
            playerMove.BeginTransition(EntryPosition);
            Vector3 destination = cameraPoint.position;
            destination.z = cameraStartPosition.z;
            transitioningCamera.position = destination;
            Physics2D.SyncTransforms();
            yield return FadeStairs(1f, 0f);
            yield return WaitForResume();
            completed = CanContinueTransition;
        }
        finally { FinishTransition(completed); }
    }

    private IEnumerator MoveStairStep(Vector2 target)
    {
        transitioningPlayer.BeginTransition(target);
        float elapsed = 0f;
        float bestDistance = Vector2.Distance(transitioningPlayer.transform.position, target);
        float lastProgressTime = 0f;
        while (CanContinueTransition && (GameFlow.IsPaused || (!transitioningPlayer.ReachedTarget && elapsed < movementTimeout)))
        {
            if (GameFlow.IsPaused) { yield return null; continue; }
            elapsed += Time.deltaTime;
            float distance = Vector2.Distance(transitioningPlayer.transform.position, target);
            if (distance < bestDistance - 0.001f) { bestDistance = distance; lastProgressTime = elapsed; }
            else if (elapsed - lastProgressTime > 0.75f) yield break;
            yield return null;
        }
    }

    private IEnumerator FadeStairs(float from, float to)
    {
        float elapsed = 0f;
        if (transitionFade != null)
        {
            transitionFade.gameObject.SetActive(true);
            transitionFade.blocksRaycasts = true;
        }
        float duration = StairDuration * 0.5f;
        while (CanContinueTransition && (GameFlow.IsPaused || elapsed < duration))
        {
            if (GameFlow.IsPaused) { yield return null; continue; }
            elapsed += Time.deltaTime;
            if (transitionFade != null) transitionFade.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
    }

    private IEnumerator WaitForResume()
    {
        while (CanContinueTransition && GameFlow.IsPaused) yield return null;
    }

    private void FinishTransition(bool completed)
    {
        bool wasTransitioning = transitioningPlayer != null;
        bool defeated = transitioningPlayer != null && transitioningPlayer.IsDefeated;

        if (transitioningPlayer != null && !defeated)
        {
            if (completed)
                transitioningPlayer.EndTransition();
            else
                transitioningPlayer.CancelTransition(playerStartPosition);

            RoomZone room = completed ? destinationRoom : sourceRoom;
            if (transitioningTracker != null && room != null)
                transitioningTracker.EnterRoom(room);
        }

        if (!completed && !defeated && transitioningCamera != null)
            transitioningCamera.position = cameraStartPosition;

        if (wasTransitioning && transitionFade != null)
        {
            transitionFade.alpha = 0f;
            transitionFade.blocksRaycasts = false;
            transitionFade.gameObject.SetActive(false);
        }

        transitioningPlayer = null;
        transitioningTracker = null;
        transitioningCamera = null;
    }

    private void OnDisable()
    {
        if (GameFlow.Instance != null) GameFlow.Instance.StateChanged -= OnGameStateChanged;
        activeDoors.Remove(this);
        StopAllCoroutines();
        FinishTransition(false);
    }
}
