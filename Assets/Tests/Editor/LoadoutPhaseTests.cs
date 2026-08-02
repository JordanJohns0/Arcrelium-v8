using NUnit.Framework;
using UnityEngine;
using Equipment;
using Game;

namespace Tests
{
    [TestFixture]
    public class LoadoutPhaseTests
    {
        private GameManager manager;

        [SetUp]
        public void SetUp()
        {
            var gmGo = new GameObject("GameManager");
            manager = gmGo.AddComponent<GameManager>();
            GameState.ActiveMode = GameMode.Host;
            GameState.LocalPlayerId = 0;
        }

        [TearDown]
        public void TearDown()
        {
            GameState.ActiveMode = GameMode.SinglePlayer;
            GameState.LocalPlayerId = 0;
            GameState.MatchLoadouts.Clear();
            GameState.ShowLoadoutScreen = false;
            Object.DestroyImmediate(manager.gameObject);
        }

        [Test]
        public void Host_DoesNotStart_UntilAllLoadoutsReceived()
        {
            manager.ExpectedLoadoutCount = 2;

            manager.AddReceivedLoadout(0, new EquipmentLoadout());
            manager.TryStartAfterLoadouts();

            Assert.IsFalse(manager.AllLoadoutsReceived());
            Assert.AreEqual(0, GameState.MatchLoadouts.Count);
        }

        [Test]
        public void Host_AllLoadoutsReceived_OnceCountMatches()
        {
            manager.ExpectedLoadoutCount = 2;
            manager.AddReceivedLoadout(0, new EquipmentLoadout());
            Assert.IsFalse(manager.AllLoadoutsReceived());

            manager.AddReceivedLoadout(1, new EquipmentLoadout());
            Assert.IsTrue(manager.AllLoadoutsReceived());
        }

        [Test]
        public void CommitLoadoutsToMatchState_CopiesAllReceived()
        {
            var lo0 = new EquipmentLoadout();
            var lo1 = new EquipmentLoadout();
            manager.AddReceivedLoadout(0, lo0);
            manager.AddReceivedLoadout(1, lo1);

            manager.CommitLoadoutsToMatchState();

            Assert.AreEqual(2, GameState.MatchLoadouts.Count);
            Assert.AreSame(lo0, GameState.MatchLoadouts[0]);
            Assert.AreSame(lo1, GameState.MatchLoadouts[1]);
        }

        [Test]
        public void CommitLoadoutsToMatchState_ClearsPreviousEntries()
        {
            GameState.MatchLoadouts[9] = new EquipmentLoadout();
            manager.AddReceivedLoadout(0, new EquipmentLoadout());

            manager.CommitLoadoutsToMatchState();

            Assert.IsFalse(GameState.MatchLoadouts.ContainsKey(9));
        }
    }
}
