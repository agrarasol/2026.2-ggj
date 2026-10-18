using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class FurnitureInteractionSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string LootPath = "Assets/Data/FurnitureLootTable.asset";
    private const string FontGuid = "792b983bae7edbe4896c21d67e015812";

    static FurnitureInteractionSetup()
    {
        EditorApplication.delayCall += ConfigureOpenScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => ConfigureScene(scene);
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) ConfigureOpenScenes();
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ConfigureOpenScenes;
        };
    }

    [MenuItem("Tools/Jogo/Configurar interacoes dos moveis")]
    public static void ConfigureOpenScenes()
    {
        if (EditorApplication.isPlaying) return;
        if (EditorApplication.isUpdating || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += ConfigureOpenScenes;
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            ConfigureScene(SceneManager.GetSceneAt(i));
    }

    public static bool ConfigureScene(Scene scene)
    {
        if (EditorApplication.isPlaying || !scene.isLoaded || scene.path != ScenePath) return false;
        GameFlow flow = Find<GameFlow>(scene);
        PlayerMove player = Find<PlayerMove>(scene);
        FurnitureLootTable table = AssetDatabase.LoadAssetAtPath<FurnitureLootTable>(LootPath);
        if (flow == null || player == null || table == null) return false;
        var flowData = new SerializedObject(flow);
        GameObject menu = flowData.FindProperty("mainMenuPanel").objectReferenceValue as GameObject;
        Canvas canvas = menu != null ? menu.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null) return false;

        bool changed = SurvivalTimerSetup.ConfigureScene(scene);
        FurnitureInteractionUI ui = Find<FurnitureInteractionUI>(scene);
        if (ui == null)
        {
            ui = CreateUI(canvas.transform);
            changed = true;
        }
        changed |= ConnectUI(ui);
        FurnitureInteractor interactor = player.GetComponent<FurnitureInteractor>();
        if (interactor == null)
        {
            interactor = Undo.AddComponent<FurnitureInteractor>(player.gameObject);
            changed = true;
        }
        changed |= Link(interactor, "survivalTimer", flow.GetComponent<SurvivalTimer>());
        changed |= Link(interactor, "lootTable", table);
        changed |= Link(interactor, "feedback", ui);

        List<RoomZone> rooms = All<RoomZone>(scene);
        Physics2D.SyncTransforms();
        foreach (SpriteRenderer renderer in All<SpriteRenderer>(scene))
        {
            if (!IsFurniture(renderer)) continue;
            Collider2D collider = renderer.GetComponent<Collider2D>();
            if (collider == null || collider.isTrigger) continue;
            EnemyHidingSpot hidingSpot = renderer.GetComponentInParent<EnemyHidingSpot>();
            RoomZone room = hidingSpot != null ? hidingSpot.Room : renderer.GetComponentInParent<RoomZone>();
            if (room == null)
                foreach (RoomZone candidate in rooms)
                    if (candidate.GetComponent<BoxCollider2D>().OverlapPoint(renderer.bounds.center))
                    {
                        room = candidate;
                        break;
                    }
            if (room == null) continue;

            FurnitureSearchTarget target = renderer.GetComponent<FurnitureSearchTarget>();
            if (target == null)
            {
                target = Undo.AddComponent<FurnitureSearchTarget>(renderer.gameObject);
                var data = new SerializedObject(target);
                data.FindProperty("displayName").stringValue = hidingSpot != null ? "Lareira" : renderer.name;
                data.ApplyModifiedProperties();
                changed = true;
            }
            changed |= Link(target, "room", room);
            changed |= Link(target, "furnitureRenderer", renderer);
            changed |= Link(target, "furnitureCollider", collider);
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[FurnitureInteractionSetup] Moveis e interface de busca conectados. Salve a cena para manter a configuracao.", player);
        }
        return changed;
    }

    private static bool IsFurniture(SpriteRenderer renderer)
    {
        if (renderer.sprite == null || renderer.GetComponent<DoorTransition>() != null) return false;
        for (Transform node = renderer.transform; node != null; node = node.parent)
            if (node.name == "InvisibleWalls") return false;
        EnemyHidingSpot spot = renderer.GetComponentInParent<EnemyHidingSpot>();
        if (spot != null && spot.FurnitureRenderer == renderer) return true;
        string path = AssetDatabase.GetAssetPath(renderer.sprite);
        return path.Contains("FurnitureState") || path.Contains("/Map/Lareira");
    }

    private static FurnitureInteractionUI CreateUI(Transform parent)
    {
        GameObject root = UIObject("FurnitureInteractionUI", parent, typeof(FurnitureInteractionUI));
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        FurnitureInteractionUI ui = root.GetComponent<FurnitureInteractionUI>();
        GameObject cursor = UIObject("SearchCursor", root.transform, typeof(SearchCursorGraphic));
        ((RectTransform)cursor.transform).sizeDelta = new Vector2(44f, 44f);
        SearchCursorGraphic graphic = cursor.GetComponent<SearchCursorGraphic>();
        graphic.color = new Color(1f, 0.91f, 0.69f);
        graphic.raycastTarget = false;
        EditorUtility.SetDirty(graphic);
        TMP_Text hint = Text("SearchHint", root.transform, 24f, new Vector2(360f, 84f));
        hint.rectTransform.pivot = new Vector2(0f, 1f);
        hint.alignment = TextAlignmentOptions.TopLeft;

        GameObject result = UIObject("SearchResult", root.transform, typeof(Image), typeof(CanvasGroup));
        RectTransform resultRect = (RectTransform)result.transform;
        resultRect.anchorMin = resultRect.anchorMax = new Vector2(0.5f, 0f);
        resultRect.pivot = new Vector2(0.5f, 0f);
        resultRect.anchoredPosition = new Vector2(0f, 80f);
        resultRect.sizeDelta = new Vector2(640f, 105f);
        result.GetComponent<Image>().color = new Color(0.055f, 0.047f, 0.043f, 0.94f);
        result.GetComponent<Image>().raycastTarget = false;
        CanvasGroup group = result.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        TMP_Text resultText = Text("ResultText", result.transform, 32f, Vector2.zero);
        resultText.rectTransform.anchorMin = Vector2.zero;
        resultText.rectTransform.anchorMax = Vector2.one;
        resultText.rectTransform.sizeDelta = new Vector2(-24f, -16f);
        resultText.alignment = TextAlignmentOptions.Center;
        Link(ui, "canvasRect", rect);
        Link(ui, "pointer", graphic);
        Link(ui, "hint", hint);
        Link(ui, "resultPanel", group);
        Link(ui, "resultText", resultText);
        cursor.SetActive(false);
        hint.gameObject.SetActive(false);
        result.SetActive(false);
        return ui;
    }

    private static bool ConnectUI(FurnitureInteractionUI ui)
    {
        bool changed = Link(ui, "canvasRect", ui.transform as RectTransform);
        SearchCursorGraphic graphic = ui.transform.Find("SearchCursor")?.GetComponent<SearchCursorGraphic>();
        changed |= Link(ui, "pointer", graphic);
        if (graphic != null)
        {
            if (graphic.GetComponent<CanvasRenderer>() == null)
            {
                Undo.AddComponent<CanvasRenderer>(graphic.gameObject);
                changed = true;
            }
            if (graphic.raycastTarget)
            {
                Undo.RecordObject(graphic, "Liberar cliques do cursor");
                graphic.raycastTarget = false;
                EditorUtility.SetDirty(graphic);
                changed = true;
            }
        }
        changed |= Link(ui, "hint", ui.transform.Find("SearchHint")?.GetComponent<TMP_Text>());
        Transform result = ui.transform.Find("SearchResult");
        if (result != null)
        {
            changed |= Link(ui, "resultPanel", result.GetComponent<CanvasGroup>());
            changed |= Link(ui, "resultText", result.GetComponentInChildren<TMP_Text>(true));
            Transform iconTransform = result.Find("ResultIcon");
            if (iconTransform == null)
            {
                GameObject iconObject = UIObject("ResultIcon", result, typeof(Image));
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(16f, 0f);
                iconRect.sizeDelta = new Vector2(72f, 72f);
                Image image = iconObject.GetComponent<Image>();
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
                EditorUtility.SetDirty(image);
                EditorUtility.SetDirty(iconRect);
                iconObject.SetActive(false);
                iconTransform = iconObject.transform;
                changed = true;
            }
            changed |= Link(ui, "resultIcon", iconTransform.GetComponent<Image>());
        }
        return changed;
    }

    private static TMP_Text Text(string name, Transform parent, float fontSize, Vector2 size)
    {
        var label = UIObject(name, parent, typeof(TextMeshProUGUI));
        TMP_Text text = label.GetComponent<TMP_Text>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        text.fontSize = fontSize;
        text.raycastTarget = false;
        text.color = new Color(0.93f, 0.9f, 0.81f);
        text.text = "";
        text.rectTransform.sizeDelta = size;
        return text;
    }

    private static GameObject UIObject(string name, Transform parent, params System.Type[] components)
    {
        var types = new List<System.Type> { typeof(RectTransform) };
        types.AddRange(components);
        var node = new GameObject(name, types.ToArray());
        Undo.RegisterCreatedObjectUndo(node, "Configurar busca nos moveis");
        node.layer = parent.gameObject.layer;
        node.transform.SetParent(parent, false);
        return node;
    }

    private static bool Link(Object component, string field, Object reference)
    {
        var data = new SerializedObject(component);
        SerializedProperty property = data.FindProperty(field);
        if (property.objectReferenceValue != null || reference == null) return false;
        property.objectReferenceValue = reference;
        bool changed = data.ApplyModifiedProperties();
        if (changed) EditorUtility.SetDirty(component);
        return changed;
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        List<T> found = All<T>(scene);
        return found.Count > 0 ? found[0] : null;
    }

    private static List<T> All<T>(Scene scene) where T : Component
    {
        var found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
        return found;
    }
}
