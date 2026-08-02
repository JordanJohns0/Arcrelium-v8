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

        private void Awake()
        {
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
        /// Opens the loadout screen. <paramref name="unlockedReward"/> is the item earned
        /// at victory (null at the start of a run). <paramref name="onConfirmCallback"/>
        /// fires once when this player confirms; the host uses it to collect loadouts.
        /// </summary>
        public void Show(EquipmentData unlockedReward, System.Action onConfirmCallback)
        {
            onConfirmed = onConfirmCallback;
            multiplayer = LobbyManager.Instance.CurrentLobby.HasValue;

            if (multiplayer)
                Networking.ReadyTracker.Instance.Reset();

            var localInventory = PlayerInventoryProvider.GetLocal();

            Debug.Log($"[LoadoutScreen] Show unlocked={localInventory.UnlockedEquipment.Count} loadout={string.Join(",", localInventory.Loadout.ToNames())}");

            var gameCanvas = GameObject.Find("Canvas");
            equipmentUI = gameCanvas != null ? gameCanvas.transform.Find("Equipment UI")?.gameObject : null;
            handPanel = gameCanvas != null ? gameCanvas.transform.Find("HandPanel")?.gameObject : null;
            buttonsPanel = gameCanvas != null ? gameCanvas.transform.Find("Buttons")?.gameObject : null;

            if (equipmentUI != null)
            {
                var eq = equipmentUI.GetComponent<EquipmentUI>();
                equipmentUI.SetActive(true);
                if (eq != null)
                {
                    eq.SetInventory(localInventory);
                    eq.SetEditLoadout(localInventory.Loadout);
                    eq.Refresh();
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
                        loadoutNames = localInventory.Loadout.ToNames()
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
            canvas.sortingOrder = 100;

            var bg = new GameObject("Background", typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.55f);
            bgImage.raycastTarget = false;
            var bgRect = bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(660, 276);
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0, -18);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = 6;
            layout.padding = new RectOffset(20, 20, 12, 12);

            var titleObj = new GameObject("Title", typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(panel.transform, false);
            titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.text = "Choose Your Loadout";
            titleText.fontSize = 26;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleText.rectTransform.sizeDelta = new Vector2(620, 34);

            readyListContent = new GameObject("ReadyList", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            readyListContent.SetParent(panel.transform, false);
            var readyRt = readyListContent.GetComponent<RectTransform>();
            readyRt.sizeDelta = new Vector2(620, 88);
            var readyLayout = readyListContent.GetComponent<VerticalLayoutGroup>();
            readyLayout.spacing = 2;
            readyLayout.childAlignment = TextAnchor.UpperCenter;

            var statusObj = new GameObject("Status", typeof(TextMeshProUGUI));
            statusObj.transform.SetParent(panel.transform, false);
            statusText = statusObj.GetComponent<TextMeshProUGUI>();
            statusText.text = "";
            statusText.fontSize = 14;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            statusText.rectTransform.sizeDelta = new Vector2(620, 18);

            confirmBtn = CreateButton(panel.transform, "Confirm Loadout", ConfirmLoadout);
        }

        private static TMP_Text CreateLabel(Transform parent, string text, int size, Color color)
        {
            var obj = new GameObject("Label", typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(500, 30);
            return tmp;
        }

        private static GameObject CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject(label.Replace(" ", "") + "Button", typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var btnImage = btnObj.GetComponent<Image>();
            btnImage.color = new Color(0.2f, 0.5f, 0.2f, 1f);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(280, 54);

            var textObj = new GameObject("Text", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22;
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
                    20, new Color(p.colorR, p.colorG, p.colorB, 1f));
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
            blocker.color = new Color(0, 0, 0, 0.7f);
            blocker.raycastTarget = true;
            var blockerRect = popup.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.sizeDelta = Vector2.zero;

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(popup.transform, false);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.15f, 0.12f, 0.08f, 1f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(420, 220);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.spacing = 10;
            panelLayout.padding = new RectOffset(24, 24, 20, 20);

            var titleObj = new GameObject("Title", typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(panel.transform, false);
            var title = titleObj.GetComponent<TextMeshProUGUI>();
            title.text = "Victory!";
            title.fontSize = 30;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            title.rectTransform.sizeDelta = new Vector2(400, 36);

            var rewardObj = new GameObject("Reward", typeof(TextMeshProUGUI));
            rewardObj.transform.SetParent(panel.transform, false);
            var rewardLabel = rewardObj.GetComponent<TextMeshProUGUI>();
            rewardLabel.text = $"You unlocked:\n{reward.EquipmentName}";
            rewardLabel.fontSize = 26;
            rewardLabel.alignment = TextAlignmentOptions.Center;
            rewardLabel.color = new Color(1f, 0.85f, 0.3f, 1f);
            rewardLabel.rectTransform.sizeDelta = new Vector2(400, 70);

            var okObj = CreateButton(panel.transform, "Nice!", () => DismissRewardPopup(popup));
            okObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 50);

            rewardPopup = popup;
        }

        private void DismissRewardPopup(GameObject popup)
        {
            if (rewardPopup == popup) rewardPopup = null;
            if (popup != null) Destroy(popup);
        }
    }
}
