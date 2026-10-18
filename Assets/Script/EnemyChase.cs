using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChase : MonoBehaviour
{
    [Header("Movimento")]
    public float speed = 3f;
    public float stopDistance = 0.5f;

    [Header("Chance de perseguir")]
    public float maxDetectDistance = 15f; // além disso nunca persegue
    public float midDistance = 6f;        // nessa distância a chance é 50%
    public float falloff = 1.5f;          // menor = queda brusca, maior = queda suave

    [Header("Decisão")]
    public float decisionInterval = 0.5f; // de quanto em quanto tempo ele "rola o dado"
    public Vector2 chaseDuration = new Vector2(2f, 4f); // quanto tempo persegue depois que decide ir

    [Header("Debug (só leitura)")]
    [SerializeField] private float currentChance;
    [SerializeField] private bool chasing;

    private Rigidbody2D rb;
    private Transform player;
    private float nextDecisionTime;
    private float chaseEndTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        else Debug.LogWarning("EnemyChase: nenhum objeto com a tag Player na cena.");
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        currentChance = ChaseChance(dist);

        // se estiver perseguindo, para quando o tempo acaba ou o player foge demais
        if (chasing && (Time.time >= chaseEndTime || dist > maxDetectDistance))
            chasing = false;

        // se estiver parado, rola o dado de tempos em tempos
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
        if (!chasing || player == null) return;

        Vector2 target = player.position;
        if (Vector2.Distance(rb.position, target) > stopDistance)
        {
            Vector2 newPos = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, midDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxDetectDistance);
    }
}