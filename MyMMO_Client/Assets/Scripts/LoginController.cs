using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginController : MonoBehaviour
{
    [Header("Referencias de UI")]
    public TMP_InputField inputUsername; 
    public TMP_Dropdown dropdownClass;
    public Button btnConnect;

    [Header("Referencias de Red")]
    public NetworkManager netManager; 

    [Header("Paneles / Controladores")]
    public GameObject loginPanel;   
    public GameObject playerLocal; // Arrastra aquí a Player_Local
    public PlayerHUD playerHUD;    // Arrastra aquí el Canvas (que contiene el script PlayerHUD)
    public ActionBarController actionBarController;

    private void Start()
    {
        // 1. Desactivar el personaje local al iniciar (para que no se vea/mueva durante el login)
        if (playerLocal != null)
        {
            playerLocal.SetActive(false);
        }

        // 2. Escuchar el clic del botón de conectar
        if (btnConnect != null)
        {
            btnConnect.onClick.RemoveAllListeners();
            btnConnect.onClick.AddListener(OnConnectClicked);
        }
    }

    public async void OnConnectClicked()
    {
        string username = inputUsername != null ? inputUsername.text.Trim() : "";
        if (string.IsNullOrEmpty(username))
        {
            username = "Heroe_" + Random.Range(100, 999);
        }

        string selectedClass = "Guerrero";
        if (dropdownClass != null && dropdownClass.options.Count > 0)
        {
            selectedClass = dropdownClass.options[dropdownClass.value].text;
        }

        GameData.PlayerUsername = username;
        GameData.PlayerClass = selectedClass;

        // 1. Conectar al servidor
        if (netManager != null)
        {
            await netManager.ConnectToServerAsync();
        }

        GameData.IsLoggedIn = true;

        // 2. Apagar Login y Encender Personaje
        if (loginPanel != null) loginPanel.SetActive(false);
        if (playerLocal != null) playerLocal.SetActive(true);

        // 3. Encender el HUD
        if (playerHUD != null)
        {
            playerHUD.ShowHUD();
        }

        if (actionBarController != null)
        {
            actionBarController.ShowActionBar();
        }

        // 4. Activar Cámara
        if (Camera.main != null)
        {
            MMOCamera mmoCam = Camera.main.GetComponent<MMOCamera>();
            if (mmoCam != null) mmoCam.enabled = true;
        }
    }
}