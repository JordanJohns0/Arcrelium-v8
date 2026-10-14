using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using Game;
using Networking;
using Equipment;

namespace UI
{
    /// <summary>
    /// Loadout overlay shown at the start of a run and after a victory. Players see their
    /// private inventory, pick their gear for the level, and confirm. In multiplayer every
    /// player confirms; the host starts the match once it has everyone's loadout.
    ///
    /// Replaces the old VictoryScreen. The reward unlocked at victory is shown as a popup
    /// on each player's own machine only.
    /// </summary>
    public class LoadoutScreen : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject uiPrefab;

        [Header("Canvas")]
        [SerializeField] private int sortingOrder = 100;

        [Header("Background")]
        [SerializeField] private Color backgroundTint = new Color(0, 0, 0, 0.55f);

        [Header("Panel")]
        [SerializeField] private Vector2 panelSize = new Vector2(660, 276);
        [SerializeField] private Color panelColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private Vector2 panelAnchoredPosition = new Vector2(0, -18);
        [SerializeField] private float panelSpacing = 6f;
        [SerializeField] private int panelPaddingLeft = 20;
        [SerializeField] private int panelPaddingRight = 20;
        [SerializeField] private int panelPaddingTop = 12;
        [SerializeField] private int panelPaddingBottom = 12;

        [Header("Title")]
        [SerializeField] private string defaultTitleText = "Choose Your Loadout";
        [SerializeField] private int titleFontSize = 26;
        [SerializeField] private Vector2 titleSize = new Vector2(620, 34);

        [Header("Ready List")]
        [SerializeField] private Vector2 readyListSize = new Vector2(620, 88);
        [SerializeField] private float readyListSpacing = 2f;
        [SerializeField] private int readyRowFontSize = 20;

        [Header("Status")]
        [SerializeField] private int statusFontSize = 14;
        [SerializeField] private Color statusColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Vector2 statusSize = new Vector2(620, 18);

        [Header("Labels")]
        [SerializeField] private Vector2 labelSize = new Vector2(500, 30);

        [Header("Confirm Button")]
        [SerializeField] private string confirmLabel = "Confirm Loadout";
        [SerializeField] private Vector2 confirmButtonSize = new Vector2(280, 54);
        [SerializeField] private Color confirmButtonColor = new Color(0.2f, 0.5f, 0.2f, 1f);
        [SerializeField] private int confirmButtonFontSize = 22;

        [Header("Reward Popup")]
        [SerializeField] private Color rewardBlockerColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private Vector2 rewardPanelSize = new Vector2(420, 220);
        [SerializeField] private Color rewardPanelColor = new Color(0.15f, 0.12f, 0.08f, 1f);
        [SerializeField] private float rewardSpacing = 10f;
        [SerializeField] private int rewardPadding = 24;
        [SerializeField] private int rewardPaddingVertical = 20;
        [SerializeField] private string rewardTitleText = "Victory!";
        [SerializeField] private int rewardTitleFontSize = 30;
        [SerializeField] private Vector2 rewardTitleSize = new Vector2(400, 36);
        [SerializeField] private int rewardFontSize = 26;
        [SerializeField] private Color rewardColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Vector2 rewardLabelSize = new Vector2(400, 70);
        [SerializeField] private Vector2 okButtonSize = new Vector2(200, 50);
        [SerializeField] private string okLabel = "Nice!";

        private Canvas canvas;
        private TMP_Text titleText;
        private TMP_Text statusText;
        private Transform readyListContent;
        private GameObject confirmBtn;
        private GameObject rewardPopup;

        private GameObject equipmentUI;
        private GameObject handPanel;
        private GameObject buttonsPanel;

        private bool multiplayer;
        private bool confirmed;
        private System.Action onConfirmed;
        private static GameObject staticPrefab;

        private void Awake()
        {
            if (staticPrefab == null && uiPrefab != null) staticPrefab = uiPrefab;
            CreateUI();
            Hide();
            _ = Networking.ReadyTracker.Instance;
        }

        private void OnEnable()
        {
            Networking.ReadyTracker.OnReadyStateChanged += RefreshReadyUI;
        }

        private void OnDisable()
        {
            Networking.ReadyTracker.OnReadyStateChanged -= RefreshReadyUI;
        }

        /// <summary>
        /// Instantiates the loadout screen from the UI prefab, falling back to a plain
        /// GameObject if the prefab is missing, so the screen always exists at runtime.
        /// </summary>
        public static LoadoutScreen Create()
        {
            var existing = FindAnyObjectByType<LoadoutScreen>();
            if (existing != null)
                return existing;
            if (staticPrefab != null)
                return Object.Instantiate(staticPrefab).GetComponent<LoadoutScreen>();
            var obj = new GameObject("LoadoutScreen", typeof(LoadoutScreen));
            return obj.GetComponent<LoadoutScreen>();
        }

        /// <summary>
        /// Opens the loadout screen. <paramref name="unlockedReward"/> is the item earned
        /// at victory (null at the start of a run). <paramref name="onConfirmCallback"/>
        /// fires once when this player confirms; the host uses it to collect loadouts.
        /// When <paramref name="withShop"/> is set the Special column is filled with shop
        /// offers and the gold display tracks the balance (end-of-level loadout).
        /// </summary>
        public void Show(EquipmentData unlockedReward, System.Action onConfirmCallback, bool withShop = false)
        {
            onConfirmed = onConfirmCallback;
            multiplayer = LobbyManager.Instance.CurrentLobby.HasValue;

            if (multiplayer)
                Networking.ReadyTracker.Instance.Reset();

            var localInventory = PlayerInventoryProvider.GetLocal();

            Debug.Log($"[LoadoutScreen] Show unlocked={localInventory.UnlockedEquipment.Count} loadout={string.Join(",", localInventory.Loadout.ToNames())}");

            // The scene canvas is named "UI" (was "Canvas"), so find the equipment panel by
            // component (including inactive — EquipmentUI deactivates itself in Awake) and
            // reach the rest of the HUD through its parent canvas.
            var equipmentPanels = Object.FindObjectsByType<EquipmentUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            EquipmentUI equipmentPanelComponent = equipmentPanels != null && equipmentPanels.Length > 0
                ? equipmentPanels[0]
                : null;
            equipmentUI = equipmentPanelComponent != null ? equipmentPanelComponent.gameObject : null;

            Transform gameCanvas = equipmentUI != null ? equipmentUI.transform.parent : null;
            handPanel = gameCanvas != null ? gameCanvas.Find("HandPanel")?.gameObject : null;
            buttonsPanel = gameCanvas != null ? gameCanvas.Find("Buttons")?.gameObject : null;

            if (equipmentUI != null)
            {
                equipmentUI.SetActive(true);
                if (equipmentPanelComponent != null)
                {
                    equipmentPanelComponent.SetInventory(localInventory);
                    equipmentPanelComponent.SetEditLoadout(localInventory.Loadout);
                    equipmentPanelComponent.Refresh();

                    Transform specialPanel = equipmentPanelComponent.ShopPanel;
                    GameObject slotPrefab = equipmentPanelComponent.InventorySlotPrefab != null
                        ? equipmentPanelComponent.InventorySlotPrefab.gameObject
                        : null;

                    // The Special pane hosts either the shop or the enchantment station; hide
                    // whichever was left over from a previous level before showing this one.
                    var shop = specialPanel != null ? specialPanel.GetComponent<ShopScreen>() : null;
                    var enchant = specialPanel != null ? specialPanel.GetComponent<EnchantScreen>() : null;
                    if (shop != null) shop.Hide();
                    if (enchant != null) enchant.Hide();

                    // The Special page's own title/description labels head whichever area is
                    // shown (shop or enchantment station).
                    TMP_Text specialTitle = equipmentPanelComponent.SpecialTitleLabel;
                    TMP_Text specialDescription = equipmentPanelComponent.SpecialDescriptionLabel;

                    if (withShop && GameState.CurrentSpecialArea == SpecialArea.Enchant && HasEnchantments())
                    {
                        EnchantScreen screen = EnchantScreen.Attach(specialPanel, slotPrefab, specialTitle, specialDescription);
                        if (screen != null) screen.Show();
                    }
                    else if (withShop)
                    {
                        ShopScreen screen = ShopScreen.Attach(specialPanel, slotPrefab, equipmentPanelComponent.GoldLabel,
                            specialTitle, specialDescription);
                        if (screen != null) screen.Show();
                    }

                    // Swap the Special tab's bookmark icon to match the area this level rolled.
                    equipmentPanelComponent.ApplySpecialArea(GameState.CurrentSpecialArea);

                    // Open on the special page after a victory; otherwise start on the inventory.
                    equipmentPanelComponent.SelectPage(withShop ? "Special Page" : "Inventory Slots");
                }
            }

            if (handPanel != null) handPanel.SetActive(false);
            if (buttonsPanel != null) buttonsPanel.SetActive(false);

            titleText.text = $"Level {GameState.CurrentLevel} — Choose Your Loadout";

            confirmBtn.SetActive(true);
            confirmed = false;
            statusText.text = "Pick your gear, then confirm.";
            RefreshReadyUI();
            canvas.enabled = true;

            if (unlockedReward != null)
                ShowRewardPopup(unlockedReward);
        }

        /// <summary>True when the library offers at least one enchantment to hand out.</summary>
        private static bool HasEnchantments()
        {
            var library = GameDataLibrary.Instance;
            var pool = library != null ? library.GetAllEnchantments() : null;
            return pool != null && pool.Count > 0;
        }

        /// <summary>Closes the overlay, restores the game UI, and destroys the screen.</summary>
        public void Close()
        {
            if (equipmentUI != null) equipmentUI.SetActive(false);
            if (handPanel != null) handPanel.SetActive(true);
            if (buttonsPanel != null) buttonsPanel.SetActive(true);
            Destroy(gameObject);
        }

        public void Hide()
        {
            if (canvas != null) canvas.enabled = false;
        }

        private void ConfirmLoadout()
        {
            if (confirmed) return;
            confirmed = true;

            var localInventory = PlayerInventoryProvider.GetLocal();
            PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, localInventory);

            if (multiplayer)
            {
                if (LobbyManager.Instance.IsHost)
                {
                    Networking.ReadyTracker.Instance.SetLocalReady(true);
                }
                else
                {
                    Networking.NetworkManager.Instance.SendLoadoutSync(new LoadoutSync
                    {
                        playerIndex = GameState.LocalPlayerId,
                        loadoutNames = localInventory.Loadout.ToNames(),
                        enchantments = localInventory.Loadout.ToEnchantmentAssignments()
                    });
                    Networking.ReadyTracker.Instance.SetLocalReady(true);
                }
                statusText.text = "Waiting for other players…";
            }

            confirmBtn.SetActive(false);
            onConfirmed?.Invoke();
        }

        private void CreateUI()
        {
            var root = new GameObject("LoadoutCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var bg = new GameObject("Background", typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = backgroundTint;
            bgImage.raycastTarget = false;
            var bgRect = bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = panelColor;
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = panelSize;
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = panelAnchoredPosition;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = panelSpacing;
            layout.padding = new RectOffset(panelPaddingLeft, panelPaddingRight, panelPaddingTop, panelPaddingBottom);

            var titleObj = new GameObject("Title", typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(panel.transform, false);
            titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.text = defaultTitleText;
            titleText.fontSize = titleFontSize;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleText.rectTransform.sizeDelta = titleSize;

            readyListContent = new GameObject("ReadyList", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            readyListContent.SetParent(panel.transform, false);
            var readyRt = readyListContent.GetComponent<RectTransform>();
            readyRt.sizeDelta = readyListSize;
            var readyLayout = readyListContent.GetComponent<VerticalLayoutGroup>();
            readyLayout.spacing = readyListSpacing;
            readyLayout.childAlignment = TextAnchor.UpperCenter;

            var statusObj = new GameObject("Status", typeof(TextMeshProUGUI));
            statusObj.transform.SetParent(panel.transform, false);
            statusText = statusObj.GetComponent<TextMeshProUGUI>();
            statusText.text = "";
            statusText.fontSize = statusFontSize;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = statusColor;
            statusText.rectTransform.sizeDelta = statusSize;

            confirmBtn = CreateButton(panel.transform, confirmLabel, ConfirmLoadout);
        }

        private TMP_Text CreateLabel(Transform parent, string text, int size, Color color)
        {
            var obj = new GameObject("Label", typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = labelSize;
            return tmp;
        }

        private GameObject CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject(label.Replace(" ", "") + "Button", typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var btnImage = btnObj.GetComponent<Image>();
            btnImage.color = confirmButtonColor;
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = confirmButtonSize;

            var textObj = new GameObject("Text", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = confirmButtonFontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;

            var button = btnObj.GetComponent<Button>();
            button.targetGraphic = btnImage;
            button.onClick.AddListener(onClick);
            return btnObj;
        }

        private void RefreshReadyUI()
        {
            if (readyListContent == null) return;

            foreach (Transform child in readyListContent)
                Destroy(child.gameObject);

            if (!multiplayer) return;

            foreach (var p in Networking.ReadyTracker.Instance.GetPlayers())
            {
                CreateLabel(readyListContent, $"{p.name} — {(p.ready ? "Ready" : "Picking")}",
                    readyRowFontSize, new Color(p.colorR, p.colorG, p.colorB, 1f));
            }
        }

        /// <summary>
        /// Modal popup announcing the item just unlocked. Blocks the equipment selection
        /// behind it until the player dismisses it, so the "what you got" reveal always
        /// happens before any gear is picked.
        /// </summary>
        private void ShowRewardPopup(EquipmentData reward)
        {
            if (reward == null || canvas == null) return;

            var popup = new GameObject("RewardPopup", typeof(Image));
            popup.transform.SetParent(canvas.transform, false);
            var blocker = popup.GetComponent<Image>();
            blocker.color = rewardBlockerColor;
            blocker.raycastTarget = true;
            var blockerRect = popup.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.sizeDelta = Vector2.zero;

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(popup.transform, false);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = rewardPanelColor;
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = rewardPanelSize;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.spacing = rewardSpacing;
            panelLayout.padding = new RectOffset(rewardPadding, rewardPadding, rewardPaddingVertical, rewardPaddingVertical);

            var titleObj = new GameObject("Title", typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(panel.transform, false);
            var title = titleObj.GetComponent<TextMeshProUGUI>();
            title.text = rewardTitleText;
            title.fontSize = rewardTitleFontSize;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            title.rectTransform.sizeDelta = rewardTitleSize;

            var rewardObj = new GameObject("Reward", typeof(TextMeshProUGUI));
            rewardObj.transform.SetParent(panel.transform, false);
            var rewardLabel = rewardObj.GetComponent<TextMeshProUGUI>();
            rewardLabel.text = $"You unlocked:\n{reward.EquipmentName}";
            rewardLabel.fontSize = rewardFontSize;
            rewardLabel.alignment = TextAlignmentOptions.Center;
            rewardLabel.color = rewardColor;
            rewardLabel.rectTransform.sizeDelta = rewardLabelSize;

            var okObj = CreateButton(panel.transform, okLabel, () => DismissRewardPopup(popup));
            okObj.GetComponent<RectTransform>().sizeDelta = okButtonSize;

            rewardPopup = popup;
        }

        private void DismissRewardPopup(GameObject popup)
        {
            if (rewardPopup == popup) rewardPopup = null;
            if (popup != null) Destroy(popup);
        }
    }
}
