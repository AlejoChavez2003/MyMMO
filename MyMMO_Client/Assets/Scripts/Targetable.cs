using UnityEngine;
using TMPro;

public class Targetable : MonoBehaviour
{
    [Header("Configuración")]
    public bool isLocalPlayer = false;
    public TMP_Text nameplateText; // Arrastra NameplateText aquí

    [Header("Información de la Entidad")]
    public int networkId = -1;
    public string entityName = "Objetivo";
    public int level = 1;
    public int currentHP = 100;
    public int maxHP = 100;

    void OnEnable()
    {
        UpdateLocalData();
    }

    private void UpdateLocalData()
    {
        if (isLocalPlayer)
        {
            entityName = GameData.PlayerUsername;
            level = GameData.Level;
            currentHP = GameData.CurrentHP;
            maxHP = GameData.MaxHP;

            if (nameplateText != null)
            {
                nameplateText.text = GameData.PlayerUsername;
            }
        }
    }

    void OnMouseDown()
    {
        UpdateLocalData();

        PlayerHUD hud = FindAnyObjectByType<PlayerHUD>();
        if (hud != null)
        {
            hud.SetTarget(this);
        }
    }
}