using UnityEngine;

public class VisualProjectile : MonoBehaviour
{
    public Transform target;
    public float speed = 15f;

    public void Initialize(Transform target, float speed = 15f)
    {
        this.speed = speed;
        SetTarget(target);
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = target.position + Vector3.up * 1.5f;

        if (Vector3.Distance(transform.position, targetPosition) < 0.2f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime);
    }
}
