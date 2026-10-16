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
        [Tooltip("Background art shown while an item is equipped here (the generic empty slot frame). " +
            "The slot's own type art — whatever sprite the background starts with — is shown while the slot is empty.")]
        [SerializeField] private Sprite filledBackgroundSprite;
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color validDropColor = new Color(0.3f, 0.6f, 0.3f, 0.8f);

        private Equipment.EquipmentData currentEquipment;
        private Equipment.EquipmentSlotType slotType;
        private int slotIndex;
        private EquipmentUI parentUI;
        private Sprite typeBackgroundSprite;

        public Equipment.EquipmentData CurrentEquipment => currentEquipment;
        public Equipment.EquipmentSlotType SlotType => slotType;
        public int SlotIndex => slotIndex;

        public event Action<EquipmentSlotUI> OnSlotChanged;

        /// <summary>Test seam: resolves the local player's inventory (for enchantments on the tooltip).</summary>
        internal static Func<Equipment.PlayerInventory> LocalInventory = Equipment.PlayerInventoryProvider.GetLocal;

        public void Setup(Equipment.EquipmentSlotType type, int index, EquipmentUI parent)
        {
            slotType = type;
            slotIndex = index;
            parentUI = parent;
            Refresh();
        }

        /// <summary>Sets the generic frame shown while an item is equipped (shared by all slots).</summary>
        public void SetFilledBackgroundSprite(Sprite sprite)
        {
            filledBackgroundSprite = sprite;
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
                Equipment.PlayerInventory inventory = LocalInventory != null ? LocalInventory() : null;
                var enchantments = inventory != null ? inventory.GetEnchantments(currentEquipment) : null;
                Tooltip.Show(currentEquipment, eventData.position, enchantments);
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
                    iconImage.enabled = currentEquipment.Icon != null;
                }
                if (nameLabel != null) nameLabel.text = currentEquipment.EquipmentName;
            }
            else
            {
                if (iconImage != null) iconImage.enabled = false;
                if (nameLabel != null) nameLabel.text = GetSlotLabel();
            }

            UpdateIconMirror();
            ApplyBackground();
        }

        /// <summary>
        /// Swaps the slot's background art: the generic frame while an item is equipped, or the
        /// slot's own type art (its initial sprite) while empty. Blocks with no frame configured
        /// fall back to the type art, preserving the original look.
        /// </summary>
        private void ApplyBackground()
        {
            if (background == null) return;

            if (typeBackgroundSprite == null)
                typeBackgroundSprite = background.sprite;

            background.sprite = currentEquipment != null && filledBackgroundSprite != null
                ? filledBackgroundSprite
                : typeBackgroundSprite;
            background.color = Color.white;
        }

        /// <summary>
        /// Mirrors the icon horizontally when the equipment occupies the left hand so the
        /// weapon/off-hand artwork faces the correct direction. Right hand and all other
        /// slots render un-mirrored.
        /// </summary>
        private void UpdateIconMirror()
        {
            if (iconImage == null) return;
            if (iconImage.rectTransform == null) return;

            bool mirrored = slotType == Equipment.EquipmentSlotType.Hand && slotIndex == 0 && currentEquipment != null;
            Vector3 scale = iconImage.rectTransform.localScale;
            iconImage.rectTransform.localScale = mirrored
                ? new Vector3(-Mathf.Abs(scale.x), scale.y, scale.z)
                : new Vector3(Mathf.Abs(scale.x), scale.y, scale.z);
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
