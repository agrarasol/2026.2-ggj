using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class PixelScreenTransition : MaskableGraphic
{
    [Header("Transicao pixelada")]
    [SerializeField, Min(0f)] private float coverDuration = 0.45f;
    [SerializeField, Min(0f)] private float revealDuration = 0.55f;
    [Tooltip("Quantidade de colunas no ponto mais pixelado da transicao.")]
    [SerializeField, Range(1, 16)] private int minimumColumns = 4;

    private static readonly int ColumnsId = Shader.PropertyToID("_Columns");
    private static readonly int AspectId = Shader.PropertyToID("_AspectRatio");
    private static RenderTexture frameForReload;
    private RenderTexture capturedFrame;
    private Material pixelMaterial;
    private Material originalMaterial;
    private bool frameHandedOff;
    private float coverage;
    public float Coverage => coverage;
    public bool IsAnimating { get; private set; }
    public override Texture mainTexture => capturedFrame != null ? capturedFrame : Texture2D.blackTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFrameForReload() => ReleaseTexture(ref frameForReload);

    public IEnumerator Cover() => Animate(1f, coverDuration);
    public IEnumerator Reveal() => Animate(0f, revealDuration);

    public void ShowCovered()
    {
        if (frameForReload != null)
        {
            ReleaseFrame();
            capturedFrame = frameForReload;
            frameForReload = null;
        }
        EnsureMaterial();
        Show();
        SetCoverage(1f);
    }

    public void PreserveForReload()
    {
        ReleaseTexture(ref frameForReload);
        frameForReload = capturedFrame;
        frameHandedOff = capturedFrame != null;
    }

    public void Clear()
    {
        IsAnimating = false;
        SetCoverage(0f);
        raycastTarget = false;
        gameObject.SetActive(false);
        ReleaseFrame();
    }

    private void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        raycastTarget = true;
    }

    private IEnumerator Animate(float target, float duration)
    {
        // Atualiza a imagem depois que o GameFlow troca os paineis, ainda no maximo de pixels.
        CaptureFrame();
        EnsureMaterial();
        Show();
        IsAnimating = true;
        float start = coverage;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            SetCoverage(Mathf.Lerp(start, target, progress));
            yield return null;
        }
        SetCoverage(target);
        IsAnimating = false;
    }

    private void SetCoverage(float value)
    {
        coverage = Mathf.Clamp01(value);
        if (pixelMaterial == null) return;
        float width = capturedFrame != null ? capturedFrame.width : Mathf.Max(1, Screen.width);
        float height = capturedFrame != null ? capturedFrame.height : Mathf.Max(1, Screen.height);
        float smallestGrid = Mathf.Clamp(minimumColumns, 1, 16);
        // A escala exponencial passa por pixels pequenos antes dos blocos grandes.
        pixelMaterial.SetFloat(ColumnsId, Mathf.Exp(Mathf.Lerp(Mathf.Log(width), Mathf.Log(smallestGrid), coverage)));
        pixelMaterial.SetFloat(AspectId, width / height);
    }

    private void EnsureMaterial()
    {
        if (pixelMaterial != null) return;
        originalMaterial = material;
        Shader shader = Shader.Find("UI/PixelScreenTransition");
        if (shader == null)
        {
            Debug.LogError("Shader UI/PixelScreenTransition nao encontrado.", this);
            return;
        }
        pixelMaterial = new Material(shader) { name = "PixelScreenTransition (Runtime)" };
        material = pixelMaterial;
        color = Color.white;
    }

    private void CaptureFrame()
    {
        Camera camera = Camera.main;
        Canvas root = canvas != null ? canvas.rootCanvas : GetComponentInParent<Canvas>(true)?.rootCanvas;
        if (camera == null || root == null) return;

        var nextFrame = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24, RenderTextureFormat.ARGB32)
        {
            name = "Pixel transition frame",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        RenderMode previousMode = root.renderMode;
        Camera previousCamera = root.worldCamera;
        float previousPlane = root.planeDistance;
        int previousOrder = root.sortingOrder;
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        bool wasEnabled = enabled;
        bool completed = false;
        try
        {
            // Captura tambem menu, HUD e Game Over sem capturar o proprio efeito.
            enabled = false;
            nextFrame.Create();
            camera.targetTexture = nextFrame;
            root.renderMode = RenderMode.ScreenSpaceCamera;
            root.worldCamera = camera;
            root.planeDistance = camera.nearClipPlane + 0.1f;
            root.sortingOrder = short.MaxValue;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = nextFrame });
            completed = true;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            root.renderMode = previousMode;
            root.worldCamera = previousCamera;
            root.planeDistance = previousPlane;
            root.sortingOrder = previousOrder;
            enabled = wasEnabled;
            Canvas.ForceUpdateCanvases();
            if (!completed) ReleaseTexture(ref nextFrame);
        }
        ReleaseFrame();
        capturedFrame = nextFrame;
        SetMaterialDirty();
        SetVerticesDirty();
    }

    private void ReleaseFrame()
    {
        if (!frameHandedOff) ReleaseTexture(ref capturedFrame);
        capturedFrame = null;
        frameHandedOff = false;
    }

    private static void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release();
        if (Application.isPlaying) Destroy(texture);
        else DestroyImmediate(texture);
        texture = null;
    }

    private void OnApplicationQuit() => ReleaseTexture(ref frameForReload);

    protected override void OnDestroy()
    {
        ReleaseFrame();
        if (pixelMaterial != null)
        {
            material = originalMaterial;
            if (Application.isPlaying) Destroy(pixelMaterial);
            else DestroyImmediate(pixelMaterial);
        }
        base.OnDestroy();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, new Vector2(0f, 0f));
        mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, new Vector2(0f, 1f));
        mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, new Vector2(1f, 1f));
        mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, new Vector2(1f, 0f));
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(2, 3, 0);
    }
}
