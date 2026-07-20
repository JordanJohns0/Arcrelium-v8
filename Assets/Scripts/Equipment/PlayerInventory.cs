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
    [CreateAssetMenu(fileName = "PlayerInventory", menuName = "/Player Inventory")]
    public class PlayerInventory : ScriptableObject
    {
        [Tooltip("All equipment the player currently owns. Unequipped items sit here.")]
        [SerializeField] private List<EquipmentData> unlockedEquipment = new List<EquipmentData>();

        public IReadOnlyList<EquipmentData> UnlockedEquipment => unlockedEquipment;

        public event Action OnInventoryChanged;

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
