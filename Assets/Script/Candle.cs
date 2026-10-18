using UnityEngine;

public class Candle : MonoBehaviour
{
    [SerializeField] private RoomZone room;

    [SerializeField] private Transform interactionPoint;

    [SerializeField] private GameObject candleLight;

    private SpriteRenderer[] candleRenderers;

    public RoomZone Room => room;

    public Vector2 InteractionPosition => interactionPoint!=null ? interactionPoint.position : gameObject.transform.position;

    public bool IsLit { get; private set; } = false;

    void Awake()
    {
        candleRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        IsLit = true;
    }

    public bool ContainsPointer(Vector2 position)
    {
        if (!isActiveAndEnabled)
            return false;

        foreach (SpriteRenderer candleRenderer in candleRenderers)
        {
            if (candleRenderer == null || candleRenderer.sprite == null || !candleRenderer.enabled ||
                !candleRenderer.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = candleRenderer.bounds;
            if (position.x >= bounds.min.x && position.x <= bounds.max.x &&
                position.y >= bounds.min.y && position.y <= bounds.max.y)
                return true;
        }
        return false;
    }

    public void SetLit(bool isLit)
    {
        IsLit = isLit;

        if (candleLight != null)
        {
            candleLight.SetActive(isLit);
        }
    }

}
