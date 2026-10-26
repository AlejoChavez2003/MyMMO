using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameData
{
    public static string PlayerUsername = "Heroe";
    public static string PlayerClass = "Guerrero";
    
    // Stats del Jugador
    public static int Level = 1;
    public static int CurrentHP = 100;
    public static int MaxHP = 100;
    public static int CurrentEXP = 0;
    public static int MaxEXP = 1000;
    public static int AttributePoints = 0;
    public static int Strength = 1;
    public static int Precision = 1;
    public static int Agility = 1;
    public static int Vitality = 1;
    public static int BronzeCoins = 0;
    public static bool IsLoggedIn = false;
    public static event Action Changed;

    public static bool IsClass(string requiredClass)
    {
        if (string.IsNullOrWhiteSpace(requiredClass)) return true;

        bool playerIsArcher = IsAnyClass(PlayerClass, "Cazador", "Arquero", "Archer", "Explorador", "Ranger");
        bool requiredIsArcher = IsAnyClass(requiredClass, "Cazador", "Arquero", "Archer", "Explorador", "Ranger");
        return playerIsArcher && requiredIsArcher ||
               string.Equals(PlayerClass?.Trim(), requiredClass.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAnyClass(string characterClass, params string[] classNames)
    {
        foreach (string className in classNames)
        {
            if (string.Equals(characterClass?.Trim(), className, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    public static void NotifyChanged()
    {
        Changed?.Invoke();
    }
}

public enum ItemType
{
    Consumable,
    Equipment,
    Material
}

public sealed class ItemDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ItemType Type { get; }
    public int MaxStack { get; }
    public Color SlotColor { get; }
    public string EquipmentSlot { get; }
    public int PhysicalDamageBonus { get; }
    public int AttackSpeedBonus { get; }

    public ItemDefinition(
        string id,
        string displayName,
        string description,
        ItemType type,
        int maxStack,
        Color slotColor,
        string equipmentSlot = "",
        int physicalDamageBonus = 0,
        int attackSpeedBonus = 0)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Type = type;
        MaxStack = maxStack;
        SlotColor = slotColor;
        EquipmentSlot = equipmentSlot;
        PhysicalDamageBonus = physicalDamageBonus;
        AttackSpeedBonus = attackSpeedBonus;
    }
}

public static class ItemCatalog
{
    private static readonly Dictionary<string, ItemDefinition> definitions =
        new Dictionary<string, ItemDefinition>
        {
            {
                "health_potion",
                new ItemDefinition(
                    "health_potion",
                    "Pocion de salud",
                    "Recupera vida al consumirla.",
                    ItemType.Consumable,
                    20,
                    new Color(0.85f, 0.2f, 0.2f, 1f))
            },
            {
                "iron_sword",
                new ItemDefinition(
                    "iron_sword",
                    "Espada de hierro",
                    "Arma basica para el guerrero.\nBonus: +5 dano fisico.",
                    ItemType.Equipment,
                    1,
                    new Color(0.55f, 0.65f, 0.8f, 1f),
                    "weapon",
                    5)
            },
            {
                "novice_bow",
                new ItemDefinition(
                    "novice_bow",
                    "Arco de Novato",
                    "Arma a distancia para comenzar la aventura.\nBonus: +10 dano fisico, +5 velocidad de ataque.",
                    ItemType.Equipment,
                    1,
                    new Color(0.65f, 0.48f, 0.28f, 1f),
                    "weapon",
                    10,
                    5)
            },
            {
                "leather_armor",
                new ItemDefinition(
                    "leather_armor",
                    "Armadura de cuero",
                    "Proteccion ligera para comenzar la aventura.\nBonus: -3 dano recibido.",
                    ItemType.Equipment,
                    1,
                    new Color(0.55f, 0.35f, 0.18f, 1f),
                    "armor")
            },
            {
                "armor_scraps",
                new ItemDefinition(
                    "armor_scraps",
                    "Restos de armadura",
                    "Fragmentos de armadura militar encontrados entre los restos de un paladin.",
                    ItemType.Material,
                    20,
                    new Color(0.48f, 0.5f, 0.54f, 1f))
            },
            {
                "broken_sword",
                new ItemDefinition(
                    "broken_sword",
                    "Espada rota",
                    "Una espada militar quebrada que aun puede aprovecharse como material.",
                    ItemType.Material,
                    20,
                    new Color(0.58f, 0.61f, 0.68f, 1f))
            },
            {
                "wolf_fang",
                new ItemDefinition(
                    "wolf_fang",
                    "Colmillo de Lobo",
                    "Un colmillo afilado obtenido de un lobo.",
                    ItemType.Material,
                    20,
                    new Color(0.82f, 0.8f, 0.7f, 1f))
            },
            {
                "wolf_pelt",
                new ItemDefinition(
                    "wolf_pelt",
                    "Piel de Lobo",
                    "Piel resistente que puede servir para fabricar equipo.",
                    ItemType.Material,
                    20,
                    new Color(0.48f, 0.37f, 0.28f, 1f))
            },
            {
                "raw_meat",
                new ItemDefinition(
                    "raw_meat",
                    "Carne cruda",
                    "Carne obtenida de una criatura salvaje.",
                    ItemType.Material,
                    20,
                    new Color(0.72f, 0.32f, 0.3f, 1f))
            },
            {
                "beast_fang",
                new ItemDefinition(
                    "beast_fang",
                    "Colmillo de bestia",
                    "Material obtenido de criaturas rapidas. Puede servir para fabricar equipo.",
                    ItemType.Material,
                    20,
                    new Color(0.75f, 0.72f, 0.58f, 1f))
            },
            {
                "shadow_essence",
                new ItemDefinition(
                    "shadow_essence",
                    "Esencia de sombra",
                    "Material raro dejado por enemigos elite.",
                    ItemType.Material,
                    20,
                    new Color(0.42f, 0.2f, 0.58f, 1f))
            }
        };

    public static ItemDefinition Get(string itemId)
    {
        if (!string.IsNullOrEmpty(itemId) && definitions.TryGetValue(itemId, out ItemDefinition definition))
        {
            return definition;
        }

        return new ItemDefinition(
            itemId,
            itemId,
            "Objeto sin descripcion.",
            ItemType.Material,
            1,
            Color.white);
    }
}

public sealed class EnemyDropPreview
{
    public string ItemId { get; }
    public int MinAmount { get; }
    public int MaxAmount { get; }
    public int ChancePercent { get; }

    public EnemyDropPreview(string itemId, int minAmount, int maxAmount, int chancePercent)
    {
        ItemId = itemId;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        ChancePercent = chancePercent;
    }
}

public static class EnemyDropCatalog
{
    public static EnemyDropPreview[] GetDrops(int enemyType)
    {
        switch (enemyType)
        {
            case 2:
                return new[]
                {
                    new EnemyDropPreview("armor_scraps", 1, 1, 45),
                    new EnemyDropPreview("broken_sword", 1, 1, 30)
                };
            case 3:
                return new[]
                {
                    new EnemyDropPreview("wolf_fang", 1, 1, 50),
                    new EnemyDropPreview("wolf_pelt", 1, 1, 35),
                    new EnemyDropPreview("raw_meat", 1, 1, 60)
                };
            default:
                return new[]
                {
                    new EnemyDropPreview("armor_scraps", 1, 1, 35),
                    new EnemyDropPreview("broken_sword", 1, 1, 20)
                };
        }
    }
}

[Serializable]
public class InventorySlotData
{
    public string itemId = "";
    public int quantity;
}

public static class InventoryData
{
    public const int SlotCount = 24;
    public static readonly InventorySlotData[] Slots = CreateSlots();
    public static event Action Changed;

    private static InventorySlotData[] CreateSlots()
    {
        InventorySlotData[] slots = new InventorySlotData[SlotCount];
        for (int index = 0; index < slots.Length; index++) slots[index] = new InventorySlotData();
        return slots;
    }

    public static void ApplySerialized(string serialized)
    {
        Clear();
        string[] serializedSlots = serialized.Split(';');
        foreach (string serializedSlot in serializedSlots)
        {
            string[] values = serializedSlot.Split(',');
            if (values.Length < 3 || !int.TryParse(values[0], out int index) ||
                !int.TryParse(values[2], out int quantity) || index < 0 || index >= SlotCount) continue;

            Slots[index].itemId = values[1];
            Slots[index].quantity = Math.Max(0, quantity);
        }

        Changed?.Invoke();
    }

    private static void Clear()
    {
        foreach (InventorySlotData slot in Slots)
        {
            slot.itemId = "";
            slot.quantity = 0;
        }
    }
}

public sealed class EquipmentSlotData
{
    public string itemId = "";
}

public static class EquipmentData
{
    public static readonly Dictionary<string, EquipmentSlotData> Slots =
        new Dictionary<string, EquipmentSlotData>
        {
            { "weapon", new EquipmentSlotData() },
            { "armor", new EquipmentSlotData() },
            { "ring_left", new EquipmentSlotData() },
            { "ring_right", new EquipmentSlotData() },
            { "necklace", new EquipmentSlotData() },
            { "special", new EquipmentSlotData() },
            { "head", new EquipmentSlotData() },
            { "chest", new EquipmentSlotData() },
            { "gloves", new EquipmentSlotData() },
            { "legs", new EquipmentSlotData() },
            { "boots", new EquipmentSlotData() }
        };

    public static event Action Changed;

    public static void ApplySerialized(string serialized)
    {
        foreach (EquipmentSlotData slot in Slots.Values) slot.itemId = "";

        string[] serializedSlots = serialized.Split(';');
        foreach (string serializedSlot in serializedSlots)
        {
            string[] values = serializedSlot.Split(',');
            if (values.Length < 2 || !Slots.TryGetValue(values[0], out EquipmentSlotData slot)) continue;
            slot.itemId = values[1];
        }

        Changed?.Invoke();
    }
}