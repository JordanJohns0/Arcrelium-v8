using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cards;
using Cards.Effects;
using Characters;
using HexGrid;
using Game;
using Networking;

namespace Tests.PlayMode
{
    [TestFixture]
    public class GameManagerMultiplayerTests
    {
        private GameManager manager;
        private TurnManager turnManager;
        private ActionResolver actionResolver;
        private HexGridManager grid;
        private Character player;
        private Character enemy;
        private readonly List<Character> tracked = new List<Character>();

        private static readonly FieldInfo deckField = typeof(Character).GetField("deck", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo cellsField = typeof(HexGridManager).GetField("cells", BindingFlags.NonPublic | BindingFlags.Instance);

        private static HexCell CreateCell(int q, int r)
        {
            var go = new GameObject($"Cell {q},{r}");
            var cell = go.AddComponent<HexCell>();
            cell.Initialize(new HexCoordinates(q, r));
            return cell;
        }

        private static Character CreateCharacter(int ownerId, int q, int r, int teamId)
        {
            var cell = CreateCell(q, r);
            var go = new GameObject($"Character {ownerId}");
            var character = go.AddComponent<Character>();

            var deck = new Deck();
            deckField.SetValue(character, deck);

            character.Initialize(ownerId, cell, 0, teamId);
            return character;
        }

        private static HexGridManager CreateGrid(int radius)
        {
            var gridGo = new GameObject("TestGrid");
            var grid = gridGo.AddComponent<HexGridManager>();

            var cells = (Dictionary<HexCoordinates, HexCell>)cellsField.GetValue(grid);

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    var coords = new HexCoordinates(q, r);
                    var cell = CreateCell(q, r);
                    cell.transform.SetParent(gridGo.transform);
                    cells[coords] = cell;
                }
            }

            return grid;
        }

        private static CardData MakeCard(string name, TargetType targetType, int cost, CardEffectSO effect)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = name;
            data.TargetType = targetType;
            data.Cost = cost;
            data.Effect = effect;
            return data;
        }

        private static CardData SelfCard(string name, int cost = 0)
        {
            var effect = ScriptableObject.CreateInstance<GainBlockEffect>();
            effect.Amount = CardValue.MakeConstant(1);
            return MakeCard(name, TargetType.Self, cost, effect);
        }

        private static CardData AttackCard(string name, int damage)
        {
            var effect = ScriptableObject.CreateInstance<AttackEffect>();
            effect.Damage = CardValue.MakeConstant(damage);
            var data = MakeCard(name, TargetType.EnemyCharacter, 0, effect);
            data.Range = 5;
            return data;
        }

        [SetUp]
        public void SetUp()
        {
            GameState.ActiveMode = GameMode.SinglePlayer;
            GameState.LocalPlayerId = 0;
            GameState.CurrentLevel = 1;
            GameManager.PlayerIdLookup = id => 0;

            var gmGo = new GameObject("GameManager");
            manager = gmGo.AddComponent<GameManager>();
            gmGo.SetActive(false);

            var tmGo = new GameObject("TurnManager");
            turnManager = tmGo.AddComponent<TurnManager>();

            var arGo = new GameObject("ActionResolver");
            actionResolver = arGo.AddComponent<ActionResolver>();

            grid = CreateGrid(2);

            typeof(TurnManager).GetField("actionResolver", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(turnManager, actionResolver);
            typeof(ActionResolver).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(actionResolver, grid);

            typeof(GameManager).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, grid);
            typeof(GameManager).GetField("turnManager", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, turnManager);
            var playerPrefab = CreateCharacter(0, 0, 0, 0);
            var enemyPrefab = CreateCharacter(100, 0, 0, 1);
            tracked.Add(playerPrefab);
            tracked.Add(enemyPrefab);
            typeof(GameManager).GetField("playerCharacterPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, playerPrefab);
            typeof(GameManager).GetField("enemyCharacterPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, enemyPrefab);
        }

        private void SetUpPlayersAndStartMatch()
        {
            player = CreateCharacter(0, 0, 0, 0);
            enemy = CreateCharacter(1, 1, 0, 1);
            tracked.Add(player);
            tracked.Add(enemy);
            manager.activeCharacters.AddRange(new[] { player, enemy });
            turnManager.SetTurnOrder(new List<Character>(manager.activeCharacters));
            turnManager.StartMatch();
        }

        private static void DestroyCharacter(Character c)
        {
            if (c == null) return;
            if (c.CurrentCell != null)
                Object.DestroyImmediate(c.CurrentCell.gameObject);
            Object.DestroyImmediate(c.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            GameManager.PlayerIdLookup = null;

            foreach (var c in tracked)
                DestroyCharacter(c);
            tracked.Clear();

            if (manager != null)
            {
                foreach (var c in new List<Character>(manager.activeCharacters))
                    DestroyCharacter(c);
                manager.activeCharacters.Clear();
                Object.DestroyImmediate(manager.gameObject);
            }
            if (turnManager != null)
                Object.DestroyImmediate(turnManager.gameObject);
            if (actionResolver != null)
                Object.DestroyImmediate(actionResolver.gameObject);
            if (grid != null)
                Object.DestroyImmediate(grid.gameObject);
            if (NetworkManager.Instance != null)
                Object.DestroyImmediate(NetworkManager.Instance.gameObject);
        }

        [Test]
        public void HandlePlayCardRequest_ValidPlay_RemovesCardFromHand()
        {
            SetUpPlayersAndStartMatch();
            var card = new CardInstance(SelfCard("Defend"));
            player.Hand.AddCard(card);

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest { cardHandIndex = 0 });

            Assert.AreEqual(0, player.Hand.Cards.Count);
            Assert.AreEqual(player, turnManager.ActiveCharacter);
        }

        [Test]
        public void HandlePlayCardRequest_OutOfRangeIndex_Rejected()
        {
            SetUpPlayersAndStartMatch();
            var card = new CardInstance(SelfCard("Defend"));
            player.Hand.AddCard(card);

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest { cardHandIndex = 5 });

            Assert.AreEqual(1, player.Hand.Cards.Count);
        }

        [Test]
        public void HandlePlayCardRequest_FromNonActiveCharacter_Rejected()
        {
            SetUpPlayersAndStartMatch();
            GameManager.PlayerIdLookup = id => 1;

            var card = new CardInstance(SelfCard("Defend"));
            enemy.Hand.AddCard(card);

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest { cardHandIndex = 0 });

            Assert.AreEqual(1, enemy.Hand.Cards.Count);
        }

        [Test]
        public void HandlePlayCardRequest_UnknownSender_Rejected()
        {
            SetUpPlayersAndStartMatch();
            GameManager.PlayerIdLookup = id => -1;

            var card = new CardInstance(SelfCard("Defend"));
            player.Hand.AddCard(card);

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest { cardHandIndex = 0 });

            Assert.AreEqual(1, player.Hand.Cards.Count);
        }

        [Test]
        public void HandlePlayCardRequest_WithCostCards_PaysCost()
        {
            SetUpPlayersAndStartMatch();
            var toPlay = new CardInstance(SelfCard("Slash", cost: 1));
            var costCard = new CardInstance(SelfCard("Defend"));
            player.Hand.AddCard(toPlay);
            player.Hand.AddCard(costCard);

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest
            {
                cardHandIndex = 0,
                costCardHandIndices = new List<int> { 1 }
            });

            Assert.AreEqual(0, player.Hand.Cards.Count);
            Assert.Greater(player.Stats.Block, 0);
        }

        [Test]
        public void HandlePlayCardRequest_CostIndexEqualToPlayedCard_Skipped()
        {
            SetUpPlayersAndStartMatch();
            var toPlay = new CardInstance(SelfCard("Slash", cost: 1));
            var costCard = new CardInstance(SelfCard("Defend"));
            player.Hand.AddCard(toPlay);
            player.Hand.AddCard(costCard);

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest
            {
                cardHandIndex = 0,
                costCardHandIndices = new List<int> { 0 }
            });

            Assert.AreEqual(2, player.Hand.Cards.Count);
            Assert.AreEqual(0, player.Stats.Block);
        }

        [Test]
        public void HandlePlayCardRequest_ResolvesTargetByOwnerPlayerId()
        {
            SetUpPlayersAndStartMatch();
            int enemyHpBefore = enemy.Stats.CurrentHealth;
            player.Hand.AddCard(new CardInstance(AttackCard("Strike", 2)));

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest
            {
                cardHandIndex = 0,
                hasBoardTarget = true,
                targetQ = 1,
                targetR = 0,
                targetOwnerPlayerId = 1
            });

            Assert.AreEqual(enemyHpBefore - 2, enemy.Stats.CurrentHealth);
        }

        [UnityTest]
        public IEnumerator HandlePassRequest_FromActivePlayer_AdvancesTurn()
        {
            SetUpPlayersAndStartMatch();

            bool passed = false;
            turnManager.OnPlayerPassed += (c) => passed = true;

            manager.HandlePassRequest(123ul);

            Assert.IsTrue(passed);
            Assert.IsTrue(turnManager.Busy);

            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(enemy, turnManager.ActiveCharacter);
            Assert.IsFalse(turnManager.Busy);
        }

        [Test]
        public void HandlePassRequest_FromNonActivePlayer_Rejected()
        {
            SetUpPlayersAndStartMatch();
            GameManager.PlayerIdLookup = id => 1;

            bool passed = false;
            turnManager.OnPlayerPassed += (c) => passed = true;

            manager.HandlePassRequest(123ul);

            Assert.IsFalse(passed);
            Assert.IsFalse(turnManager.Busy);
        }

        [Test]
        public void HandleGameStateReceived_RebuildsBoardFromSync()
        {
            var old = CreateCharacter(0, 0, 1, 0);
            tracked.Add(old);
            manager.activeCharacters.Add(old);

            var sync = new GameStateSync { currentTurnIndex = 1 };
            sync.characterStates.Add(new CharacterState
            {
                ownerPlayerId = 0,
                teamId = 0,
                q = 0,
                r = -2,
                currentHp = 7,
                maxHp = 10,
                armor = 3,
                isAlive = true,
                name = "Alice",
                colorR = 0.2f,
                colorG = 0.5f,
                colorB = 0.8f,
                handCardNames = new string[0]
            });
            sync.characterStates.Add(new CharacterState
            {
                ownerPlayerId = 100,
                teamId = 1,
                q = 1,
                r = -1,
                currentHp = 4,
                maxHp = 6,
                armor = 0,
                isAlive = true,
                name = "Enemy",
                colorR = 0.8f,
                colorG = 0.3f,
                colorB = 0.3f
            });
            sync.characterStates.Add(new CharacterState
            {
                ownerPlayerId = 99,
                teamId = 0,
                q = 0,
                r = -1,
                isAlive = false
            });
            sync.characterStates.Add(new CharacterState
            {
                ownerPlayerId = 98,
                teamId = 0,
                q = 9,
                r = 9,
                isAlive = true
            });

            manager.HandleGameStateReceived(sync);

            Assert.AreEqual(2, manager.activeCharacters.Count);
            Assert.IsFalse(manager.activeCharacters.Contains(old));

            Character rebuiltPlayer = manager.activeCharacters.Find(c => c.OwnerPlayerId == 0);
            Character rebuiltEnemy = manager.activeCharacters.Find(c => c.OwnerPlayerId == 100);

            Assert.IsNotNull(rebuiltPlayer);
            Assert.IsNotNull(rebuiltEnemy);

            Assert.AreEqual("Alice", rebuiltPlayer.Stats.CharacterName);
            Assert.AreEqual(0, rebuiltPlayer.TeamId);
            Assert.AreEqual(7, rebuiltPlayer.Stats.CurrentHealth);
            Assert.AreEqual(10, rebuiltPlayer.Stats.MaxHealth);
            Assert.AreEqual(3, rebuiltPlayer.Stats.Armor);
            Assert.IsTrue(rebuiltPlayer.PlayerColor.HasValue);
            Assert.AreEqual(0.2f, rebuiltPlayer.PlayerColor.Value.r, 0.0001f);
            Assert.AreEqual(0.5f, rebuiltPlayer.PlayerColor.Value.g, 0.0001f);
            Assert.AreEqual(0.8f, rebuiltPlayer.PlayerColor.Value.b, 0.0001f);
            Assert.AreEqual(new HexCoordinates(0, -2), rebuiltPlayer.CurrentCell.Coordinates);

            Assert.AreEqual("Enemy", rebuiltEnemy.Stats.CharacterName);
            Assert.AreEqual(1, rebuiltEnemy.TeamId);
            Assert.AreEqual(4, rebuiltEnemy.Stats.CurrentHealth);
            Assert.AreEqual(6, rebuiltEnemy.Stats.MaxHealth);
            Assert.AreEqual(new HexCoordinates(1, -1), rebuiltEnemy.CurrentCell.Coordinates);

            Assert.AreEqual(1, turnManager.CurrentTurnIndex);
            Assert.AreEqual(rebuiltEnemy, turnManager.ActiveCharacter);
        }
    }
}
