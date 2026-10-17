using System;
using UnityEngine;

public class PlayerRoomTracker : MonoBehaviour
{
    private RoomZone _currentRoom;

    public RoomZone CurrentRoom
    {
        get => _currentRoom;
        set => _currentRoom = value;
    }

    public void EnterRoom(RoomZone room)
    {
        _currentRoom = room;
        Debug.Log($"Entered room: {room.name}");
    }

    public void ExitRoom(RoomZone room)
    {
        if (_currentRoom == room)
        {
            _currentRoom = null;
            Debug.Log($"Exited room: {room.name}");
        }
    }
}
