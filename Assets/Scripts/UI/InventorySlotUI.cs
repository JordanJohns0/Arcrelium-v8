using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// Represents one piece of equipment in the inventory panel. Draggable — can be dropped
    /// onto an EquipmentSlotUI to equip it. Right-clicking shows the classic text tooltip,
    /// which stays available even on non-interactable tiles (shop offers) so their info can
    /// still be inspected; actual-card hover previews live on the deck list rows instead
    /// (see DeckListUI).
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float dragAlpha = 0.6f;
        [Tooltip("Shared Card prefab for hover previews. Storage point only — EquipmentUI hands this to the deck list; slots themselves never render previews.")]
        [SerializeField] private CardView cardPreviewPrefab;
        [Tooltip("When false the slot ignores dragging (used for shop offer tiles); right-click tooltips still work.")]
        [SerializeField] private bool interactable = true;

        private Equipment.EquipmentData equipmentData;
        private Canvas parentCanvas;
        private GameObject dragIcon;

        public Equipment.EquipmentData EquipmentData => equipmentData;
        public Equipment.EquipmentSlotType SlotType => equipmentData != null ? equipmentData.SlotType : default;

        /// <summary>Shared Card preview prefab (storage point for EquipmentUI → DeckListUI).</summary>
        public CardView CardPreviewPrefab => cardPreviewPrefab;

        /// <summary>When false the slot ignores dragging (shop tiles) but still shows its tooltip.</summary>
        public bool Interactable
        {
            get => interactable;
            set => interactable = value;
        }

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
                    iconImage.enabled = data.Icon != null;
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
            if (!interactable || equipmentData == null) return;

            Tooltip.Hide();
            BeginDragIcon();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!interactable || equipmentData == null || dragIcon == null) return;
            dragIcon.GetComponent<RectTransform>().position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!interactable || equipmentData == null) return;
            EndDragIcon();
        }

        private void OnDisable()
        {
            if (dragIcon != null)
            {
                Destroy(dragIcon);
                dragIcon = null;
            }
        }

        /// <summary>
        /// Drags a floating copy of just the item icon, leaving the slot — and its background —
        /// anchored in the grid. The source icon is hidden while the copy is held, then restored
        /// on drop (or when the slot is rebuilt).
        /// </summary>
        private void BeginDragIcon()
        {
            EndDragIcon();

            Transform host = parentCanvas != null ? parentCanvas.transform : transform.root;
            dragIcon = new GameObject("Drag Icon", typeof(RectTransform), typeof(Image));
            var rect = dragIcon.GetComponent<RectTransform>();
            rect.SetParent(host, false);
            rect.SetAsLastSibling();

            var image = dragIcon.GetComponent<Image>();
            image.raycastTarget = false;

            if (iconImage != null)
            {
                rect.sizeDelta = iconImage.rectTransform.rect.size;
                image.sprite = iconImage.sprite;
                image.preserveAspect = iconImage.preserveAspect;
                Color color = iconImage.color;
                image.color = new Color(color.r, color.g, color.b, color.a * dragAlpha);
                iconImage.enabled = false;
            }
            else
            {
                rect.sizeDelta = new Vector2(100f, 100f);
            }
        }

        private void EndDragIcon()
        {
            if (dragIcon != null)
            {
                Destroy(dragIcon);
                dragIcon = null;
            }

            if (iconImage != null)
                iconImage.enabled = equipmentData != null && equipmentData.Icon != null;
        }

        /// <summary>Test seam: resolves the local player's inventory (for enchantments on the tooltip).</summary>
        internal static System.Func<Equipment.PlayerInventory> LocalInventory = Equipment.PlayerInventoryProvider.GetLocal;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;
            if (equipmentData == null) return;

            Equipment.PlayerInventory inventory = LocalInventory();
            var enchantments = inventory != null ? inventory.GetEnchantments(equipmentData) : null;
            Tooltip.Show(equipmentData, eventData.position, enchantments);
        }
    }
}
