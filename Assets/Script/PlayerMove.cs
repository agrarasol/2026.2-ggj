using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{

    [Header("movimento")]
    public float speed = 5f;
    public float deadZone = 0.15f;
    public bool holdToFollow = true;

    [Header("Sprites ( desenhos olhando pra direita )")]
    public Sprite spriteSide;
    public Sprite spriteUpSide;
    public Sprite spriteUp;
    public Sprite spriteDownSide;
    public Sprite spriteDown;
    public bool sideSpritesFaceRight = true;

    public float speed = 5f;
    public float deadZone = 0.05f;        // distância horizontal mínima pra ele virar
    public bool holdToFollow = true;      // segurando o botão, o destino acompanha o mouse
    public bool spriteFacesRight = false; // desenhado olhando pra direita? true. Pra esquerda? false.

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Camera cam;
    private Vector2 targetPosition;
    private bool pressStartedOnUI;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        cam = Camera.main;
    }

    void Start()
    {
        targetPosition = rb.position;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressStartedOnUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!pressStartedOnUI) targetPosition = mouseWorldPos;
        }
        else if (holdToFollow && Mouse.current.leftButton.isPressed && !pressStartedOnUI)
        {
            targetPosition = mouseWorldPos;
        }

        // vira pro lado do mouse
        float dx = mouseWorldPos.x - rb.position.x;
        if (Mathf.Abs(dx) > deadZone)
        {
            bool mouseOnLeft = dx < 0;
            sr.flipX = spriteFacesRight ? mouseOnLeft : !mouseOnLeft;
        }
    }

    void FixedUpdate()
    {
        // MoveTowards já para exatamente no destino, então não treme
        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPosition, speed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }
}