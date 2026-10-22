using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class GameOverSequence : MonoBehaviour
{
    [Header("Painel (Canvas)")]
    [SerializeField] private CanvasGroup panel;            // painel preto que cobre a tela
    [SerializeField] private RectTransform creditsContent; // texto dos créditos
    [FormerlySerializedAs("restartHint")]
    [SerializeField] private GameObject skipHint;

    [Header("Logos finais")]
    [SerializeField] private CanvasGroup logosGroup;       // logo do jogo + logo da empresa
    [SerializeField, Min(0f)] private float pauseBeforeLogos = 0.8f;
    [SerializeField, Min(0.1f)] private float logosFadeDuration = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip deathClip;          // deixa vazio por enquanto
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    [Header("Tempos")]
    [SerializeField, Min(0.1f)] private float fadeDuration = 1.5f;
    [SerializeField, Min(10f)] private float scrollSpeed = 60f;

    private bool started;
    private int startedFrame;
    private AudioClip fallbackClip;
    private Graphic panelBackground;
    private Color panelBackgroundColor;

    public bool IsRunning => started && !IsComplete;
    public bool IsComplete { get; private set; }
    public event Action Completed;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (panel != null)
        {
            panelBackground = panel.GetComponent<Graphic>();
            if (panelBackground != null) panelBackgroundColor = panelBackground.color;
        }
        HidePanel();
    }

    public void HidePanel()
    {
        if (panelBackground != null) panelBackground.color = panelBackgroundColor;
        if (panel != null)
        {
            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }
        if (logosGroup != null) logosGroup.alpha = 0f;
        if (skipHint != null) skipHint.SetActive(false);
    }

    // Os creditos podem ser abertos pelo menu inicial ou depois do Game Over.
    public void Play()
    {
        if (started || GameFlow.Instance == null || GameFlow.Instance.IsLoading ||
            GameFlow.Instance.State != GameState.Credits)
            return;

        started = true;
        startedFrame = Time.frameCount;
        IsComplete = false;
        StartCoroutine(Sequence(GameFlow.Instance.PreviousState == GameState.Menu));
    }

    private IEnumerator Sequence(bool showOverMenu)
    {
        if (creditsContent != null) creditsContent.gameObject.SetActive(false);
        if (skipHint != null) skipHint.SetActive(true);

        // 2. escurece a tela
        if (panel != null)
        {
            if (panelBackground != null)
            {
                Color color = panelBackgroundColor;
                if (showOverMenu) color.a = 0.80f;
                panelBackground.color = color;
            }
            panel.transform.SetAsLastSibling();
            panel.gameObject.SetActive(true);
            panel.blocksRaycasts = true;

            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                panel.alpha = t / fadeDuration;
                yield return null;
            }
            panel.alpha = 1f;
        }

        // 3. créditos sobem até sair da tela
        if (creditsContent != null)
        {
            RectTransform parent = creditsContent.parent as RectTransform;
            creditsContent.gameObject.SetActive(true);
            creditsContent.anchorMin = new Vector2(0.5f, 0f);
            creditsContent.anchorMax = new Vector2(0.5f, 0f);
            creditsContent.pivot = new Vector2(0.5f, 1f);
            creditsContent.anchoredPosition = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(creditsContent);

            if (creditsContent.TryGetComponent(out TMP_Text credits))
            {
                credits.ForceMeshUpdate();
                creditsContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, credits.preferredHeight);
            }

            float endY = (parent != null ? parent.rect.height : Screen.height) + creditsContent.rect.height;
            while (creditsContent.anchoredPosition.y < endY)
            {
                creditsContent.anchoredPosition += Vector2.up * (scrollSpeed * Time.unscaledDeltaTime);
                yield return null;
            }
        }

        // 4. logos aparecem no final
        yield return new WaitForSecondsRealtime(pauseBeforeLogos);

        if (logosGroup != null)
        {
            for (float t = 0f; t < logosFadeDuration; t += Time.unscaledDeltaTime)
            {
                logosGroup.alpha = t / logosFadeDuration;
                yield return null;
            }
            logosGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.5f);
        }

        IsComplete = true;
        Completed?.Invoke();
    }

    private void Update()
    {
        if (!IsRunning || Time.frameCount == startedFrame || GameFlow.Instance == null ||
            GameFlow.Instance.IsLoading || GameFlow.Instance.State != GameState.Credits)
            return;

        Keyboard keyboard = Keyboard.current;
        bool keyPressed = keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame);

        // celular: tocar na tela pula os creditos
        bool tapped = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

        if (keyPressed || tapped)
            Skip();
    }

    public void PlayDeathSound()
    {
        if (audioSource == null) return;
        if (deathClip == null && fallbackClip == null) fallbackClip = MakeFallbackTone();
        audioSource.volume = SoundSystem.EffectsGain;
        audioSource.PlayOneShot(deathClip != null ? deathClip : fallbackClip, volume);
    }

    public void Skip()
    {
        if (!IsRunning || GameFlow.Instance == null || GameFlow.Instance.State != GameState.Credits) return;
        StopAllCoroutines();
        IsComplete = true;
        Completed?.Invoke();
    }

    public void StopSequence()
    {
        StopAllCoroutines();
        if (audioSource != null)
            audioSource.Stop();

        started = false;
        IsComplete = false;
        HidePanel();
    }

    private void OnDisable()
    {
        StopSequence();
    }

    private void OnDestroy()
    {
        if (fallbackClip != null)
            Destroy(fallbackClip);
    }

    // tom grave descendo, só pra não ficar mudo enquanto não tem o som final
    private AudioClip MakeFallbackTone()
    {
        const int sampleRate = 44100;
        int samples = Mathf.RoundToInt(sampleRate * 1.5f);
        float[] data = new float[samples];
        float phase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            phase += 2f * Mathf.PI * Mathf.Lerp(220f, 40f, t) / sampleRate;
            data[i] = Mathf.Sin(phase) * (1f - t) * 0.5f;
        }

        AudioClip clip = AudioClip.Create("fallbackDeath", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
