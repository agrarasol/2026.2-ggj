using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    public float speed = 5f;
    public float deadZone = 0.05f;        // distância horizontal mínima pra ele virar
    public bool holdToFollow = true;      // segurando o botão, o destino acompanha o mouse
    public bool spriteFacesRight = false; // desenhado olhando pra direita? true. Pra esquerda? false.

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Camera cam;
    private Vector2 targetPosition;
    private bool pressStartedOnUI;

    public bool IsTransitioning { get; private set;}
    public bool IsDefeated { get; private set; }
    public bool IsInteracting { get; private set; }
    private bool waitForMouseRelease;
    private int consumedMouseFrame = -1;
    public bool ReachedTarget => Vector2.Distance(rb.position, targetPosition) < 0.01f;
    public Vector2 MovementDirection => (targetPosition - rb.position).normalized;

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

    private void OnEnable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged -= OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            EndTransition();
            return;
        }

        targetPosition = rb.position;
        rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
        if (!GameFlow.CanPlay || IsDefeated || IsTransitioning || IsInteracting ||
            consumedMouseFrame == Time.frameCount || Mouse.current == null) return;

        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        if(waitForMouseRelease)
        {
            if (Mouse.current.leftButton.isPressed &&
                !Mouse.current.leftButton.wasPressedThisFrame)
                return;
            
            waitForMouseRelease = false;
        }

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
            sr.flipX = spriteFacesRight ? !mouseOnLeft : mouseOnLeft;
        }
    }

    void FixedUpdate()
    {
        if (!GameFlow.CanPlay || IsDefeated || IsInteracting)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // MoveTowards já para exatamente no destino, então não treme
        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPosition, speed * Time.fixedDeltaTime);

        if (!IsTransitioning && DoorTransition.TryHandleCrossing(this, rb, newPos))
            return;

        rb.MovePosition(newPos);
    }

    public void BeginTransition(Vector2 destination)
    {
        if (!GameFlow.CanPlay || IsDefeated || IsInteracting)
            return;

        IsTransitioning = true;
        rb.linearVelocity = Vector2.zero;
        targetPosition = destination;
    }

    public void ConsumeMovementClick()
    {
        targetPosition = rb.position;
        rb.linearVelocity = Vector2.zero;
        consumedMouseFrame = Time.frameCount;
        waitForMouseRelease = true;
        pressStartedOnUI = false;
    }

    public void EndTransition()
    {
        targetPosition = rb.position;
        rb.linearVelocity = Vector2.zero;

        IsTransitioning = false;
        waitForMouseRelease = Mouse.current != null && Mouse.current.leftButton.isPressed;
        pressStartedOnUI = false;
    }

    public bool TryBeginInteraction()
    {
        if (!GameFlow.CanPlay || IsDefeated || IsTransitioning || IsInteracting)
            return false;

        IsInteracting = true;
        targetPosition = rb.position;
        rb.linearVelocity = Vector2.zero;
        return true;
    }

    public void EndInteraction()
    {
        IsInteracting = false;
        targetPosition = rb.position;
        rb.linearVelocity = Vector2.zero;
        waitForMouseRelease = Mouse.current != null && Mouse.current.leftButton.isPressed;
        pressStartedOnUI = false;
    }

    public bool WantsToCrossDoor(Vector2 direction, Bounds doorwayBounds)
    {
        if (!GameFlow.CanPlay || IsDefeated || IsTransitioning || IsInteracting || waitForMouseRelease || direction.sqrMagnitude < 0.0001f)
            return false;

        Vector2 normal = direction.normalized;
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        Vector2 movement = targetPosition - rb.position;
        float forward = Vector2.Dot(movement, normal);
        float sideways = Mathf.Abs(Vector2.Dot(movement, tangent));

        // Uma pequena componente para tras num clique lateral nao e uma volta.
        if (forward <= 0.01f || forward < sideways)
            return false;

        Vector2 center = doorwayBounds.center;
        float startSide = Vector2.Dot(rb.position - center, normal);
        float targetSide = Vector2.Dot(targetPosition - center, normal);
        float halfDepth = Mathf.Abs(normal.x) * doorwayBounds.extents.x +
            Mathf.Abs(normal.y) * doorwayBounds.extents.y;

        // O clique precisa alcancar a entrada. 
        if (startSide > 0.01f || targetSide <= -halfDepth + 0.05f)
            return false;

        Vector2 crossingPoint = rb.position + movement * (-startSide / forward);
        float halfWidth = Mathf.Abs(tangent.x) * doorwayBounds.extents.x +
            Mathf.Abs(tangent.y) * doorwayBounds.extents.y;

        // Clicar do outro lado de uma parede, fora da abertura, nao usa a porta.
        return Mathf.Abs(Vector2.Dot(crossingPoint - center, tangent)) <= halfWidth;
    }

    public void CancelTransition(Vector2 position)
    {
        rb.position = position;
        EndTransition();
    }

    public void Defeat()
    {
        IsDefeated = true;
        IsInteracting = false;
        IsTransitioning = false;
        targetPosition = rb.position;
        rb.linearVelocity = Vector2.zero;
    }
}
