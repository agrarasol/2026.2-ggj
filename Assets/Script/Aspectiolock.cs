using UnityEngine;

// Poe na Main Camera. Mantem o enquadramento 16:9 em qualquer tela (PC, celular, janela livre),
// com barras pretas, pra nunca aparecer o que fica fora do mapa.
[RequireComponent(typeof(Camera))]
public class AspectRatioLock : MonoBehaviour
{
    [SerializeField] private float targetAspect = 16f / 9f;

    private Camera cam;
    private Camera background;
    private int lastWidth;
    private int lastHeight;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        CreateBackgroundCamera();
        Apply();
    }

    private void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
            Apply();
    }

    private void OnDestroy()
    {
        if (background != null)
            Destroy(background.gameObject);
    }

    private void Apply()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        float windowAspect = (float)Screen.width / Mathf.Max(Screen.height, 1);
        float scale = windowAspect / targetAspect;

        Rect rect = new Rect(0f, 0f, 1f, 1f);

        if (scale < 1f)
        {
            // janela mais "alta" que 16:9: barras em cima e embaixo
            rect.height = scale;
            rect.y = (1f - scale) * 0.5f;
        }
        else
        {
            // janela mais larga que 16:9 (celular, ultrawide): barras nas laterais
            rect.width = 1f / scale;
            rect.x = (1f - rect.width) * 0.5f;
        }

        cam.rect = rect;
    }

    // camera que so pinta de preto atras da principal (as barras)
    private void CreateBackgroundCamera()
    {
        GameObject go = new GameObject("BarrasPretas (auto)");
        background = go.AddComponent<Camera>();
        background.clearFlags = CameraClearFlags.SolidColor;
        background.backgroundColor = Color.black;
        background.cullingMask = 0;
        background.orthographic = true;
        background.depth = cam.depth - 1f;
    }
}