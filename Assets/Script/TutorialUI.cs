using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialUI : MonoBehaviour
{
    public static TutorialUI instance;
    public CanvasGroup panel;
    public TMP_Text text;

    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float textDuration = 3.0f;

    private Coroutine messageCoroutine;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        TutorialManager.CallStartTutorial(); // Chama tutorial inicial do jogo
    }

    public void ShowMensage(string message)
    {
        if (instance == null)
        {
            return;
        }

        if (messageCoroutine != null)
            StopCoroutine(messageCoroutine);

        text.text = message;
        messageCoroutine = StartCoroutine(StartMessage());
    }

    private IEnumerator StartMessage()
    {
        yield return Fade(0, 1);
        yield return new WaitForSeconds(textDuration);
        yield return Fade(1, 0);

        messageCoroutine = null;
    }

    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float delta = 0.0f;
        panel.alpha = startAlpha;

        while (delta < fadeDuration)
        {
            delta += Time.deltaTime;

            panel.alpha =  Mathf.Lerp(
                startAlpha,
                endAlpha,
                delta / fadeDuration
            );

            yield return null;
        }

        panel.alpha = endAlpha;
    }
}
