using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RoomZone : MonoBehaviour
{
    private static readonly List<RoomZone> allRooms = new List<RoomZone>();
    private BoxCollider2D box;

    void Awake() { box = GetComponent<BoxCollider2D>(); }
    void OnEnable() { if (!allRooms.Contains(this)) allRooms.Add(this); }
    void OnDisable() { allRooms.Remove(this); }

    public bool Contains(Vector2 point) => box != null && box.OverlapPoint(point);

    public static RoomZone FindAt(Vector2 point)
    {
        foreach (RoomZone room in allRooms)
            if (room != null && room.Contains(point))
                return room;
        return null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        PlayerRoomTracker playerTracker = body != null
            ? body.GetComponent<PlayerRoomTracker>()
            : other.GetComponentInParent<PlayerRoomTracker>();

        if (playerTracker == null || playerTracker.CurrentRoom != null)
            return;

        PlayerMove playerMove = playerTracker.GetComponent<PlayerMove>();
        if (playerMove != null && playerMove.IsTransitioning)
            return;

        playerTracker.EnterRoom(this);
    }
}