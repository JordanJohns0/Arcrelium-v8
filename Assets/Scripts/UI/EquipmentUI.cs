using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        [Tooltip("Generic frame shown on every equipment slot while it holds an item. Slots fall back " +
            "to their own type art when this is unset.")]
        [SerializeField] private Sprite filledSlotSprite;

        [Header("Inventory")]
        [SerializeField] private Transform inventoryPanel;
        [SerializeField] private InventorySlotUI inventorySlotPrefab;

        [Header("Tabs")]
        [Tooltip("Page shown when the UI opens. Must match a tab's page name.")]
        [SerializeField] private string defaultPage = "Inventory Slots";

        private readonly List<InventorySlotUI> spawnedInventorySlots = new List<InventorySlotUI>();
        private const int InventorySlotCount = 20;
        private readonly List<TabButton> tabButtons = new List<TabButton>();
        private EquipmentManager cachedEquipmentManager;
        private EquipmentSlotType? currentFilter;
        private DeckListUI deckList;
        private TMP_Text cachedGoldLabel;

        /// <summary>The mutually-exclusive pages switched by the bookmark tabs.</summary>
        private static readonly string[] PageNames = { "Inventory Slots", "Decklist", "Special" };

        /// <summary>The shared Inventory Slot prefab (also used by the shop's offer tiles).</summary>
        public InventorySlotUI InventorySlotPrefab => inventorySlotPrefab;

        /// <summary>The Special column the end-of-level shop fills (empty when absent).</summary>
        public Transform ShopPanel => transform.Find("Special");

        /// <summary>The "Gold Number Display" TMP under "Gold Display", found lazily.</summary>
        public TMP_Text GoldLabel
        {
            get
            {
                if (cachedGoldLabel == null)
                {
                    Transform gold = transform.Find("Gold Display/Gold Number Display");
                    if (gold != null) cachedGoldLabel = gold.GetComponent<TMP_Text>();
                }
                return cachedGoldLabel;
            }
        }

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

            InitializeTabs();
            SelectPage(defaultPage);
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

            SetFilledSprite(headSlot);
            SetFilledSprite(chestSlot);
            SetFilledSprite(feetSlot);
            SetFilledSprite(leftHandSlot);
            SetFilledSprite(rightHandSlot);
            SetFilledSprite(trinket1Slot);
            SetFilledSprite(trinket2Slot);
            SetFilledSprite(trinket3Slot);

            PopulateFromEquipmentManager();
        }

        private void SetFilledSprite(EquipmentSlotUI slot)
        {
            if (slot != null)
                slot.SetFilledBackgroundSprite(filledSlotSprite);
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
        /// Creates the deck list (once) inside the "Decklist" page and makes it fill that page.
        /// Falls back to a direct child when no such page exists (e.g. unit tests).
        /// </summary>
        private void EnsureDeckList()
        {
            Transform page = transform.Find("Decklist");

            if (deckList == null)
            {
                Transform existing = page != null ? page.Find("Deck List") : transform.Find("Deck List");
                deckList = existing != null ? existing.GetComponent<DeckListUI>() : null;
            }

            if (deckList == null)
            {
                deckList = DeckListUI.Create();
                deckList.transform.SetParent(page != null ? page : transform, false);
            }

            if (page != null)
            {
                if (deckList.transform.parent != page)
                    deckList.transform.SetParent(page, false);

                // The Decklist page ships with a grid layout (copied from the Special page);
                // the deck list builds and lays out its own rows, so the grid must not drive it.
                var grid = page.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                if (grid != null) grid.enabled = false;

                var rect = (RectTransform)deckList.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            // The Card prefab is stored on the inventory slot prefab; hand it to the deck
            // list so its rows can preview cards, and to the played-card notifications so
            // plays render as the real card (like the deck-window hover previews).
            if (inventorySlotPrefab != null)
            {
                CardView cardView = inventorySlotPrefab.CardPreviewPrefab;
                deckList.SetCardPrefab(cardView);
                PlayedCardDisplay.SetCardPrefab(cardView);
            }
            else
            {
                Debug.LogWarning("[DeckPreview] EnsureDeckList: inventorySlotPrefab is NULL, no card prefab injected.");
            }
        }

        /// <summary>
        /// Wires the bookmark tabs under "Bookmarks" (creating a TabButton for any plain
        /// Button) and lets the code drive their selection.
        /// </summary>
        private void InitializeTabs()
        {
            tabButtons.Clear();
            Transform bookmarks = transform.Find("Bookmarks");
            if (bookmarks == null) return;

            foreach (Transform child in bookmarks)
            {
                Button button = child.GetComponent<Button>();
                if (button == null) continue;

                TabButton tab = child.GetComponent<TabButton>();
                if (tab == null) tab = child.gameObject.AddComponent<TabButton>();
                if (string.IsNullOrEmpty(tab.PageName)) tab.SetPageName(PageNameFor(child.name));

                tab.Wire();
                tab.Selected = () => SelectPage(tab.PageName);
                tabButtons.Add(tab);
            }
        }

        /// <summary>Maps a tab GameObject name to its page name ("Inventory Tab" -> "Inventory Slots").</summary>
        private static string PageNameFor(string tabName)
        {
            const string suffix = " Tab";
            string key = tabName.EndsWith(suffix) ? tabName.Substring(0, tabName.Length - suffix.Length) : tabName;
            return key == "Inventory" ? "Inventory Slots" : key;
        }

        /// <summary>
        /// Shows only the given page and marks its tab selected. Unknown names fall back to
        /// the default page.
        /// </summary>
        public void SelectPage(string pageName)
        {
            if (tabButtons.Count == 0 && transform.Find("Bookmarks") != null)
                InitializeTabs();

            string requested = string.IsNullOrEmpty(pageName) ? defaultPage : pageName;

            foreach (string page in PageNames)
            {
                Transform pageTransform = transform.Find(page);
                if (pageTransform != null)
                    pageTransform.gameObject.SetActive(page == requested);
            }

            foreach (TabButton tab in tabButtons)
                tab.SetSelected(string.Equals(tab.PageName, requested, System.StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshGoldLabel()
        {
            if (GoldLabel != null && inventory != null)
                GoldLabel.text = inventory.Gold.ToString();
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
            RefreshGoldLabel();

            foreach (InventorySlotUI slot in spawnedInventorySlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            spawnedInventorySlots.Clear();

            if (inventory == null || inventorySlotPrefab == null || inventoryPanel == null) return;

            foreach (EquipmentData data in inventory.UnlockedEquipment)
            {
                if (data == null || IsEquipped(data)) continue;
                SpawnInventorySlot(data);
            }

            // Keep a fixed-size grid: pad the remainder with blank slots so the panel always
            // shows the full set of cells regardless of how much gear the player owns.
            while (spawnedInventorySlots.Count < InventorySlotCount)
                SpawnInventorySlot(null);
        }

        private void SpawnInventorySlot(EquipmentData data)
        {
            InventorySlotUI slot = Instantiate(inventorySlotPrefab, inventoryPanel);
            slot.Setup(data, canvas);
            spawnedInventorySlots.Add(slot);
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
