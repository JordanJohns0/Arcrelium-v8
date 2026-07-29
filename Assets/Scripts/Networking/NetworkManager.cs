using System;
using System.Collections.Generic;
using Steamworks;
using Steam;
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
        }

        public void StartHost()
        {
            IsHost = true;
            HostSteamId = Steam.SteamManager.Instance.SteamId;
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

        private void SendToHost(MessageType type, string json)
        {
            byte[] header = { (byte)type };
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            byte[] packet = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, packet, 0, header.Length);
            Buffer.BlockCopy(body, 0, packet, header.Length, body.Length);

            SteamNetworking.SendP2PPacket(new SteamId(HostSteamId), packet);
        }

        private void SendToAllClients(MessageType type, string json)
        {
            if (LobbyManager.Instance.CurrentLobby == null) return;
            byte[] header = { (byte)type };
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            byte[] packet = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, packet, 0, header.Length);
            Buffer.BlockCopy(body, 0, packet, header.Length, body.Length);

            foreach (var member in LobbyManager.Instance.CurrentLobby?.Members)
            {
                if (member == Steam.SteamManager.Instance.SteamId) continue;
                SteamNetworking.SendP2PPacket(member, packet);
            }
        }

        private void Update()
        {
            if (!Steam.SteamManager.Instance.IsInitialized) return;
            ProcessIncomingPackets();
        }

        private void ProcessIncomingPackets()
        {
            while (SteamNetworking.IsP2PPacketAvailable())
            {
                var result = SteamNetworking.ReadP2PPacket();
                if (!result.HasValue) continue;

                var (sender, data) = result.Value;
                if (data.Length == 0) continue;

                MessageType type = (MessageType)data[0];
                string json = System.Text.Encoding.UTF8.GetString(data, 1, data.Length - 1);

                HandleMessage(sender, type, json);
            }
        }

        private void HandleMessage(SteamId sender, MessageType type, string json)
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
            }
        }
    }
}
