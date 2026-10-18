using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CollectedItemsHUD : MonoBehaviour
{
    [SerializeField] private GameFlow gameFlow;
    [SerializeField] private RectTransform hudPanel;
    [SerializeField] private Image itemTemplate;

    private sealed class ItemDisplay
    {
        public int Count;
        public Image Icon;
        public TMP_Text Quantity;
    }

    private readonly Dictionary<FurnitureLootEntry, ItemDisplay> items = new Dictionary<FurnitureLootEntry, ItemDisplay>();
    public int ItemTypesCount => items.Count;

    private void Awake()
    {
        if (gameFlow == null) gameFlow = GameFlow.Instance;
        Canvas canvas = gameFlow != null ? gameFlow.InterfaceCanvas : null;
        if (hudPanel == null && canvas != null)
            hudPanel = GameFlow.FindUIElement(canvas.transform, "ItensHUD") as RectTransform;
        if (itemTemplate == null && hudPanel != null)
            itemTemplate = GameFlow.FindUIElement(hudPanel, "IconBase")?.GetComponent<Image>();
        if (hudPanel == null || itemTemplate == null)
            Debug.LogWarning("HUD de itens: nao foi possivel encontrar ItensHUD e IconBase no Canvas.", this);
        if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        if (hudPanel != null)
            foreach (Graphic graphic in hudPanel.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        UpdateVisibility();
    }

    private void OnEnable()
    {
        if (gameFlow != null) gameFlow.StateChanged += OnStateChanged;
        UpdateVisibility();
    }

    private void OnDisable()
    {
        if (gameFlow != null) gameFlow.StateChanged -= OnStateChanged;
        if (hudPanel != null) hudPanel.gameObject.SetActive(false);
    }

    private void OnStateChanged(GameState state) => UpdateVisibility();

    private void UpdateVisibility()
    {
        if (hudPanel != null)
            hudPanel.gameObject.SetActive(gameFlow != null &&
                (gameFlow.State == GameState.Playing || gameFlow.State == GameState.Paused));
    }

    public int GetQuantity(FurnitureLootEntry item) => item != null && items.TryGetValue(item, out ItemDisplay display)
        ? display.Count : 0;

    public void Collect(FurnitureLootEntry item)
    {
        if (!GameFlow.CanPlay || item == null || itemTemplate == null || hudPanel == null) return;
        if (!items.TryGetValue(item, out ItemDisplay display))
        {
            Image icon = Instantiate(itemTemplate, hudPanel);
            icon.name = "Item_" + item.itemName;
            icon.gameObject.SetActive(true);
            display = new ItemDisplay { Icon = icon, Quantity = icon.GetComponentInChildren<TMP_Text>(true) };
            if (display.Quantity != null)
            {
                display.Quantity.fontSizeMax = display.Quantity.fontSize;
                display.Quantity.fontSizeMin = 12f;
                display.Quantity.enableAutoSizing = true;
                display.Quantity.textWrappingMode = TextWrappingModes.NoWrap;
            }
            foreach (Graphic graphic in icon.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            items.Add(item, display);
        }
        display.Count++;
        display.Icon.sprite = item.icon;
        display.Icon.color = Color.white;
        display.Icon.preserveAspect = true;
        if (display.Quantity != null) display.Quantity.text = $"x{display.Count}";
    }
}
