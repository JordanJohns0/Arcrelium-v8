using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game;
using Equipment;

namespace UI
{
    /// <summary>
    /// End-of-level reward screen embedded in the Equipment UI panel. After a victory the
    /// loadout screen shows directly: the "Special" column on the right of the inventory
    /// panel gets one tile per offer (three by default) — the same Inventory Slot prefab the
    /// inventory uses — captioned with the item's name.
    ///
    /// The player picks ONE offer for free. The chosen item is unlocked into their inventory
    /// and persisted; the other offers are then locked. There is no gold involved.
    ///
    /// Offers come from <see cref="ShopPool"/> (falling back to <see cref="RewardPool"/>) and
    /// skip items the player already owns. They are rolled once per run+level, so the set
    /// stays stable while the screen is open or revisited.
    ///
    /// Attach one to the Special pane's transform via <see cref="Attach"/> and call
    /// <see cref="Show"/> to populate it. Tiles are non-draggable but still show their item
    /// tooltip on right-click, just like the inventory tiles they are cloned from.
    /// </summary>
    public class ShopScreen : MonoBehaviour
    {
        [Header("Offers")]
        [Tooltip("How many items to offer the player to choose from.")]
        [SerializeField] [Min(1)] private int offerCount = 3;

        [Header("Info")]
        [Tooltip("Heading shown above the offers (the Special page's title label).")]
        [SerializeField] private string shopTitle = "Choose a Reward";
        [Tooltip("Flavour text shown under the heading (the Special page's description label).")]
        [SerializeField] private string shopDescription = "Pick one piece of equipment to add to your collection.";

        [Header("Tile Widgets")]
        [SerializeField] private Color chosenColor = new Color(1f, 0.85f, 0.3f, 1f);
        [Tooltip("Opacity of the offers that were not taken once one has been chosen.")]
        [SerializeField] [Range(0f, 1f)] private float dimmedAlpha = 0.35f;
        [SerializeField] private int statusFontSize = 16;

        /// <summary>Test seam: resolves the local player's inventory.</summary>
        internal static System.Func<PlayerInventory> LocalInventory = PlayerInventoryProvider.GetLocal;

        /// <summary>Test seam: persists the local player's inventory after a pick.</summary>
        internal static System.Action<PlayerInventory> PersistInventory = inventory =>
            PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inventory);

        private Transform offersPanel;
        private GameObject tilePrefab;
        private TMP_Text titleLabel;
        private TMP_Text descriptionLabel;
        private PlayerInventory currentInventory;

        private readonly List<ShopOffer> offers = new List<ShopOffer>();
        private readonly List<TileEntry> tiles = new List<TileEntry>();

        // The run+level the cached offers were rolled for, so we only re-roll when that changes.
        private bool offersBuilt;
        private int offersSeed;
        private PlayerInventory offersInventory;

        // Set once the player takes an offer for this run+level; the rest are locked.
        private bool hasPicked;
        private EquipmentData pickedItem;

        /// <summary>A spawned offer tile plus the widgets we need to keep its state in sync.</summary>
        private class TileEntry
        {
            public GameObject Root;
            public ShopOffer Offer;
            public TMP_Text Status;
            public Button Pick;
        }

        /// <summary>
        /// Binds a shop to an existing offers panel (e.g. the Equipment UI "Special" pane).
        /// Returns null when the panel is missing. <paramref name="tilePrefab"/> is the shared
        /// Inventory Slot prefab; a status label and pick overlay are added around each tile.
        /// </summary>
        public static ShopScreen Attach(Transform offersPanel, GameObject tilePrefab,
            TMP_Text titleLabel = null, TMP_Text descriptionLabel = null)
        {
            if (offersPanel == null) return null;

            ShopScreen screen = offersPanel.GetComponent<ShopScreen>();
            if (screen == null) screen = offersPanel.gameObject.AddComponent<ShopScreen>();
            screen.Setup(offersPanel, tilePrefab, titleLabel, descriptionLabel);
            return screen;
        }

        private void Setup(Transform offersPanel, GameObject tilePrefab, TMP_Text titleLabel, TMP_Text descriptionLabel)
        {
            this.offersPanel = offersPanel;
            this.tilePrefab = tilePrefab;
            this.titleLabel = titleLabel;
            this.descriptionLabel = descriptionLabel;
        }

        private void OnDestroy()
        {
            if (currentInventory != null)
                currentInventory.OnInventoryChanged -= Refresh;
        }

        /// <summary>Fills the offers panel and keeps it in sync until the pane is hidden.</summary>
        public void Show()
        {
            var inventory = LocalInventory();
            if (inventory != null && inventory != currentInventory)
            {
                if (currentInventory != null) currentInventory.OnInventoryChanged -= Refresh;
                currentInventory = inventory;
                currentInventory.OnInventoryChanged += Refresh;
            }
            Refresh();
        }

        /// <summary>Unsubscribes and empties the offers panel.</summary>
        public void Hide()
        {
            if (currentInventory != null)
            {
                currentInventory.OnInventoryChanged -= Refresh;
                currentInventory = null;
            }
            ClearTiles();
        }

        private void Refresh()
        {
            RefreshInfo();

            // Only rebuild the tiles when the offers themselves change (new run/level) or the
            // panel is empty. Ordinary inventory changes just restate the existing tiles.
            if (EnsureOffers() || tiles.Count == 0)
                Populate();
            else
                UpdateTileStates();
        }

        private void RefreshInfo()
        {
            if (titleLabel != null) titleLabel.text = shopTitle;
            if (descriptionLabel != null) descriptionLabel.text = shopDescription;
        }

        /// <summary>
        /// Rolls the offers once per run+level (seeded so they're stable within that window and
        /// only change for a new run/level). Returns true when the offers were (re)built.
        /// </summary>
        private bool EnsureOffers()
        {
            int seed = unchecked(GameState.ShopSeed + GameState.CurrentLevel);
            if (offersBuilt && offersSeed == seed && offersInventory == currentInventory)
                return false;

            offers.Clear();
            if (currentInventory != null)
                offers.AddRange(ShopPool.Offers(currentInventory, offerCount, new System.Random(seed)));

            offersSeed = seed;
            offersInventory = currentInventory;
            offersBuilt = true;
            hasPicked = false;
            pickedItem = null;
            return true;
        }

        private void Populate()
        {
            ClearTiles();

            if (currentInventory == null || offersPanel == null || tilePrefab == null) return;

            foreach (ShopOffer offer in offers)
                CreateOfferTile(offer);

            UpdateTileStates();
        }

        private void CreateOfferTile(ShopOffer offer)
        {
            GameObject root = Object.Instantiate(tilePrefab, offersPanel);
            root.name = $"Offer {offer.Item.EquipmentName}";

            var slot = root.GetComponent<InventorySlotUI>();
            if (slot != null)
            {
                slot.Interactable = false;
                slot.Setup(offer.Item, null);
            }

            var entry = new TileEntry
            {
                Root = root,
                Offer = offer,
                Status = CreateStatusLabel(root.transform),
            };
            entry.Pick = AddPickOverlay(root.transform, entry);
            tiles.Add(entry);
        }

        private TMP_Text CreateStatusLabel(Transform tile)
        {
            var statusObj = new GameObject("Status", typeof(TextMeshProUGUI));
            statusObj.transform.SetParent(tile, false);
            var tmp = statusObj.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = statusFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = chosenColor;
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -2);
            rt.sizeDelta = new Vector2(0, 22);
            return tmp;
        }

        private Button AddPickOverlay(Transform tile, TileEntry entry)
        {
            // The overlay is a translucent tint over the tile; the Button lives on the tile
            // itself (next to InventorySlotUI) so a right-click still reaches the slot's
            // tooltip instead of being swallowed by the overlay.
            var overlay = new GameObject("Pick", typeof(Image));
            overlay.transform.SetParent(tile, false);

            var image = overlay.GetComponent<Image>();
            image.color = new Color(1, 1, 1, 0.08f);

            var rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            button.onClick.AddListener(() => Pick(entry));
            return button;
        }

        /// <summary>Takes the chosen offer for free and locks the rest of this run+level's offers.</summary>
        private void Pick(TileEntry tile)
        {
            if (currentInventory == null || tile == null || hasPicked) return;
            if (Owns(currentInventory, tile.Offer.Item)) { UpdateTileStates(); return; }

            hasPicked = true;
            pickedItem = tile.Offer.Item;

            // Unlock raises OnInventoryChanged → Refresh → UpdateTileStates, so the remaining
            // tiles lock without the shop being rebuilt.
            currentInventory.Unlock(tile.Offer.Item);
            PersistInventory(currentInventory);
        }

        /// <summary>Restates every tile from the current state — no rebuild, no reshuffle.</summary>
        private void UpdateTileStates()
        {
            foreach (TileEntry tile in tiles)
            {
                if (tile == null || tile.Root == null) continue;

                bool isPicked = hasPicked && tile.Offer.Item == pickedItem;

                if (tile.Pick != null)
                    tile.Pick.interactable = !hasPicked;

                if (tile.Status != null)
                    tile.Status.text = isPicked ? "Chosen" : "";

                // Once one offer is taken, the others fade out.
                SetTileAlpha(tile.Root, !hasPicked || isPicked ? 1f : dimmedAlpha);
            }
        }

        private static void SetTileAlpha(GameObject root, float alpha)
        {
            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
            group.alpha = alpha;
        }

        private static bool Owns(PlayerInventory inventory, EquipmentData item)
        {
            if (inventory.IsUnlocked(item)) return true;
            return inventory.Loadout != null && inventory.Loadout.ContainsItem(item);
        }

        private void ClearTiles()
        {
            foreach (TileEntry tile in tiles)
            {
                if (tile != null && tile.Root != null) DestroyObject(tile.Root);
            }
            tiles.Clear();
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
