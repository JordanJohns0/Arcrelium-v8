using System.Collections.Generic;
using NUnit.Framework;
using Game;

namespace Tests.Game
{
    using Equipment = global::Equipment;

    [TestFixture]
    public class GameStateTests
    {
        [Test]
        public void GetPlayerIndex_SortsUnsortedMemberIds()
        {
            var ids = new List<ulong> { 30, 10, 20 };
            Assert.AreEqual(0, GameState.GetPlayerIndex(ids, 10));
            Assert.AreEqual(1, GameState.GetPlayerIndex(ids, 20));
            Assert.AreEqual(2, GameState.GetPlayerIndex(ids, 30));
        }

        [Test]
        public void GetPlayerIndex_UnknownId_ReturnsMinusOne()
        {
            var ids = new List<ulong> { 10, 20 };
            Assert.AreEqual(-1, GameState.GetPlayerIndex(ids, 999));
        }

        [Test]
        public void GetPlayerIndex_EmptyList_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, GameState.GetPlayerIndex(new List<ulong>(), 10));
        }

        [Test]
        public void GetPlayerIndex_NullList_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, GameState.GetPlayerIndex(null, 10));
        }

        [TearDown]
        public void TearDown()
        {
            GameState.ShowLoadoutScreen = false;
            GameState.MatchLoadouts.Clear();
        }

        [Test]
        public void ShowLoadoutScreen_DefaultsToFalse()
        {
            Assert.IsFalse(GameState.ShowLoadoutScreen);
        }

        [Test]
        public void MatchLoadouts_StoresAndClearsPerPlayer()
        {
            var loadout = new Equipment.EquipmentLoadout();
            GameState.MatchLoadouts[0] = loadout;
            Assert.AreSame(loadout, GameState.MatchLoadouts[0]);
            GameState.MatchLoadouts.Clear();
            Assert.IsFalse(GameState.MatchLoadouts.ContainsKey(0));
        }
    }
}
