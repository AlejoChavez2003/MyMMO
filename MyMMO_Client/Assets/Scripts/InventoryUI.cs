using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Transform slotContainer;
    [SerializeField] private Color emptySlotColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color occupiedSlotColor = Color.white;

    private Button[] slotButtons;
    private TMP_Text[] slotLabels;
    private Text[] legacySlotLabels;
    private Text currencyLabel;
    private int draggedSlotIndex = -1;
    private int hoveredSlotIndex = -1;

    private void Awake()
    {
        ApplyInventoryStyle();
        CacheSlots();
        Refresh();
    }

    private void OnEnable()
    {
        InventoryData.Changed += Refresh;
        GameData.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        InventoryData.Changed -= Refresh;
        GameData.Changed -= Refresh;
    }

    private void CacheSlots()
    {
        if (slotContainer == null)
        {
            Debug.LogWarning("InventoryUI necesita una referencia a InventoryGrid.");
            slotButtons = new Button[0];
            slotLabels = new TMP_Text[0];
            legacySlotLabels = new Text[0];
            return;
        }

        int slotCount = Mathf.Min(slotContainer.childCount, InventoryData.SlotCount);
        slotButtons = new Button[slotCount];
        slotLabels = new TMP_Text[slotCount];
        legacySlotLabels = new Text[slotCount];

        for (int index = 0; index < slotCount; index++)
        {
            Transform slot = slotContainer.GetChild(index);
            slotButtons[index] = slot.GetComponent<Button>();
            slotLabels[index] = slot.GetComponentInChildren<TMP_Text>(true);
            legacySlotLabels[index] = slot.GetComponentInChildren<Text>(true);

            InventorySlotUI slotUI = slot.GetComponent<InventorySlotUI>();
            if (slotUI == null) slotUI = slot.gameObject.AddComponent<InventorySlotUI>();
            slotUI.Configure(this, index);
        }

        if (slotCount != InventoryData.SlotCount)
        {
            Debug.LogWarning($"InventoryGrid tiene {slotCount} hijos, pero se esperan {InventoryData.SlotCount}.");
        }
    }

    private void Refresh()
    {
        if (slotButtons == null) return;

        for (int index = 0; index < slotButtons.Length; index++)
        {
            InventorySlotData data = InventoryData.Slots[index];
            bool occupied = !string.IsNullOrEmpty(data.itemId) && data.quantity > 0;
            ItemDefinition definition = ItemCatalog.Get(data.itemId);

            if (slotButtons[index] != null)
            {
                Image image = slotButtons[index].GetComponent<Image>();
                if (image != null) image.color = occupied ? definition.SlotColor : emptySlotColor;
            }

            if (slotLabels[index] != null)
            {
                slotLabels[index].text = occupied ? $"{definition.DisplayName}\n{data.quantity}" : "";
            }

            if (legacySlotLabels[index] != null)
            {
                legacySlotLabels[index].text = occupied ? $"{definition.DisplayName}\n{data.quantity}" : "";
            }
        }

        if (currencyLabel != null) currencyLabel.text = $"Monedas de bronce: {GameData.BronzeCoins}";
    }

    private void OnGUI()
    {
        if (hoveredSlotIndex < 0 || hoveredSlotIndex >= InventoryData.SlotCount) return;

        InventorySlotData data = InventoryData.Slots[hoveredSlotIndex];
        if (string.IsNullOrEmpty(data.itemId) || data.quantity <= 0) return;

        ItemDefinition definition = ItemCatalog.Get(data.itemId);
        Rect tooltipRect = new Rect(Event.current.mousePosition.x + 12, Event.current.mousePosition.y + 12, 260, 88);
        GUI.Box(tooltipRect, GUIContent.none);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle descriptionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(tooltipRect.x + 10, tooltipRect.y + 8, 240, 24), definition.DisplayName, titleStyle);
        GUI.Label(new Rect(tooltipRect.x + 10, tooltipRect.y + 34, 240, 44), definition.Description, descriptionStyle);
    }

    public void ShowTooltip(int slotIndex)
    {
        hoveredSlotIndex = slotIndex;
    }

    public void HideTooltip()
    {
        hoveredSlotIndex = -1;
    }

    public void UseSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= InventoryData.SlotCount) return;
        InventorySlotData data = InventoryData.Slots[slotIndex];
        if (string.IsNullOrEmpty(data.itemId) || data.quantity <= 0) return;

        ItemDefinition definition = ItemCatalog.Get(data.itemId);
        NetworkManager networkManager = FindAnyObjectByType<NetworkManager>();
        if (networkManager == null) return;

        if (definition.Type == ItemType.Equipment && !string.IsNullOrEmpty(definition.EquipmentSlot))
        {
            Debug.Log($"[Equipo] Equipando {data.itemId} desde slot {slotIndex} en {definition.EquipmentSlot}.");
            networkManager.SendEquipItem(slotIndex, definition.EquipmentSlot);
            return;
        }

        if (definition.Type != ItemType.Consumable)
        {
            Debug.Log($"[Inventario] {definition.DisplayName} es un material y no se puede equipar ni usar.");
            return;
        }

        networkManager.SendUseItem(data.itemId);
    }

    public void BeginSlotDrag(int slotIndex)
    {
        draggedSlotIndex = slotIndex;
    }

    public void DropOnSlot(int destinationSlotIndex)
    {
        if (draggedSlotIndex < 0 || draggedSlotIndex == destinationSlotIndex) return;

        NetworkManager networkManager = FindAnyObjectByType<NetworkManager>();
        if (networkManager != null)
        {
            networkManager.SendInventoryMove(draggedSlotIndex, destinationSlotIndex);
        }

        draggedSlotIndex = -1;
    }

    public void EndSlotDrag()
    {
        draggedSlotIndex = -1;
    }

    public void DropOnActionBar(int actionBarSlot, ActionBarController actionBarController)
    {
        if (draggedSlotIndex < 0 || actionBarController == null) return;

        InventorySlotData data = InventoryData.Slots[draggedSlotIndex];
        if (!string.IsNullOrEmpty(data.itemId) && data.quantity > 0)
        {
            actionBarController.AssignItem(actionBarSlot, data.itemId);
            Debug.Log($"[Inventario] {data.itemId} asignado a la tecla {actionBarSlot + 1}.");
        }

        draggedSlotIndex = -1;
    }

    public void CloseInventory()
    {
        gameObject.SetActive(false);
    }

    private void ApplyInventoryStyle()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null) return;

        Image panelImage = GetComponent<Image>();
        if (panelImage != null) panelImage.color = new Color(0.08f, 0.08f, 0.08f, 0.94f);

        Transform title = transform.Find("Inventario");
        Transform close = transform.Find("CloseButton");
        if (title == null || close == null) return;

        Transform existingHeader = transform.Find("InventoryHeader");
        GameObject headerObject = existingHeader != null ? existingHeader.gameObject : new GameObject("InventoryHeader", typeof(RectTransform), typeof(Image));
        headerObject.transform.SetParent(transform, false);
        headerObject.transform.SetAsFirstSibling();

        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 42f);
        headerObject.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 1f);

        title.SetParent(headerObject.transform, false);
        RectTransform titleRect = title as RectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(14f, 0f);
        titleRect.offsetMax = new Vector2(-52f, 0f);
        titleRect.localScale = Vector3.one;

        Text titleText = title.GetComponent<Text>();
        if (titleText != null)
        {
            titleText.text = "Inventario";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 18;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.raycastTarget = true;
        }

        TMP_Text titleTMP = title.GetComponent<TMP_Text>();
        if (titleTMP != null)
        {
            titleTMP.text = "Inventario";
            titleTMP.fontSize = 18f;
            titleTMP.color = Color.white;
            titleTMP.alignment = TextAlignmentOptions.Left;
            titleTMP.raycastTarget = true;
        }

        close.SetParent(headerObject.transform, false);
        RectTransform closeRect = close as RectTransform;
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-8f, 0f);
        closeRect.sizeDelta = new Vector2(34f, 30f);
        Image closeImage = close.GetComponent<Image>();
        if (closeImage != null) closeImage.color = new Color(0.7f, 0.18f, 0.18f, 1f);

        Button closeButton = close.GetComponent<Button>();
        if (closeButton != null) closeButton.onClick.AddListener(CloseInventory);

        GameObject currencyObject = new GameObject("CurrencyLabel", typeof(RectTransform), typeof(Text));
        currencyObject.transform.SetParent(transform, false);
        RectTransform currencyRect = currencyObject.GetComponent<RectTransform>();
        currencyRect.anchorMin = new Vector2(0f, 0f);
        currencyRect.anchorMax = new Vector2(1f, 0f);
        currencyRect.pivot = new Vector2(0.5f, 0f);
        currencyRect.anchoredPosition = new Vector2(0f, 8f);
        currencyRect.sizeDelta = new Vector2(-24f, 28f);
        currencyLabel = currencyObject.GetComponent<Text>();
        currencyLabel.text = $"Monedas de bronce: {GameData.BronzeCoins}";
        currencyLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        currencyLabel.fontSize = 15;
        currencyLabel.color = new Color(0.86f, 0.62f, 0.22f, 1f);
        currencyLabel.alignment = TextAnchor.MiddleLeft;

        DraggableUI dragHandle = title.GetComponent<DraggableUI>();
        if (dragHandle == null) dragHandle = title.gameObject.AddComponent<DraggableUI>();
        dragHandle.SetTarget(panelRect);
    }
}

public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IDropHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private InventoryUI inventoryUI;
    private int slotIndex;

    public void Configure(InventoryUI owner, int index)
    {
        inventoryUI = owner;
        slotIndex = index;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventoryUI != null) inventoryUI.BeginSlotDrag(slotIndex);
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (inventoryUI != null) inventoryUI.DropOnSlot(slotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (inventoryUI != null) inventoryUI.EndSlotDrag();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inventoryUI != null) inventoryUI.ShowTooltip(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventoryUI != null) inventoryUI.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2 && inventoryUI != null)
        {
            inventoryUI.UseSlot(slotIndex);
        }
    }
}

public class EquipmentUI : MonoBehaviour
{
    private readonly Dictionary<string, Text> slotLabels = new Dictionary<string, Text>();
    private Text statsLabel;
    private string hoveredSlot = "";
    private bool built;

    public void Build()
    {
        if (built) return;
        built = true;

        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null) return;

        CreateLabel(transform, "EquipmentTitle", "Ficha de personaje", new Vector2(0f, 218f), new Vector2(700f, 38f), 22, TextAnchor.MiddleLeft);
        DraggableUI dragHandle = GetComponent<DraggableUI>();
        if (dragHandle == null) dragHandle = gameObject.AddComponent<DraggableUI>();
        dragHandle.SetTarget(panelRect);

        CreateButton("CloseButton", "X", new Vector2(378f, 220f), new Color(0.7f, 0.18f, 0.18f, 1f), ClosePanel);
        CreateLabel(transform, "LeftColumnTitle", "Accesorios", new Vector2(-280f, 170f), new Vector2(220f, 28f), 17, TextAnchor.MiddleCenter);
        CreateLabel(transform, "CenterColumnTitle", "Equipo", new Vector2(0f, 170f), new Vector2(220f, 28f), 17, TextAnchor.MiddleCenter);
        CreateLabel(transform, "RightColumnTitle", "Atributos", new Vector2(280f, 170f), new Vector2(220f, 28f), 17, TextAnchor.MiddleCenter);

        CreateEquipmentSlot("weapon", "Arma", new Vector2(-280f, 125f), new Color(0.22f, 0.3f, 0.42f, 1f));
        CreateEquipmentSlot("ring_left", "Anillo izquierdo", new Vector2(-280f, 65f), new Color(0.28f, 0.24f, 0.38f, 1f));
        CreateEquipmentSlot("ring_right", "Anillo derecho", new Vector2(-280f, 5f), new Color(0.28f, 0.24f, 0.38f, 1f));
        CreateEquipmentSlot("necklace", "Collar", new Vector2(-280f, -55f), new Color(0.32f, 0.27f, 0.16f, 1f));
        CreateEquipmentSlot("special", "Especial / pasiva", new Vector2(-280f, -115f), new Color(0.24f, 0.34f, 0.27f, 1f));

        CreateEquipmentSlot("head", "Cabeza", new Vector2(0f, 125f), new Color(0.28f, 0.28f, 0.32f, 1f));
        CreateEquipmentSlot("chest", "Pecho", new Vector2(0f, 65f), new Color(0.38f, 0.25f, 0.16f, 1f));
        CreateEquipmentSlot("gloves", "Guantes", new Vector2(0f, 5f), new Color(0.32f, 0.25f, 0.2f, 1f));
        CreateEquipmentSlot("legs", "Piernas", new Vector2(0f, -55f), new Color(0.25f, 0.27f, 0.32f, 1f));
        CreateEquipmentSlot("boots", "Botas", new Vector2(0f, -115f), new Color(0.3f, 0.22f, 0.16f, 1f));

        CreateStatsColumn();
        CreateResetAttributesButton();
        CreateAttributeButton("strength", new Vector2(370f, 80f));
        CreateAttributeButton("precision", new Vector2(370f, 35f));
        CreateAttributeButton("agility", new Vector2(370f, -10f));
        CreateAttributeButton("vitality", new Vector2(370f, -55f));

        EquipmentData.Changed += Refresh;
        GameData.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        EquipmentData.Changed -= Refresh;
        GameData.Changed -= Refresh;
    }

    private void Refresh()
    {
        foreach (KeyValuePair<string, Text> entry in slotLabels)
        {
            string equipmentSlot = GetDataSlot(entry.Key);
            string itemId = EquipmentData.Slots[equipmentSlot].itemId;
            entry.Value.text = string.IsNullOrEmpty(itemId)
                ? $"{GetDisplayName(entry.Key)}\nVacio"
                : $"{GetDisplayName(entry.Key)}\n{ItemCatalog.Get(itemId).DisplayName}";
        }

        if (statsLabel != null) statsLabel.text = BuildStatsText();
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(hoveredSlot) || !EquipmentData.Slots.ContainsKey(GetDataSlot(hoveredSlot))) return;

        string itemId = EquipmentData.Slots[GetDataSlot(hoveredSlot)].itemId;
        if (string.IsNullOrEmpty(itemId)) return;

        ItemDefinition definition = ItemCatalog.Get(itemId);
        Rect tooltipRect = new Rect(Event.current.mousePosition.x + 12, Event.current.mousePosition.y + 12, 280, 100);
        GUI.Box(tooltipRect, GUIContent.none);
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle descriptionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(tooltipRect.x + 10, tooltipRect.y + 8, 260, 24), definition.DisplayName, titleStyle);
        GUI.Label(new Rect(tooltipRect.x + 10, tooltipRect.y + 34, 260, 58), definition.Description, descriptionStyle);
    }

    private GameObject CreateLabel(Transform parent, string objectName, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text label = labelObject.GetComponent<Text>();
        label.text = value;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        return labelObject;
    }

    private void CreateEquipmentSlot(string slot, string label, Vector2 position, Color color)
    {
        CreateButton(slot, label, position, color);
    }

    private void CreateButton(string slot, string label, Vector2 position, Color color, UnityEngine.Events.UnityAction action = null)
    {
        GameObject buttonObject = new GameObject(slot, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = slot == "CloseButton" ? new Vector2(34f, 30f) : new Vector2(220f, 48f);
        buttonObject.GetComponent<Image>().color = color;

        Button button = buttonObject.GetComponent<Button>();
        if (action != null) button.onClick.AddListener(action);
        else button.onClick.AddListener(() => Unequip(slot));

        CreateLabel(buttonObject.transform, "Label", label, Vector2.zero, rect.sizeDelta - new Vector2(16f, 6f), 15, TextAnchor.MiddleCenter);
        Text valueLabel = buttonObject.GetComponentInChildren<Text>();
        if (slot != "CloseButton")
        {
            slotLabels[slot] = valueLabel;
            EquipmentSlotTooltipUI tooltipUI = buttonObject.AddComponent<EquipmentSlotTooltipUI>();
            tooltipUI.Configure(this, slot);
        }
    }

    public void ShowEquipmentTooltip(string slot)
    {
        hoveredSlot = slot;
    }

    public void HideEquipmentTooltip(string slot)
    {
        if (hoveredSlot == slot) hoveredSlot = "";
    }

    private void CreateStatsColumn()
    {
        GameObject statsObject = CreateLabel(transform, "StatsLabel", BuildStatsText(), new Vector2(280f, 20f), new Vector2(220f, 260f), 15, TextAnchor.UpperLeft);
        statsLabel = statsObject.GetComponent<Text>();
    }

    private string BuildStatsText()
    {
        return $"Nivel {GameData.Level}\n\nPuntos disponibles: {GameData.AttributePoints}\n\nFuerza       {GameData.Strength}\nPrecision    {GameData.Precision}\nAgilidad     {GameData.Agility}\nVitalidad    {GameData.Vitality}\n\nVida         {GameData.CurrentHP} / {GameData.MaxHP}\n\nReinicio: 100 bronce";
    }

    private void CreateResetAttributesButton()
    {
        GameObject buttonObject = new GameObject("ResetAttributes", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(280f, -145f);
        rect.sizeDelta = new Vector2(220f, 34f);
        buttonObject.GetComponent<Image>().color = new Color(0.55f, 0.3f, 0.16f, 1f);
        buttonObject.GetComponent<Button>().onClick.AddListener(() =>
        {
            NetworkManager networkManager = FindAnyObjectByType<NetworkManager>();
            if (networkManager != null) networkManager.SendResetAttributes();
        });
        CreateLabel(buttonObject.transform, "Label", "Reiniciar atributos", Vector2.zero, new Vector2(210f, 28f), 13, TextAnchor.MiddleCenter);
    }

    private void CreateAttributeButton(string attribute, Vector2 position)
    {
        GameObject buttonObject = new GameObject($"Add_{attribute}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(28f, 28f);
        buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.52f, 0.28f, 1f);

        buttonObject.GetComponent<Button>().onClick.AddListener(() =>
        {
            NetworkManager networkManager = FindAnyObjectByType<NetworkManager>();
            if (networkManager != null) networkManager.SendSpendAttribute(attribute);
        });

        CreateLabel(buttonObject.transform, "Label", "+", Vector2.zero, new Vector2(24f, 24f), 18, TextAnchor.MiddleCenter);
    }

    private void Unequip(string slot)
    {
        NetworkManager networkManager = FindAnyObjectByType<NetworkManager>();
        string equipmentSlot = GetDataSlot(slot);
        if (networkManager != null && !string.IsNullOrEmpty(EquipmentData.Slots[equipmentSlot].itemId))
        {
            networkManager.SendUnequipItem(equipmentSlot);
        }
    }

    private static string GetDataSlot(string visualSlot)
    {
        return visualSlot == "chest" ? "armor" : visualSlot;
    }

    private static string GetDisplayName(string slot)
    {
        switch (slot)
        {
            case "weapon": return "Arma";
            case "ring_left": return "Anillo izquierdo";
            case "ring_right": return "Anillo derecho";
            case "necklace": return "Collar";
            case "special": return "Especial / pasiva";
            case "head": return "Cabeza";
            case "chest": return "Pecho";
            case "gloves": return "Guantes";
            case "legs": return "Piernas";
            case "boots": return "Botas";
            default: return slot;
        }
    }

    private void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}

public sealed class EquipmentSlotTooltipUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private EquipmentUI equipmentUI;
    private string slot;

    public void Configure(EquipmentUI owner, string equipmentSlot)
    {
        equipmentUI = owner;
        slot = equipmentSlot;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (equipmentUI != null) equipmentUI.ShowEquipmentTooltip(slot);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (equipmentUI != null) equipmentUI.HideEquipmentTooltip(slot);
    }
}
