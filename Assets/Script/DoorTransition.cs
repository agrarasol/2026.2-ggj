using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorTransition : MonoBehaviour
{
    private static readonly List<DoorTransition> activeDoors = new List<DoorTransition>();

    public RoomZone SourceRoom => sourceRoom;
    public RoomZone DestinationRoom => destinationRoom;
    public Vector2 ApproachPosition => approachPoint != null ? (Vector2)approachPoint.position : (Vector2)transform.position;
    public Vector2 EntryPosition => entryPoint != null ? (Vector2)entryPoint.position : (Vector2)transform.position;

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

    private PlayerMove transitioningPlayer;
    private PlayerRoomTracker transitioningTracker;
    private Transform transitioningCamera;
    private Vector2 playerStartPosition;
    private Vector3 cameraStartPosition;
    private bool reportedMissingReferences;


    private void OnEnable()
    {
        if (!activeDoors.Contains(this))
            activeDoors.Add(this);
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
            if (door == null || !door.CanStartTransition(playerMove, tracker))
                continue;

            if (door.TryGetCrossing(body, nextPosition, out float fraction) && fraction < nearestFraction)
            {
                nearestDoor = door;
                nearestFraction = fraction;
            }
        }

        if (nearestDoor == null)
            return false;

        body.linearVelocity = Vector2.zero;
        nearestDoor.TryBeginTransition(playerMove, tracker);
        return true;
    }

    private bool TryGetCrossing(Rigidbody2D body, Vector2 nextPosition, out float fraction)
    {
        fraction = 0f;
        if (approachPoint == null || entryPoint == null)
            return false;

        Collider2D doorway = GetComponent<Collider2D>();
        Vector2 direction = entryPoint.position - approachPoint.position;
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
            !playerMove.IsDefeated &&
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

        StartCoroutine(TransitionCoroutine(playerMove, tracker));
        return true;
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
        if (!isActiveAndEnabled || transitioningPlayer != null)
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
        transitioningPlayer = playerMove;
        transitioningTracker = playerTracker;
        transitioningCamera = Camera.main.transform;
        playerStartPosition = playerMove.GetComponent<Rigidbody2D>().position;
        cameraStartPosition = transitioningCamera.position;

        bool completed = false;

        try
        {
            playerMove.BeginTransition(approachPoint.position);

            float elapsed = 0f;
            float timeout = Mathf.Max(movementTimeout, 0.1f);

            while (GameFlow.CanPlay && playerMove != null && playerMove.isActiveAndEnabled && !playerMove.IsDefeated &&
                   !playerMove.ReachedTarget && elapsed < timeout)
            {
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

            while (GameFlow.CanPlay && playerMove != null && playerMove.isActiveAndEnabled && !playerMove.IsDefeated &&
                   transitioningCamera != null &&
                   (elapsed < duration || !playerMove.ReachedTarget))
            {
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

    private void FinishTransition(bool completed)
    {
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

        transitioningPlayer = null;
        transitioningTracker = null;
        transitioningCamera = null;
    }

    private void OnDisable()
    {
        activeDoors.Remove(this);
        StopAllCoroutines();
        FinishTransition(false);
    }
}
