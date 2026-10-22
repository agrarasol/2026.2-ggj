using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Junta mouse (PC) e toque (Android) num lugar so.
public static class PointerInput
{
    private static Vector2 lastTouchPosition = new Vector2(-1f, -1f);

    public static bool Available => Mouse.current != null || Touchscreen.current != null;

    public static Vector2 Position
    {
        get
        {
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                lastTouchPosition = touch.primaryTouch.position.ReadValue();
                return lastTouchPosition;
            }

            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            // dedo levantado: mantem o ultimo ponto tocado (nao pula pro canto da tela)
            if (lastTouchPosition.x < 0f)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            return lastTouchPosition;
        }
    }

    public static bool PressedThisFrame =>
        (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
        (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

    public static bool IsPressed =>
        (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
        (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed);

    // true se o mouse/dedo esta em cima de um botao ou painel da UI
    public static bool IsOverUI
    {
        get
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;

            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
                return es.IsPointerOverGameObject(touch.primaryTouch.touchId.ReadValue());

            return es.IsPointerOverGameObject();
        }
    }
}
