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
using Equipment;
using UI;

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
        private readonly List<ScriptableObject> trackedSops = new List<ScriptableObject>();

        private static readonly FieldInfo loadoutScreenField =
            typeof(GameManager).GetField("loadoutScreen", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo spawnsField =
            typeof(GameManager).GetField("spawns", BindingFlags.NonPublic | BindingFlags.Instance);

        private CardData SelfCard(string name, int cost = 0)
        {
            var effect = ScriptableObject.CreateInstance<GainBlockEffect>();
            effect.Amount = CardValue.MakeConstant(1);
            trackedSops.Add(effect);
            var data = TestHelper.MakeCard(name, cost, TestHelper.Step(effect, TargetType.Self));
            trackedSops.Add(data);
            return data;
        }

        private CardData AttackCard(string name, int damage)
        {
            var effect = ScriptableObject.CreateInstance<AttackEffect>();
            effect.Damage = CardValue.MakeConstant(damage);
            trackedSops.Add(effect);
            var data = TestHelper.MakeCard(name, 0, TestHelper.Step(effect, TargetType.EnemyCharacter, 5));
            trackedSops.Add(data);
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

            grid = TestHelper.CreateTestGrid(2);

            var hexPrefabGo = new GameObject("HexCellPrefab");
            hexPrefabGo.transform.SetParent(grid.transform);
            var hexPrefab = hexPrefabGo.AddComponent<HexCell>();
            typeof(HexGridManager).GetField("hexCellPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(grid, hexPrefab);

            typeof(TurnManager).GetField("actionResolver", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(turnManager, actionResolver);
            typeof(ActionResolver).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(actionResolver, grid);

            typeof(GameManager).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, grid);
            typeof(GameManager).GetField("turnManager", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, turnManager);

            var playerPrefab = TestHelper.CreateCharacter(0, 0, 0, 0);
            var enemyPrefab = TestHelper.CreateCharacter(100, 0, 0, 1);
            tracked.Add(playerPrefab);
            tracked.Add(enemyPrefab);
            typeof(GameManager).GetField("playerCharacterPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, playerPrefab);
            typeof(GameManager).GetField("enemyCharacterPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, enemyPrefab);
        }

        private void SetUpPlayersAndStartMatch()
        {
            player = TestHelper.CreateCharacter(0, 0, 0, 0);
            enemy = TestHelper.CreateCharacter(1, 1, 0, 1);
            tracked.Add(player);
            tracked.Add(enemy);
            manager.activeCharacters.AddRange(new[] { player, enemy });
            turnManager.SetTurnOrder(new List<Character>(manager.activeCharacters));
            turnManager.StartMatch();
        }

        private void SeedSpawns(Character playerPrefab, Character enemyPrefab)
        {
            spawnsField.SetValue(manager, new List<GameManager.CharacterSpawn>
            {
                new GameManager.CharacterSpawn { characterPrefab = playerPrefab, ownerPlayerId = 0, spawnQ = 0, spawnR = 0 },
                new GameManager.CharacterSpawn { characterPrefab = enemyPrefab, ownerPlayerId = 100, spawnQ = 1, spawnR = 0 }
            });
        }

        private static GameStateSync VictorySync(int ownerPlayerId, int maxHp, int currentHp)
        {
            var sync = new GameStateSync { currentTurnIndex = 0, victory = true };
            sync.characterStates.Add(new CharacterState
            {
                ownerPlayerId = ownerPlayerId,
                teamId = 0,
                q = 0,
                r = 0,
                currentHp = currentHp,
                maxHp = maxHp,
                isAlive = true,
                name = "Player",
                colorR = 0.2f,
                colorG = 0.5f,
                colorB = 0.8f,
                handCardNames = new string[0]
            });
            return sync;
        }

        private void CleanupLoadoutScreen()
        {
            var screen = loadoutScreenField.GetValue(manager) as LoadoutScreen;
            if (screen != null) Object.DestroyImmediate(screen.gameObject);
            loadoutScreenField.SetValue(manager, null);

            var ready = Object.FindAnyObjectByType<ReadyTracker>();
            if (ready != null) Object.DestroyImmediate(ready.gameObject);
            var lobby = Object.FindAnyObjectByType<LobbyManager>();
            if (lobby != null) Object.DestroyImmediate(lobby.gameObject);
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
            GameState.ActiveMode = GameMode.SinglePlayer;
            GameState.LocalPlayerId = 0;
            GameState.CurrentLevel = 1;
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

            foreach (var so in trackedSops)
            {
                if (so != null)
                    Object.DestroyImmediate(so);
            }
            trackedSops.Clear();
        }

        [Test]
        public void HandlePlayCardRequest_ValidPlay_RemovesCardFromHand()
        {
            SetUpPlayersAndStartMatch();
            player.Hand.AddCard(new CardInstance(SelfCard("Defend")));

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest { cardHandIndex = 0 });

            Assert.AreEqual(0, player.Hand.Cards.Count);
            Assert.AreEqual(player, turnManager.ActiveCharacter);
        }

        [Test]
        public void HandlePlayCardRequest_OutOfRangeIndex_Rejected()
        {
            SetUpPlayersAndStartMatch();
            player.Hand.AddCard(new CardInstance(SelfCard("Defend")));

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest { cardHandIndex = 5 });

            Assert.AreEqual(1, player.Hand.Cards.Count);
        }

        [Test]
        public void HandlePlayCardRequest_FromNonActiveCharacter_Rejected()
        {
            SetUpPlayersAndStartMatch();
            GameManager.PlayerIdLookup = id => 1;

            enemy.Hand.AddCard(new CardInstance(SelfCard("Defend")));

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest { cardHandIndex = 0 });

            Assert.AreEqual(1, enemy.Hand.Cards.Count);
        }

        [Test]
        public void HandlePlayCardRequest_UnknownSender_Rejected()
        {
            SetUpPlayersAndStartMatch();
            GameManager.PlayerIdLookup = id => -1;

            player.Hand.AddCard(new CardInstance(SelfCard("Defend")));

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest { cardHandIndex = 0 });

            Assert.AreEqual(1, player.Hand.Cards.Count);
        }

        [Test]
        public void HandlePlayCardRequest_WithCostCards_PaysCost()
        {
            SetUpPlayersAndStartMatch();
            player.Hand.AddCard(new CardInstance(SelfCard("Slash", cost: 1)));
            player.Hand.AddCard(new CardInstance(SelfCard("Defend")));

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
            player.Hand.AddCard(new CardInstance(SelfCard("Slash", cost: 1)));
            player.Hand.AddCard(new CardInstance(SelfCard("Defend")));

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

            yield return TestHelper.WaitUntilOrTimeout(() => !turnManager.Busy);
            Assert.IsFalse(turnManager.Busy, "Turn transition did not finish in time");

            Assert.AreEqual(enemy, turnManager.ActiveCharacter);
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
            var old = TestHelper.CreateCharacter(0, 0, 1, 0);
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

        [Test]
        public void HandleGameStateReceived_SyncedMaxHpAppliedBeforeOnCharacterCreated()
        {
            int maxHpSeen = -1;
            System.Action<Character> listener = c => maxHpSeen = c.Stats.MaxHealth;
            Character.OnCharacterCreated += listener;
            try
            {
                var sync = new GameStateSync { currentTurnIndex = 0 };
                sync.characterStates.Add(new CharacterState
                {
                    ownerPlayerId = 100,
                    teamId = 1,
                    q = 1,
                    r = -1,
                    currentHp = 4,
                    maxHp = 6,
                    isAlive = true,
                    name = "Enemy",
                    colorR = 0.8f,
                    colorG = 0.3f,
                    colorB = 0.3f
                });

                manager.HandleGameStateReceived(sync);

                Assert.AreEqual(6, maxHpSeen,
                    "StatsHUD builds segments from MaxHealth at OnCharacterCreated — it must already be the synced value.");
            }
            finally
            {
                Character.OnCharacterCreated -= listener;
            }
        }

        [UnityTest]
        public IEnumerator SetupMatch_Rerun_DestroysPreviousSpawns()
        {
            var playerPrefab = TestHelper.CreateCharacter(0, 0, 0, 0);
            var enemyPrefab = TestHelper.CreateCharacter(100, 0, 0, 1);
            tracked.Add(playerPrefab);
            tracked.Add(enemyPrefab);
            SeedSpawns(playerPrefab, enemyPrefab);

            manager.SetupMatch();
            var firstBatch = new List<Character>(manager.activeCharacters);
            manager.SetupMatch();
            var secondBatch = new List<Character>(manager.activeCharacters);

            Assert.AreEqual(2, secondBatch.Count, "Re-running SetupMatch must not stack duplicate spawns.");
            Assert.AreNotSame(firstBatch[0], secondBatch[0], "Second run must build a fresh board.");

            yield return null;

            foreach (var stale in firstBatch)
                Assert.IsTrue(stale == null, "Stale spawn must be destroyed when SetupMatch runs again.");
            foreach (var fresh in secondBatch)
                Assert.IsFalse(fresh == null, "Fresh spawn must remain alive.");
        }

        [UnityTest]
        public IEnumerator SetupDynamicMatch_Rerun_DestroysPreviousSpawns()
        {
            var setupDynamicMatch = typeof(GameManager).GetMethod("SetupDynamicMatch", BindingFlags.NonPublic | BindingFlags.Instance);

            setupDynamicMatch.Invoke(manager, null);
            var firstBatch = new List<Character>(manager.activeCharacters);
            int firstCount = firstBatch.Count;
            Assert.Greater(firstCount, 0, "SetupDynamicMatch must spawn at least the player character.");

            setupDynamicMatch.Invoke(manager, null);
            var secondBatch = new List<Character>(manager.activeCharacters);

            Assert.AreEqual(firstCount, secondBatch.Count, "Re-running SetupDynamicMatch must not stack duplicate spawns.");
            Assert.AreNotSame(firstBatch[0], secondBatch[0], "Second run must build a fresh board.");

            yield return null;

            foreach (var stale in firstBatch)
                Assert.IsTrue(stale == null, "Stale spawn must be destroyed when SetupDynamicMatch runs again.");
            foreach (var fresh in secondBatch)
                Assert.IsFalse(fresh == null, "Fresh spawn must remain alive.");
        }

        [Test]
        public void GetMaxSpreadSpawnCells_EachSpawnIsTheFarthestPossibleCell()
        {
            var spreadGrid = TestHelper.CreateTestGrid(4);
            typeof(GameManager).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, spreadGrid);

            var method = typeof(GameManager).GetMethod("GetMaxSpreadSpawnCells", BindingFlags.NonPublic | BindingFlags.Instance);

            for (int count = 2; count <= 8; count++)
            {
                var spawns = (List<HexCell>)method.Invoke(manager, new object[] { count });

                Assert.AreEqual(count, spawns.Count, $"Count {count} must produce exactly {count} spawn cells.");
                Assert.IsTrue(spawns.TrueForAll(c => c.IsWalkable), "Every spawn cell must be walkable.");

                var chosen = new List<HexCell>();
                foreach (var spawn in spawns)
                {
                    Assert.IsFalse(chosen.Exists(c => c.Coordinates == spawn.Coordinates),
                        $"Count {count}: spawn cell {spawn.Coordinates} must not repeat.");

                    if (chosen.Count == 0)
                    {
                        chosen.Add(spawn);
                        continue;
                    }

                    // This spawn must be the walkable cell farthest from the already-placed
                    // spawns (the previous set), which is the greedy property we want to lock in.
                    int actualMin = MinDistanceTo(spawn, chosen);
                    int bestPossible = 0;
                    foreach (var candidate in spreadGrid.AllCells)
                    {
                        if (!candidate.IsWalkable || chosen.Exists(c => c.Coordinates == candidate.Coordinates))
                            continue;
                        int min = MinDistanceTo(candidate, chosen);
                        if (min > bestPossible) bestPossible = min;
                    }

                    Assert.AreEqual(bestPossible, actualMin,
                        $"Count {count}: spawn #{chosen.Count + 1} must be the cell farthest from the already-placed players.");

                    chosen.Add(spawn);
                }
            }
        }

        private static int MinDistanceTo(HexCell cell, List<HexCell> others)
        {
            int min = int.MaxValue;
            foreach (var other in others)
            {
                int d = cell.Coordinates.DistanceTo(other.Coordinates);
                if (d < min) min = d;
            }
            return min;
        }

        [UnityTest]
        public IEnumerator BeginMatch_Twice_IsNoOpAfterFirstStart()
        {
            var playerPrefab = TestHelper.CreateCharacter(0, 0, 0, 0);
            var enemyPrefab = TestHelper.CreateCharacter(100, 0, 0, 1);
            tracked.Add(playerPrefab);
            tracked.Add(enemyPrefab);
            SeedSpawns(playerPrefab, enemyPrefab);

            typeof(GameManager).GetField("playerCharacterPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, null);
            typeof(GameManager).GetField("enemyCharacterPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, null);

            var beginMatch = typeof(GameManager).GetMethod("BeginMatch", BindingFlags.NonPublic | BindingFlags.Instance);

            beginMatch.Invoke(manager, null);
            var firstBatch = new List<Character>(manager.activeCharacters);
            beginMatch.Invoke(manager, null);

            Assert.AreEqual(2, manager.activeCharacters.Count, "BeginMatch must spawn exactly one batch.");
            Assert.AreSame(firstBatch[0], manager.activeCharacters[0],
                "The re-entrancy guard must make a second BeginMatch a no-op instead of re-spawning.");

            yield return null;

            Assert.IsFalse(firstBatch[0] == null, "A guarded no-op BeginMatch must not tear down the running match.");
        }

        [Test]
        public void HandlePlayCardRequest_FullHand_DrawCardStillDraws()
        {
            SetUpPlayersAndStartMatch();

            var drawEffect = ScriptableObject.CreateInstance<DrawCardsEffect>();
            drawEffect.Amount = CardValue.MakeConstant(1);
            trackedSops.Add(drawEffect);
            var drawData = TestHelper.MakeCard("Draw", 0, TestHelper.Step(drawEffect, TargetType.Self));
            trackedSops.Add(drawData);

            player.Deck.SetStartingCards(new List<CardData> { TestHelper.MakeCard("Drawable") });
            player.Deck.Shuffle();
            player.Hand.AddCard(new CardInstance(drawData));
            while (!player.Hand.IsFull)
                player.Hand.AddCard(new CardInstance(drawData));

            manager.HandlePlayCardRequest(123ul, new PlayCardRequest { cardHandIndex = 0 });

            Assert.AreEqual(player.MaxHandSize, player.Hand.Cards.Count,
                "The played card's slot must be freed before draw effects run, so a full hand doesn't block the draw.");
        }

        [Test]
        public void HandleGameStateReceived_VictorySync_KeepsLoadoutScreenOpen()
        {
            var emptyPool = ScriptableObject.CreateInstance<RewardPool>();
            trackedSops.Add(emptyPool);
            RewardPool.SetMain(emptyPool);
            try
            {
                var sync = VictorySync(0, 10, 7);

                manager.HandleGameStateReceived(sync);
                var screen = loadoutScreenField.GetValue(manager) as LoadoutScreen;
                manager.HandleGameStateReceived(sync);

                Assert.IsNotNull(screen, "Victory must open the loadout screen so the reward is visible.");
                Assert.AreSame(screen, loadoutScreenField.GetValue(manager),
                    "A second victory sync must not destroy and recreate the loadout screen.");
            }
            finally
            {
                RewardPool.SetMain(null);
                PlayerInventoryProvider.ResetForTests();
                CleanupLoadoutScreen();
            }
        }

        [Test]
        public void ShowVictory_Twice_UnlocksRewardOnce()
        {
            var rewardA = ScriptableObject.CreateInstance<EquipmentData>();
            rewardA.EquipmentName = "RewardA";
            var rewardB = ScriptableObject.CreateInstance<EquipmentData>();
            rewardB.EquipmentName = "RewardB";
            trackedSops.Add(rewardA);
            trackedSops.Add(rewardB);

            var pool = ScriptableObject.CreateInstance<RewardPool>();
            pool.rewards.Add(rewardA);
            pool.rewards.Add(rewardB);
            trackedSops.Add(pool);
            RewardPool.SetMain(pool);

            var blankTemplate = ScriptableObject.CreateInstance<PlayerInventory>();
            trackedSops.Add(blankTemplate);

            string tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ArcreliumInvTests_" + System.Guid.NewGuid().ToString("N"));
            PlayerInventoryProvider.RootOverride = tempRoot;
            PlayerInventoryProvider.DefaultOverride = blankTemplate;
            PlayerInventoryProvider.ResetForTests();

            try
            {
                var showVictory = typeof(GameManager).GetMethod("ShowVictory", BindingFlags.NonPublic | BindingFlags.Instance);

                showVictory.Invoke(manager, null);
                showVictory.Invoke(manager, null);

                var inventory = PlayerInventoryProvider.GetFor(0UL);
                Assert.AreEqual(1, inventory.UnlockedEquipment.Count,
                    "ShowVictory must roll and unlock a single reward even when triggered twice.");
            }
            finally
            {
                PlayerInventoryProvider.RootOverride = null;
                PlayerInventoryProvider.DefaultOverride = null;
                PlayerInventoryProvider.ResetForTests();
                RewardPool.SetMain(null);
                if (System.IO.Directory.Exists(tempRoot))
                    System.IO.Directory.Delete(tempRoot, true);
                CleanupLoadoutScreen();
            }
        }
    }
}
