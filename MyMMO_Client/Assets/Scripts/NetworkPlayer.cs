using UnityEngine;

public class NetworkPlayer : MonoBehaviour
{
    public GameObject basicArrowPrefab;
    public GameObject skill1Prefab;
    public GameObject skill2Prefab;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Animator animator;
    private Transform firePoint;

    [Header("Configuración de Suavizado")]
    public float lerpSpeed = 15f;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        firePoint = FindFirePoint();
        if (animator != null)
        {
            animator.applyRootMotion = false; // Evita que la animación desplace al avatar
        }
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    void Update()
    {
        // Suavizado de posición y rotación
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);
    }

    public void UpdateNetworkState(Vector3 newPos, float rotY, bool isMoving, bool isRunning, bool isFalling, bool jumpTrigger)
    {
        targetPosition = newPos;
        targetRotation = Quaternion.Euler(0, rotY, 0);

        if (animator != null)
        {
            // Sincronizar movimiento y carrera
            animator.SetBool("isMoving", isMoving);
            animator.SetBool("isRunning", isRunning);
            animator.SetBool("isFalling", isFalling);

            // Disparar salto solo si entra la señal positiva
            if (jumpTrigger)
            {
                animator.SetTrigger("Jump");
            }
        }
    }

    public void SetTargetPosition(Vector3 newPos)
    {
        targetPosition = newPos;
    }

    public void SetVisualReferences(GameObject basicArrow, GameObject skill1, GameObject skill2)
    {
        basicArrowPrefab = basicArrow;
        skill1Prefab = skill1;
        skill2Prefab = skill2;

    }

    public void PlayRemoteAttack(string abilityId, int slot, Transform target)
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        GameObject projectilePrefab = abilityId == "BASIC_ATTACK"
            ? basicArrowPrefab
            : slot == 0 ? skill1Prefab : slot == 1 ? skill2Prefab : null;

        if (projectilePrefab == null || target == null) return;

        Vector3 spawnPosition = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up * 1.2f;
        Quaternion spawnRotation = firePoint != null ? firePoint.rotation : transform.rotation;

        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
        VisualProjectile visualProjectile = projectile.GetComponent<VisualProjectile>();
        if (visualProjectile != null)
        {
            visualProjectile.SetTarget(target);
        }
    }

    private Transform FindFirePoint()
    {
        Transform directFirePoint = transform.Find("FirePoint");
        if (directFirePoint != null) return directFirePoint;

        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            if (child != transform && child.name == "FirePoint") return child;
        }

        return null;
    }

    public void SetPlayerName(string name)
    {
        Targetable targetable = GetComponent<Targetable>();
        if (targetable != null)
        {
            targetable.entityName = name;
            if (targetable.nameplateText != null)
            {
                targetable.nameplateText.text = name;
            }
        }
    }
}