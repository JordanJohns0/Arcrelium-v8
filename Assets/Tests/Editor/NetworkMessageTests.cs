using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Steamworks;
using Networking;

namespace Tests.Network
{
    [TestFixture]
    public class NetworkMessageTests
    {
        [Test]
        public void GameStateSync_RoundTripsAllFields()
        {
            var sync = new GameStateSync
            {
                currentTurnIndex = 3,
                activeCharacterId = 7,
                victory = true
            };
            sync.characterStates.Add(new CharacterState
            {
                characterId = 42,
                ownerPlayerId = 1,
                teamId = 0,
                q = 2,
                r = -3,
                currentHp = 5,
                maxHp = 10,
                armor = 2,
                block = 3,
                isAlive = true,
                name = "Alice",
                colorR = 0.2f,
                colorG = 0.55f,
                colorB = 0.9f,
                handCardNames = new[] { "Slash", "Defend" }
            });

            string json = JsonUtility.ToJson(sync);
            var decoded = JsonUtility.FromJson<GameStateSync>(json);

            Assert.AreEqual(1, decoded.characterStates.Count);
            Assert.AreEqual(3, decoded.currentTurnIndex);
            Assert.AreEqual(7, decoded.activeCharacterId);
            Assert.IsTrue(decoded.victory);

            var d = decoded.characterStates[0];
            Assert.AreEqual(42, d.characterId);
            Assert.AreEqual(1, d.ownerPlayerId);
            Assert.AreEqual(0, d.teamId);
            Assert.AreEqual(2, d.q);
            Assert.AreEqual(-3, d.r);
            Assert.AreEqual(5, d.currentHp);
            Assert.AreEqual(10, d.maxHp);
            Assert.AreEqual(2, d.armor);
            Assert.AreEqual(3, d.block);
            Assert.IsTrue(d.isAlive);
            Assert.AreEqual("Alice", d.name);
            Assert.AreEqual(0.2f, d.colorR, 0.0001f);
            Assert.AreEqual(0.55f, d.colorG, 0.0001f);
            Assert.AreEqual(0.9f, d.colorB, 0.0001f);
            Assert.AreEqual(2, d.handCardNames.Length);
            Assert.AreEqual("Slash", d.handCardNames[0]);
            Assert.AreEqual("Defend", d.handCardNames[1]);
        }

        [Test]
        public void PlayCardRequest_RoundTripsDefaults()
        {
            string json = JsonUtility.ToJson(new PlayCardRequest());
            var decoded = JsonUtility.FromJson<PlayCardRequest>(json);

            Assert.AreEqual(-1, decoded.cardHandIndex);
            Assert.AreEqual(0, decoded.costCardHandIndices.Count);
            Assert.IsFalse(decoded.hasBoardTarget);
            Assert.AreEqual(0, decoded.targetQ);
            Assert.AreEqual(0, decoded.targetR);
            Assert.AreEqual(-1, decoded.targetOwnerPlayerId);
        }

        [Test]
        public void PlayCardRequest_RoundTripsPopulated()
        {
            var request = new PlayCardRequest
            {
                cardHandIndex = 2,
                costCardHandIndices = new List<int> { 0, 1 },
                hasBoardTarget = true,
                targetQ = 1,
                targetR = -2,
                targetOwnerPlayerId = 1
            };

            string json = JsonUtility.ToJson(request);
            var decoded = JsonUtility.FromJson<PlayCardRequest>(json);

            Assert.AreEqual(2, decoded.cardHandIndex);
            Assert.AreEqual(2, decoded.costCardHandIndices.Count);
            Assert.AreEqual(0, decoded.costCardHandIndices[0]);
            Assert.AreEqual(1, decoded.costCardHandIndices[1]);
            Assert.IsTrue(decoded.hasBoardTarget);
            Assert.AreEqual(1, decoded.targetQ);
            Assert.AreEqual(-2, decoded.targetR);
            Assert.AreEqual(1, decoded.targetOwnerPlayerId);
        }

        [Test]
        public void MessageTypes_HaveUniqueByteValues()
        {
            var seen = new HashSet<byte>();
            foreach (MessageType type in Enum.GetValues(typeof(MessageType)))
            {
                byte value = (byte)type;
                Assert.IsTrue(seen.Add(value), $"Duplicate MessageType value {value} for {type}");
            }
        }

        [Test]
        public void HandleMessage_GameStateSync_RaisesEvent()
        {
            var sync = new GameStateSync { currentTurnIndex = 2 };
            sync.characterStates.Add(new CharacterState { name = "Bob" });
            string json = JsonUtility.ToJson(sync);

            GameStateSync received = null;
            Action<GameStateSync> handler = (s) => received = s;
            NetworkManager.OnGameStateReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)123ul, MessageType.GameStateSync, json);
            }
            finally
            {
                NetworkManager.OnGameStateReceived -= handler;
            }

            Assert.IsNotNull(received);
            Assert.AreEqual(2, received.currentTurnIndex);
            Assert.AreEqual(1, received.characterStates.Count);
            Assert.AreEqual("Bob", received.characterStates[0].name);
        }

        [Test]
        public void HandleMessage_PlayCardRequest_RaisesEventWithSenderAndPayload()
        {
            string json = JsonUtility.ToJson(new PlayCardRequest { cardHandIndex = 1, targetOwnerPlayerId = 2 });

            ulong sender = 0;
            PlayCardRequest received = null;
            Action<ulong, PlayCardRequest> handler = (s, r) => { sender = s; received = r; };
            NetworkManager.OnPlayCardRequestReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)123ul, MessageType.PlayCardRequest, json);
            }
            finally
            {
                NetworkManager.OnPlayCardRequestReceived -= handler;
            }

            Assert.AreEqual(123ul, sender);
            Assert.AreEqual(1, received.cardHandIndex);
            Assert.AreEqual(2, received.targetOwnerPlayerId);
        }

        [Test]
        public void HandleMessage_PassRequest_RaisesEventWithSender()
        {
            ulong sender = 0;
            Action<ulong> handler = (s) => sender = s;
            NetworkManager.OnPassRequestReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)456ul, MessageType.PassRequest, "{}");
            }
            finally
            {
                NetworkManager.OnPassRequestReceived -= handler;
            }

            Assert.AreEqual(456ul, sender);
        }

        [Test]
        public void HandleMessage_StartGame_RaisesEvent()
        {
            bool fired = false;
            Action handler = () => fired = true;
            NetworkManager.OnStartGameReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)0ul, MessageType.StartGame, "{}");
            }
            finally
            {
                NetworkManager.OnStartGameReceived -= handler;
            }

            Assert.IsTrue(fired);
        }

        [Test]
        public void HandleMessage_RequestInitialState_RaisesEvent()
        {
            bool fired = false;
            Action handler = () => fired = true;
            NetworkManager.OnRequestInitialStateReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)0ul, MessageType.RequestInitialState, "{}");
            }
            finally
            {
                NetworkManager.OnRequestInitialStateReceived -= handler;
            }

            Assert.IsTrue(fired);
        }

        [Test]
        public void ReadyRequest_RoundTrips()
        {
            string json = JsonUtility.ToJson(new ReadyRequest { ready = true });
            var decoded = JsonUtility.FromJson<ReadyRequest>(json);

            Assert.IsTrue(decoded.ready);
        }

        [Test]
        public void ReadyStateSync_RoundTripsPlayers()
        {
            var sync = new ReadyStateSync();
            sync.players.Add(new PlayerReadyState
            {
                playerIndex = 1,
                name = "Alice",
                ready = true,
                colorR = 0.2f,
                colorG = 0.5f,
                colorB = 0.9f
            });
            sync.players.Add(new PlayerReadyState
            {
                playerIndex = 0,
                name = "Bob",
                ready = false,
                colorR = 1f,
                colorG = 1f,
                colorB = 1f
            });

            string json = JsonUtility.ToJson(sync);
            var decoded = JsonUtility.FromJson<ReadyStateSync>(json);

            Assert.AreEqual(2, decoded.players.Count);
            Assert.AreEqual(1, decoded.players[0].playerIndex);
            Assert.AreEqual("Alice", decoded.players[0].name);
            Assert.IsTrue(decoded.players[0].ready);
            Assert.AreEqual(0.2f, decoded.players[0].colorR, 0.0001f);
            Assert.AreEqual(0.5f, decoded.players[0].colorG, 0.0001f);
            Assert.AreEqual(0.9f, decoded.players[0].colorB, 0.0001f);

            Assert.AreEqual(0, decoded.players[1].playerIndex);
            Assert.AreEqual("Bob", decoded.players[1].name);
            Assert.IsFalse(decoded.players[1].ready);
            Assert.AreEqual(1f, decoded.players[1].colorR, 0.0001f);
            Assert.AreEqual(1f, decoded.players[1].colorG, 0.0001f);
            Assert.AreEqual(1f, decoded.players[1].colorB, 0.0001f);
        }

        [Test]
        public void HandleMessage_ReadyRequest_RaisesEventWithSenderAndPayload()
        {
            string json = JsonUtility.ToJson(new ReadyRequest { ready = true });

            ulong sender = 0;
            ReadyRequest received = null;
            Action<ulong, ReadyRequest> handler = (s, r) => { sender = s; received = r; };
            NetworkManager.OnReadyRequestReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)123ul, MessageType.ReadyRequest, json);
            }
            finally
            {
                NetworkManager.OnReadyRequestReceived -= handler;
            }

            Assert.AreEqual(123ul, sender);
            Assert.IsNotNull(received);
            Assert.IsTrue(received.ready);
        }

        [Test]
        public void HandleMessage_ReadyStateSync_RaisesEvent()
        {
            var sync = new ReadyStateSync();
            sync.players.Add(new PlayerReadyState { playerIndex = 0, name = "Bob", ready = true });
            string json = JsonUtility.ToJson(sync);

            ReadyStateSync received = null;
            Action<ReadyStateSync> handler = (s) => received = s;
            NetworkManager.OnReadyStateReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)0ul, MessageType.ReadyStateSync, json);
            }
            finally
            {
                NetworkManager.OnReadyStateReceived -= handler;
            }

            Assert.IsNotNull(received);
            Assert.AreEqual(1, received.players.Count);
            Assert.AreEqual("Bob", received.players[0].name);
            Assert.IsTrue(received.players[0].ready);
        }

        [Test]
        public void HandleMessage_LoadNextLevel_RaisesEvent()
        {
            bool fired = false;
            Action handler = () => fired = true;
            NetworkManager.OnLoadNextLevelReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)0ul, MessageType.LoadNextLevel, "{}");
            }
            finally
            {
                NetworkManager.OnLoadNextLevelReceived -= handler;
            }

            Assert.IsTrue(fired);
        }

        [Test]
        public void LoadoutSync_RoundTripsNames()
        {
            var sync = new LoadoutSync
            {
                playerIndex = 1,
                loadoutNames = new[] { "Helm", "Plate", "Boots", "Sword", "Shield", "Ring 1", "Ring 2", "Amulet" }
            };

            string json = JsonUtility.ToJson(sync);
            var decoded = JsonUtility.FromJson<LoadoutSync>(json);

            Assert.AreEqual(1, decoded.playerIndex);
            Assert.AreEqual(8, decoded.loadoutNames.Length);
            Assert.AreEqual("Helm", decoded.loadoutNames[0]);
            Assert.AreEqual("Amulet", decoded.loadoutNames[7]);
        }

        [Test]
        public void HandleMessage_LoadoutSync_RaisesEventWithSenderAndPayload()
        {
            string json = JsonUtility.ToJson(new LoadoutSync
            {
                playerIndex = 2,
                loadoutNames = new[] { "Helm", "", "", "", "", "", "", "" }
            });

            ulong sender = 0;
            LoadoutSync received = null;
            Action<ulong, LoadoutSync> handler = (s, sync) => { sender = s; received = sync; };
            NetworkManager.OnLoadoutSyncReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)456ul, MessageType.LoadoutSync, json);
            }
            finally
            {
                NetworkManager.OnLoadoutSyncReceived -= handler;
            }

            Assert.AreEqual(456ul, sender);
            Assert.IsNotNull(received);
            Assert.AreEqual(2, received.playerIndex);
            Assert.AreEqual("Helm", received.loadoutNames[0]);
        }

        [Test]
        public void CardPlayedDisplay_RoundTrips()
        {
            var display = new CardPlayedDisplay
            {
                cardName = "Slash",
                actorName = "Alice",
                actorPlayerId = 1
            };

            string json = JsonUtility.ToJson(display);
            var decoded = JsonUtility.FromJson<CardPlayedDisplay>(json);

            Assert.AreEqual("Slash", decoded.cardName);
            Assert.AreEqual("Alice", decoded.actorName);
            Assert.AreEqual(1, decoded.actorPlayerId);
        }

        [Test]
        public void HandleMessage_CardPlayedDisplay_RaisesEvent()
        {
            string json = JsonUtility.ToJson(new CardPlayedDisplay
            {
                cardName = "Slash",
                actorName = "Bob",
                actorPlayerId = 0
            });

            CardPlayedDisplay received = null;
            Action<CardPlayedDisplay> handler = (d) => received = d;
            NetworkManager.OnCardPlayedDisplayReceived += handler;
            try
            {
                NetworkManager.HandleMessage((SteamId)123ul, MessageType.CardPlayedDisplay, json);
            }
            finally
            {
                NetworkManager.OnCardPlayedDisplayReceived -= handler;
            }

            Assert.IsNotNull(received);
            Assert.AreEqual("Slash", received.cardName);
            Assert.AreEqual("Bob", received.actorName);
            Assert.AreEqual(0, received.actorPlayerId);
        }
    }
}
