using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CandleLighting : MonoBehaviour
{
    [SerializeField] private Light2D globalLight;
    [SerializeField] private Candle[] candles = new Candle[3];

    [SerializeField, Min(0f)]
    private float allLitIntensity = 0.25f;

    [SerializeField, Min(0f)]
    private float candleOutIntensity = 0.06f;

    [SerializeField, Min(0.01f)]
    private float fadeSpeed = 0.4f;

    private void Start()
    {
        if (globalLight != null)
            globalLight.intensity = GetTargetIntensity();
    }

    private void LateUpdate()
    {
        if (!GameFlow.CanPlay || globalLight == null)
            return;

        globalLight.intensity = Mathf.MoveTowards(
            globalLight.intensity,
            GetTargetIntensity(),
            fadeSpeed * Time.deltaTime
        );
    }

    private float GetTargetIntensity()
    {
        if (candles == null || candles.Length != 3)
            return candleOutIntensity;

        foreach (Candle candle in candles)
        {
            if (candle == null || !candle.IsLit)
                return candleOutIntensity;
        }

        return allLitIntensity;
    }
}
