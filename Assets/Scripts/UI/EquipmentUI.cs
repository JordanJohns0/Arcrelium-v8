using System.Collections.Generic;
using UnityEngine;
using Equipment;
using Characters;

namespace UI
{
    /// <summary>
    /// Main controller for the equipment/inventory UI. Shows equipment slots on one side
    /// and inventory on the other. Handles equipping/unequipping via drag-and-drop or click.
    /// Automatically finds the player's (OwnerPlayerId 0) EquipmentManager on open.
    ///
    /// Setup:
    /// 1. Create a UI panel with two sections: Equipment (left) and Inventory (right)
    /// 2. Add EquipmentSlotUI children for each slot (head, chest, feet, left hand, right hand, trinket x3)
    /// 3. Add an empty InventoryPanel child with a GridLayoutGroup — InventorySlotUIs are spawned there
    /// 4. Assign the PlayerInventory ScriptableObject
    /// </summary>
    public class EquipmentUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Canvas canvas;
        [SerializeField] private int localPlayerId = 0;

        [Header("Equipment Slots")]
        [SerializeField] private EquipmentSlotUI headSlot;
        [SerializeField] private EquipmentSlotUI chestSlot;
        [SerializeField] private EquipmentSlotUI feetSlot;
        [SerializeField] private EquipmentSlotUI leftHandSlot;
        [SerializeField] private EquipmentSlotUI rightHandSlot;
        [SerializeField] private EquipmentSlotUI trinket1Slot;
        [SerializeField] private EquipmentSlotUI trinket2Slot;
        [SerializeField] private EquipmentSlotUI trinket3Slot;

        [Header("Inventory")]
        [SerializeField] private Transform inventoryPanel;
        [SerializeField] private InventorySlotUI inventorySlotPrefab;

        private readonly List<InventorySlotUI> spawnedInventorySlots = new List<InventorySlotUI>();
        private EquipmentManager cachedEquipmentManager;
        private EquipmentSlotType? currentFilter;

        private void OnEnable()
        {
            FindPlayerEquipmentManager();
            InitializeSlots();
            RefreshInventory();

            if (inventory != null)
                inventory.OnInventoryChanged += RefreshInventory;
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.OnInventoryChanged -= RefreshInventory;
        }

        private void FindPlayerEquipmentManager()
        {
            if (cachedEquipmentManager != null) return;

            foreach (Character c in FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                if (c.OwnerPlayerId == localPlayerId && c.Equipment != null)
                {
                    cachedEquipmentManager = c.Equipment;
                    return;
                }
            }
        }

        private void InitializeSlots()
        {
            headSlot.Setup(EquipmentSlotType.Head, 0, this);
            chestSlot.Setup(EquipmentSlotType.Chest, 0, this);
            feetSlot.Setup(EquipmentSlotType.Feet, 0, this);
            leftHandSlot.Setup(EquipmentSlotType.Hand, 0, this);
            rightHandSlot.Setup(EquipmentSlotType.Hand, 1, this);
            trinket1Slot.Setup(EquipmentSlotType.Trinket, 0, this);
            trinket2Slot.Setup(EquipmentSlotType.Trinket, 1, this);
            trinket3Slot.Setup(EquipmentSlotType.Trinket, 2, this);

            PopulateFromEquipmentManager();
        }

        private void PopulateFromEquipmentManager()
        {
            if (cachedEquipmentManager == null) return;

            SetSlotFromManager(headSlot, EquipmentSlotType.Head, 0);
            SetSlotFromManager(chestSlot, EquipmentSlotType.Chest, 0);
            SetSlotFromManager(feetSlot, EquipmentSlotType.Feet, 0);
            SetSlotFromManager(leftHandSlot, EquipmentSlotType.Hand, 0);
            SetSlotFromManager(rightHandSlot, EquipmentSlotType.Hand, 1);
            SetSlotFromManager(trinket1Slot, EquipmentSlotType.Trinket, 0);
            SetSlotFromManager(trinket2Slot, EquipmentSlotType.Trinket, 1);
            SetSlotFromManager(trinket3Slot, EquipmentSlotType.Trinket, 2);
        }

        private void SetSlotFromManager(EquipmentSlotUI slotUI, EquipmentSlotType type, int index)
        {
            EquipmentInstance instance = cachedEquipmentManager.GetEquipped(type, index);
            slotUI.SetEquipment(instance?.Data);
        }

        public void TryEquip(EquipmentData data, EquipmentSlotType slotType, int slotIndex, InventorySlotUI draggedFrom)
        {
            if (data == null) return;
            if (data.SlotType != slotType) return;
            if (cachedEquipmentManager == null) return;

            EquipmentSlotUI targetSlot = GetSlot(slotType, slotIndex);
            if (targetSlot == null) return;

            // Unequip existing item if slot is occupied
            if (targetSlot.CurrentEquipment != null)
            {
                cachedEquipmentManager.Unequip(slotType, slotIndex);
                inventory.Unlock(targetSlot.CurrentEquipment);
            }

            // Equip into the EquipmentManager
            if (cachedEquipmentManager.Equip(data, slotType, slotIndex))
            {
                targetSlot.SetEquipment(data);
                inventory.Remove(data);
            }
        }

        public void TryUnequip(EquipmentSlotUI slot)
        {
            if (slot == null || slot.CurrentEquipment == null) return;
            if (cachedEquipmentManager == null) return;

            cachedEquipmentManager.Unequip(slot.SlotType, slot.SlotIndex);
            inventory.Unlock(slot.CurrentEquipment);
            slot.Clear();
        }

        private void RefreshInventory()
        {
            foreach (InventorySlotUI slot in spawnedInventorySlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            spawnedInventorySlots.Clear();

            if (inventory == null || inventorySlotPrefab == null || inventoryPanel == null) return;

            foreach (EquipmentData data in inventory.UnlockedEquipment)
            {
                InventorySlotUI slot = Instantiate(inventorySlotPrefab, inventoryPanel);
                slot.Setup(data, canvas);
                spawnedInventorySlots.Add(slot);

                bool visible = currentFilter == null || data.SlotType == currentFilter.Value;
                slot.SetVisible(visible);
            }
        }

        public void SetFilter(EquipmentSlotType? slotType)
        {
            currentFilter = slotType;

            foreach (InventorySlotUI slot in spawnedInventorySlots)
            {
                if (slot == null || slot.EquipmentData == null) continue;
                bool visible = currentFilter == null || slot.EquipmentData.SlotType == currentFilter.Value;
                slot.SetVisible(visible);
            }
        }

        /// <summary>
        /// Button-friendly filter. Call from Inspector OnClick.
        /// 0=All, 1=Head, 2=Chest, 3=Feet, 4=Hand, 5=Trinket
        /// </summary>
        public void SetFilterByIndex(int index)
        {
            switch (index)
            {
                case 0: SetFilter(null); break;
                case 1: SetFilter(EquipmentSlotType.Head); break;
                case 2: SetFilter(EquipmentSlotType.Chest); break;
                case 3: SetFilter(EquipmentSlotType.Feet); break;
                case 4: SetFilter(EquipmentSlotType.Hand); break;
                case 5: SetFilter(EquipmentSlotType.Trinket); break;
                default: SetFilter(null); break;
            }
        }

        private EquipmentSlotUI GetSlot(EquipmentSlotType type, int index)
        {
            switch (type)
            {
                case EquipmentSlotType.Head: return headSlot;
                case EquipmentSlotType.Chest: return chestSlot;
                case EquipmentSlotType.Feet: return feetSlot;
                case EquipmentSlotType.Hand:
                    return index == 0 ? leftHandSlot : rightHandSlot;
                case EquipmentSlotType.Trinket:
                    if (index == 0) return trinket1Slot;
                    if (index == 1) return trinket2Slot;
                    return trinket3Slot;
                default: return null;
            }
        }
    }
}
