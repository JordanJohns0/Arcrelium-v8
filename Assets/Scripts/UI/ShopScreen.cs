using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game;
using Equipment;

namespace UI
{
    /// <summary>
    /// End-of-level shop overlay. Shown after a victory in place of the old random reward:
    /// the player spends the gold they just earned on equipment, then continues to the
    /// loadout screen to equip it. Built lazily (no scene wiring) like the other overlays.
    ///
    /// Offers are drawn from <see cref="ShopPool"/> (falling back to <see cref="RewardPool"/>)
    /// and filtered to items the player does not already own. Everything is local to this
    /// machine — the host never sees another player's gold or offers.
    /// </summary>
    public class ShopScreen : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private int sortingOrder = 200;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
        [SerializeField] private Color overlayColor = new Color(0, 0, 0, 0.72f);

        [Header("Panel")]
        [SerializeField] private Vector2 panelSize = new Vector2(720, 520);
        [SerializeField] private Color panelColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private float panelSpacing = 8f;
        [SerializeField] private int panelPadding = 22;

        [Header("Title")]
        [SerializeField] private string titleText = "Shop";
        [SerializeField] private int titleFontSize = 30;
        [SerializeField] private Color titleColor = new Color(1f, 0.85f, 0.3f, 1f);

        [Header("Offers")]
        [SerializeField] [Min(0)] private int offerCount = 4;
        [SerializeField] private Vector2 rowSize = new Vector2(660, 56);
        [SerializeField] private Color rowColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        [SerializeField] private int rowFontSize = 22;
        [SerializeField] private Vector2 buyButtonSize = new Vector2(150, 44);
        [SerializeField] private Color buyButtonColor = new Color(0.2f, 0.5f, 0.2f, 1f);
        [SerializeField] private Color disabledButtonColor = new Color(0.28f, 0.28f, 0.28f, 1f);
        [SerializeField] private string buyLabel = "Buy";
        [SerializeField] private string continueLabel = "Continue";

        private sealed class OfferRow
        {
            public EquipmentData item;
            public int price;
            public TMP_Text nameLabel;
            public TMP_Text priceLabel;
            public TMP_Text buyLabel;
            public Button buyButton;
        }

        private readonly List<OfferRow> rows = new List<OfferRow>();

        private Canvas canvas;
        private TMP_Text goldLabel;
        private Transform offersContent;
        private System.Action onClose;

        /// <summary>Test seam: resolves the local player's inventory.</summary>
        internal static System.Func<PlayerInventory> LocalInventory = PlayerInventoryProvider.GetLocal;

        /// <summary>Test seam: persists the local player's inventory after a purchase.</summary>
        internal static System.Action<PlayerInventory> PersistInventory = inventory =>
            PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inventory);

        private static GameObject staticPrefab;

        /// <summary>
        /// Opens the shop. <paramref name="onCloseCallback"/> fires once when the player
        /// continues, letting the game flow move on to the loadout screen.
        /// </summary>
        public static ShopScreen Show(System.Action onCloseCallback)
        {
            var existing = FindAnyObjectByType<ShopScreen>();
            ShopScreen screen;
            if (existing != null)
            {
                screen = existing;
            }
            else if (staticPrefab != null)
            {
                screen = Object.Instantiate(staticPrefab).GetComponent<ShopScreen>();
            }
            else
            {
                screen = new GameObject("ShopScreen").AddComponent<ShopScreen>();
            }
            screen.Open(onCloseCallback);
            return screen;
        }

        private void Open(System.Action onCloseCallback)
        {
            onClose = onCloseCallback;
            if (canvas == null) CreateUI();
            Populate();
            canvas.enabled = true;
        }

        private void Populate()
        {
            var inventory = LocalInventory();
            RefreshGold(inventory);

            foreach (Transform child in offersContent)
                DestroyObject(child.gameObject);
            rows.Clear();

            // Seeded by level so a given level shows a stable set of offers.
            var offers = ShopPool.Offers(inventory, offerCount, new System.Random(GameState.CurrentLevel));
            foreach (var offer in offers)
                CreateOfferRow(offer);

            if (rows.Count == 0)
            {
                var empty = CreateLabel(offersContent, "No items available.", rowFontSize, new Color(0.8f, 0.8f, 0.8f, 1f));
                empty.rectTransform.sizeDelta = rowSize;
            }
        }

        private void RefreshGold(PlayerInventory inventory)
        {
            if (goldLabel != null)
                goldLabel.text = $"Gold: {inventory.Gold}";
        }

        private void CreateOfferRow(ShopOffer offer)
        {
            var row = new OfferRow { item = offer.Item, price = offer.Price };

            var rowObj = new GameObject("OfferRow", typeof(Image), typeof(HorizontalLayoutGroup));
            rowObj.transform.SetParent(offersContent, false);
            rowObj.GetComponent<Image>().color = rowColor;
            rowObj.GetComponent<RectTransform>().sizeDelta = rowSize;
            var layout = rowObj.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = 10f;
            layout.padding = new RectOffset(12, 12, 4, 4);

            row.nameLabel = CreateLabel(rowObj.transform, offer.Item.EquipmentName, rowFontSize, Color.white);

            row.priceLabel = CreateLabel(rowObj.transform, $"{offer.Price} gold", rowFontSize, new Color(1f, 0.85f, 0.3f, 1f));
            row.priceLabel.rectTransform.sizeDelta = new Vector2(150, rowSize.y);

            row.buyButton = CreateButton(rowObj.transform, buyLabel, buyButtonColor, buyButtonSize,
                () => Buy(row), out row.buyLabel);

            rows.Add(row);
            RefreshRow(row);
        }

        private void RefreshRow(OfferRow row)
        {
            var inventory = LocalInventory();
            bool owned = Owns(inventory, row.item);
            row.buyButton.interactable = !owned;
            row.buyLabel.text = owned ? "Owned" : buyLabel;
            row.buyButton.targetGraphic.color = owned ? disabledButtonColor : buyButtonColor;
            if (owned) row.priceLabel.text = "Owned";
            else row.priceLabel.text = $"{row.price} gold";
        }

        private static bool Owns(PlayerInventory inventory, EquipmentData item)
        {
            if (inventory.IsUnlocked(item)) return true;
            return inventory.Loadout != null && inventory.Loadout.ContainsItem(item);
        }

        private void Buy(OfferRow row)
        {
            var inventory = LocalInventory();
            if (Owns(inventory, row.item)) { RefreshRow(row); return; }

            if (!inventory.TrySpendGold(row.price))
            {
                row.priceLabel.text = $"Need {row.price}";
                return;
            }

            inventory.Unlock(row.item);
            PersistInventory(inventory);
            RefreshGold(inventory);
            RefreshRow(row);
        }

        /// <summary>Closes the shop, destroys the overlay, and advances the game flow.</summary>
        public void Close()
        {
            var callback = onClose;
            onClose = null;
            DestroyObject(gameObject);
            callback?.Invoke();
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void CreateUI()
        {
            var root = new GameObject("ShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;

            var bg = new GameObject("Background", typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = overlayColor;
            bgImage.raycastTarget = true;
            var bgRect = bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            panel.GetComponent<Image>().color = panelColor;
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = panelSize;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = panelSpacing;
            layout.padding = new RectOffset(panelPadding, panelPadding, panelPadding, panelPadding);

            CreateLabel(panel.transform, titleText, titleFontSize, titleColor);

            goldLabel = CreateLabel(panel.transform, "Gold: 0", rowFontSize, Color.white);

            offersContent = new GameObject("Offers", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            offersContent.SetParent(panel.transform, false);
            var offersLayout = offersContent.GetComponent<VerticalLayoutGroup>();
            offersLayout.childAlignment = TextAnchor.UpperCenter;
            offersLayout.childControlWidth = true;
            offersLayout.childControlHeight = true;
            offersLayout.spacing = panelSpacing;

            CreateButton(panel.transform, continueLabel, new Color(0.25f, 0.25f, 0.35f, 1f),
                new Vector2(260, 54), Close, out _);
        }

        private static TMP_Text CreateLabel(Transform parent, string text, int fontSize, Color color)
        {
            var obj = new GameObject("Label", typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(600, fontSize + 12);
            return tmp;
        }

        private static Button CreateButton(Transform parent, string label, Color color, Vector2 size,
            UnityEngine.Events.UnityAction onClick, out TMP_Text labelText)
        {
            var btnObj = new GameObject(label + "Button", typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var image = btnObj.GetComponent<Image>();
            image.color = color;
            btnObj.GetComponent<RectTransform>().sizeDelta = size;

            var textObj = new GameObject("Text", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            labelText = textObj.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 22;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.sizeDelta = Vector2.zero;

            var button = btnObj.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            return button;
        }
    }
}
