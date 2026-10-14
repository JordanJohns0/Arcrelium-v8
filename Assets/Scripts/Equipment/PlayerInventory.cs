using System;
using System.Collections.Generic;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Persistent store of all equipment the player has unlocked. ScriptableObject so it
    /// survives scene loads. Create one asset per save slot (right-click > Create >  > Player Inventory).
    /// Populate it in the Inspector or via code (e.g. after earning rewards).
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerInventory", menuName = "Arcrelium/Player Inventory")]
    public class PlayerInventory : ScriptableObject
    {
        [Tooltip("All equipment the player currently owns. Unequipped items sit here.")]
        [SerializeField] private List<EquipmentData> unlockedEquipment = new List<EquipmentData>();

        [Tooltip("The player's currently equipped items, applied to their character at match start.")]
        [SerializeField] private EquipmentLoadout loadout = new EquipmentLoadout();

        [Tooltip("Currency the player earns each level and spends in the end-of-level shop.")]
        [SerializeField] private int gold;

        public IReadOnlyList<EquipmentData> UnlockedEquipment => unlockedEquipment;
        public EquipmentLoadout Loadout => loadout;
        public int Gold => gold;

        public event Action OnInventoryChanged;

        /// <summary>
        /// Enchantments applied to the given equipment this run. Backed by the loadout's
        /// enchantment map so it travels with the loadout to the host and into each match.
        /// </summary>
        public IReadOnlyList<EquipmentEnchantmentSO> GetEnchantments(EquipmentData data)
        {
            return loadout.GetEnchantments(data);
        }

        /// <summary>Applies an enchantment to a piece of equipment (equipped or in the bag).</summary>
        public void AddEnchantment(EquipmentData data, EquipmentEnchantmentSO enchantment)
        {
            if (data == null || enchantment == null) return;
            loadout.AddEnchantment(data, enchantment);
            OnInventoryChanged?.Invoke();
        }

        public void Unlock(EquipmentData data)
        {
            if (data == null || unlockedEquipment.Contains(data)) return;
            unlockedEquipment.Add(data);
            OnInventoryChanged?.Invoke();
        }

        public void Remove(EquipmentData data)
        {
            if (unlockedEquipment.Remove(data))
                OnInventoryChanged?.Invoke();
        }

        public bool IsUnlocked(EquipmentData data)
        {
            return data != null && unlockedEquipment.Contains(data);
        }

        public void SetUnlocked(List<EquipmentData> items)
        {
            unlockedEquipment = items ?? new List<EquipmentData>();
            OnInventoryChanged?.Invoke();
        }

        /// <summary>Adds currency. Non-positive amounts are ignored.</summary>
        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            gold += amount;
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Spends currency when the player can afford it. Returns false (and changes nothing)
        /// when the amount is negative or exceeds the current balance.
        /// </summary>
        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || gold < amount) return false;
            gold -= amount;
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>Overwrites the balance (used when loading a save or template). Clamps to zero.</summary>
        public void SetGold(int amount)
        {
            gold = amount < 0 ? 0 : amount;
            OnInventoryChanged?.Invoke();
        }

        public void Clear()
        {
            unlockedEquipment.Clear();
            gold = 0;
            OnInventoryChanged?.Invoke();
        }
    }
}
