using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Game;

namespace Networking
{
    /// <summary>
    /// Host-authoritative ready-state tracker for lobby members. Used both in the lobby
    /// (before a match) and on the victory screen (before the next level).
    ///
    /// The host owns the state: clients send ReadyRequest messages, the host updates its
    /// dictionary and broadcasts a ReadyStateSync snapshot to everyone. Clients apply that
    /// snapshot. The host's own toggles are applied locally then broadcast.
    /// </summary>
    public class ReadyTracker : MonoBehaviour
    {
        private static ReadyTracker instance;

        public static event Action OnReadyStateChanged;

        public static ReadyTracker Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("ReadyTracker");
                    instance = go.AddComponent<ReadyTracker>();
                }
                return instance;
            }
        }

        private readonly Dictionary<int, bool> readyStates = new Dictionary<int, bool>();
        private bool eventsSubscribed;

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (instance == this) instance = null;
        }

        private void SubscribeEvents()
        {
            if (eventsSubscribed) return;
            eventsSubscribed = true;
            NetworkManager.OnReadyRequestReceived += HandleReadyRequest;
            NetworkManager.OnReadyStateReceived += HandleReadyStateReceived;
            LobbyManager.OnMembersChanged += HandleMembersChanged;
        }

        private void UnsubscribeEvents()
        {
            if (!eventsSubscribed) return;
            eventsSubscribed = false;
            NetworkManager.OnReadyRequestReceived -= HandleReadyRequest;
            NetworkManager.OnReadyStateReceived -= HandleReadyStateReceived;
            LobbyManager.OnMembersChanged -= HandleMembersChanged;
        }

        private void HandleReadyRequest(ulong senderSteamId, ReadyRequest request)
        {
            if (!LobbyManager.Instance.IsHost) return;
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (!lobby.HasValue) return;

            int idx = GameState.GetPlayerIndex(lobby.Value, senderSteamId);
            if (idx < 0) return;

            readyStates[idx] = request.ready;
            Broadcast();
        }

        private void HandleReadyStateReceived(ReadyStateSync sync)
        {
            readyStates.Clear();
            if (sync != null)
            {
                foreach (PlayerReadyState p in sync.players)
                    readyStates[p.playerIndex] = p.ready;
            }
            OnReadyStateChanged?.Invoke();
        }

        private void HandleMembersChanged()
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (!lobby.HasValue) return;

            lobby.Value.Refresh();
            foreach (Friend member in lobby.Value.Members)
            {
                int idx = GameState.GetPlayerIndex(lobby.Value, member.Id);
                if (idx >= 0 && !readyStates.ContainsKey(idx))
                    readyStates[idx] = false;
            }

            if (LobbyManager.Instance.IsHost)
                Broadcast();
            else
                OnReadyStateChanged?.Invoke();
        }

        /// <summary>
        /// Sets this machine's player ready state. The host applies and broadcasts immediately;
        /// clients optimistically reflect the change locally, then notify the host.
        /// </summary>
        public void SetLocalReady(bool ready)
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (!lobby.HasValue) return;

            int idx = GameState.GetPlayerIndex(lobby.Value, SteamClient.SteamId);
            if (idx < 0) return;

            readyStates[idx] = ready;
            OnReadyStateChanged?.Invoke();

            if (LobbyManager.Instance.IsHost)
                Broadcast();
            else
                NetworkManager.Instance.SendReadyRequest(ready);
        }

        public bool IsReady(int playerIndex)
        {
            return readyStates.TryGetValue(playerIndex, out bool ready) && ready;
        }

        public bool AllReady
        {
            get
            {
                var lobby = LobbyManager.Instance.CurrentLobby;
                if (!lobby.HasValue) return false;

                lobby.Value.Refresh();
                int count = 0;
                foreach (Friend member in lobby.Value.Members)
                {
                    int idx = GameState.GetPlayerIndex(lobby.Value, member.Id);
                    if (idx < 0 || !readyStates.TryGetValue(idx, out bool ready) || !ready)
                        return false;
                    count++;
                }
                return count > 0;
            }
        }

        /// <summary>
        /// Snapshot of every lobby member's ready state, sorted by player index, coloured
        /// via PlayerPalette so names match character colours.
        /// </summary>
        public List<PlayerReadyState> GetPlayers()
        {
            var result = new List<PlayerReadyState>();
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (!lobby.HasValue) return result;

            lobby.Value.Refresh();
            foreach (Friend member in lobby.Value.Members)
            {
                int idx = GameState.GetPlayerIndex(lobby.Value, member.Id);
                if (idx < 0) continue;

                var color = PlayerPalette.GetColor(idx);
                result.Add(new PlayerReadyState
                {
                    playerIndex = idx,
                    name = member.Name,
                    ready = readyStates.TryGetValue(idx, out bool ready) && ready,
                    colorR = color.r,
                    colorG = color.g,
                    colorB = color.b
                });
            }
            result.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
            return result;
        }

        /// <summary>
        /// Clears the current ready round and starts everyone as not ready. The host
        /// broadcasts the fresh state; clients just clear locally and follow the host.
        /// </summary>
        public void Reset()
        {
            readyStates.Clear();
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (lobby.HasValue)
            {
                lobby.Value.Refresh();
                foreach (Friend member in lobby.Value.Members)
                {
                    int idx = GameState.GetPlayerIndex(lobby.Value, member.Id);
                    if (idx >= 0) readyStates[idx] = false;
                }
            }

            if (LobbyManager.Instance.IsHost)
                Broadcast();
            else
                OnReadyStateChanged?.Invoke();
        }

        private void Broadcast()
        {
            var sync = new ReadyStateSync { players = GetPlayers() };
            NetworkManager.Instance.BroadcastReadyState(sync);
            OnReadyStateChanged?.Invoke();
        }
    }
}
