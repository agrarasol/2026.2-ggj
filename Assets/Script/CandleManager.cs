using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class CandleManager : MonoBehaviour
{
    [Header("Velas")]
    [SerializeField] private Candle[] candles;

    [Header("Jogador")]
    [SerializeField] private PlayerRoomTracker roomTracker;
    [SerializeField] private Transform player;

    [Header("Intervalo entre apagamentos")]
    [SerializeField] private float minWaitTime = 10f;
    [SerializeField] private float maxWaitTime = 20f;

    [Header("Tempo para reacender")]
    [SerializeField] private float relightDuration = 25f;

    [Header("Interacao")]
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private float interactionDistance = 1.2f;

    [Header("Audio")]
    [SerializeField] private AudioSource beepSource;
    [SerializeField] private AudioClip beepClip;
    [SerializeField, Range(0f, 1f)] private float beepVolume = 0.25f;

    [SerializeField] private float slowBeepInterval = 1.2f;
    [SerializeField] private float fastBeepInterval = 0.18f;
    [SerializeField] private float directionRange = 8f;
    [Header("Alcance do Som")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 15f;
    [Header("Eventos")]
    [SerializeField] private UnityEvent onTimeExpired;

    private Candle activeCandle;

    private float remainingTime;
    private float nextExtinguishTime;
    private float beepTimer;

    private bool gameOver;

    public Candle ActiveCandle => activeCandle;
    public float RemainingTime => remainingTime;
    public float InteractionDistance => interactionDistance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        if(beepSource != null)
        {
            beepSource.playOnAwake = false;

            beepSource.spatialBlend = 0f;
        }
        ScheduleNextExtinguish();
    }

    // Update is called once per frame
    private void Update()
    {
        if(gameOver) return;

        if(activeCandle == null)
        {
            if(Time.time >= nextExtinguishTime)
            {
                TryExtinguishCandle();
            }
            return;
        }
        UpdateActiveCandle();
    }
    private void ScheduleNextExtinguish()
    {

        nextExtinguishTime = Time.time + Random.Range(minWaitTime, maxWaitTime);
    }

    private void TryExtinguishCandle()
    {
        if (activeCandle != null)
            return;
        
        if(roomTracker == null|| roomTracker.CurrentRoom == null)
        {
            nextExtinguishTime = Time.time + 1f;
            return;
        }
        List<Candle> availableCandles = new List<Candle>();
        foreach (Candle candle in candles)
        {
            if(candle == null|| !candle.IsLit|| candle.Room == roomTracker.CurrentRoom) continue;

            availableCandles.Add(candle);
        }
        if (availableCandles.Count == 0)
        {
           nextExtinguishTime = Time.time + 1f;
            return;
        }

        int randomIndex = Random.Range(0, availableCandles.Count);

        activeCandle = availableCandles[randomIndex];
        activeCandle.SetLit(false);

        remainingTime = relightDuration;
        beepTimer = 0f;

        Debug.Log("Vela Apagada: " + activeCandle.name);
        
    }
    private void UpdateActiveCandle()
    {
        remainingTime -= Time.deltaTime;

        if(remainingTime <= 0f)
        {
            remainingTime = 0f;
            HandleTimeExpired();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && interactionKey != Key.None &&
            keyboard[interactionKey].wasPressedThisFrame && TryRelight(activeCandle))
        {
            return;
        }

        UpdateBeep();
    }

    private void UpdateBeep()
    {
        beepTimer -= Time.deltaTime;

        if(beepTimer > 0f)
            return;
        
        float progress = Mathf.Clamp01(1f -remainingTime / Mathf.Max(relightDuration, 0.01f));
        float currentInterval = Mathf.Lerp(slowBeepInterval, fastBeepInterval, progress);

        PlayDirectionalBeep(progress);

        beepTimer =  Mathf.Max(currentInterval, 0.05f);
    }

    private void PlayDirectionalBeep(float progress)
    {
        if (beepSource == null|| beepClip == null||player == null|| activeCandle == null)
            return;
        
        Vector2 direction = (Vector2)activeCandle.transform.position - (Vector2)player.position;

        float stereoPan = Mathf.Clamp(direction.x / Mathf.Max(directionRange, 0.01f), -1f, 1f);

        beepSource.panStereo = stereoPan;

        float verticalDirection = Mathf.Clamp(direction.y / Mathf.Max(directionRange, 0.01f), -1f, 1f);

        beepSource.pitch = 1f + verticalDirection * 0.2f;

      
        float distance = Vector2.Distance(
            player.position,
            activeCandle.transform.position
        );

        if (distance >= maxDistance)
            return;

        float proximity = 1f - Mathf.InverseLerp(
            minDistance,
            maxDistance,
            distance
        );

        float volume = beepVolume * proximity * proximity;

        beepSource.PlayOneShot(beepClip, volume);
    }
    

    public bool TryRelight(Candle candle)
    {
        if (gameOver || player == null)
            return false;

        if (candle == null || candle != activeCandle)
            return false;

        float distance = Vector2.Distance(
            player.position,
            candle.InteractionPosition
        );

        if (distance > interactionDistance)
            return false;

        candle.SetLit(true);

        activeCandle = null;

        remainingTime = 0f;

        if (beepSource != null)
        {
            beepSource.Stop();
        }

        ScheduleNextExtinguish();

        Debug.Log("Vela reacendida com sucesso.");

        return true;
    }

    private void HandleTimeExpired()
    {
        gameOver = true;

        if (beepSource != null)
        {
            beepSource.Stop();
        }

        Debug.Log("Tempo esgotado!");

        onTimeExpired?.Invoke();
    }

    [ContextMenu("DEBUG/Apagar vela agora")]
    private void DebugExtinguish()
    {
        if (activeCandle != null)
        {
            activeCandle.SetLit(true);
            activeCandle = null;
        }

        gameOver = false;
        remainingTime = 0f;
        beepTimer = 0f;

        if (beepSource != null)
        {
            beepSource.Stop();
        }

        TryExtinguishCandle();
    }

    [ContextMenu("DEBUG/Reacender vela")]
    private void DebugRelight()
    {
        if (activeCandle == null)
            return;

        activeCandle.SetLit(true);

        activeCandle = null;

        remainingTime = 0f;

        if (beepSource != null)
        {
            beepSource.Stop();
        }

        gameOver = false;

        ScheduleNextExtinguish();
    }
}
