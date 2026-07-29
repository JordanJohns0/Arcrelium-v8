using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// Represents one piece of equipment in the inventory panel. Draggable — can be dropped
    /// onto an EquipmentSlotUI to equip it.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private CanvasGroup canvasGroup;

        private Equipment.EquipmentData equipmentData;
        private Transform originalParent;
        private Canvas parentCanvas;
        private readonly List<Graphic> childGraphics = new List<Graphic>();

        public Equipment.EquipmentData EquipmentData => equipmentData;
        public Equipment.EquipmentSlotType SlotType => equipmentData != null ? equipmentData.SlotType : default;

        public void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
                canvasGroup.interactable = visible;
            }
            gameObject.SetActive(visible);
        }

        public void Setup(Equipment.EquipmentData data, Canvas canvas)
        {
            equipmentData = data;
            parentCanvas = canvas;

            if (data != null)
            {
                if (iconImage != null)
                {
                    iconImage.sprite = data.Icon;
                    iconImage.enabled = true;
                }
                if (nameLabel != null) nameLabel.text = data.EquipmentName;
            }
            else
            {
                if (iconImage != null) iconImage.enabled = false;
                if (nameLabel != null) nameLabel.text = "";
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (equipmentData == null) return;

            Tooltip.Hide();
            originalParent = transform.parent;
            transform.SetParent(parentCanvas.transform);
            transform.SetAsLastSibling();

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0.6f;
            }
            else
            {
                childGraphics.Clear();
                GetComponentsInChildren(childGraphics);
                foreach (Graphic g in childGraphics)
                    g.raycastTarget = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (equipmentData == null) return;
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (equipmentData == null) return;

            transform.SetParent(originalParent);
            transform.localPosition = Vector3.zero;

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.alpha = 1f;
            }
            else
            {
                foreach (Graphic g in childGraphics)
                    g.raycastTarget = true;
                childGraphics.Clear();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;
            if (equipmentData == null) return;
            Tooltip.Show(equipmentData, eventData.position);
        }
    }
}
