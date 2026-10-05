using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ActionBarController : MonoBehaviour
{
    [SerializeField] private AbilityData[] abilities = new AbilityData[5];
    [SerializeField] private string[] itemIds = new string[5];
    [SerializeField] private Image[] slotImages = new Image[5];
    [SerializeField] private GameObject actionBarObject;

    public event Action<AbilityData, int> AbilityPressed;
    private bool dropTargetsConfigured;
    private Sprite[] defaultSlotSprites;
    private readonly Dictionary<string, Sprite> abilityIcons = new Dictionary<string, Sprite>();

    private void Awake()
    {
        if (actionBarObject == null) actionBarObject = gameObject;
        defaultSlotSprites = new Sprite[slotImages.Length];
        for (int slot = 0; slot < slotImages.Length; slot++)
        {
            if (slotImages[slot] != null) defaultSlotSprites[slot] = slotImages[slot].sprite;
        }
        actionBarObject.SetActive(false);
    }

    private void Start()
    {
        ConfigureDropTargets();
        UpdateUI();
    }

    private void ConfigureDropTargets()
    {
        if (actionBarObject == null || dropTargetsConfigured) return;

        for (int slot = 0; slot < Mathf.Min(actionBarObject.transform.childCount, 5); slot++)
        {
            ActionBarSlotUI dropTarget = actionBarObject.transform.GetChild(slot).GetComponent<ActionBarSlotUI>();
            if (dropTarget == null) dropTarget = actionBarObject.transform.GetChild(slot).gameObject.AddComponent<ActionBarSlotUI>();
            dropTarget.Configure(this, slot);
        }

        dropTargetsConfigured = true;
    }

    public void ShowActionBar()
    {
        if (actionBarObject == null) actionBarObject = gameObject;
        actionBarObject.SetActive(true);
        ConfigureDropTargets();
        UpdateUI();
    }

    public void UpdateUI()
    {
        int slotCount = Mathf.Min(abilities.Length, slotImages.Length, 5);
        for (int slot = 0; slot < slotCount; slot++)
        {
            if (slotImages[slot] != null)
            {
                bool abilityUnlocked = abilities[slot] != null && IsAbilityUnlocked(abilities[slot]);
                if (abilities[slot] != null)
                {
                    slotImages[slot].sprite = GetAbilityIcon(abilities[slot]);
                    slotImages[slot].preserveAspect = true;
                }
                else if (slot < itemIds.Length && !string.IsNullOrEmpty(itemIds[slot]))
                {
                    slotImages[slot].sprite = defaultSlotSprites[slot];
                    slotImages[slot].preserveAspect = false;
                }
                else
                {
                    slotImages[slot].sprite = defaultSlotSprites[slot];
                    slotImages[slot].preserveAspect = false;
                }

                slotImages[slot].color = abilityUnlocked
                    ? new Color(1f, 1f, 1f, 1f)
                    : abilities[slot] != null
                        ? new Color(0.45f, 0.45f, 0.45f, 0.65f)
                        : slot < itemIds.Length && !string.IsNullOrEmpty(itemIds[slot])
                        ? new Color(1f, 0.35f, 0.35f, 1f)
                        : new Color(1f, 1f, 1f, 0.2f);
            }
        }
    }

    private Sprite GetAbilityIcon(AbilityData ability)
    {
        string abilityKey = ability != null ? ability.abilityId : "unknown";
        if (abilityIcons.TryGetValue(abilityKey, out Sprite existingIcon)) return existingIcon;

        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        Color fill = ability != null && ability.abilityId == "1002"
            ? new Color(0.95f, 0.7f, 0.15f, 1f)
            : new Color(0.25f, 0.75f, 0.95f, 1f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool diagonal = Mathf.Abs(x - y) < 7 || Mathf.Abs(x + y - 63) < 7;
                texture.SetPixel(x, y, diagonal ? fill : new Color(0f, 0f, 0f, 0f));
            }
        }

        texture.Apply();
        Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        icon.name = $"RuntimeAbilityIcon_{abilityKey}";
        abilityIcons[abilityKey] = icon;
        return icon;
    }

    public AbilityData GetAbility(int slot)
    {
        return slot >= 0 && slot < abilities.Length ? abilities[slot] : null;
    }

    public void AssignItem(int slot, string itemId)
    {
        if (slot < 0 || slot >= itemIds.Length || string.IsNullOrEmpty(itemId)) return;
        itemIds[slot] = itemId;
        Debug.Log($"[ActionBar] Objeto {itemId} asignado al slot {slot + 1}.");
        UpdateUI();
    }

    public void AssignAbility(int slot, AbilityData ability)
    {
        if (ability == null || slot < 0 || slot >= abilities.Length) return;
        abilities[slot] = ability;
        if (slot < itemIds.Length) itemIds[slot] = "";
        Debug.Log($"[ActionBar] Habilidad {ability.abilityName} asignada al slot {slot + 1}.");
        UpdateUI();
    }

    public bool TryAssignAbilityAtScreenPosition(Vector2 screenPosition, Camera eventCamera, AbilityData ability)
    {
        if (ability == null || actionBarObject == null) return false;

        for (int slot = 0; slot < Mathf.Min(actionBarObject.transform.childCount, 5); slot++)
        {
            RectTransform slotRect = actionBarObject.transform.GetChild(slot) as RectTransform;
            if (slotRect == null || !RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPosition, eventCamera)) continue;

            ActionBarSlotUI actionBarSlot = slotRect.GetComponent<ActionBarSlotUI>();
            if (actionBarSlot != null)
            {
                actionBarSlot.AssignDraggedAbility(ability);
            }
            else
            {
                AssignAbility(slot, ability);
                AbilityBookUI.ClearDraggedAbility();
            }

            return true;
        }

        return false;
    }

    public void BeginSlotDrag(int slot)
    {
        Debug.Log($"[ActionBar] Arrastrando slot {slot + 1}.");
    }

    public void EndSlotDrag(int slot, PointerEventData eventData)
    {
        GameObject releasedObject = eventData.pointerCurrentRaycast.gameObject;
        ActionBarSlotUI releasedOnActionBar = releasedObject != null
            ? releasedObject.GetComponentInParent<ActionBarSlotUI>()
            : null;

        if (releasedOnActionBar == null)
        {
            ClearSlot(slot);
        }
    }

    public void ClearSlot(int slot)
    {
        if (slot < 0 || slot >= itemIds.Length) return;
        itemIds[slot] = "";
        abilities[slot] = null;
        Debug.Log($"[ActionBar] Slot {slot + 1} liberado. El objeto no se ha eliminado del inventario.");
        UpdateUI();
    }

    private void Update()
    {
        UpdateUI();

        for (int slot = 0; slot < Mathf.Min(abilities.Length, 5); slot++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + slot) || Input.GetKeyDown(KeyCode.Keypad1 + slot))
            {
                AbilityData ability = abilities[slot];
                if (ability != null)
                {
                    if (!IsAbilityUnlocked(ability))
                    {
                        Debug.Log($"[ActionBar] {ability.abilityName} requiere nivel {ability.requiredLevel}.");
                        continue;
                    }

                    Debug.Log($"¡Se presionó la tecla {slot + 1}! Intentando lanzar habilidad {slot + 1}...");
                    AbilityPressed?.Invoke(ability, slot);
                }
                else if (slot < itemIds.Length && !string.IsNullOrEmpty(itemIds[slot]))
                {
                    NetworkManager networkManager = FindAnyObjectByType<NetworkManager>();
                    if (networkManager != null) networkManager.SendUseItem(itemIds[slot]);
                }
            }
        }
    }

    private static bool IsAbilityUnlocked(AbilityData ability)
    {
        return ability != null && GameData.Level >= ability.requiredLevel &&
            (string.IsNullOrEmpty(ability.requiredClass) || ability.requiredClass == GameData.PlayerClass);
    }
}

public class ActionBarSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private ActionBarController actionBarController;
    private int slotIndex;

    public void Configure(ActionBarController owner, int index)
    {
        actionBarController = owner;
        slotIndex = index;
    }

    public void OnDrop(PointerEventData eventData)
    {
        InventoryUI inventoryUI = FindAnyObjectByType<InventoryUI>();
        if (inventoryUI != null) inventoryUI.DropOnActionBar(slotIndex, actionBarController);

        if (AbilityBookUI.DraggedAbility != null && actionBarController != null)
        {
            actionBarController.AssignAbility(slotIndex, AbilityBookUI.DraggedAbility);
            AbilityBookUI.ClearDraggedAbility();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || actionBarController == null) return;

        AbilityData selectedAbility = AbilityBookUI.SelectedAbility;
        if (selectedAbility == null) return;

        actionBarController.AssignAbility(slotIndex, selectedAbility);
        AbilityBookUI.ClearSelectedAbility();
        Debug.Log($"[ActionBar] Habilidad {selectedAbility.abilityName} asignada por clic al slot {slotIndex + 1}.");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (actionBarController != null)
        {
            AbilityTooltipUI.Show(actionBarController.GetAbility(slotIndex));
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AbilityTooltipUI.Hide();
    }

    public void AssignDraggedAbility(AbilityData ability)
    {
        if (actionBarController == null || ability == null) return;
        actionBarController.AssignAbility(slotIndex, ability);
        AbilityBookUI.ClearDraggedAbility();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (actionBarController != null) actionBarController.BeginSlotDrag(slotIndex);
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (actionBarController != null) actionBarController.EndSlotDrag(slotIndex, eventData);
    }
}
