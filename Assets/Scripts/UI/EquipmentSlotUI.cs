using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// Represents one equipment slot in the equipment UI (head, chest, feet, hand x2, trinket x3).
    /// Accepts equipment via drag-and-drop from inventory slots. Click to unequip.
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Image background;
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color validDropColor = new Color(0.3f, 0.6f, 0.3f, 0.8f);

        private Equipment.EquipmentData currentEquipment;
        private Equipment.EquipmentSlotType slotType;
        private int slotIndex;
        private EquipmentUI parentUI;

        public Equipment.EquipmentData CurrentEquipment => currentEquipment;
        public Equipment.EquipmentSlotType SlotType => slotType;
        public int SlotIndex => slotIndex;

        public event Action<EquipmentSlotUI> OnSlotChanged;

        public void Setup(Equipment.EquipmentSlotType type, int index, EquipmentUI parent)
        {
            slotType = type;
            slotIndex = index;
            parentUI = parent;
            Refresh();
        }

        public void SetEquipment(Equipment.EquipmentData data)
        {
            currentEquipment = data;
            Refresh();
            OnSlotChanged?.Invoke(this);
        }

        public void Clear()
        {
            currentEquipment = null;
            Refresh();
            OnSlotChanged?.Invoke(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (background != null) background.color = emptyColor;

            InventorySlotUI draggedSlot = eventData.pointerDrag?.GetComponent<InventorySlotUI>();
            if (draggedSlot == null) return;

            Equipment.EquipmentData draggedEquipment = draggedSlot.EquipmentData;
            if (draggedEquipment == null) return;

            parentUI.TryEquip(draggedEquipment, slotType, slotIndex, draggedSlot);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (currentEquipment == null) return;
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Tooltip.Show(currentEquipment, eventData.position);
                return;
            }
            parentUI.TryUnequip(this);
        }

        private void Refresh()
        {
            if (currentEquipment != null)
            {
                if (iconImage != null)
                {
                    iconImage.sprite = currentEquipment.Icon;
                    iconImage.enabled = true;
                }
                if (nameLabel != null) nameLabel.text = currentEquipment.EquipmentName;
            }
            else
            {
                if (iconImage != null) iconImage.enabled = false;
                if (nameLabel != null) nameLabel.text = GetSlotLabel();
            }
        }

        private string GetSlotLabel()
        {
            switch (slotType)
            {
                case Equipment.EquipmentSlotType.Head: return "Head";
                case Equipment.EquipmentSlotType.Chest: return "Chest";
                case Equipment.EquipmentSlotType.Feet: return "Feet";
                case Equipment.EquipmentSlotType.Hand:
                    return slotIndex == 0 ? "Left Hand" : "Right Hand";
                case Equipment.EquipmentSlotType.Trinket:
                    return $"Trinket {slotIndex + 1}";
                default: return "Empty";
            }
        }
    }
}
