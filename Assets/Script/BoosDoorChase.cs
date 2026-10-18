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

    [Header("Debug (so leitura)")]
    [SerializeField] private float currentChance;
    [SerializeField] private bool chasing;

    private enum DoorPhase { None, ToApproach, ToEntry }

    private Rigidbody2D rb;
    private RoomZone currentRoom;
    private DoorTransition door;
    private DoorPhase phase = DoorPhase.None;
    private float resumeTime;
    private float nextDecisionTime;
    private float chaseEndTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerMove>();
        if (playerRoom == null && player != null) playerRoom = player.GetComponent<PlayerRoomTracker>();
        if (gameManager == null) gameManager = FindAnyObjectByType<CandleManager>();

        currentRoom = RoomZone.FindAt(rb.position);
    }

    void Update()
    {
        if (player == null || player.IsDefeated || (gameManager != null && gameManager.IsGameOver))
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
        if (player == null || playerRoom == null) return;
        if (player.IsDefeated || (gameManager != null && gameManager.IsGameOver)) return;
        if (Time.time < resumeTime) return;

        // atravessando uma porta: termina o caminho mesmo que a perseguição tenha acabado,
        // pra ele não ficar parado no meio do vão
        if (phase != DoorPhase.None)
        {
            FollowDoor();
            return;
        }

        if (!chasing) return;

        RoomZone targetRoom = playerRoom.CurrentRoom;
        if (targetRoom == null) return; // player no meio de uma porta: espera

        if (currentRoom == null)
            currentRoom = RoomZone.FindAt(rb.position);

        // mesma sala (ou fora de qualquer sala): vai direto no player
        if (currentRoom == null || currentRoom == targetRoom)
        {
            ChasePlayer();
            return;
        }

        door = DoorTransition.NextDoorTowards(currentRoom, targetRoom);
        if (door != null)
            phase = DoorPhase.ToApproach;
    }

    void ChasePlayer()
    {
        Vector2 target = player.transform.position;
        MoveTo(target);

        if (!player.IsTransitioning && gameManager != null &&
            Vector2.Distance(rb.position, target) <= catchDistance)
            gameManager.LoseGame();
    }

    void FollowDoor()
    {
        if (door == null)
        {
            phase = DoorPhase.None;
            return;
        }

        Vector2 target = phase == DoorPhase.ToApproach ? door.ApproachPosition : door.EntryPosition;
        MoveTo(target);

        if (Vector2.Distance(rb.position, target) > pointReachedDistance)
            return;

        if (phase == DoorPhase.ToApproach)
        {
            phase = DoorPhase.ToEntry;
        }
        else
        {
            currentRoom = door.DestinationRoom;
            door = null;
            phase = DoorPhase.None;
        }

        resumeTime = Time.time + doorPause;
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