using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-60)]
[RequireComponent(typeof(PlayerMove))]
public class StairInteractor : MonoBehaviour
{
    [SerializeField] private FurnitureInteractionUI interactionUI;
    private PlayerMove player;
    private PlayerRoomTracker tracker;
    private Camera worldCamera;
    private DoorTransition hovered;

    private void Awake()
    {
        player = GetComponent<PlayerMove>();
        tracker = GetComponent<PlayerRoomTracker>();
        worldCamera = Camera.main;
    }

    private void Update()
    {
        hovered = null;
        Mouse mouse = Mouse.current;
        if (!GameFlow.CanPlay || player.IsDefeated || player.IsTransitioning || player.IsInteracting ||
            tracker == null || mouse == null || worldCamera == null ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
        Vector2 position = worldCamera.ScreenToWorldPoint(mouse.position.ReadValue());
        foreach (DoorTransition passage in DoorTransition.ActivePassages)
        {
            if (passage == null || passage.SourceRoom != tracker.CurrentRoom || !passage.ContainsStairPointer(position)) continue;
            hovered = passage;
            break;
        }
        if (hovered != null && mouse.leftButton.wasPressedThisFrame && hovered.TryUseStairs(player)) hovered = null;
    }

    private void LateUpdate()
    {
        if (hovered != null && !player.IsTransitioning && GameFlow.CanPlay && interactionUI != null)
            interactionUI.ShowInteractionHint(hovered.InteractionHint);
    }
}
