using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FurnitureInteractionUI : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private SearchCursorGraphic pointer;
    [SerializeField] private TMP_Text hint;
    [SerializeField] private CanvasGroup resultPanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Image resultIcon;
    [SerializeField, Min(0.1f)] private float resultDuration = 2.5f;
    private float resultEndTime;
    private Vector4 resultTextMargins;

    private void Awake()
    {
        ConnectExistingElements();
        if (resultText != null) resultTextMargins = resultText.margin;
        HideAll();
    }

    // Os elementos continuam no Canvas mesmo se uma referencia do Inspector
    // for perdida ao importar scripts ou salvar uma cena que ja estava aberta.
    private void ConnectExistingElements()
    {
        if (canvasRect == null) canvasRect = transform as RectTransform;
        if (pointer == null) pointer = transform.Find("SearchCursor")?.GetComponent<SearchCursorGraphic>();
        if (hint == null) hint = transform.Find("SearchHint")?.GetComponent<TMP_Text>();
        Transform result = transform.Find("SearchResult");
        if (resultPanel == null && result != null) resultPanel = result.GetComponent<CanvasGroup>();
        if (resultText == null && result != null) resultText = result.GetComponentInChildren<TMP_Text>(true);
        if (resultIcon == null && result != null) resultIcon = result.Find("ResultIcon")?.GetComponent<Image>();
    }
    private void OnEnable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged += OnStateChanged;
    }
    private void OnDisable()
    {
        if (GameFlow.Instance != null)
            GameFlow.Instance.StateChanged -= OnStateChanged;
        HideAll();
    }
    private void OnStateChanged(GameState state)
    {
        if (state != GameState.Playing) HideAll();
    }

    private void LateUpdate()
    {
        if (!GameFlow.CanPlay)
        {
            HideAll();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
            ClearPointer();
        else if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, mouse.position.ReadValue(), null, out Vector2 local))
        {
            if (pointer != null) pointer.rectTransform.anchoredPosition = local;
            if (hint != null)
            {
                Vector2 size = hint.rectTransform.sizeDelta;
                Rect bounds = canvasRect.rect;
                hint.rectTransform.anchoredPosition = new Vector2(
                    Mathf.Clamp(local.x + 24f, bounds.xMin + 8f, bounds.xMax - size.x - 8f),
                    Mathf.Clamp(local.y - 28f, bounds.yMin + size.y + 8f, bounds.yMax - 8f));
            }
        }

        if (resultPanel != null && resultPanel.gameObject.activeSelf)
        {
            float left = resultEndTime - Time.unscaledTime;
            resultPanel.alpha = Mathf.Clamp01(left / 0.35f);
            if (left <= 0f) resultPanel.gameObject.SetActive(false);
        }
    }

    public void ShowHover(FurnitureSearchTarget target, bool inReach)
    {
        if (target == null)
        {
            ClearPointer();
            return;
        }
        bool available = target.IsAvailable;
        SetPointer(available && inReach, false, 0f);
        if (hint == null) return;
        hint.gameObject.SetActive(true);
        hint.text = target.CooldownRemaining > 0f
            ? $"{target.DisplayName}\nDisponível em {Mathf.CeilToInt(target.CooldownRemaining)}s"
            : inReach ? $"{target.DisplayName}\nClique para vasculhar" : $"{target.DisplayName}\nChegue mais perto";
    }

    public void ShowCandleHover(bool inReach)
    {
        SetPointer(inReach, false, 0f);
        if (hint == null) return;
        hint.gameObject.SetActive(true);
        hint.text = inReach ? "Vela\nClique para reacender" : "Vela\nChegue mais perto";
    }

    public void ShowProgress(float progress)
    {
        SetPointer(true, true, progress);
        if (hint != null)
        {
            hint.gameObject.SetActive(true);
            hint.text = "Vasculhando...";
        }
    }

    public void ShowInteractionHint(string text)
    {
        ClearPointer();
        if (hint == null) return;
        hint.gameObject.SetActive(true);
        hint.text = text;
    }

    public void ShowResult(FurnitureLootEntry item)
    {
        ClearPointer();
        if (resultPanel == null || resultText == null) return;
        resultText.text = item == null ? "Nada encontrado" :
            $"{item.itemName} · {item.RarityName}\n+{item.timeBonus:0.#}s de sobrevivência";
        resultText.color = item == null ? new Color(0.72f, 0.67f, 0.6f) : item.Color;
        bool showIcon = item != null && resultIcon != null;
        if (resultIcon != null)
        {
            resultIcon.sprite = item != null ? item.icon : null;
            resultIcon.color = Color.white;
            resultIcon.preserveAspect = true;
            resultIcon.raycastTarget = false;
            resultIcon.gameObject.SetActive(showIcon);
        }
        resultText.margin = resultTextMargins + new Vector4(
            showIcon ? resultIcon.rectTransform.rect.width + 20f : 0f, 0f, 0f, 0f);
        resultEndTime = Time.unscaledTime + resultDuration;
        resultPanel.alpha = 1f;
        resultPanel.gameObject.SetActive(true);
    }

    public void ClearPointer()
    {
        SetPointer(false, false, 0f);
        if (hint != null) hint.gameObject.SetActive(false);
    }

    private void SetPointer(bool visible, bool progress, float amount)
    {
        if (pointer != null)
        {
            pointer.gameObject.SetActive(visible);
            pointer.SetDisplay(progress, amount);
        }
        Cursor.visible = !visible || pointer == null;
    }

    private void HideAll()
    {
        ClearPointer();
        if (resultPanel != null) resultPanel.gameObject.SetActive(false);
    }
}
