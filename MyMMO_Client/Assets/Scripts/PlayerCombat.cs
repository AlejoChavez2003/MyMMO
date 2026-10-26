using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private ActionBarController actionBarController;
    [SerializeField] private PlayerHUD playerHUD;
    public Animator animator;

    [Header("Feedback visual")]
    public GameObject basicArrowPrefab;
    public GameObject skill1Prefab;
    public GameObject skill2Prefab;
    public Transform firePoint;

    [Header("Ataque básico")]
    [SerializeField] private float basicAttackRange = 3f;
    [SerializeField] private float basicAttackCooldown = 1f;
    [SerializeField] private bool autoAttack;

    private readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();
    private float nextBasicAttackTime;

    private void Awake()
    {
        if (networkManager == null) networkManager = FindAnyObjectByType<NetworkManager>();
        if (actionBarController == null) actionBarController = GetComponent<ActionBarController>();
        if (playerHUD == null) playerHUD = FindAnyObjectByType<PlayerHUD>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (actionBarController != null) actionBarController.AbilityPressed += CastAbility;
    }

    private void OnDisable()
    {
        if (actionBarController != null) actionBarController.AbilityPressed -= CastAbility;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || autoAttack)
        {
            TryBasicAttack();
        }
    }

    public bool TryBasicAttack()
    {
        if (Time.time < nextBasicAttackTime || playerHUD == null) return false;
        if (!HasCompatibleWeapon(null)) return false;

        Targetable target = playerHUD.CurrentTarget;
        if (!TryGetTargetId(target, out int targetId) || !IsInRange(target.transform, basicAttackRange)) return false;

        ItemDefinition equippedWeapon = ItemCatalog.Get(EquipmentData.Slots["weapon"].itemId);
        float attackSpeedMultiplier = 1f + Mathf.Max(0, equippedWeapon.AttackSpeedBonus) / 100f;
        nextBasicAttackTime = Time.time + basicAttackCooldown / attackSpeedMultiplier;
        TrySetAnimationTrigger("BasicAttack");
        networkManager?.SendAttack("BASIC_ATTACK", targetId, -1);
        TrySetAnimationTrigger("Attack");
        if (equippedWeapon.Id == "novice_bow")
        {
            SpawnVisualProjectile(basicArrowPrefab, target.transform);
        }

        return true;
    }

    public void CastAbility(AbilityData ability, int slot)
    {
        if (ability == null || playerHUD == null) return;
        if (GameData.Level < ability.requiredLevel || !GameData.IsClass(ability.requiredClass))
        {
            playerHUD.ShowCombatMessage($"Habilidad exclusiva de {ability.requiredClass}.");
            return;
        }

        if (!HasCompatibleWeapon(ability) || IsOnCooldown(ability.abilityId)) return;

        Targetable target = playerHUD.CurrentTarget;
        if (!TryGetTargetId(target, out int targetId) || !IsInRange(target.transform, ability.range)) return;

        cooldowns[ability.abilityId] = Time.time + Mathf.Max(0f, ability.cooldown);
        if (!string.IsNullOrWhiteSpace(ability.animationName)) TrySetAnimationTrigger(ability.animationName);
        networkManager?.SendAttack(ability.abilityId, targetId, slot);
        TrySetAnimationTrigger("Attack");

        GameObject projectilePrefab = slot == 0 ? skill1Prefab : slot == 1 ? skill2Prefab : null;
        SpawnVisualProjectile(projectilePrefab, target.transform);
    }

    private bool HasCompatibleWeapon(AbilityData ability)
    {
        EquipmentSlotData weaponSlot = EquipmentData.Slots["weapon"];
        if (string.IsNullOrEmpty(weaponSlot.itemId))
        {
            playerHUD?.ShowCombatMessage("¡Necesitas un arma equipada!");
            return false;
        }

        if (ability != null && !string.IsNullOrEmpty(ability.requiredWeaponItemId) &&
            weaponSlot.itemId != ability.requiredWeaponItemId)
        {
            playerHUD?.ShowCombatMessage($"¡Necesitas equipar {ItemCatalog.Get(ability.requiredWeaponItemId).DisplayName}!");
            return false;
        }

        return true;
    }

    private void SpawnVisualProjectile(GameObject projectilePrefab, Transform target)
    {
        if (projectilePrefab == null || firePoint == null || target == null) return;

        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        VisualProjectile visualProjectile = projectile.GetComponent<VisualProjectile>();
        if (visualProjectile != null)
        {
            visualProjectile.SetTarget(target);
        }
    }

    private void TrySetAnimationTrigger(string parameterName)
    {
        if (animator == null) return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(parameterName);
                return;
            }
        }

        Debug.LogWarning($"[Combate] El parámetro de animación '{parameterName}' no existe en el Animator.");
    }

    private bool IsOnCooldown(string abilityId)
    {
        return !string.IsNullOrEmpty(abilityId) && cooldowns.TryGetValue(abilityId, out float readyAt) && Time.time < readyAt;
    }

    private bool IsInRange(Transform target, float range)
    {
        return target != null && Vector3.Distance(transform.position, target.position) <= range;
    }

    private static bool TryGetTargetId(Targetable target, out int targetId)
    {
        targetId = target != null ? target.networkId : -1;
        return targetId >= 0;
    }
}