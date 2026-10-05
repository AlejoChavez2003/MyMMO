using UnityEngine;

public class CharacterAppearance : MonoBehaviour
{
    [Header("Ajustes de Prueba (Cubo)")]
    public Renderer playerRenderer;

    // Método que aplica los datos recibidos de la red o del menú
    public void AplicarApariencia(string clase, float altura, float grosor)
    {
        // 1. Aplicar escala física (Altura y Grosor)
        transform.localScale = new Vector3(grosor, altura, grosor);

        // 2. Aplicar color por Clase (Para el cubo actual)
        if (playerRenderer == null) playerRenderer = GetComponent<Renderer>();

        if (playerRenderer != null)
        {
            switch (clase)
            {
                case "Guerrero": playerRenderer.material.color = Color.red; break;
                case "Mago": playerRenderer.material.color = Color.blue; break;
                case "Cazador": playerRenderer.material.color = Color.green; break;
                default: playerRenderer.material.color = Color.gray; break;
            }
        }
    }
}