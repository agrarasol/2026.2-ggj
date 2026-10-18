using System.Collections;
using TMPro;
using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    public static TutorialUI instance;
    public static TMP_Text text;

    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float textDuration = 3.0f;

    private Coroutine messageCoroutine;

    void Awake()
    {
        instance = this;
        text = GetComponent<TMP_Text>();
    }

    public static void ShowMensage(string message)
    {
        if (instance == null)
        {
            return;
        }

        text.text = message;
        instance.messageCoroutine = instance.StartCoroutine(instance.StartMessage());
    }

    private IEnumerator StartMessage()
    {
        yield return StartCoroutine(Fade(0, 1));
        yield return new WaitForSeconds(textDuration);
        yield return StartCoroutine(Fade(1, 0));

        instance.messageCoroutine = null;
    }

    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float delta = 0.0f;
        SetAlpha(startAlpha);

        while (delta < fadeDuration)
        {
            delta += Time.deltaTime;

            float a =  Mathf.Lerp(
                startAlpha,
                endAlpha,
                delta / fadeDuration
            );

            SetAlpha(a);
            yield return null;
        }

        SetAlpha(endAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (text == null)
        {
            return;
        }

        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }
}
