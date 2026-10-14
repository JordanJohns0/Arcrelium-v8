using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game;
using Equipment;

namespace UI
{
    /// <summary>
    /// End-of-level shop embedded in the Equipment UI panel. After a victory the loadout
    /// screen shows directly (no separate overlay phase): the "Special" column on the right
    /// of the inventory panel gets one tile per offer — the same Inventory Slot prefab the
    /// inventory uses — capped with a price label and a full-tile buy button, and the gold
    /// display beside the pane updates as the player spends.
    ///
    /// Offers are drawn from <see cref="ShopPool"/> (falling back to <see cref="RewardPool"/>)
    /// and filtered to items the player does not already own. Offers are locked in the first
    /// time the shop opens for a run+level, so buying a tile never reshuffles the rest: the
    /// purchased tile stays put and switches to an "Owned" state. Every purchase spends gold,
    /// unlocks the item, and persists locally — the host never sees another player's gold.
    ///
    /// Attach one to the Special pane's transform via <see cref="Attach"/> and call
    /// <see cref="Show"/> to populate it. Tiles are non-draggable (they can't be dropped onto
    /// an equipment slot) but still show their item tooltip on right-click, just like the
    /// inventory tiles they are cloned from.
    /// </summary>
    public class ShopScreen : MonoBehaviour
    {
        [Header("Offers")]
        [SerializeField] [Min(0)] private int offerCount = 4;

        [Header("Info")]
        [Tooltip("Heading shown above the offers (the Special page's title label).")]
        [SerializeField] private string shopTitle = "Shop";
        [Tooltip("Flavour text shown under the heading (the Special page's description label).")]
        [SerializeField] private string shopDescription = "Spend gold on new equipment for your run.";

        [Header("Tile Widgets")]
        [SerializeField] private Color priceColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color needGoldColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color ownedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private int priceFontSize = 16;

        /// <summary>Test seam: resolves the local player's inventory.</summary>
        internal static System.Func<PlayerInventory> LocalInventory = PlayerInventoryProvider.GetLocal;

        /// <summary>Test seam: persists the local player's inventory after a purchase.</summary>
        internal static System.Action<PlayerInventory> PersistInventory = inventory =>
            PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inventory);

        private Transform offersPanel;
        private GameObject tilePrefab;
        private TMP_Text goldLabel;
        private TMP_Text titleLabel;
        private TMP_Text descriptionLabel;
        private PlayerInventory currentInventory;

        private readonly List<ShopOffer> offers = new List<ShopOffer>();
        private readonly List<TileEntry> tiles = new List<TileEntry>();

        // The run+level the cached offers were rolled for, so we only re-roll when that changes.
        private bool offersBuilt;
        private int offersSeed;
        private PlayerInventory offersInventory;

        /// <summary>A spawned shop tile plus the widgets we need to keep its state in sync.</summary>
        private class TileEntry
        {
            public GameObject Root;
            public ShopOffer Offer;
            public TMP_Text Price;
            public Button Buy;
            public bool NeedsGold;
        }

        /// <summary>
        /// Binds a shop to an existing offers panel (e.g. the Equipment UI "Special" pane).
        /// Returns null when the panel is missing. <paramref name="tilePrefab"/> is the shared
        /// Inventory Slot prefab; a price label and buy overlay are added around each tile.
        /// </summary>
        public static ShopScreen Attach(Transform offersPanel, GameObject tilePrefab, TMP_Text goldLabel,
            TMP_Text titleLabel = null, TMP_Text descriptionLabel = null)
        {
            if (offersPanel == null) return null;

            ShopScreen screen = offersPanel.GetComponent<ShopScreen>();
            if (screen == null) screen = offersPanel.gameObject.AddComponent<ShopScreen>();
            screen.Setup(offersPanel, tilePrefab, goldLabel, titleLabel, descriptionLabel);
            return screen;
        }

        private void Setup(Transform offersPanel, GameObject tilePrefab, TMP_Text goldLabel,
            TMP_Text titleLabel, TMP_Text descriptionLabel)
        {
            this.offersPanel = offersPanel;
            this.tilePrefab = tilePrefab;
            this.goldLabel = goldLabel;
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
            RefreshGold();

            // Only rebuild the tiles when the offers themselves change (new run/level) or the
            // panel is empty. Ordinary inventory changes — a purchase, a spend, equipping gear —
            // just restate the existing tiles, so buying never reshuffles the shop.
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

        private void RefreshGold()
        {
            if (goldLabel != null)
                goldLabel.text = currentInventory != null ? currentInventory.Gold.ToString() : "";
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
            return true;
        }

        private void Populate()
        {
            ClearTiles();

            if (currentInventory == null || offersPanel == null || tilePrefab == null) return;

            foreach (ShopOffer offer in offers)
                CreateShopTile(offer);

            UpdateTileStates();
        }

        private void CreateShopTile(ShopOffer offer)
        {
            GameObject root = Object.Instantiate(tilePrefab, offersPanel);
            root.name = $"Shop {offer.Item.EquipmentName}";

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
                Price = CreatePriceLabel(root.transform, offer.Price),
            };
            entry.Buy = AddBuyOverlay(root.transform, entry);
            tiles.Add(entry);
        }

        private TMP_Text CreatePriceLabel(Transform tile, int price)
        {
            var priceObj = new GameObject("Price", typeof(TextMeshProUGUI));
            priceObj.transform.SetParent(tile, false);
            var tmp = priceObj.GetComponent<TextMeshProUGUI>();
            tmp.text = price.ToString();
            tmp.fontSize = priceFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = priceColor;
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -2);
            rt.sizeDelta = new Vector2(0, 22);
            return tmp;
        }

        private Button AddBuyOverlay(Transform tile, TileEntry entry)
        {
            // The overlay is a translucent tint over the tile; the buy Button lives on the
            // tile itself (next to InventorySlotUI) so a right-click still reaches the slot's
            // tooltip instead of being swallowed by the overlay.
            var overlay = new GameObject("Buy", typeof(Image));
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

            button.onClick.AddListener(() => Buy(entry));
            return button;
        }

        private void Buy(TileEntry tile)
        {
            if (currentInventory == null || tile == null) return;
            if (Owns(currentInventory, tile.Offer.Item)) { UpdateTileStates(); return; }

            if (!currentInventory.TrySpendGold(tile.Offer.Price))
            {
                tile.NeedsGold = true;
                UpdateTileStates();
                return;
            }

            currentInventory.Unlock(tile.Offer.Item);
            PersistInventory(currentInventory);
            // Unlock raises OnInventoryChanged → Refresh → UpdateTileStates, so the tile flips
            // to "Owned" and its button is disabled without the shop being rebuilt.
        }

        /// <summary>Restates every tile from the current inventory — no rebuild, no reshuffle.</summary>
        private void UpdateTileStates()
        {
            foreach (TileEntry tile in tiles)
            {
                if (tile == null || tile.Root == null) continue;

                bool owned = Owns(currentInventory, tile.Offer.Item);
                if (tile.Buy != null)
                    tile.Buy.interactable = !owned;

                if (tile.Price == null) continue;

                if (owned)
                {
                    tile.Price.text = "Owned";
                    tile.Price.color = ownedColor;
                }
                else if (tile.NeedsGold)
                {
                    tile.Price.text = $"Need {tile.Offer.Price}";
                    tile.Price.color = needGoldColor;
                }
                else
                {
                    tile.Price.text = tile.Offer.Price.ToString();
                    tile.Price.color = priceColor;
                }
            }
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
