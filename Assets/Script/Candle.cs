using UnityEngine;

public class Candle : MonoBehaviour
{
    [SerializeField] private RoomZone room;

    [SerializeField] private Transform interactionPoint;

    [SerializeField] private GameObject candleLight;

    public RoomZone Room => room;

    public Vector2 InteractionPosition => interactionPoint!=null ? interactionPoint.position : gameObject.transform.position;

    public bool IsLit { get; private set; } = false;

    void Awake()
    {
        IsLit = true;
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
