using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using Lobby = Steamworks.Data.Lobby;
using Game;

namespace UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string version = "v0.1.1";

        private Transform mainContent;
        private Transform lobbyContent;
        private Transform lobbyListContent;
        private TMP_Text statusText;
        private TMP_Text currentLobbyLabel;
        private GameObject hostGameBtn;
        private GameObject refreshLobbiesBtn;
        private GameObject backBtn;
        private Transform readyListContent;
        private GameObject readyToggleBtn;
        private GameObject inviteFriendsBtn;
        private GameObject startMatchBtn;
        private GameObject leaveLobbyBtn;
        private readonly List<Lobby> foundLobbies = new List<Lobby>();

        private bool lobbyEventsSubscribed;

        private void Awake()
        {
            CreateUI();
            _ = SteamManager.Instance;
            _ = Networking.NetworkManager.Instance;
            _ = LobbyManager.Instance;
            _ = Networking.ReadyTracker.Instance;
            SubscribeLobbyEvents();
        }

        private void OnEnable()
        {
            if (lobbyEventsSubscribed) return;
            lobbyEventsSubscribed = true;
            SteamMatchmaking.OnLobbyMemberJoined += HandleSteamMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += HandleSteamMemberLeft;
            SteamMatchmaking.OnLobbyEntered += HandleSteamLobbyEntered;
            Debug.Log("[MainMenu] Subscribed to SteamMatchmaking events directly");
        }

        private void OnDisable()
        {
            SteamMatchmaking.OnLobbyMemberJoined -= HandleSteamMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= HandleSteamMemberLeft;
            SteamMatchmaking.OnLobbyEntered -= HandleSteamLobbyEntered;
        }

        private void OnDestroy()
        {
            UnsubscribeLobbyEvents();
        }

        private void HandleSteamMemberJoined(Lobby lobby, Friend friend)
        {
            Debug.Log($"[MainMenu] SteamMemberJoined: {friend.Name}, lobby={lobby.Id}");
            UpdateCurrentLobbyLabel();
        }

        private void HandleSteamMemberLeft(Lobby lobby, Friend friend)
        {
            Debug.Log($"[MainMenu] SteamMemberLeft: {friend.Name}");
            UpdateCurrentLobbyLabel();
        }

        private void HandleSteamLobbyEntered(Lobby lobby)
        {
            Debug.Log($"[MainMenu] SteamLobbyEntered: id={lobby.Id}, memberCount={lobby.MemberCount}, owner={lobby.Owner.Id}");
            UpdateCurrentLobbyLabel();
        }

        private void SubscribeLobbyEvents()
        {
            LobbyManager.OnLobbyCreated += HandleLobbyCreated;
            LobbyManager.OnLobbyJoined += HandleLobbyJoined;
            LobbyManager.OnLobbyListReceived += HandleLobbyListReceived;
            LobbyManager.OnMembersChanged += HandleMembersChanged;
            Networking.NetworkManager.OnStartGameReceived += HandleStartGameReceived;
            Networking.ReadyTracker.OnReadyStateChanged += RefreshReadyUI;
        }

        private void UnsubscribeLobbyEvents()
        {
            LobbyManager.OnLobbyCreated -= HandleLobbyCreated;
            LobbyManager.OnLobbyJoined -= HandleLobbyJoined;
            LobbyManager.OnLobbyListReceived -= HandleLobbyListReceived;
            LobbyManager.OnMembersChanged -= HandleMembersChanged;
            Networking.NetworkManager.OnStartGameReceived -= HandleStartGameReceived;
            Networking.ReadyTracker.OnReadyStateChanged -= RefreshReadyUI;
        }

        private void HandleMembersChanged()
        {
            UpdateCurrentLobbyLabel();
            RefreshReadyUI();
        }

        private void HandleStartGameReceived()
        {
            Debug.Log("[MainMenu] Received StartGame, starting as client");
            Game.GameState.ActiveMode = Game.GameMode.Client;
            Networking.NetworkManager.Instance.StartClient(LobbyManager.Instance.CurrentLobby.Value.Owner.Id);
            Networking.ReadyTracker.Instance.Reset();
            GameState.Restart();
        }

        private void HandleLobbyCreated(Lobby lobby)
        {
            Networking.NetworkManager.Instance.StartHost();
            ShowLobbyPanel();
            statusText.text = "Waiting for players...";
            statusText.gameObject.SetActive(true);
            Networking.ReadyTracker.Instance.Reset();
            UpdateCurrentLobbyLabel();
            RefreshReadyUI();
        }

        private void HandleLobbyJoined(Lobby lobby)
        {
            Networking.NetworkManager.Instance.StartClient(lobby.Owner.Id);
            ShowLobbyPanel();
            statusText.text = "Joined lobby — waiting for host to start...";
            statusText.gameObject.SetActive(true);
            Networking.ReadyTracker.Instance.Reset();
            UpdateCurrentLobbyLabel();
            RefreshReadyUI();
        }

        private void UpdateCurrentLobbyLabel()
        {
            if (!LobbyManager.Instance.CurrentLobby.HasValue) return;
            var lobby = LobbyManager.Instance.CurrentLobby.Value;
            lobby.Refresh();
            string name = lobby.GetData("name") ?? $"Lobby {lobby.Id}";
            int memberCount = lobby.MemberCount;
            currentLobbyLabel.text = $"{name} ({memberCount}/{lobby.MaxMembers} players)";
            currentLobbyLabel.gameObject.SetActive(true);
            Debug.Log($"[MainMenu] Updated lobby label: {name} ({memberCount}/{lobby.MaxMembers})");
        }

        private void HandleLobbyListReceived(List<Lobby> lobbies)
        {
            foundLobbies.Clear();
            foundLobbies.AddRange(lobbies);
            RefreshLobbyList();
        }

        private void CreateUI()
        {
            var canvasGo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = new Vector2(0, 0);
            panel.anchorMax = new Vector2(1, 1);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.08f, 1);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = 30;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(panel, false);
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(500, 600);
            content.anchoredPosition = Vector2.zero;
            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.MiddleCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.spacing = 10;

            var titleObj = new GameObject("Title", typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(content, false);
            var title = titleObj.GetComponent<TextMeshProUGUI>();
            title.text = "Arcrelium";
            title.fontSize = 64;
            title.fontStyle = FontStyles.Bold;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.Center;
            title.rectTransform.sizeDelta = new Vector2(500, 100);

            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(content, false);
            spacer.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 30);

            // Main menu buttons
            mainContent = new GameObject("MainContent", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            mainContent.SetParent(content, false);
            var mcLayout = mainContent.GetComponent<VerticalLayoutGroup>();
            mcLayout.childAlignment = TextAnchor.MiddleCenter;
            mcLayout.childControlWidth = true;
            mcLayout.childControlHeight = true;
            mcLayout.spacing = 10;
            CreateButton(mainContent, "Start Game (Single Player)", StartGame);
            CreateButton(mainContent, "Multiplayer", ShowMultiplayerPanel);
            CreateButton(mainContent, "Quit", QuitGame);

            // Multiplayer panel (hidden by default)
            lobbyContent = new GameObject("LobbyContent", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            lobbyContent.SetParent(content, false);
            var lcLayout = lobbyContent.GetComponent<VerticalLayoutGroup>();
            lcLayout.childAlignment = TextAnchor.MiddleCenter;
            lcLayout.childControlWidth = true;
            lcLayout.childControlHeight = true;
            lcLayout.spacing = 10;
            lobbyContent.gameObject.SetActive(false);

            statusText = CreateLabel(lobbyContent, "");
            statusText.fontSize = 22;
            statusText.gameObject.SetActive(false);

            // Browse panel: host/search/join another lobby
            hostGameBtn = CreateButton(lobbyContent, "Host Game", HostGame);
            refreshLobbiesBtn = CreateButton(lobbyContent, "Refresh Lobbies", RefreshLobbies);

            lobbyListContent = new GameObject("LobbyList", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            lobbyListContent.SetParent(lobbyContent, false);
            var listRt = lobbyListContent.GetComponent<RectTransform>();
            listRt.sizeDelta = new Vector2(450, 200);
            var listLayout = lobbyListContent.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 4;

            backBtn = CreateButton(lobbyContent, "Back", ShowMainPanel);

            // In-lobby room: no lobby browsing, just the member ready list
            currentLobbyLabel = CreateLabel(lobbyContent, "");
            currentLobbyLabel.fontSize = 18;
            currentLobbyLabel.color = new Color(0.4f, 0.8f, 0.4f);
            currentLobbyLabel.gameObject.SetActive(false);

            readyListContent = new GameObject("ReadyList", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            readyListContent.SetParent(lobbyContent, false);
            var readyRt = readyListContent.GetComponent<RectTransform>();
            readyRt.sizeDelta = new Vector2(450, 200);
            var readyLayout = readyListContent.GetComponent<VerticalLayoutGroup>();
            readyLayout.spacing = 4;
            readyListContent.gameObject.SetActive(false);

            readyToggleBtn = CreateButton(lobbyContent, "Ready Up", ToggleReady);
            readyToggleBtn.gameObject.SetActive(false);

            inviteFriendsBtn = CreateButton(lobbyContent, "Invite Friends", InviteFriends);
            inviteFriendsBtn.gameObject.SetActive(false);

            startMatchBtn = CreateButton(lobbyContent, "Start Match", StartMatch);
            startMatchBtn.gameObject.SetActive(false);

            leaveLobbyBtn = CreateButton(lobbyContent, "Leave Lobby", LeaveLobby);
            leaveLobbyBtn.gameObject.SetActive(false);

            var versionObj = new GameObject("VersionLabel", typeof(TextMeshProUGUI));
            versionObj.transform.SetParent(canvasGo.transform, false);
            var versionText = versionObj.GetComponent<TextMeshProUGUI>();
            versionText.text = version;
            versionText.fontSize = 18;
            versionText.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            versionText.alignment = TextAlignmentOptions.TopRight;
            var versionRt = versionText.rectTransform;
            versionRt.anchorMin = new Vector2(1f, 1f);
            versionRt.anchorMax = new Vector2(1f, 1f);
            versionRt.pivot = new Vector2(1f, 1f);
            versionRt.anchoredPosition = new Vector2(-16f, -8f);
            versionRt.sizeDelta = new Vector2(200f, 30f);

            EnsureEventSystem();
        }

        private static TMP_Text CreateLabel(Transform parent, string text)
        {
            var obj = new GameObject("Label", typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(450, 40);
            return tmp;
        }

        private static GameObject CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject(GetCleanName(label) + "Button", typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(300, 50);

            var btnImage = btnObj.GetComponent<Image>();
            btnImage.color = new Color(0.2f, 0.2f, 0.2f, 1);

            var textObj = new GameObject("Text", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(300, 50);

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);

            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
            btn.colors = colors;

            return btnObj;
        }

        private static string GetCleanName(string label)
        {
            return label.Replace(" ", "").Replace("(", "").Replace(")", "");
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            EnsureCamera();
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);
        }

        private void ShowMainPanel()
        {
            mainContent.gameObject.SetActive(true);
            lobbyContent.gameObject.SetActive(false);
            statusText.gameObject.SetActive(false);
        }

        private void ShowMultiplayerPanel()
        {
            mainContent.gameObject.SetActive(false);
            lobbyContent.gameObject.SetActive(true);
            statusText.gameObject.SetActive(false);
            currentLobbyLabel.gameObject.SetActive(false);
            readyListContent.gameObject.SetActive(false);
            readyToggleBtn.gameObject.SetActive(false);
            inviteFriendsBtn.SetActive(false);
            startMatchBtn.gameObject.SetActive(false);

            hostGameBtn.SetActive(true);
            refreshLobbiesBtn.SetActive(true);
            lobbyListContent.gameObject.SetActive(true);
            backBtn.SetActive(true);
            leaveLobbyBtn.SetActive(false);
            RefreshLobbies();
        }

        private void ShowLobbyPanel()
        {
            mainContent.gameObject.SetActive(false);
            lobbyContent.gameObject.SetActive(true);
            statusText.gameObject.SetActive(true);
            currentLobbyLabel.gameObject.SetActive(true);
            readyListContent.gameObject.SetActive(true);
            readyToggleBtn.gameObject.SetActive(true);
            inviteFriendsBtn.SetActive(true);
            startMatchBtn.gameObject.SetActive(true);

            hostGameBtn.SetActive(false);
            refreshLobbiesBtn.SetActive(false);
            lobbyListContent.gameObject.SetActive(false);
            backBtn.SetActive(false);
            leaveLobbyBtn.SetActive(true);
        }

        private void HostGame()
        {
            LobbyManager.Instance.CreateLobby(4);
            statusText.gameObject.SetActive(true);
            statusText.text = "Creating lobby...";
        }

        private void RefreshLobbies()
        {
            statusText.gameObject.SetActive(true);
            statusText.text = "Searching for lobbies...";
            LobbyManager.Instance.FindLobbies();
        }

        private void RefreshLobbyList()
        {
            foreach (Transform child in lobbyListContent)
                Destroy(child.gameObject);

            if (foundLobbies.Count == 0)
            {
                var lbl = CreateLabel(lobbyListContent, "No lobbies found");
                lbl.fontSize = 18;
                lbl.color = new Color(0.7f, 0.7f, 0.7f);
                return;
            }

            foreach (var lobby in foundLobbies.Take(5))
            {
                string name = lobby.GetData("name") ?? $"Lobby {lobby.Id}";
                int members = lobby.MemberCount;
                var btn = CreateButton(lobbyListContent, $"{name} ({members}/4)", () => JoinLobby(lobby));
                var rt = btn.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(430, 40);
            }
        }

        private void JoinLobby(Lobby lobby)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = "Joining lobby...";
            LobbyManager.Instance.JoinLobby(lobby);
        }

        private void LeaveLobby()
        {
            LobbyManager.Instance.LeaveLobby();
            Networking.ReadyTracker.Instance.Reset();
            ShowMultiplayerPanel();
        }

        private void InviteFriends()
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (!lobby.HasValue) return;
            SteamFriends.OpenGameInviteOverlay(lobby.Value.Id.Value);
        }

        private static void StartGame()
        {
            Game.GameState.ActiveMode = Game.GameMode.SinglePlayer;
            GameState.Restart();
        }

        private void StartMatch()
        {
            if (!Networking.ReadyTracker.Instance.AllReady) return;
            Game.GameState.ActiveMode = Game.GameMode.Host;
            Networking.NetworkManager.Instance.StartHost();
            Networking.NetworkManager.Instance.SendStartGame();
            Networking.ReadyTracker.Instance.Reset();
            GameState.Restart();
        }

        private void ToggleReady()
        {
            int idx = GetLocalPlayerIndex();
            if (idx < 0) return;
            Networking.ReadyTracker.Instance.SetLocalReady(!Networking.ReadyTracker.Instance.IsReady(idx));
        }

        private static int GetLocalPlayerIndex()
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (!lobby.HasValue) return -1;
            return GameState.GetPlayerIndex(lobby.Value, SteamClient.SteamId);
        }

        private void RefreshReadyUI()
        {
            if (readyListContent == null) return;

            foreach (Transform child in readyListContent)
                Destroy(child.gameObject);

            foreach (var p in Networking.ReadyTracker.Instance.GetPlayers())
            {
                var lbl = CreateLabel(readyListContent, $"{p.name} — {(p.ready ? "Ready" : "Not ready")}");
                lbl.fontSize = 20;
                lbl.color = new Color(p.colorR, p.colorG, p.colorB, 1f);
            }

            int localIdx = GetLocalPlayerIndex();
            bool localReady = localIdx >= 0 && Networking.ReadyTracker.Instance.IsReady(localIdx);
            if (readyToggleBtn != null)
            {
                var text = readyToggleBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = localReady ? "Cancel Ready" : "Ready Up";
            }

            if (startMatchBtn != null)
            {
                bool isHost = LobbyManager.Instance.IsHost;
                startMatchBtn.SetActive(isHost);
                var btn = startMatchBtn.GetComponent<Button>();
                if (btn != null) btn.interactable = Networking.ReadyTracker.Instance.AllReady;
            }
        }

        private static void QuitGame()
        {
            Application.Quit();
        }
    }
}
