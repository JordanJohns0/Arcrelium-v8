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

        [Test]
        public void RollSpecialArea_SeededIsDeterministic()
        {
            var first = GameState.RollSpecialArea(new System.Random(12345));
            var second = GameState.RollSpecialArea(new System.Random(12345));
            Assert.AreEqual(first, second, "The same seed must decide the same Special pane.");
        }

        [Test]
        public void RollSpecialArea_ReturnsOnlyKnownAreas()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var area = GameState.RollSpecialArea(new System.Random(seed));
                Assert.IsTrue(area == SpecialArea.Shop || area == SpecialArea.Enchant);
            }
        }

        [Test]
        public void RollSpecialArea_ProducesBothAreasAcrossSeeds()
        {
            bool sawShop = false;
            bool sawEnchant = false;
            for (int seed = 1; seed <= 400 && !(sawShop && sawEnchant); seed++)
            {
                var area = GameState.RollSpecialArea(new System.Random(seed));
                sawShop |= area == SpecialArea.Shop;
                sawEnchant |= area == SpecialArea.Enchant;
            }
            Assert.IsTrue(sawShop, "Some seeds must yield the shop.");
            Assert.IsTrue(sawEnchant, "Some seeds must yield the enchantment station.");
        }

        [Test]
        public void CurrentSpecialArea_DefaultsToShop()
        {
            Assert.AreEqual(SpecialArea.Shop, GameState.CurrentSpecialArea);
        }
    }
}
