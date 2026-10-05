using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    public float riseSpeed = 0.5f;

    private void Awake()
    {
        Destroy(gameObject, 1.5f);
    }

    private void Update()
    {
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;
    }

    public void SetDamage(int damage)
    {
        TMP_Text text = GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = damage.ToString();
        }
    }
}
