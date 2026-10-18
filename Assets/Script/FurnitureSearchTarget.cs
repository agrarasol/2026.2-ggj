using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FurnitureSearchTarget : MonoBehaviour
{
    private static readonly List<FurnitureSearchTarget> targets = new List<FurnitureSearchTarget>();
    public static IReadOnlyList<FurnitureSearchTarget> Targets => targets;

    [SerializeField] private string displayName = "Móvel";
    [SerializeField] private RoomZone room;
    [SerializeField] private SpriteRenderer furnitureRenderer;
    [SerializeField] private Collider2D furnitureCollider;
    [SerializeField, Min(0.1f)] private float searchDuration = 2.5f;
    [SerializeField, Min(0f)] private float cooldown = 25f;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.1f;

    private float nextSearchTime;
    private bool reserved;
    public string DisplayName => displayName;
    public RoomZone Room => room;
    public SpriteRenderer Renderer => furnitureRenderer;
    public float SearchDuration => Mathf.Max(0.1f, searchDuration);
    public float CooldownRemaining => Mathf.Max(0f, nextSearchTime - Time.time);
    public bool IsAvailable => isActiveAndEnabled && !reserved && CooldownRemaining <= 0f;

    private void OnEnable() { if (!targets.Contains(this)) targets.Add(this); }
    private void OnDisable() { targets.Remove(this); reserved = false; }

    public bool ContainsPointer(Vector2 position)
    {
        if (furnitureRenderer == null || !furnitureRenderer.enabled ||
            !furnitureRenderer.gameObject.activeInHierarchy)
            return false;
        Bounds bounds = furnitureRenderer.bounds;
        return position.x >= bounds.min.x && position.x <= bounds.max.x &&
            position.y >= bounds.min.y && position.y <= bounds.max.y;
    }

    public bool IsInReach(Collider2D playerCollider)
    {
        if (playerCollider == null || !playerCollider.enabled || furnitureCollider == null ||
            !furnitureCollider.enabled || !furnitureCollider.gameObject.activeInHierarchy)
            return false;

        ColliderDistance2D gap = playerCollider.Distance(furnitureCollider);
        if (!gap.isValid || gap.distance > interactionDistance)
            return false;

        Vector2 start = playerCollider.bounds.center;
        Vector2 end = furnitureCollider.ClosestPoint(start);
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(start, end))
        {
            Collider2D obstacle = hit.collider;
            if (obstacle.isTrigger || obstacle == playerCollider || obstacle == furnitureCollider ||
                (playerCollider.attachedRigidbody != null && obstacle.attachedRigidbody == playerCollider.attachedRigidbody) ||
                obstacle.transform.IsChildOf(transform))
                continue;
            return false;
        }
        return true;
    }

    public bool TryReserve()
    {
        if (!GameFlow.CanPlay || !IsAvailable)
            return false;
        reserved = true;
        return true;
    }

    public bool CompleteSearch()
    {
        if (!GameFlow.CanPlay || !isActiveAndEnabled || !reserved)
            return false;
        reserved = false;
        nextSearchTime = Time.time + Mathf.Max(0f, cooldown);
        return true;
    }

    public void CancelSearch() => reserved = false;
}
