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
    /// and filtered to items the player does not already own. Every purchase spends gold,
    /// unlocks the item, and persists locally — the host never sees another player's gold.
    ///
    /// Attach one to the Special pane's transform via <see cref="Attach"/> and call
    /// <see cref="Show"/> to populate it. Tiles are non-interactable (no drag/right-click
    /// tooltip) so a not-yet-owned item can't be dropped onto an equipment slot.
    /// </summary>
    public class ShopScreen : MonoBehaviour
    {
        [Header("Offers")]
        [SerializeField] [Min(0)] private int offerCount = 4;

        [Header("Tile Widgets")]
        [SerializeField] private Color priceColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color needGoldColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        [SerializeField] private int priceFontSize = 16;

        /// <summary>Test seam: resolves the local player's inventory.</summary>
        internal static System.Func<PlayerInventory> LocalInventory = PlayerInventoryProvider.GetLocal;

        /// <summary>Test seam: persists the local player's inventory after a purchase.</summary>
        internal static System.Action<PlayerInventory> PersistInventory = inventory =>
            PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inventory);

        private Transform offersPanel;
        private GameObject tilePrefab;
        private TMP_Text goldLabel;
        private PlayerInventory currentInventory;
        private readonly List<GameObject> spawnedTiles = new List<GameObject>();

        /// <summary>
        /// Binds a shop to an existing offers panel (e.g. the Equipment UI "Special" pane).
        /// Returns null when the panel is missing. <paramref name="tilePrefab"/> is the shared
        /// Inventory Slot prefab; a price label and buy overlay are added around each tile.
        /// </summary>
        public static ShopScreen Attach(Transform offersPanel, GameObject tilePrefab, TMP_Text goldLabel)
        {
            if (offersPanel == null) return null;

            ShopScreen screen = offersPanel.GetComponent<ShopScreen>();
            if (screen == null) screen = offersPanel.gameObject.AddComponent<ShopScreen>();
            screen.Setup(offersPanel, tilePrefab, goldLabel);
            return screen;
        }

        private void Setup(Transform offersPanel, GameObject tilePrefab, TMP_Text goldLabel)
        {
            this.offersPanel = offersPanel;
            this.tilePrefab = tilePrefab;
            this.goldLabel = goldLabel;
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
            RefreshGold();
            Populate();
        }

        private void RefreshGold()
        {
            if (goldLabel != null)
                goldLabel.text = currentInventory != null ? currentInventory.Gold.ToString() : "";
        }

        private void Populate()
        {
            ClearTiles();

            if (currentInventory == null || offersPanel == null) return;

            var offers = ShopPool.Offers(currentInventory, offerCount, new System.Random(GameState.CurrentLevel));
            foreach (var offer in offers)
                CreateShopTile(offer);
        }

        private void CreateShopTile(ShopOffer offer)
        {
            if (tilePrefab == null) return;

            GameObject tile = Object.Instantiate(tilePrefab, offersPanel);
            tile.name = $"Shop {offer.Item.EquipmentName}";
            spawnedTiles.Add(tile);

            var slot = tile.GetComponent<InventorySlotUI>();
            if (slot != null)
            {
                slot.Interactable = false;
                slot.Setup(offer.Item, null);
            }

            TMP_Text price = CreatePriceLabel(tile.transform, offer.Price);
            AddBuyOverlay(tile.transform, offer, price);
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

        private void AddBuyOverlay(Transform tile, ShopOffer offer, TMP_Text price)
        {
            var overlay = new GameObject("Buy", typeof(Image), typeof(Button));
            overlay.transform.SetParent(tile, false);

            var image = overlay.GetComponent<Image>();
            image.color = new Color(1, 1, 1, 0.08f);

            var button = overlay.GetComponent<Button>();
            button.targetGraphic = image;

            var rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            EquipmentData item = offer.Item;
            int priceValue = offer.Price;
            button.onClick.AddListener(() => Buy(item, priceValue, price));
        }

        private void Buy(EquipmentData item, int price, TMP_Text priceLabel)
        {
            var inventory = currentInventory;
            if (inventory == null) return;
            if (Owns(inventory, item)) { Refresh(); return; }

            if (!inventory.TrySpendGold(price))
            {
                priceLabel.text = $"Need {price}";
                priceLabel.color = needGoldColor;
                return;
            }

            inventory.Unlock(item);
            PersistInventory(inventory);
        }

        private static bool Owns(PlayerInventory inventory, EquipmentData item)
        {
            if (inventory.IsUnlocked(item)) return true;
            return inventory.Loadout != null && inventory.Loadout.ContainsItem(item);
        }

        private void ClearTiles()
        {
            foreach (GameObject tile in spawnedTiles)
            {
                if (tile != null) DestroyObject(tile);
            }
            spawnedTiles.Clear();
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}