using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerHUD : MonoBehaviour
{
    [Header("Panel Principal del HUD")]
    public GameObject playerHUDPanel; // El panel raíz de tu HUD

    [Header("HUD Jugador Local")]
    public TMP_Text textName;
    public TMP_Text textLevel;
    public Image barHPFill;
    public TMP_Text textHPValues;
    public Image barEXPFill;
    public TMP_Text textEXPValues;

    [Header("Inventario")]
    public GameObject inventoryPanel;

    [Header("Equipamiento")]
    public GameObject equipmentPanel;

    [Header("HUD Objetivos (Target)")]
    public GameObject targetPanel;
    public TMP_Text targetTextName;
    public TMP_Text targetTextLevel;
    public Image targetBarHPFill;
    public TMP_Text targetTextHPValues;

    private Targetable currentTarget;
    private GameObject exitMenuPanel;
    private GameObject rewardsPanel;
    private EquipmentUI equipmentUI;
    private GameObject combatMessagePanel;
    private TMP_Text combatMessageText;
    private Coroutine combatMessageCoroutine;

    public Targetable CurrentTarget => currentTarget;

    void Start()
    {
        // Ocultar paneles al arrancar (esperando el Login)
        if (targetPanel != null) targetPanel.SetActive(false);
        if (playerHUDPanel != null) playerHUDPanel.SetActive(false); 
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (equipmentPanel != null) equipmentPanel.SetActive(false);
        if (rewardsPanel != null) rewardsPanel.SetActive(false);
    }

    void Update()
    {
        if (playerHUDPanel != null && playerHUDPanel.activeSelf &&
            Input.GetKeyDown(KeyCode.I) && inventoryPanel != null)
        {
            inventoryPanel.SetActive(!inventoryPanel.activeSelf);
        }

        if (playerHUDPanel != null && playerHUDPanel.activeSelf && Input.GetKeyDown(KeyCode.O))
        {
            EnsureEquipmentPanel();
            if (equipmentPanel != null) equipmentPanel.SetActive(!equipmentPanel.activeSelf);
        }

        // Si el panel de HUD está activo, mantener datos actualizados
        if (playerHUDPanel != null && playerHUDPanel.activeSelf)
        {
            UpdateLocalHUD();
        }

        if (GameData.IsLoggedIn && Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleExitMenu();
        }

        if (GameData.IsLoggedIn && Input.GetMouseButtonDown(1))
        {
            TryOpenTargetRewards();
        }

        if (currentTarget == null && targetPanel != null && targetPanel.activeSelf)
        {
            ClearTarget();
        }
    }

    // Llamar este método cuando el login sea exitoso
    public void ShowHUD()
    {
        if (playerHUDPanel != null) playerHUDPanel.SetActive(true);
        EnsureEquipmentPanel();
        UpdateLocalHUD();
    }

    private void EnsureEquipmentPanel()
    {
        if (equipmentPanel == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            equipmentPanel = new GameObject("EquipmentPanel", typeof(RectTransform), typeof(Image));
            equipmentPanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = equipmentPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(190f, 0f);
            panelRect.sizeDelta = new Vector2(820f, 500f);
            equipmentPanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
            equipmentPanel.SetActive(false);
        }

        if (equipmentUI == null)
        {
            equipmentUI = equipmentPanel.GetComponent<EquipmentUI>();
            if (equipmentUI == null) equipmentUI = equipmentPanel.AddComponent<EquipmentUI>();
            equipmentUI.Build();
        }
    }

    public void UpdateLocalHUD()
    {
        // Lee en tiempo real el nombre ingresado en el Login
        if (textName != null) textName.text = GameData.PlayerUsername;
        if (textLevel != null) textLevel.text = "Nv. " + GameData.Level;

        if (barHPFill != null)
        {
            float hpPercent = (float)GameData.CurrentHP / GameData.MaxHP;
            barHPFill.fillAmount = Mathf.Clamp01(hpPercent);
        }
        if (textHPValues != null)
        {
            textHPValues.text = $"{GameData.CurrentHP} / {GameData.MaxHP}";
        }

        if (barEXPFill != null)
        {
            float expPercent = (float)GameData.CurrentEXP / GameData.MaxEXP;
            barEXPFill.fillAmount = Mathf.Clamp01(expPercent);
        }
        if (textEXPValues != null)
        {
            textEXPValues.text = $"{GameData.CurrentEXP} / {GameData.MaxEXP}";
        }
    }

    public void ShowCombatMessage(string message)
    {
        EnsureCombatMessagePanel();
        if (combatMessageText == null) return;

        combatMessageText.text = message;
        combatMessagePanel.SetActive(true);
        if (combatMessageCoroutine != null) StopCoroutine(combatMessageCoroutine);
        combatMessageCoroutine = StartCoroutine(HideCombatMessageAfterDelay());
    }

    private void EnsureCombatMessagePanel()
    {
        if (combatMessagePanel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[Combate] No se pudo mostrar el mensaje: PlayerHUD no está dentro de un Canvas.");
            return;
        }

        combatMessagePanel = new GameObject("CombatMessage", typeof(RectTransform), typeof(Image));
        combatMessagePanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = combatMessagePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -80f);
        panelRect.sizeDelta = new Vector2(460f, 48f);
        combatMessagePanel.GetComponent<Image>().color = new Color(0.35f, 0.08f, 0.06f, 0.92f);

        GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(combatMessagePanel.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);
        combatMessageText = textObject.GetComponent<TextMeshProUGUI>();
        combatMessageText.fontSize = 20f;
        combatMessageText.color = Color.white;
        combatMessageText.alignment = TextAlignmentOptions.Center;
        combatMessagePanel.SetActive(false);
    }

    private IEnumerator HideCombatMessageAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (combatMessagePanel != null) combatMessagePanel.SetActive(false);
        combatMessageCoroutine = null;
    }

    public void SetTarget(Targetable target)
    {
        currentTarget = target;
        if (rewardsPanel != null) rewardsPanel.SetActive(false);

        if (targetPanel != null) targetPanel.SetActive(true);
        if (targetTextName != null) targetTextName.text = target.entityName;
        if (targetTextLevel != null) targetTextLevel.text = "Nv. " + target.level;

        UpdateTargetHUD();
    }

    public void UpdateTargetHUD()
    {
        if (currentTarget == null) return;

        UpdateTargetHP(currentTarget.currentHP);
    }

    public void UpdateTargetHP(int currentHP)
    {
        if (currentTarget == null) return;

        currentTarget.currentHP = Mathf.Clamp(currentHP, 0, currentTarget.maxHP);
        float hpPercent = currentTarget.maxHP > 0
            ? (float)currentTarget.currentHP / currentTarget.maxHP
            : 0f;

        if (targetBarHPFill != null)
        {
            targetBarHPFill.fillAmount = Mathf.Clamp01(hpPercent);
        }
        if (targetTextHPValues != null)
        {
            targetTextHPValues.text = $"{currentTarget.currentHP} / {currentTarget.maxHP}";
        }
    }

    public void ClearTargetIf(Targetable target)
    {
        if (currentTarget == target)
        {
            ClearTarget();
        }
    }

    public void ClearTarget()
    {
        currentTarget = null;
        if (targetPanel != null) targetPanel.SetActive(false);
        if (rewardsPanel != null) rewardsPanel.SetActive(false);
    }

    private void TryOpenTargetRewards()
    {
        if (currentTarget == null || targetPanel == null) return;

        RectTransform targetRect = targetPanel.GetComponent<RectTransform>();
        if (targetRect == null || !RectTransformUtility.RectangleContainsScreenPoint(targetRect, Input.mousePosition, null)) return;

        NetworkEnemy enemy = currentTarget as NetworkEnemy;
        if (enemy == null) return;

        EnsureRewardsPanel();
        PopulateRewardsPanel(enemy);
        PositionRewardsPanel(Input.mousePosition);
        rewardsPanel.SetActive(true);
    }

    private void EnsureRewardsPanel()
    {
        if (rewardsPanel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        rewardsPanel = new GameObject("TargetRewardsPanel", typeof(RectTransform), typeof(Image));
        rewardsPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = rewardsPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(320f, 210f);
        panelRect.pivot = new Vector2(0f, 1f);
        rewardsPanel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 0.97f);

        CreateRewardsText(rewardsPanel.transform, "Recompensas", new Vector2(0f, -22f), new Vector2(290f, 32f), 18, TextAlignmentOptions.Center);
        CreateRewardsText(rewardsPanel.transform, "", new Vector2(0f, -82f), new Vector2(290f, 105f), 13, TextAlignmentOptions.TopLeft, "RewardsBody");
        CreateRewardsButton(rewardsPanel.transform, "Cerrar", new Vector2(0f, -184f), HideRewardsPanel);
        rewardsPanel.SetActive(false);
    }

    private void PopulateRewardsPanel(NetworkEnemy enemy)
    {
        TMP_Text body = rewardsPanel.transform.Find("RewardsBody")?.GetComponent<TMP_Text>();
        if (body == null) return;

        string rewardText;
        switch (enemy.enemyTypeId)
        {
            case 1:
                rewardText = "Paladín Básico\nEXP: 100\nMonedas: 5 - 15";
                break;
            case 2:
                rewardText = "Paladín Élite\nEXP: 500\nMonedas: 25 - 60";
                break;
            case 3:
                rewardText = "Lobo\nEXP: 175\nMonedas: 8 - 20";
                break;
            default:
                rewardText = "Criatura\nEXP: 100\nMonedas: 5 - 15";
                break;
        }

        foreach (EnemyDropPreview drop in EnemyDropCatalog.GetDrops(enemy.enemyTypeId))
        {
            ItemDefinition item = ItemCatalog.Get(drop.ItemId);
            rewardText += $"\n{item.DisplayName}: {drop.MinAmount}-{drop.MaxAmount} ({drop.ChancePercent}%)";
        }

        body.text = rewardText;
    }

    private void PositionRewardsPanel(Vector2 screenPosition)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        RectTransform panelRect = rewardsPanel != null ? rewardsPanel.GetComponent<RectTransform>() : null;
        if (canvasRect == null || panelRect == null) return;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            panelRect.anchoredPosition = localPoint + new Vector2(12f, -12f);
        }
    }

    private void HideRewardsPanel()
    {
        if (rewardsPanel != null) rewardsPanel.SetActive(false);
    }

    private TMP_Text CreateRewardsText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAlignmentOptions alignment, string objectName = "Label")
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = position;
        textRect.sizeDelta = size;
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private void CreateRewardsButton(Transform parent, string value, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(value, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 1f);
        buttonRect.anchorMax = new Vector2(0.5f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(100f, 28f);
        buttonObject.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
        buttonObject.GetComponent<Button>().onClick.AddListener(action);
        CreateRewardsText(buttonObject.transform, value, Vector2.zero, new Vector2(100f, 28f), 12, TextAlignmentOptions.Center);
    }

    private void ToggleExitMenu()
    {
        EnsureExitMenu();
        exitMenuPanel.SetActive(!exitMenuPanel.activeSelf);
    }

    private void EnsureExitMenu()
    {
        if (exitMenuPanel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        exitMenuPanel = new GameObject("ExitMenu", typeof(RectTransform), typeof(Image));
        exitMenuPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = exitMenuPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(300f, 150f);
        exitMenuPanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        CreateExitMenuText(exitMenuPanel.transform, "¿Salir del juego?", new Vector2(0f, 38f), 20);
        CreateExitMenuButton(exitMenuPanel.transform, "Salir", new Vector2(-68f, -35f), ExitGame);
        CreateExitMenuButton(exitMenuPanel.transform, "Cancelar", new Vector2(68f, -35f), () => exitMenuPanel.SetActive(false));
        exitMenuPanel.SetActive(false);
    }

    private void CreateExitMenuText(Transform parent, string value, Vector2 position, int fontSize)
    {
        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = position;
        textRect.sizeDelta = new Vector2(260f, 32f);
        Text label = textObject.GetComponent<Text>();
        label.text = value;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
    }

    private void CreateExitMenuButton(Transform parent, string value, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(value, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(110f, 36f);
        buttonObject.GetComponent<Image>().color = value == "Salir"
            ? new Color(0.7f, 0.18f, 0.18f, 1f)
            : new Color(0.25f, 0.25f, 0.25f, 1f);
        buttonObject.GetComponent<Button>().onClick.AddListener(action);
        CreateExitMenuText(buttonObject.transform, value, Vector2.zero, 14);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}