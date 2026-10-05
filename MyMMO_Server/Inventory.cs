using System;
using System.Collections.Generic;
using System.Linq;

namespace MyMMOServer
{
    public sealed class InventorySlot
    {
        public string ItemId { get; set; } = "";
        public int Quantity { get; set; }
    }

    public sealed class Inventory
    {
        public const int SlotCount = 24;
        private readonly InventorySlot[] slots = Enumerable.Range(0, SlotCount)
            .Select(_ => new InventorySlot())
            .ToArray();

        public IReadOnlyList<InventorySlot> Slots => slots;

        public Inventory()
        {
            AddItem("health_potion", 5);
            AddItem("iron_sword", 1);
            AddItem("leather_armor", 1);
        }

        public bool AddItem(string itemId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0) return false;

            InventorySlot? existingSlot = slots.FirstOrDefault(slot => slot.ItemId == itemId);
            if (existingSlot != null)
            {
                existingSlot.Quantity += quantity;
                return true;
            }

            InventorySlot? emptySlot = slots.FirstOrDefault(slot => slot.Quantity <= 0);
            if (emptySlot == null) return false;

            emptySlot.ItemId = itemId;
            emptySlot.Quantity = quantity;
            return true;
        }

        public bool MoveOrSwap(int sourceIndex, int destinationIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= SlotCount ||
                destinationIndex < 0 || destinationIndex >= SlotCount ||
                sourceIndex == destinationIndex || slots[sourceIndex].Quantity <= 0)
            {
                return false;
            }

            (slots[sourceIndex], slots[destinationIndex]) =
                (slots[destinationIndex], slots[sourceIndex]);
            return true;
        }

        public bool TryTake(int index, out string itemId, out int quantity)
        {
            itemId = "";
            quantity = 0;
            if (index < 0 || index >= SlotCount || slots[index].Quantity <= 0) return false;

            itemId = slots[index].ItemId;
            quantity = slots[index].Quantity;
            slots[index].ItemId = "";
            slots[index].Quantity = 0;
            return true;
        }

        public string GetItemId(int index)
        {
            if (index < 0 || index >= SlotCount || slots[index].Quantity <= 0) return "";
            return slots[index].ItemId;
        }

        public bool TryConsume(string itemId, out int restoredAmount)
        {
            restoredAmount = 0;
            InventorySlot? slot = slots.FirstOrDefault(candidate => candidate.ItemId == itemId && candidate.Quantity > 0);
            if (slot == null) return false;

            slot.Quantity--;
            if (slot.Quantity == 0) slot.ItemId = "";
            restoredAmount = itemId == "health_potion" ? 50 : 0;
            return restoredAmount > 0;
        }

        public string Serialize()
        {
            return string.Join(";", slots.Select((slot, index) =>
                $"{index},{slot.ItemId},{slot.Quantity}"));
        }
    }

    public sealed class Equipment
    {
        public string WeaponItemId { get; private set; } = "";
        public string ArmorItemId { get; private set; } = "";

        public bool TryEquip(Inventory inventory, int inventoryIndex, string slot)
        {
            if (inventory == null || !IsValidSlot(slot) || GetItemId(slot).Length > 0) return false;
            string itemId = inventory.GetItemId(inventoryIndex);
            if (!CanEquip(itemId, slot) ||
                !inventory.TryTake(inventoryIndex, out itemId, out int quantity) || quantity != 1) return false;

            SetItemId(slot, itemId);
            return true;
        }

        public bool TryUnequip(Inventory inventory, string slot)
        {
            if (inventory == null || !IsValidSlot(slot)) return false;
            string itemId = GetItemId(slot);
            if (string.IsNullOrEmpty(itemId) || !inventory.AddItem(itemId, 1)) return false;

            SetItemId(slot, "");
            return true;
        }

        public string Serialize()
        {
            return $"weapon,{WeaponItemId};armor,{ArmorItemId}";
        }

        private static bool IsValidSlot(string slot) => slot == "weapon" || slot == "armor";

        private static bool CanEquip(string itemId, string slot)
        {
            return (slot == "weapon" && itemId == "iron_sword") ||
                   (slot == "armor" && itemId == "leather_armor");
        }

        private string GetItemId(string slot) => slot == "weapon" ? WeaponItemId : ArmorItemId;

        private void SetItemId(string slot, string itemId)
        {
            if (slot == "weapon") WeaponItemId = itemId;
            else ArmorItemId = itemId;
        }
    }
}