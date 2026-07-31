using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using Game;
using Networking;

namespace UI
{
    /// <summary>
    /// Victory overlay. In single player the host simply advances to the next level.
    /// In multiplayer every player must ready up; the host can then start the next level,
    /// which is broadcast so clients load it too.
    /// </summary>
    public class VictoryScreen : MonoBehaviour
    {
        private Canvas canvas;
        private TMP_Text victoryText;
        private Transform readyListContent;
        private GameObject readyToggleBtn;
        private GameObject nextLevelBtn;

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

        private void CreateUI()
        {
            var root = new GameObject("VictoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var bg = new GameObject("Background", typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);
            var bgRect = bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.15f, 0.15f, 0.15f, 1);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(420, 480);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = 16;
            layout.padding = new RectOffset(20, 20, 20, 20);

            var textObj = new GameObject("VictoryText", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(panel.transform, false);
            victoryText = textObj.GetComponent<TextMeshProUGUI>();
            victoryText.text = $"Victory! Level {GameState.CurrentLevel} Complete!";
            victoryText.fontSize = 28;
            victoryText.alignment = TextAlignmentOptions.Center;
            victoryText.color = Color.white;
            victoryText.rectTransform.sizeDelta = new Vector2(380, 80);

            var readyLabel = CreateLabel(panel.transform, "Players");
            readyLabel.fontSize = 18;
            readyLabel.fontStyle = FontStyles.Bold;

            readyListContent = new GameObject("ReadyList", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            readyListContent.SetParent(panel.transform, false);
            var readyRt = readyListContent.GetComponent<RectTransform>();
            readyRt.sizeDelta = new Vector2(380, 200);
            var readyLayout = readyListContent.GetComponent<VerticalLayoutGroup>();
            readyLayout.spacing = 4;
            readyLayout.childAlignment = TextAnchor.MiddleCenter;

            readyToggleBtn = CreateButton(panel.transform, "Ready Up", ToggleReady);

            nextLevelBtn = CreateButton(panel.transform, "Next Level", StartNextLevel);

            var nextText = nextLevelBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (nextText != null) nextText.text = "Next Level";
        }

        private static TMP_Text CreateLabel(Transform parent, string text)
        {
            var obj = new GameObject("Label", typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 22;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(380, 40);
            return tmp;
        }

        private static GameObject CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject(label.Replace(" ", "") + "Button", typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var btnImage = btnObj.GetComponent<Image>();
            btnImage.color = new Color(0.2f, 0.5f, 0.2f, 1);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(240, 50);

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

            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            return btnObj;
        }

        public void Show()
        {
            victoryText.text = $"Victory! Level {GameState.CurrentLevel} Complete!";
            RefreshReadyUI();
            canvas.enabled = true;
        }

        public void Hide()
        {
            canvas.enabled = false;
        }

        private static bool InLobby => LobbyManager.Instance.CurrentLobby.HasValue;

        private void ToggleReady()
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (!lobby.HasValue) return;
            int idx = GameState.GetPlayerIndex(lobby.Value, SteamClient.SteamId);
            if (idx < 0) return;
            Networking.ReadyTracker.Instance.SetLocalReady(!Networking.ReadyTracker.Instance.IsReady(idx));
        }

        private void StartNextLevel()
        {
            if (InLobby)
            {
                if (!Networking.ReadyTracker.Instance.AllReady) return;
                Networking.NetworkManager.Instance.SendLoadNextLevel();
                Networking.ReadyTracker.Instance.Reset();
            }
            GameState.LoadNextLevel();
        }

        private void RefreshReadyUI()
        {
            bool multiplayer = InLobby;
            bool isHost = LobbyManager.Instance.IsHost;

            if (readyListContent != null)
                readyListContent.gameObject.SetActive(multiplayer);
            if (readyToggleBtn != null)
                readyToggleBtn.SetActive(multiplayer);

            if (multiplayer && readyListContent != null)
            {
                foreach (Transform child in readyListContent)
                    Destroy(child.gameObject);

                foreach (var p in Networking.ReadyTracker.Instance.GetPlayers())
                {
                    var entry = CreateLabel(readyListContent, $"{p.name} — {(p.ready ? "Ready" : "Not ready")}");
                    entry.fontSize = 20;
                    entry.color = new Color(p.colorR, p.colorG, p.colorB, 1f);
                    entry.rectTransform.sizeDelta = new Vector2(380, 30);
                }
            }

            if (multiplayer && readyToggleBtn != null)
            {
                int idx = GameState.GetPlayerIndex(LobbyManager.Instance.CurrentLobby.Value, SteamClient.SteamId);
                bool localReady = idx >= 0 && Networking.ReadyTracker.Instance.IsReady(idx);
                var text = readyToggleBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = localReady ? "Cancel Ready" : "Ready Up";
            }

            if (nextLevelBtn != null)
            {
                nextLevelBtn.SetActive(!multiplayer || isHost);
                var button = nextLevelBtn.GetComponent<Button>();
                if (button != null)
                    button.interactable = !multiplayer || Networking.ReadyTracker.Instance.AllReady;
            }
        }
    }
}
