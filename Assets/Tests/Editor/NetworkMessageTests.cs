using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Steamworks;
using Networking;

namespace Tests
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
                activeCharacterId = 7
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

            var d = decoded.characterStates[0];
            Assert.AreEqual(42, d.characterId);
            Assert.AreEqual(1, d.ownerPlayerId);
            Assert.AreEqual(0, d.teamId);
            Assert.AreEqual(2, d.q);
            Assert.AreEqual(-3, d.r);
            Assert.AreEqual(5, d.currentHp);
            Assert.AreEqual(10, d.maxHp);
            Assert.AreEqual(2, d.armor);
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
    }
}
