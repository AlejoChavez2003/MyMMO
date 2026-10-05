using UnityEngine;
using TMPro;

public class Nameplate : MonoBehaviour
{
    public Vector3 offset = new Vector3(0, 2.2f, 0); // Altura sobre la cabeza
    public TMP_Text textNameplate;

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    public void SetInfo(string name, int level)
    {
        if (textNameplate != null)
        {
            textNameplate.text = $"{name} <color=yellow>[Nv.{level}]</color>";
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (mainCamera != null)
        {
            // El texto siempre gira hacia donde está mirando la cámara
            transform.rotation = mainCamera.transform.rotation;
        }
    }
}