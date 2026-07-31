using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

public class LobbyManager : MonoBehaviour
    {
        private static LobbyManager instance;

        public Lobby? CurrentLobby { get; private set; }
        public bool IsHost { get; private set; }

        public static event Action<Lobby> OnLobbyCreated;
        public static event Action<Lobby> OnLobbyJoined;
        public static event Action OnLobbyLeft;
        public static event Action<List<Lobby>> OnLobbyListReceived;
        public static event Action OnMembersChanged;

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
        }

        private void OnDisable()
        {
            SteamMatchmaking.OnLobbyMemberJoined -= HandleMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= HandleMemberLeft;
        }

        private void HandleMemberJoined(Lobby lobby, Friend friend)
        {
            Debug.Log($"[LobbyManager] Member joined: {friend.Name} ({friend.Id}) — lobby now has {lobby.MemberCount} members");
            OnMembersChanged?.Invoke();
        }

        private void HandleMemberLeft(Lobby lobby, Friend friend)
        {
            Debug.Log($"[LobbyManager] Member left: {friend.Name} ({friend.Id})");
            OnMembersChanged?.Invoke();
        }

        public async void CreateLobby(int maxMembers = 4)
        {
            var result = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
            if (result.HasValue)
            {
                CurrentLobby = result.Value;
                IsHost = true;
                CurrentLobby?.SetData("name", $"{SteamClient.Name}'s Game");
                CurrentLobby?.SetData("HostSteamId", SteamClient.SteamId.ToString());
                CurrentLobby?.SetPublic();
                CurrentLobby?.SetJoinable(true);
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
                OnLobbyLeft?.Invoke();
            }
        }
    }
