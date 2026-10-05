using UnityEngine;

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
    private float nextAttackTriggerTime;
    private NetworkManager networkManager;

    [Header("UI Flotante sobre la cabeza")]
    public TMPro.TextMeshProUGUI nameText;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        serverPosition = transform.position;
        networkManager = FindAnyObjectByType<NetworkManager>();
    }

    private void Update()
    {
        if (isDying) return;

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
                UpdateAnimator();
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

        UpdateAnimator();
    }

    public void Initialize(int id, string type, int hp, int enemyLevel)
    {
        enemyId = id;
        int.TryParse(type, out enemyTypeId);
        enemyType = GetDisplayName(type);
        networkId = id;
        serverPosition = transform.position;
        
        maxHP = hp;
        currentHP = hp;
        level = Mathf.Max(1, enemyLevel);

        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(entityName) ? enemyType : entityName;
        }

        Debug.Log($"[NetworkEnemy] Inicializado: {enemyType} (ID: {enemyId}) con {maxHP} HP.");
    }

    private static string GetDisplayName(string type)
    {
        switch (type)
        {
            case "2": return "Paladin oscuro";
            case "3": return "Bestia veloz";
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
        bool isMoving = currentState == EnemyAiState.Chase
            || (currentState == EnemyAiState.Wander && Vector3.Distance(transform.position, serverPosition) > 0.1f);

        SetAnimatorBool("isMoving", isMoving);
        SetAnimatorBool("isChasing", currentState == EnemyAiState.Chase);
        SetAnimatorBool("isRunning", currentState == EnemyAiState.Chase);
        SetAnimatorBool("isAttacking", currentState == EnemyAiState.Attack);

        if (inCombat && currentState == EnemyAiState.Attack)
        {
            SetAnimatorBool("isMoving", false);
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