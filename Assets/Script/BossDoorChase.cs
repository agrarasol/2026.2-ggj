using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossDoorChase : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CandleManager gameManager;
    [SerializeField] private PlayerMove player;
    [SerializeField] private PlayerRoomTracker playerRoom;

    [Header("Movimento")]
    [SerializeField, Min(0.1f)] private float speed = 2.5f;
    [SerializeField, Min(0.1f)] private float catchDistance = 0.6f;
    [SerializeField, Min(0.01f)] private float pointReachedDistance = 0.1f;

    [Header("Chance de perseguir")]
    [SerializeField] private float maxDetectDistance = 15f; // além disso nunca persegue
    [SerializeField] private float midDistance = 6f;        // nessa distância a chance é 50%
    [SerializeField] private float falloff = 1.5f;          // menor = queda brusca

    [Header("Decisao")]
    [SerializeField] private float decisionInterval = 0.5f;
    [SerializeField] private Vector2 chaseDuration = new Vector2(4f, 8f);

    [Header("Portas")]
    [SerializeField, Min(0f)] private float doorPause = 0.4f;
    [Tooltip("Recuo em relacao ao ponto de aproximacao enquanto o jogador troca de sala.")]
    [SerializeField, Min(0.1f)] private float doorWaitingDistance = 1.5f;
    [Tooltip("Quanto o boss avanca alem da chegada do jogador para liberar a passagem.")]
    [SerializeField, Min(0.1f)] private float doorClearanceDistance = 1.5f;

    [Header("Debug (so leitura)")]
    [SerializeField] private float currentChance;
    [SerializeField] private bool chasing;

    public bool IsChasing => chasing;
    public RoomZone CurrentRoom => currentRoom;

    private enum DoorPhase { None, ToApproach, ToEntry, ToClearance }

    private Rigidbody2D rb;
    private Collider2D bossCollider;
    private Collider2D playerCollider;
    private RoomZone currentRoom;
    private DoorTransition door;
    private DoorPhase phase = DoorPhase.None;
    private float resumeTime;
    private float nextDecisionTime;
    private float chaseEndTime;
    private Vector2 clearancePoint;
    private float clearanceBestDistance;
    private float clearanceLastProgressTime;
    private readonly RaycastHit2D[] clearanceHits = new RaycastHit2D[16];
    private Vector2 passageApproach;
    private Vector2 passageEntry;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bossCollider = GetComponent<Collider2D>();
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerMove>();
        if (playerRoom == null && player != null) playerRoom = player.GetComponent<PlayerRoomTracker>();
        if (gameManager == null) gameManager = FindAnyObjectByType<CandleManager>();
        if (player != null) playerCollider = player.GetComponent<Collider2D>();

        currentRoom = RoomZone.FindAt(rb.position);
    }

    void Update()
    {
        if (!GameFlow.CanPlay || player == null || player.IsDefeated || (gameManager != null && gameManager.IsGameOver))
            return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        currentChance = ChaseChance(dist);

        // para de perseguir quando o tempo acaba ou o player fica longe demais
        if (chasing && (Time.time >= chaseEndTime || dist > maxDetectDistance))
            chasing = false;

        // parado: rola o dado de tempos em tempos
        if (!chasing && Time.time >= nextDecisionTime)
        {
            nextDecisionTime = Time.time + decisionInterval;

            if (Random.value < currentChance)
            {
                chasing = true;
                chaseEndTime = Time.time + Random.Range(chaseDuration.x, chaseDuration.y);
                TutorialManager.CallChaseTutorial();
            }
        }
    }

    float ChaseChance(float dist)
    {
        if (dist > maxDetectDistance) return 0f;
        return 1f / (1f + Mathf.Exp((dist - midDistance) / falloff));
    }

    void FixedUpdate()
    {
        if (!GameFlow.CanPlay || player == null || playerRoom == null ||
            player.IsDefeated || (gameManager != null && gameManager.IsGameOver))
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        RoomZone targetRoom = playerRoom.CurrentRoom;
        if (currentRoom == null)
            currentRoom = RoomZone.FindAt(rb.position);

        // Depois de cruzar o centro da porta, termina de liberar o vao.
        bool committed = phase == DoorPhase.ToClearance ||
            (phase == DoorPhase.ToEntry && HasCrossedDoor());
        if (!committed)
        {
            // CurrentRoom ainda pode ser a sala antiga durante a transicao do player.
            if (player.IsTransitioning || targetRoom == null)
            {
                WaitOutsideDoor();
                return;
            }

            if (phase != DoorPhase.None)
            {
                DoorTransition nextDoor = chasing && currentRoom != targetRoom
                    ? DoorTransition.NextDoorTowards(currentRoom, targetRoom)
                    : null;
                if (door == null || !door.isActiveAndEnabled || door != nextDoor)
                    CancelDoorApproach();
            }
        }

        if (Time.time < resumeTime)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (phase != DoorPhase.None)
        {
            FollowDoor();
            return;
        }

        if (!chasing)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // mesma sala (ou fora de qualquer sala): vai direto no player
        if (currentRoom == null || currentRoom == targetRoom)
        {
            ChasePlayer();
            return;
        }

        door = DoorTransition.NextDoorTowards(currentRoom, targetRoom);
        if (door != null)
        {
            passageApproach = door.GetApproachPosition(rb.position);
            passageEntry = door.GetEntryPosition(rb.position);
            phase = DoorPhase.ToApproach;
        }
    }

    void ChasePlayer()
    {
        if (!TryCatchPlayer())
            MoveTo(player.transform.position);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CatchOnContact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CatchOnContact(collision);
    }

    private void CatchOnContact(Collision2D collision)
    {
        if (!GameFlow.CanPlay || player == null || player.IsDefeated ||
            gameManager == null || gameManager.IsGameOver)
            return;

        if (collision.collider.GetComponentInParent<PlayerMove>() == player)
        {
            gameManager.LoseGame();
            rb.linearVelocity = Vector2.zero;
        }
    }

    private bool TryCatchPlayer()
    {
        if (player.IsTransitioning || gameManager == null)
            return false;

        float distance = bossCollider != null && bossCollider.enabled &&
            playerCollider != null && playerCollider.enabled
            ? bossCollider.Distance(playerCollider).distance
            : Vector2.Distance(rb.position, player.transform.position);

        if (distance <= catchDistance)
        {
            gameManager.LoseGame();
            rb.linearVelocity = Vector2.zero;
            return true;
        }

        return false;
    }

    private bool HasCrossedDoor()
    {
        if (door == null)
            return false;
        if (door.PassageType == RoomPassageType.Stairs) return false;

        Vector2 direction = door.PassageDirection;
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        Collider2D doorway = door.GetComponent<Collider2D>();
        Vector2 center = doorway != null ? (Vector2)doorway.bounds.center :
            (door.ApproachPosition + door.EntryPosition) * 0.5f;
        return Vector2.Dot(rb.position - center, direction.normalized) >= 0f;
    }

    private void WaitOutsideDoor()
    {
        rb.linearVelocity = Vector2.zero;
        if (door == null || !door.isActiveAndEnabled)
        {
            CancelDoorApproach();
            return;
        }

        Vector2 direction = door.PassageDirection;
        Vector2 waitingPoint = passageApproach - direction * doorWaitingDistance;
        float retreat = Vector2.Dot(rb.position - waitingPoint, direction);
        if (retreat > pointReachedDistance)
        {
            Vector2 target = rb.position - direction * retreat;
            if (currentRoom != null && currentRoom.Contains(target))
                MoveTo(target);
        }

        phase = DoorPhase.ToApproach;
        resumeTime = 0f;
    }

    private void CancelDoorApproach()
    {
        door = null;
        phase = DoorPhase.None;
        resumeTime = 0f;
        rb.linearVelocity = Vector2.zero;
    }

    void FollowDoor()
    {
        if (phase == DoorPhase.ToClearance)
        {
            if (!player.IsTransitioning && playerRoom.CurrentRoom == currentRoom && TryCatchPlayer())
                return;

            float distance = Vector2.Distance(rb.position, clearancePoint);
            if (distance < clearanceBestDistance - 0.01f)
            {
                clearanceBestDistance = distance;
                clearanceLastProgressTime = Time.time;
            }

            // Um obstaculo que surgir depois do calculo nao prende o boss nesta etapa.
            if (distance <= pointReachedDistance || Time.time - clearanceLastProgressTime >= 0.75f)
            {
                CancelDoorApproach();
                resumeTime = Time.time + doorPause;
                return;
            }

            MoveTo(clearancePoint);
            return;
        }

        if (door == null)
        {
            CancelDoorApproach();
            return;
        }

        if (phase == DoorPhase.ToEntry && door.PassageType == RoomPassageType.Stairs)
        {
            rb.position = passageEntry;
            transform.position = new Vector3(passageEntry.x, passageEntry.y, transform.position.z);
            currentRoom = door.DestinationRoom;
            StartClearance();
            return;
        }

        Vector2 target = phase == DoorPhase.ToApproach ? passageApproach : passageEntry;
        MoveTo(target);

        if (Vector2.Distance(rb.position, target) > pointReachedDistance)
            return;

        if (phase == DoorPhase.ToApproach)
        {
            phase = DoorPhase.ToEntry;
            resumeTime = Time.time + (door.PassageType == RoomPassageType.Stairs ? door.StairDuration : doorPause);
        }
        else
        {
            currentRoom = door.DestinationRoom;
            StartClearance();
            // A pausa de chegada acontece depois de sair do vao, sem parar no EntryPoint.
        }
    }

    private void StartClearance()
    {
        clearancePoint = GetClearancePoint();
        clearanceBestDistance = Vector2.Distance(rb.position, clearancePoint);
        clearanceLastProgressTime = Time.time;
        phase = DoorPhase.ToClearance;
    }

    private Vector2 GetClearancePoint()
    {
        Vector2 direction = door.ArrivalDirection;
        Vector2 target = passageEntry + direction * doorClearanceDistance;
        if (currentRoom != null && !currentRoom.Contains(target))
        {
            for (int step = 9; step >= 0; step--)
            {
                target = passageEntry + direction * (doorClearanceDistance * step / 10f);
                if (currentRoom.Contains(target))
                    break;
            }
        }

        Vector2 travel = target - rb.position;
        float distance = travel.magnitude;
        if (bossCollider == null || !bossCollider.enabled || distance < 0.001f)
            return target;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        filter.useTriggers = false;
        int count = bossCollider.Cast(travel.normalized, filter, clearanceHits, distance);
        for (int i = 0; i < count; i++)
        {
            if (clearanceHits[i].rigidbody == rb)
                continue;

            distance = Mathf.Min(distance, Mathf.Max(0f, clearanceHits[i].distance - 0.05f));
        }

        return rb.position + travel.normalized * distance;
    }

    void MoveTo(Vector2 target)
    {
        rb.MovePosition(Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, midDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxDetectDistance);
    }
}
