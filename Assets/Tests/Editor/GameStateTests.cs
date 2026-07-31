using System.Collections.Generic;
using NUnit.Framework;
using Game;

namespace Tests
{
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
    }
}
