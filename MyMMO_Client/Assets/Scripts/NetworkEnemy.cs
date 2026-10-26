using UnityEngine;
using TMPro;

public enum EnemyAiState
{
    Wander = 0,
    Chase = 1,
    Attack = 2,
    Return = 3
}

// Heredamos de Targetable para que PlayerHUD lo reconozca automáticamente
public class NetworkEnemy : Targetable 
{
    private const float MovementThreshold = 0.05f;
    private static readonly int[] WalkingStateHashes =
    {
        Animator.StringToHash("Base Layer.Walking_New"),
        Animator.StringToHash("Base Layer.Walking")
    };
    private static readonly int[] IdleStateHashes =
    {
        Animator.StringToHash("Base Layer.Idle_New"),
        Animator.StringToHash("Base Layer.Idle")
    };

    public int enemyId;
    public int enemyTypeId;
    public string enemyType;
    public Animator animator;
    public EnemyAiState currentState = EnemyAiState.Wander;
    public int targetClientId = 0;

    [Header("Sincronización del servidor")]
    [SerializeField] private float syncLerpSpeed = 8f;
    [SerializeField] private float chaseMoveSpeed = 3.2f;
    [SerializeField] private float attackAnimationCooldown = 0.5f;

    [Header("Reacciones visuales")]
    [SerializeField] private float hitVisualCooldown = 0.2f;
    private float nextHitVisualTime;
    private bool isDying;
    private Vector3 serverPosition;
    private Vector3 lastPosition;
    private bool isMoving;
    private float nextAttackTriggerTime;
    private NetworkManager networkManager;

    [Header("UI Flotante sobre la cabeza")]
    public TMPro.TextMeshProUGUI nameText;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        serverPosition = transform.position;
        lastPosition = transform.position;
        networkManager = FindAnyObjectByType<NetworkManager>();
    }

    private void Update()
    {
        if (isDying)
        {
            lastPosition = transform.position;
            return;
        }

        Transform combatTarget = targetClientId > 0 ? FindTargetTransform() : null;
        Vector3 moveTarget = serverPosition;

        if ((currentState == EnemyAiState.Chase || currentState == EnemyAiState.Attack) && combatTarget != null)
        {
            moveTarget = new Vector3(combatTarget.position.x, transform.position.y, combatTarget.position.z);
            FacePosition(moveTarget);

            float distanceToTarget = Vector3.Distance(transform.position, combatTarget.position);
            if (currentState == EnemyAiState.Chase && distanceToTarget <= 1.5f)
            {
                transform.position = Vector3.Lerp(transform.position, serverPosition, Time.deltaTime * syncLerpSpeed);
                UpdateMovementAnimation();
                return;
            }
        }

        if (currentState == EnemyAiState.Attack)
        {
            transform.position = Vector3.Lerp(transform.position, serverPosition, Time.deltaTime * syncLerpSpeed);
        }
        else if (currentState == EnemyAiState.Chase)
        {
            float distanceToTarget = combatTarget != null ? Vector3.Distance(transform.position, combatTarget.position) : 0f;
            if (combatTarget != null && distanceToTarget <= 1.5f)
            {
                transform.position = Vector3.Lerp(transform.position, serverPosition, Time.deltaTime * syncLerpSpeed);
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, moveTarget, chaseMoveSpeed * Time.deltaTime);
            }
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, serverPosition, Time.deltaTime * syncLerpSpeed);
        }

        UpdateMovementAnimation();
    }

    public void Initialize(int id, string type, int hp, int enemyLevel)
    {
        enemyId = id;
        int.TryParse(type, out enemyTypeId);
        enemyType = GetDisplayName(type);
        entityName = enemyType;
        networkId = id;
        serverPosition = transform.position;
        lastPosition = transform.position;
        
        maxHP = hp;
        currentHP = hp;
        level = Mathf.Max(1, enemyLevel);
        EnsureNameplate();
        nameplateText = nameText;

        if (nameText != null)
        {
            nameText.text = entityName;
        }

        if (nameplateText != null)
        {
            nameplateText.text = entityName;
        }

        Debug.Log($"[NetworkEnemy] Inicializado: {enemyType} (ID: {enemyId}) con {maxHP} HP.");
    }

    private void EnsureNameplate()
    {
        if (nameText == null)
        {
            nameText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (nameText != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("EnemyNameplate", typeof(RectTransform), typeof(Canvas), typeof(Billboard));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 2f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        GameObject textObject = new GameObject("NameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(200f, 50f);

        nameText = textObject.GetComponent<TextMeshProUGUI>();
        nameText.font = TMP_Settings.defaultFontAsset;
        nameText.fontSize = 12f;
        nameText.alignment = TextAlignmentOptions.Center;
    }

    private static string GetDisplayName(string type)
    {
        switch (int.TryParse(type, out int typeId) ? typeId : 0)
        {
            case 1: return "Paladín Básico";
            case 2: return "Paladín Élite";
            case 3: return "Lobo";
            default: return "Criatura";
        }
    }

    public void ApplyServerState(int stateValue, int targetId, Vector3 newPosition)
    {
        currentState = (EnemyAiState)stateValue;
        targetClientId = targetId;
        serverPosition = newPosition;

        Debug.Log($"[NetworkEnemy {enemyId}] Estado={currentState} TargetClientId={targetClientId}");

        if (currentState == EnemyAiState.Attack)
        {
            TriggerAttackAnimation();
            CancelInvoke(nameof(ClearAttackVisualState));
            Invoke(nameof(ClearAttackVisualState), 0.5f);
        }
        else
        {
            CancelInvoke(nameof(ClearAttackVisualState));
        }

        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool inCombat = currentState == EnemyAiState.Chase || currentState == EnemyAiState.Attack;
        bool shouldMove = isMoving && currentState != EnemyAiState.Attack;

        SetAnimatorBool("isMoving", shouldMove);
        SetAnimatorBool("IsMoving", shouldMove);
        SetAnimatorBool("isChasing", currentState == EnemyAiState.Chase);
        SetAnimatorBool("isRunning", shouldMove && currentState == EnemyAiState.Chase);
        SetAnimatorBool("isAttacking", currentState == EnemyAiState.Attack);

        if (!HasAnimatorParameter("isMoving", AnimatorControllerParameterType.Bool)
            && !HasAnimatorParameter("IsMoving", AnimatorControllerParameterType.Bool))
        {
            PlayMovementState(shouldMove);
        }

        if (inCombat && currentState == EnemyAiState.Attack)
        {
            SetAnimatorBool("isMoving", false);
            SetAnimatorBool("IsMoving", false);
        }
    }

    private void UpdateMovementAnimation()
    {
        isMoving = Vector3.Distance(transform.position, lastPosition) > MovementThreshold;
        UpdateAnimator();
        lastPosition = transform.position;
    }

    private void PlayMovementState(bool moving)
    {
        int[] stateHashes = moving ? WalkingStateHashes : IdleStateHashes;

        foreach (int stateHash in stateHashes)
        {
            if (animator.HasState(0, stateHash))
            {
                if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash != stateHash)
                {
                    animator.Play(stateHash, 0);
                }

                return;
            }
        }
    }

    private void FacePosition(Vector3 lookPosition)
    {
        Vector3 direction = lookPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
    }

    private Transform FindTargetTransform()
    {
        if (networkManager == null)
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
        }

        return networkManager != null ? networkManager.GetEntityTransform(targetClientId) : null;
    }

    public void SetServerPosition(Vector3 position)
    {
        serverPosition = position;
    }

    public void TakeDamageVisual()
    {
        if (isDying || animator == null || Time.time < nextHitVisualTime)
        {
            return;
        }

        nextHitVisualTime = Time.time + Mathf.Max(0f, hitVisualCooldown);
        animator.ResetTrigger("Hit");
        animator.SetTrigger("Hit");
    }

    public void PlayAttackVisual()
    {
        TriggerAttackAnimation();
    }

    private void TriggerAttackAnimation()
    {
        if (animator == null || Time.time < nextAttackTriggerTime) return;

        SetAnimatorBool("isMoving", false);
        SetAnimatorBool("isChasing", false);
        SetAnimatorBool("isRunning", false);
        SetAnimatorBool("isAttacking", true);

        string[] triggerNames = { "Attack", "attack", "AttackTrigger", "Attack_Trigger", "TriggerAttack" };
        foreach (string triggerName in triggerNames)
        {
            if (HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger))
            {
                FireAnimatorTrigger(triggerName);
                break;
            }
        }

        nextAttackTriggerTime = Time.time + attackAnimationCooldown;
    }

    private void ClearAttackVisualState()
    {
        if (animator == null) return;

        SetAnimatorBool("isAttacking", false);
        SetAnimatorBool("isChasing", false);
        SetAnimatorBool("isRunning", false);
        SetAnimatorBool("isMoving", false);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterName, value);
        }
    }

    private void FireAnimatorTrigger(string parameterName)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger)) return;

        animator.ResetTrigger(parameterName);
        animator.SetTrigger(parameterName);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == type)
            {
                return true;
            }
        }

        return false;
    }

    public void DieVisual(float corpseDelay)
    {
        isDying = true;

        if (animator != null)
        {
            animator.ResetTrigger("Hit");
            animator.SetTrigger("Die");
        }

        Collider enemyCollider = GetComponent<Collider>();
        if (enemyCollider != null)
        {
            enemyCollider.enabled = false;
        }

        Destroy(gameObject, corpseDelay);
    }

    private void OnMouseDown()
    {
        Debug.Log($"[Target] ¡Clic en el enemigo: {enemyType} (ID: {enemyId})!");

        PlayerHUD hud = FindAnyObjectByType<PlayerHUD>();
        if (hud != null)
        {
            hud.SetTarget(this);
        }
        else
        {
            Debug.LogWarning("[NetworkEnemy] No se encontró ningún PlayerHUD en la escena.");
        }
    }
}