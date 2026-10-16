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

        public IReadOnlyList<EquipmentData> UnlockedEquipment => unlockedEquipment;
        public EquipmentLoadout Loadout => loadout;

        public event Action OnInventoryChanged;

        /// <summary>
        /// Enchantments applied to the given equipment this run. Backed by the loadout's
        /// enchantment map so it travels with the loadout to the host and into each match.
        /// </summary>
        public IReadOnlyList<EquipmentEnchantmentSO> GetEnchantments(EquipmentData data)
        {
            return loadout.GetEnchantments(data);
        }

        /// <summary>
        /// Applies an enchantment to a piece of equipment (equipped or in the bag). Returns false
        /// (changing nothing) when the enchantment's targets rule the piece out.
        /// </summary>
        public bool AddEnchantment(EquipmentData data, EquipmentEnchantmentSO enchantment)
        {
            if (data == null || enchantment == null) return false;
            if (!enchantment.CanApplyTo(data, loadout.ContainsItem(data))) return false;

            loadout.AddEnchantment(data, enchantment);
            OnInventoryChanged?.Invoke();
            return true;
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

        public void Clear()
        {
            unlockedEquipment.Clear();
            OnInventoryChanged?.Invoke();
        }
    }
}
