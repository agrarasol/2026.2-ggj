using UnityEngine;
[RequireComponent(typeof(BoxCollider2D))]
public class RoomZone : MonoBehaviour
{

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerRoomTracker playerTracker = other.GetComponent<PlayerRoomTracker>();
        
        if (playerTracker != null)
        {
            playerTracker.EnterRoom(this);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        PlayerRoomTracker playerTracker = other.GetComponent<PlayerRoomTracker>();

        if (playerTracker != null)
        {
            playerTracker.ExitRoom(this);
        }
    }
}
