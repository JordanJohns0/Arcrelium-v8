using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Networking
{
    public class NetworkManager : MonoBehaviour
    {
        private static NetworkManager instance;

        public bool IsHost { get; private set; }
        public ulong HostSteamId { get; private set; }

        public static event Action<GameStateSync> OnGameStateReceived;
        public static event Action<ulong, PlayCardRequest> OnPlayCardRequestReceived;
        public static event Action<ulong> OnPassRequestReceived;
        public static event Action OnStartGameReceived;
        public static event Action OnRequestInitialStateReceived;
        public static event Action<ulong, ReadyRequest> OnReadyRequestReceived;
        public static event Action<ReadyStateSync> OnReadyStateReceived;
        public static event Action OnLoadNextLevelReceived;
        public static event Action<ulong, LoadoutSync> OnLoadoutSyncReceived;
        public static event Action<CardPlayedDisplay> OnCardPlayedDisplayReceived;
        public static event Action<ulong, StageVoteRequest> OnStageVoteRequestReceived;
        public static event Action<StageVoteState> OnStageVoteStateReceived;

        public static NetworkManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("NetworkManager");
                    instance = go.AddComponent<NetworkManager>();
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

            // Safety net for sessions that skip SteamManager: an unfocused host must keep
            // processing P2P traffic or clients hang.
            Application.runInBackground = true;

            SteamNetworking.OnP2PSessionRequest = OnP2PSessionRequest;
        }

        private static void OnP2PSessionRequest(SteamId remoteId)
        {
            Debug.Log($"[NetworkManager] P2P session request from {remoteId}, accepting");
            SteamNetworking.AcceptP2PSessionWithUser(remoteId);
        }

        public void StartHost()
        {
            IsHost = true;
            HostSteamId = SteamClient.SteamId;
            Debug.Log("[NetworkManager] Started as host");
        }

        public void StartClient(ulong hostSteamId)
        {
            IsHost = false;
            HostSteamId = hostSteamId;
            Debug.Log($"[NetworkManager] Started as client, host: {hostSteamId}");
        }

        public void BroadcastGameState(GameStateSync state)
        {
            if (!IsHost) return;
            string json = JsonUtility.ToJson(state);
            SendToAllClients(MessageType.GameStateSync, json);
        }

        public void SendPlayCardRequest(PlayCardRequest request)
        {
            string json = JsonUtility.ToJson(request);
            SendToHost(MessageType.PlayCardRequest, json);
        }

        public void SendPassRequest()
        {
            SendToHost(MessageType.PassRequest, "{}");
        }

        public void SendStartGame()
        {
            Debug.Log("[NetworkManager] Sending StartGame to all clients");
            SendToAllClients(MessageType.StartGame, "{}");
        }

        public void SendRequestInitialState()
        {
            Debug.Log("[NetworkManager] Sending RequestInitialState to host");
            SendToHost(MessageType.RequestInitialState, "{}");
        }

        public void SendReadyRequest(bool ready)
        {
            string json = JsonUtility.ToJson(new ReadyRequest { ready = ready });
            SendToHost(MessageType.ReadyRequest, json);
        }

        public void BroadcastReadyState(ReadyStateSync state)
        {
            if (!IsHost) return;
            string json = JsonUtility.ToJson(state);
            SendToAllClients(MessageType.ReadyStateSync, json);
        }

        public void SendLoadNextLevel()
        {
            SendToAllClients(MessageType.LoadNextLevel, "{}");
        }

        public void SendLoadoutSync(LoadoutSync loadout)
        {
            string json = JsonUtility.ToJson(loadout);
            SendToHost(MessageType.LoadoutSync, json);
        }

        /// <summary>
        /// Host -> all clients: a card was just played and resolved. Clients show the card
        /// on screen so everyone sees what was played. The host shows it locally itself.
        /// </summary>
        public void BroadcastCardPlayed(CardPlayedDisplay display)
        {
            if (!IsHost) return;
            if (LobbyManager.Instance.CurrentLobby == null) return;
            string json = JsonUtility.ToJson(display);
            SendToAllClients(MessageType.CardPlayedDisplay, json);
        }

        /// <summary>Client -> host: this player's vote for a stage option.</summary>
        public void SendStageVoteRequest(int option)
        {
            string json = JsonUtility.ToJson(new StageVoteRequest { option = option });
            SendToHost(MessageType.StageVoteRequest, json);
        }

        /// <summary>Host -> all clients: the current state of the stage vote.</summary>
        public void BroadcastStageVote(StageVoteState state)
        {
            if (!IsHost) return;
            if (LobbyManager.Instance.CurrentLobby == null) return;
            string json = JsonUtility.ToJson(state);
            SendToAllClients(MessageType.StageVoteState, json);
        }

        private void SendToHost(MessageType type, string json)
        {
            byte[] header = { (byte)type };
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            byte[] packet = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, packet, 0, header.Length);
            Buffer.BlockCopy(body, 0, packet, header.Length, body.Length);

            SteamNetworking.SendP2PPacket(HostSteamId, packet);
        }

        private void SendToAllClients(MessageType type, string json)
        {
            if (LobbyManager.Instance.CurrentLobby == null)
            {
                Debug.LogWarning("[NetworkManager] SendToAllClients: no current lobby");
                return;
            }
            byte[] header = { (byte)type };
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            byte[] packet = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, packet, 0, header.Length);
            Buffer.BlockCopy(body, 0, packet, header.Length, body.Length);

            int sent = 0;
            foreach (var member in LobbyManager.Instance.CurrentLobby?.Members)
            {
                if (member.Id == SteamClient.SteamId) continue;
                SteamNetworking.SendP2PPacket(member.Id, packet);
                sent++;
            }
            Debug.Log($"[NetworkManager] SendToAllClients {type}: sent to {sent} members");
        }

        private void Update()
        {
            if (!SteamClient.IsValid) return;
            ProcessIncomingPackets();
        }

        private void ProcessIncomingPackets()
        {
            while (SteamNetworking.IsP2PPacketAvailable())
            {
                var result = SteamNetworking.ReadP2PPacket();
                if (!result.HasValue) continue;

                var sender = result.Value.SteamId;
                var data = result.Value.Data;
                if (data.Length == 0) continue;

                MessageType type = (MessageType)data[0];
                string json = System.Text.Encoding.UTF8.GetString(data, 1, data.Length - 1);

                Debug.Log($"[NetworkManager] Received {type} from {sender}");
                HandleMessage(sender, type, json);
            }
        }

        internal static void HandleMessage(SteamId sender, MessageType type, string json)
        {
            switch (type)
            {
                case MessageType.GameStateSync:
                    var state = JsonUtility.FromJson<GameStateSync>(json);
                    OnGameStateReceived?.Invoke(state);
                    break;

                case MessageType.PlayCardRequest:
                    var request = JsonUtility.FromJson<PlayCardRequest>(json);
                    OnPlayCardRequestReceived?.Invoke(sender.Value, request);
                    break;

                case MessageType.PassRequest:
                    OnPassRequestReceived?.Invoke(sender.Value);
                    break;

                case MessageType.StartGame:
                    OnStartGameReceived?.Invoke();
                    break;

                case MessageType.RequestInitialState:
                    OnRequestInitialStateReceived?.Invoke();
                    break;

                case MessageType.ReadyRequest:
                    var readyRequest = JsonUtility.FromJson<ReadyRequest>(json);
                    OnReadyRequestReceived?.Invoke(sender.Value, readyRequest);
                    break;

                case MessageType.ReadyStateSync:
                    var readySync = JsonUtility.FromJson<ReadyStateSync>(json);
                    OnReadyStateReceived?.Invoke(readySync);
                    break;

                case MessageType.LoadNextLevel:
                    OnLoadNextLevelReceived?.Invoke();
                    break;

                case MessageType.LoadoutSync:
                    var loadoutSync = JsonUtility.FromJson<LoadoutSync>(json);
                    OnLoadoutSyncReceived?.Invoke(sender.Value, loadoutSync);
                    break;

                case MessageType.CardPlayedDisplay:
                    var cardDisplay = JsonUtility.FromJson<CardPlayedDisplay>(json);
                    OnCardPlayedDisplayReceived?.Invoke(cardDisplay);
                    break;

                case MessageType.StageVoteRequest:
                    var voteRequest = JsonUtility.FromJson<StageVoteRequest>(json);
                    OnStageVoteRequestReceived?.Invoke(sender.Value, voteRequest);
                    break;

                case MessageType.StageVoteState:
                    var voteState = JsonUtility.FromJson<StageVoteState>(json);
                    OnStageVoteStateReceived?.Invoke(voteState);
                    break;
            }
        }
    }
}
