using UnityEngine;
[RequireComponent(typeof(BoxCollider2D))]
public class RoomZone : MonoBehaviour
{

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
