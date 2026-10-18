using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class MouseLight : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerMove player;

    [Header("Comportamento")]
    [Tooltip("Distancia maxima da luz ate o rato. 0 = sem limite.")]
    [SerializeField, Min(0f)] private float maxDistanceFromPlayer = 4f;
    [SerializeField] private bool hideWhenDefeated = true;

    [Header("Tremida (opcional)")]
    [SerializeField, Range(0f, 0.5f)] private float flickerAmount = 0.05f;
    [SerializeField, Min(0f)] private float flickerSpeed = 8f;

    private Light2D lightSource;
    private Camera cam;
    private float baseIntensity;

    private void Awake()
    {
        lightSource = GetComponent<Light2D>();
        baseIntensity = lightSource.intensity;
        cam = Camera.main;

        if (player == null)
            player = FindFirstObjectByType<PlayerMove>();
    }

    private void LateUpdate()
    {
        if (GameFlow.IsPaused) return;
        if (Mouse.current == null)
            return;

        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        if (hideWhenDefeated && player != null)
            lightSource.enabled = !player.IsDefeated;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 position = mouseWorld;

        if (player != null && maxDistanceFromPlayer > 0f)
        {
            Vector2 center = player.transform.position;
            position = center + Vector2.ClampMagnitude(position - center, maxDistanceFromPlayer);
        }

        transform.position = new Vector3(position.x, position.y, 0f);

        if (flickerAmount > 0f)
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) - 0.5f;
            lightSource.intensity = baseIntensity * (1f + noise * 2f * flickerAmount);
        }
    }
}
