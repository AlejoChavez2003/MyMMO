using UnityEngine;

public class MMOCamera : MonoBehaviour
{
    public Transform target; // Arrastra a tu jugador aquí o asígnalo por código
    
    [Header("Distancia y Zoom")]
    public float distance = 6f;
    public float minDistance = 2f;
    public float maxDistance = 15f;
    public float zoomSensitivity = 3f;

    [Header("Rotación")]
    public float rotationSpeed = 120f;
    private float currentYaw = 0f;
    private float currentPitch = 20f; // Ángulo de inclinación vertical

    // NUEVO: Método para asignarle el objetivo explícitamente
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void Start()
    {
        // Ya no usamos FindWithTag para evitar que agarre al clon por error.
        // Si tienes la cámara en la escena, puedes arrastrar tu Player_Local al campo 'Target' desde el inspector.
    }

    void Update()
    {
        if (target == null) return;

        // 1. Zoom con la rueda del ratón
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * zoomSensitivity;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // 2. Rotación con teclas Q y E
        if (Input.GetKey(KeyCode.Q)) currentYaw -= rotationSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) currentYaw += rotationSpeed * Time.deltaTime;

        // 3. Rotación manteniendo Clic Derecho
        if (Input.GetMouseButton(1))
        {
            currentYaw += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime * 2f;
            currentPitch -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime * 2f;
            currentPitch = Mathf.Clamp(currentPitch, 5f, 60f); // Limita el ángulo vertical
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Calcula la posición y rotación alrededor del objetivo
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 targetOffset = Vector3.up * 1.5f; // Apunta a la cabeza/torso, no a los pies
        Vector3 position = target.position + targetOffset - (rotation * Vector3.forward * distance);

        transform.rotation = rotation;
        transform.position = position;
    }
}