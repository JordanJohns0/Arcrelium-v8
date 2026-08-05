using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Game;

public class LobbyManager : MonoBehaviour
    {
    private static LobbyManager instance;

    public const string MatchModeKey = "matchmode";

    /// <summary>Max players in a PvE lobby.</summary>
    public const int PveMaxMembers = 4;

    /// <summary>Max players in a PvP lobby.</summary>
    public const int PvpMaxMembers = 8;

    public Lobby? CurrentLobby { get; private set; }
    public bool IsHost { get; private set; }

    /// <summary>The match format for the current lobby (PvE by default).</summary>
    public MatchMode MatchMode { get; private set; } = MatchMode.PvE;

    /// <summary>Max members for the current match format (4 for PvE, 8 for PvP).</summary>
    public int MaxMembersForCurrentMode => MatchMode == MatchMode.PvP ? PvpMaxMembers : PveMaxMembers;

    public static event Action<Lobby> OnLobbyCreated;
    public static event Action<Lobby> OnLobbyJoined;
    public static event Action OnLobbyLeft;
    public static event Action<List<Lobby>> OnLobbyListReceived;
    public static event Action OnMembersChanged;
    public static event Action<Lobby, Friend> OnLobbyMemberLeft;
    public static event Action<MatchMode> OnMatchModeChanged;

        public static LobbyManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("LobbyManager");
                    instance = go.AddComponent<LobbyManager>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            Debug.Log("[LobbyManager] OnEnable - subscribing to lobby events");
            SteamMatchmaking.OnLobbyMemberJoined += HandleMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += HandleMemberLeft;
            SteamMatchmaking.OnLobbyInvite += HandleLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
        }

        private void OnDisable()
        {
            SteamMatchmaking.OnLobbyMemberJoined -= HandleMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= HandleMemberLeft;
            SteamMatchmaking.OnLobbyInvite -= HandleLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
        }

        /// <summary>
        /// Re-reads the lobby's match format (PvE/PvP) from its metadata so this client
        /// agrees with the host. Call whenever the lobby may have changed (after joining,
        /// on member changes, or before starting a match). Host changes are picked up the
        /// next time any member refreshes the lobby, and GameManager.Start re-reads the
        /// mode before the match begins.
        /// </summary>
        public void RefreshMatchMode()
        {
            if (CurrentLobby.HasValue)
                ApplyMatchMode(CurrentLobby.Value);
        }

        private void ApplyMatchMode(Lobby lobby)
        {
            string data = lobby.GetData(MatchModeKey);
            if (string.IsNullOrEmpty(data)) return;
            if (System.Enum.TryParse<MatchMode>(data, true, out MatchMode parsed) && parsed != MatchMode)
            {
                MatchMode = parsed;
                GameState.CurrentMatchMode = parsed;
                OnMatchModeChanged?.Invoke(parsed);
                Debug.Log($"[LobbyManager] Match mode is now {parsed}");
            }
        }

        /// <summary>
        /// Sets the lobby's match format (host-only). Before a lobby exists it just stores
        /// the choice so CreateLobby writes it; inside a lobby it is persisted as lobby
        /// data (Steam propagates it to every member) and the member limit is updated to
        /// match: 4 players for PvE, 8 for PvP.
        /// </summary>
        public void SetMatchMode(MatchMode mode)
        {
            MatchMode = mode;
            GameState.CurrentMatchMode = mode;
            if (CurrentLobby.HasValue)
            {
                var lobby = CurrentLobby.Value;
                lobby.SetData(MatchModeKey, mode.ToString());
                lobby.MaxMembers = MaxMembersForCurrentMode;
                CurrentLobby = lobby;
            }
            OnMatchModeChanged?.Invoke(mode);
            Debug.Log($"[LobbyManager] SetMatchMode to {mode}");
        }

        private void HandleMemberJoined(Lobby lobby, Friend friend)
        {
            Debug.Log($"[LobbyManager] Member joined: {friend.Name} ({friend.Id}) — lobby now has {lobby.MemberCount} members");
            RefreshMatchMode();
            OnMembersChanged?.Invoke();
        }

        private void HandleMemberLeft(Lobby lobby, Friend friend)
        {
            Debug.Log($"[LobbyManager] Member left: {friend.Name} ({friend.Id})");
            RefreshMatchMode();
            OnMembersChanged?.Invoke();
            OnLobbyMemberLeft?.Invoke(lobby, friend);
        }

        /// <summary>
        /// An invite was received. Do NOT auto-join: Steam shows the invite in the overlay
        /// and only once the player actually clicks "Play game" there does Steam fire
        /// OnGameLobbyJoinRequested, which is where we join.
        /// </summary>
        private void HandleLobbyInvite(Friend friend, Lobby lobby)
        {
            Debug.Log($"[LobbyManager] Invite received from {friend.Name} ({friend.Id}) for lobby {lobby.Id} — waiting for the player to accept in the overlay.");
        }

        /// <summary>
        /// Friend clicked "Join Game" on us in the Steam friends list / overlay.
        /// </summary>
        private void HandleGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            Debug.Log($"[LobbyManager] Join game requested by {friendId} for lobby {lobby.Id}");
            if (CurrentLobby.HasValue && CurrentLobby.Value.Id != lobby.Id)
                LeaveLobby();
            JoinLobby(lobby);
        }

        public async void CreateLobby(int maxMembers = 0)
        {
            if (maxMembers <= 0)
                maxMembers = MaxMembersForCurrentMode;
            var result = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
            if (result.HasValue)
            {
                CurrentLobby = result.Value;
                IsHost = true;
                CurrentLobby?.SetData("name", $"{SteamClient.Name}'s Game");
                CurrentLobby?.SetData("HostSteamId", SteamClient.SteamId.ToString());
                CurrentLobby?.SetData(MatchModeKey, MatchMode.ToString());
                CurrentLobby?.SetPublic();
                CurrentLobby?.SetJoinable(true);
                SteamFriends.SetRichPresence("connect", result.Value.Id.ToString());
                Debug.Log($"[LobbyManager] Lobby created: {CurrentLobby?.Id}");
                OnLobbyCreated?.Invoke(result.Value);
            }
            else
            {
                Debug.LogError("[LobbyManager] Failed to create lobby");
            }
        }

        public async void FindLobbies()
        {
var result = await SteamMatchmaking.LobbyList.RequestAsync();
        if (result != null)
        {
            var lobbies = new List<Lobby>(result);
            OnLobbyListReceived?.Invoke(lobbies);
        }
        }

        public async void JoinLobby(Lobby lobby)
        {
            var result = await lobby.Join();
            if (result == RoomEnter.Success)
            {
                CurrentLobby = lobby;
                IsHost = false;
                MatchMode = MatchMode.PvE;
                GameState.CurrentMatchMode = MatchMode.PvE;
                ApplyMatchMode(lobby);
                Debug.Log($"[LobbyManager] Joined lobby: {lobby.Id}");
                OnLobbyJoined?.Invoke(lobby);
            }
            else
            {
                Debug.LogError($"[LobbyManager] Failed to join lobby: {result}");
            }
        }

        public void LeaveLobby()
        {
            if (CurrentLobby.HasValue)
            {
                CurrentLobby?.Leave();
                CurrentLobby = null;
                IsHost = false;
                MatchMode = MatchMode.PvE;
                GameState.CurrentMatchMode = MatchMode.PvE;
                SteamFriends.ClearRichPresence();
                OnLobbyLeft?.Invoke();
            }
        }
    }
