using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace Steam
{
    public class LobbyManager : MonoBehaviour
    {
        private static LobbyManager instance;

        public Lobby? CurrentLobby { get; private set; }
        public bool IsHost { get; private set; }

        public static event Action<Lobby> OnLobbyCreated;
        public static event Action<Lobby> OnLobbyJoined;
        public static event Action OnLobbyLeft;
        public static event Action<List<Lobby>> OnLobbyListReceived;

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
        }

        public async void CreateLobby(int maxMembers = 4)
        {
            var result = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
            if (result.HasValue)
            {
                CurrentLobby = result.Value;
                IsHost = true;
                CurrentLobby?.SetData("name", $"{SteamClient.Name}'s Game");
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
            if (result.HasValue)
            {
                var lobbies = new List<Lobby>(result.Value);
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
}
