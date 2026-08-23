using System.Collections.Generic;
using UnityEngine;
using Equipment;
using Characters;
using Cards;

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

        [Header("Deck List")]
        [Tooltip("Width of the deck list column shown to the left of the equipment slots.")]
        [SerializeField] private float deckListWidth = 360f;
        [Tooltip("Width of the equipment slots column. Used to size the deck list column.")]
        [SerializeField] private float equipmentSlotsWidth = 600f;
        [Tooltip("Gap between the deck list column and the equipment columns.")]
        [SerializeField] private float deckListGap = 12f;
        [Tooltip("Right margin kept for the right-edge columns when narrowing them.")]
        [SerializeField] private float rightPanelRightMargin = 62f;
        [Tooltip("Minimum width the right-edge columns can shrink to.")]
        [SerializeField] private float rightPanelMinWidth = 300f;

        private readonly List<InventorySlotUI> spawnedInventorySlots = new List<InventorySlotUI>();
        private EquipmentManager cachedEquipmentManager;
        private EquipmentSlotType? currentFilter;
        private DeckListUI deckList;

        /// <summary>
        /// When set, this UI edits a fixed loadout (the pre-match / victory selection)
        /// instead of a live EquipmentManager. Slots populate from and write into the
        /// loadout, and inventory is the persistent player inventory.
        /// </summary>
        private EquipmentLoadout editLoadout;

        private void Awake()
        {
            // Start inactive so the inventory doesn't flash before LoadoutScreen configures it.
            // LoadoutScreen.Show() and the "Open Inventory" button both call SetActive(true).
            gameObject.SetActive(false);
        }

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
                if (c.OwnerPlayerId == Game.GameState.LocalPlayerId && c.Equipment != null)
                {
                    cachedEquipmentManager = c.Equipment;
                    return;
                }
            }
        }

        private void InitializeSlots()
        {
            EnsureDeckList();

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
            if (editLoadout != null)
            {
                SetSlotData(headSlot, editLoadout.Head);
                SetSlotData(chestSlot, editLoadout.Chest);
                SetSlotData(feetSlot, editLoadout.Feet);
                SetSlotData(leftHandSlot, editLoadout.LeftHand);
                SetSlotData(rightHandSlot, editLoadout.RightHand);
                SetSlotData(trinket1Slot, editLoadout.Trinket1);
                SetSlotData(trinket2Slot, editLoadout.Trinket2);
                SetSlotData(trinket3Slot, editLoadout.Trinket3);
                return;
            }

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

        private static void SetSlotData(EquipmentSlotUI slotUI, EquipmentData data)
        {
            if (slotUI != null) slotUI.SetEquipment(data);
        }

        /// <summary>
        /// Replaces the inventory this UI operates on (used by the loadout screen to point
        /// at the runtime player inventory instead of the Inspector starter asset).
        /// </summary>
        public void SetInventory(PlayerInventory newInventory)
        {
            if (inventory == newInventory) return;
            if (inventory != null)
                inventory.OnInventoryChanged -= RefreshInventory;
            inventory = newInventory;
            if (inventory != null)
                inventory.OnInventoryChanged += RefreshInventory;
        }

        /// <summary>
        /// Switches this UI into loadout-editing mode. While set, equipping/unequipping
        /// writes into the given loadout instead of a live EquipmentManager.
        /// </summary>
        public void SetEditLoadout(EquipmentLoadout loadout)
        {
            editLoadout = loadout;
            cachedEquipmentManager = null;
        }

        /// <summary>
        /// Re-initializes slots and inventory from the current edit target. Call after
        /// SetInventory / SetEditLoadout have been set up.
        /// </summary>
        public void Refresh()
        {
            InitializeSlots();
            RefreshInventory();
        }

        /// <summary>
        /// Creates the deck list column (once) and lays it out to the left of the
        /// equipment slots, narrowing the right-side panels so nothing overlaps.
        /// </summary>
        private void EnsureDeckList()
        {
            if (deckList == null)
            {
                Transform existing = transform.Find("Deck List");
                deckList = existing != null ? existing.GetComponent<DeckListUI>() : null;
            }

            if (deckList == null)
            {
                deckList = DeckListUI.Create();
                deckList.transform.SetParent(transform, false);
            }

            // The Card prefab is stored on the inventory slot prefab; hand it to the deck
            // list so its rows can preview cards on hover/right-click.
            if (inventorySlotPrefab != null)
            {
                deckList.SetCardPrefab(inventorySlotPrefab.CardPreviewPrefab);
            }
            else
            {
                Debug.LogWarning("[DeckPreview] EnsureDeckList: inventorySlotPrefab is NULL, no card prefab injected.");
            }

            LayoutForDeckList();
        }

        private void LayoutForDeckList()
        {
            var deckRect = (RectTransform)deckList.transform;
            deckRect.anchorMin = new Vector2(0, 0);
            deckRect.anchorMax = new Vector2(0, 1);
            deckRect.pivot = new Vector2(0, 0.5f);
            deckRect.anchoredPosition = Vector2.zero;
            deckRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, deckListWidth);

            Transform equipmentSlots = transform.Find("Equipment Slots");
            if (equipmentSlots != null)
            {
                ((RectTransform)equipmentSlots).anchoredPosition =
                    new Vector2(deckListWidth, 0);
            }

            // Push the right-edge columns over so the new left column fits without overlap.
            float rectWidth = ((RectTransform)transform).rect.width;
            float leftTotal = deckListWidth + equipmentSlotsWidth + deckListGap;
            float rightWidth = Mathf.Max(rightPanelMinWidth, rectWidth - rightPanelRightMargin - leftTotal);

            NarrowRightPanel("Inventory Slots", rightWidth);
            NarrowRightPanel("Filter Buttons", rightWidth);
        }

        private void NarrowRightPanel(string childName, float width)
        {
            Transform child = transform.Find(childName);
            if (child == null) return;
            var rt = (RectTransform)child;
            if (rt.rect.width <= width + 1f) return;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private void RefreshDeckList()
        {
            if (deckList == null) EnsureDeckList();
            if (deckList == null) return;

            List<CardData> cards = null;
            if (editLoadout != null)
                cards = editLoadout.GetAllCards();
            else if (cachedEquipmentManager != null)
                cards = cachedEquipmentManager.GetAllCards();

            deckList.Refresh(cards);
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

            EquipmentSlotUI targetSlot = GetSlot(slotType, slotIndex);
            if (targetSlot == null) return;

            if (editLoadout != null)
            {
                if (targetSlot.CurrentEquipment != null)
                {
                    editLoadout.Set(slotType, slotIndex, null);
                    if (inventory != null) inventory.Unlock(targetSlot.CurrentEquipment);
                }
                editLoadout.Set(slotType, slotIndex, data);
                targetSlot.SetEquipment(data);
                RefreshInventory();
                return;
            }

            if (cachedEquipmentManager == null) return;

            // Unequip existing item if slot is occupied
            if (targetSlot.CurrentEquipment != null)
            {
                cachedEquipmentManager.Unequip(slotType, slotIndex);
                inventory.Unlock(targetSlot.CurrentEquipment);
            }

            // Equip into the EquipmentManager. The item stays owned (unlocked list) while
            // equipped — removing it would let reward rolls hand it out as a "new" unlock.
            if (cachedEquipmentManager.Equip(data, slotType, slotIndex))
            {
                targetSlot.SetEquipment(data);
            }
            RefreshInventory();
        }

        public void TryUnequip(EquipmentSlotUI slot)
        {
            if (slot == null || slot.CurrentEquipment == null) return;

            if (editLoadout != null)
            {
                editLoadout.Set(slot.SlotType, slot.SlotIndex, null);
                if (inventory != null) inventory.Unlock(slot.CurrentEquipment);
                slot.Clear();
                RefreshInventory();
                return;
            }

            if (cachedEquipmentManager == null) return;

            cachedEquipmentManager.Unequip(slot.SlotType, slot.SlotIndex);
            inventory.Unlock(slot.CurrentEquipment);
            slot.Clear();
            RefreshInventory();
        }

        private void RefreshInventory()
        {
            RefreshDeckList();

            foreach (InventorySlotUI slot in spawnedInventorySlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            spawnedInventorySlots.Clear();

            if (inventory == null || inventorySlotPrefab == null || inventoryPanel == null) return;

            foreach (EquipmentData data in inventory.UnlockedEquipment)
            {
                if (data == null || IsEquipped(data)) continue;

                InventorySlotUI slot = Instantiate(inventorySlotPrefab, inventoryPanel);
                slot.Setup(data, canvas);
                spawnedInventorySlots.Add(slot);

                bool visible = currentFilter == null || data.SlotType == currentFilter.Value;
                slot.SetVisible(visible);
            }
        }

        private bool IsEquipped(EquipmentData data)
        {
            if (data == null) return false;

            if (editLoadout != null)
            {
                return editLoadout.Get(EquipmentSlotType.Head, 0) == data
                    || editLoadout.Get(EquipmentSlotType.Chest, 0) == data
                    || editLoadout.Get(EquipmentSlotType.Feet, 0) == data
                    || editLoadout.Get(EquipmentSlotType.Hand, 0) == data
                    || editLoadout.Get(EquipmentSlotType.Hand, 1) == data
                    || editLoadout.Get(EquipmentSlotType.Trinket, 0) == data
                    || editLoadout.Get(EquipmentSlotType.Trinket, 1) == data
                    || editLoadout.Get(EquipmentSlotType.Trinket, 2) == data;
            }

            if (cachedEquipmentManager != null)
            {
                if (HasEquipped(cachedEquipmentManager.GetEquippedInSlot(EquipmentSlotType.Head), data)) return true;
                if (HasEquipped(cachedEquipmentManager.GetEquippedInSlot(EquipmentSlotType.Chest), data)) return true;
                if (HasEquipped(cachedEquipmentManager.GetEquippedInSlot(EquipmentSlotType.Feet), data)) return true;
                if (HasEquipped(cachedEquipmentManager.GetEquippedInSlot(EquipmentSlotType.Hand), data)) return true;
                if (HasEquipped(cachedEquipmentManager.GetEquippedInSlot(EquipmentSlotType.Trinket), data)) return true;
            }

            return false;
        }

        private static bool HasEquipped(IReadOnlyList<EquipmentInstance> slots, EquipmentData data)
        {
            foreach (var slot in slots)
            {
                if (slot != null && slot.Data == data) return true;
            }
            return false;
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
