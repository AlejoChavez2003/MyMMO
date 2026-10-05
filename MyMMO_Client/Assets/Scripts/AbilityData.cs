using UnityEngine;

public enum AbilityEffectType
{
    None,
    Stun,
    Burn
}

[CreateAssetMenu(fileName = "Ability_", menuName = "MyMMO/Ability Data")]
public class AbilityData : ScriptableObject
{
    public string abilityId;
    public string abilityName;
    public string requiredClass = "Guerrero";
    public int requiredLevel = 1;
    public int baseDamage;
    public float cooldown;
    public float range = 5f;
    public AbilityEffectType effectType;
    public string animationName;
}