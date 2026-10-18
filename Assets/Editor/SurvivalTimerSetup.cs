using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Conecta o HUD na cena aberta, inclusive se ela ainda estiver com uma versao
// anterior em memoria quando os scripts forem importados.
[InitializeOnLoad]
public static class SurvivalTimerSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string FontGuid = "792b983bae7edbe4896c21d67e015812";

    static SurvivalTimerSetup()
    {
        EditorApplication.delayCall += ConfigureOpenScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => ConfigureScene(scene);
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                ConfigureOpenScenes();
            else if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += ConfigureOpenScenes;
        };
    }

    [MenuItem("Tools/Jogo/Configurar timer de sobrevivencia")]
    public static void ConfigureOpenScenes()
    {
        if (EditorApplication.isPlaying)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ConfigureOpenScenes;
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
            ConfigureScene(SceneManager.GetSceneAt(i));
    }

    public static bool ConfigureScene(Scene scene)
    {
        if (EditorApplication.isPlaying || !scene.isLoaded || scene.path != ScenePath)
            return false;

        GameFlow flow = FindInScene<GameFlow>(scene);
        CandleManager manager = FindInScene<CandleManager>(scene);
        if (flow == null || manager == null)
            return false;

        SerializedObject flowData = new SerializedObject(flow);
        GameObject menu = flowData.FindProperty("mainMenuPanel").objectReferenceValue as GameObject;
        Canvas canvas = menu != null ? menu.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null)
            return false;

        bool changed = false;
        SurvivalTimer timer = flow.GetComponent<SurvivalTimer>();
        if (timer == null)
        {
            timer = Undo.AddComponent<SurvivalTimer>(flow.gameObject);
            changed = true;
        }

        SerializedObject data = new SerializedObject(timer);
        SerializedProperty hudReference = data.FindProperty("hudPanel");
        GameObject hud = hudReference.objectReferenceValue as GameObject;
        if (hud == null)
        {
            Transform existing = canvas.transform.Find("SurvivalHUD");
            hud = existing != null ? existing.gameObject : CreateHud(canvas.transform);
            hudReference.objectReferenceValue = hud;
            changed = true;
        }

        SerializedProperty textReference = data.FindProperty("timeText");
        if (textReference.objectReferenceValue == null)
        {
            TMP_Text text = hud.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
                text = CreateText(hud.transform, data.FindProperty("startingTime").floatValue);
            textReference.objectReferenceValue = text;
            changed = true;
        }

        SerializedProperty managerReference = data.FindProperty("gameManager");
        if (managerReference.objectReferenceValue == null)
        {
            managerReference.objectReferenceValue = manager;
            changed = true;
        }

        data.ApplyModifiedProperties();
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[SurvivalTimerSetup] Timer conectado em GameFlow e GameUI/SurvivalHUD. Aparece ao clicar em Jogar. Salve a cena para manter a configuracao.", flow);
        }
        return changed;
    }

    private static GameObject CreateHud(Transform parent)
    {
        GameObject hud = new GameObject("SurvivalHUD", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(hud, "Criar HUD do timer");
        hud.layer = parent.gameObject.layer;
        hud.transform.SetParent(parent, false);
        hud.transform.SetAsFirstSibling();
        RectTransform rect = (RectTransform)hud.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(260f, 108f);
        Image background = hud.GetComponent<Image>();
        background.color = new Color(0.055f, 0.047f, 0.043f, 0.85f);
        background.raycastTarget = false;
        hud.SetActive(false);
        return hud;
    }

    private static TMP_Text CreateText(Transform parent, float startingTime)
    {
        GameObject label = new GameObject("TimeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(label, "Criar texto do timer");
        label.layer = parent.gameObject.layer;
        label.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)label.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = new Vector2(-16f, -12f);
        TMP_Text text = label.GetComponent<TMP_Text>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        text.fontSize = 48f;
        text.color = new Color(1f, 0.97f, 0.88f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.richText = true;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, startingTime));
        text.text = $"<size=45%>TEMPO</size>\n{seconds / 60:00}:{seconds % 60:00}";
        return text;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }
}
