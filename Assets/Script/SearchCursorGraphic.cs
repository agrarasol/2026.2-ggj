using UnityEngine;
using UnityEngine.UI;

// Desenho vetorial da lupa e da barra circular, sem depender de uma textura externa.
[RequireComponent(typeof(CanvasRenderer))]
public class SearchCursorGraphic : MaskableGraphic
{
    public bool IsProgress { get; private set; }
    public float Progress { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetDisplay(bool isProgress, float progress = 0f)
    {
        progress = Mathf.Clamp01(progress);
        if (IsProgress == isProgress && Mathf.Approximately(Progress, progress)) return;
        IsProgress = isProgress;
        Progress = progress;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (IsProgress)
        {
            Ring(mesh, Vector2.zero, 20f, 14f, 1f, new Color(0.03f, 0.025f, 0.02f, 0.95f));
            Ring(mesh, Vector2.zero, 18f, 15f, 1f, new Color(0.35f, 0.32f, 0.27f, 1f));
            Ring(mesh, Vector2.zero, 18f, 15f, Progress, color);
        }
        else
        {
            Line(mesh, new Vector2(5f, -5f), new Vector2(17f, -17f), 8f, new Color(0.03f, 0.025f, 0.02f, 1f));
            Ring(mesh, new Vector2(-3f, 3f), 15f, 9f, 1f, new Color(0.03f, 0.025f, 0.02f, 1f));
            Line(mesh, new Vector2(6f, -6f), new Vector2(16f, -16f), 4f, color);
            Ring(mesh, new Vector2(-3f, 3f), 13f, 11f, 1f, color);
        }
    }

    private static void Ring(VertexHelper mesh, Vector2 center, float outer, float inner, float fill, Color tint)
    {
        int segments = Mathf.CeilToInt(64f * fill);
        for (int i = 0; i < segments; i++)
        {
            float a = (90f - 360f * fill * i / segments) * Mathf.Deg2Rad;
            float b = (90f - 360f * fill * (i + 1) / segments) * Mathf.Deg2Rad;
            Vector2 da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            Quad(mesh, center + da * outer, center + db * outer, center + db * inner, center + da * inner, tint);
        }
    }

    private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
        Quad(mesh, a + normal, b + normal, b - normal, a - normal, tint);
    }

    private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int index = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero);
        mesh.AddVert(b, tint, Vector2.zero);
        mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddVert(d, tint, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index + 2, index + 3, index);
    }
}
