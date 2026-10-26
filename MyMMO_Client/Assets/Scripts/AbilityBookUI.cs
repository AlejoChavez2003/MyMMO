using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AbilityBookUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform abilityContainer;

    private AbilityData[] abilities;
    private readonly List<AbilityEntryUI> entries = new List<AbilityEntryUI>();
    public static AbilityData DraggedAbility { get; private set; }
    public static AbilityData SelectedAbility { get; private set; }
    private int displayedLevel = -1;
    private string displayedClass;

    private void Start()
    {
        AbilityTooltipUI.Ensure();
        abilities = Resources.LoadAll<AbilityData>("Skills");
        Debug.Log($"[Habilidades] Cargadas {abilities.Length} habilidades desde Resources/Skills.");
        System.Array.Sort(abilities, (first, second) => first.requiredLevel != second.requiredLevel
            ? first.requiredLevel.CompareTo(second.requiredLevel)
            : string.Compare(first.abilityName, second.abilityName, System.StringComparison.Ordinal));
        CreatePanelHeader();
        BuildEntries();
        RefreshEntries();
    }

    private void Update()
    {
        if (displayedLevel != GameData.Level || displayedClass != GameData.PlayerClass)
        {
            displayedLevel = GameData.Level;
            displayedClass = GameData.PlayerClass;
            RefreshEntries();
        }

        if (GameData.IsLoggedIn && Input.GetKeyDown(KeyCode.K) && panel != null)
        {
            panel.SetActive(!panel.activeSelf);
        }
    }

    private void BuildEntries()
    {
        if (abilityContainer == null) return;

        if (abilityContainer.GetComponent<VerticalLayoutGroup>() == null)
        {
            VerticalLayoutGroup layout = abilityContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
        }

        foreach (AbilityData ability in abilities)
        {
            GameObject entryObject = new GameObject(ability.abilityName, typeof(RectTransform), typeof(Image), typeof(Button));
            entryObject.transform.SetParent(abilityContainer, false);
            RectTransform rect = entryObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 48f);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(entryObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 4f);
            labelRect.offsetMax = new Vector2(-10f, -4f);
            Text label = labelObject.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.color = Color.black;

            AbilityEntryUI entry = entryObject.AddComponent<AbilityEntryUI>();
            entry.Configure(this, ability, label);
            entries.Add(entry);
        }
    }

    private void RefreshEntries()
    {
        foreach (AbilityEntryUI entry in entries) entry.Refresh(GameData.Level, GameData.PlayerClass);
    }

    public bool IsUnlocked(AbilityData ability)
    {
        return ability != null && GameData.Level >= ability.requiredLevel &&
            GameData.IsClass(ability.requiredClass);
    }

    public void BeginAbilityDrag(AbilityData ability)
    {
        if (IsUnlocked(ability)) DraggedAbility = ability;
    }

    public void SelectAbilityForActionBar(AbilityData ability)
    {
        if (!IsUnlocked(ability))
        {
            Debug.LogWarning($"[Habilidades] {ability?.abilityName} bloqueada. Nivel actual: {GameData.Level}, clase: {GameData.PlayerClass}.");
            return;
        }

        SelectedAbility = ability;
        Debug.Log($"[Habilidades] {ability.abilityName} seleccionada. Haz clic en un slot de la action bar.");
    }

    public void EndAbilityDrag()
    {
    }

    public static void ClearDraggedAbility()
    {
        DraggedAbility = null;
    }

    public static void ClearSelectedAbility()
    {
        SelectedAbility = null;
    }

    private void CreatePanelHeader()
    {
        if (panel == null) return;

        AbilityPanelDragUI existingDrag = panel.GetComponent<AbilityPanelDragUI>();
        if (existingDrag != null) Destroy(existingDrag);

        GameObject header = new GameObject("AbilityHeader", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(panel.transform, false);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 42f);
        header.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(header.transform, false);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(14f, 0f);
        titleRect.offsetMax = new Vector2(-52f, 0f);
        Text title = titleObject.GetComponent<Text>();
        title.text = "Libro de habilidades";
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 18;
        title.color = Color.white;
        title.alignment = TextAnchor.MiddleLeft;
        title.raycastTarget = true;

        GameObject closeObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObject.transform.SetParent(header.transform, false);
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-8f, 0f);
        closeRect.sizeDelta = new Vector2(34f, 30f);
        closeObject.GetComponent<Image>().color = new Color(0.7f, 0.18f, 0.18f, 1f);
        Button closeButton = closeObject.GetComponent<Button>();
        closeButton.onClick.AddListener(() => panel.SetActive(false));

        GameObject closeLabelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        closeLabelObject.transform.SetParent(closeObject.transform, false);
        RectTransform closeLabelRect = closeLabelObject.GetComponent<RectTransform>();
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.offsetMin = Vector2.zero;
        closeLabelRect.offsetMax = Vector2.zero;
        Text closeLabel = closeLabelObject.GetComponent<Text>();
        closeLabel.text = "X";
        closeLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeLabel.fontSize = 18;
        closeLabel.color = Color.white;
        closeLabel.alignment = TextAnchor.MiddleCenter;
        closeLabel.raycastTarget = false;

        DraggableUI dragHandle = titleObject.AddComponent<DraggableUI>();
        dragHandle.SetTarget(panel.transform as RectTransform);
    }
}

public class AbilityPanelDragUI : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform panelRect;
    private RectTransform parentRect;
    private Vector2 pointerOffset;
    private bool draggingPanel;

    private void Awake()
    {
        panelRect = transform as RectTransform;
        parentRect = panelRect != null ? panelRect.parent as RectTransform : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        draggingPanel = eventData.pointerPress == gameObject ||
            eventData.pointerPress == null ||
            eventData.pointerPress.GetComponentInParent<AbilityEntryUI>() == null;
        if (!draggingPanel) return;

        if (panelRect == null || parentRect == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out Vector2 pointerPosition))
        {
            pointerOffset = panelRect.anchoredPosition - pointerPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!draggingPanel) return;
        if (panelRect == null || parentRect == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out Vector2 pointerPosition))
        {
            panelRect.anchoredPosition = pointerPosition + pointerOffset;
        }
    }
}

public class AbilityEntryUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private AbilityBookUI abilityBook;
    private AbilityData ability;
    private Text label;
    private CanvasGroup canvasGroup;
    private bool previousBlocksRaycasts;

    public void Configure(AbilityBookUI owner, AbilityData data, Text entryLabel)
    {
        abilityBook = owner;
        ability = data;
        label = entryLabel;
    }

    public void Refresh(int level, string characterClass)
    {
        bool unlocked = abilityBook.IsUnlocked(ability);
        Button button = GetComponent<Button>();
        button.interactable = unlocked;
        button.transition = Selectable.Transition.None;
        label.text = unlocked
            ? ability.abilityName
            : GameData.Level < ability.requiredLevel
                ? $"{ability.abilityName} (Nivel {ability.requiredLevel})"
                : $"{ability.abilityName} ({ability.requiredClass})";
        GetComponent<Image>().color = unlocked
            ? new Color(0.35f, 0.75f, 0.4f, 1f)
            : new Color(0.25f, 0.25f, 0.25f, 1f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        previousBlocksRaycasts = canvasGroup.blocksRaycasts;
        canvasGroup.blocksRaycasts = false;
        abilityBook.BeginAbilityDrag(ability);
        Debug.Log($"[Habilidades] Iniciando arrastre de {ability.abilityName}.");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            abilityBook.SelectAbilityForActionBar(ability);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AbilityTooltipUI.Show(ability);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AbilityTooltipUI.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 1 &&
            AbilityBookUI.SelectedAbility != ability)
        {
            abilityBook.SelectAbilityForActionBar(ability);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (AbilityBookUI.DraggedAbility != null)
        {
            ActionBarSlotUI actionBarSlot = FindActionBarSlotUnderPointer(eventData);

            if (actionBarSlot != null)
            {
                actionBarSlot.AssignDraggedAbility(AbilityBookUI.DraggedAbility);
                Debug.Log($"[Habilidades] {ability.abilityName} asignada a la action bar.");
            }
            else if (TryAssignThroughActionBar(eventData, AbilityBookUI.DraggedAbility))
            {
                Debug.Log($"[Habilidades] {ability.abilityName} asignada por posicion a la action bar.");
            }
            else
            {
                Debug.Log("[Habilidades] El arrastre termino fuera de un slot de la action bar.");
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = previousBlocksRaycasts;
        }

        abilityBook.EndAbilityDrag();
    }

    private static bool TryAssignThroughActionBar(PointerEventData eventData, AbilityData ability)
    {
        ActionBarController actionBar = FindAnyObjectByType<ActionBarController>();
        return actionBar != null && actionBar.TryAssignAbilityAtScreenPosition(
            eventData.position, eventData.pressEventCamera, ability);
    }

    private static ActionBarSlotUI FindActionBarSlotUnderPointer(PointerEventData eventData)
    {
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current?.RaycastAll(eventData, raycastResults);

        foreach (RaycastResult result in raycastResults)
        {
            ActionBarSlotUI actionBarSlot = result.gameObject.GetComponentInParent<ActionBarSlotUI>();
            if (actionBarSlot != null) return actionBarSlot;
        }

        GameObject releasedObject = eventData.pointerCurrentRaycast.gameObject;
        return releasedObject != null
            ? releasedObject.GetComponentInParent<ActionBarSlotUI>()
            : null;
    }
}

public sealed class AbilityTooltipUI : MonoBehaviour
{
    private static AbilityTooltipUI instance;
    private static AbilityData visibleAbility;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;

    public static void Ensure()
    {
        if (instance != null) return;

        GameObject tooltipObject = new GameObject("AbilityTooltip");
        instance = tooltipObject.AddComponent<AbilityTooltipUI>();
    }

    public static void Show(AbilityData ability)
    {
        if (ability == null) return;
        Ensure();
        visibleAbility = ability;
    }

    public static void Hide()
    {
        visibleAbility = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnGUI()
    {
        if (visibleAbility == null) return;

        titleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        bodyStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };

        const float width = 250f;
        const float height = 116f;
        Vector2 mouse = Event.current.mousePosition;
        float x = Mathf.Min(mouse.x + 16f, Screen.width - width - 8f);
        float y = Mathf.Min(mouse.y + 16f, Screen.height - height - 8f);
        Rect tooltipRect = new Rect(x, y, width, height);

        GUI.color = new Color(0.05f, 0.07f, 0.1f, 0.96f);
        GUI.Box(tooltipRect, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 10f, y + 8f, width - 20f, 24f), visibleAbility.abilityName, titleStyle);
        string details = $"Danio: {visibleAbility.baseDamage}\nCooldown: {visibleAbility.cooldown:0.##} s\nAlcance: {visibleAbility.range:0.##} m";
        GUI.Label(new Rect(x + 10f, y + 36f, width - 20f, height - 44f), details, bodyStyle);
    }
}
