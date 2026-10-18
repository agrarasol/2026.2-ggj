using UnityEngine;

public class EnemyHidingSpot : MonoBehaviour
{
    [SerializeField] private RoomZone room;
    [Tooltip("Posicao do monstro, atras do movel.")]
    [SerializeField] private Transform appearancePoint;
    [Tooltip("Centro do perigo, na parte acessivel em frente ao movel.")]
    [SerializeField] private Transform dangerPoint;
    [SerializeField, Min(0.1f)] private float dangerRadius = 1.25f;
    [SerializeField] private SpriteRenderer furnitureRenderer;

    public RoomZone Room => room;
    public Transform AppearancePoint => appearancePoint != null ? appearancePoint : transform;
    public Vector2 DangerPosition => dangerPoint != null ? dangerPoint.position : transform.position;
    public float DangerRadius => dangerRadius;
    public SpriteRenderer FurnitureRenderer => furnitureRenderer;

    public float DistanceToPlayer(Transform player, Collider2D playerCollider)
    {
        Vector2 position = playerCollider != null && playerCollider.enabled &&
            playerCollider.gameObject.activeInHierarchy
            ? playerCollider.ClosestPoint(DangerPosition)
            : (Vector2)player.position;

        return Vector2.Distance(position, DangerPosition);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.8f);
        Gizmos.DrawWireSphere(DangerPosition, dangerRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(AppearancePoint.position, 0.15f);
        Gizmos.DrawLine(AppearancePoint.position, DangerPosition);
    }
}
