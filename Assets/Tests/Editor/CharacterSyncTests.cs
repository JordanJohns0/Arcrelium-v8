using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Characters;
using Object = UnityEngine.Object;

namespace Tests
{
    [TestFixture]
    public class CharacterSyncTests
    {
        private Character character;
        private readonly List<ScriptableObject> trackedSops = new List<ScriptableObject>();

        private CardData MakeCard(string name)
        {
            var data = TestHelper.MakeCard(name);
            trackedSops.Add(data);
            return data;
        }

        [SetUp]
        public void SetUp()
        {
            character = TestHelper.CreateCharacter(0, 0, 0);
            character.Deck.SetStartingCards(new List<CardData>
            {
                MakeCard("Slash"),
                MakeCard("Defend")
            });
            character.Deck.Shuffle();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character.CurrentCell.gameObject);
            Object.DestroyImmediate(character.gameObject);
            foreach (var so in trackedSops)
            {
                if (so != null)
                    Object.DestroyImmediate(so);
            }
            trackedSops.Clear();
        }

        [Test]
        public void RebuildHand_ReplacesExistingHand()
        {
            character.Hand.AddCard(new CardInstance(MakeCard("Old")));

            character.RebuildHand(new[] { "Slash", "Defend" });

            Assert.AreEqual(2, character.Hand.Cards.Count);
            Assert.AreEqual("Slash", character.Hand.Cards[0].Data.CardName);
            Assert.AreEqual("Defend", character.Hand.Cards[1].Data.CardName);
        }

        [Test]
        public void RebuildHand_SkipsUnknownNames()
        {
            character.Hand.AddCard(new CardInstance(MakeCard("Old")));

            character.RebuildHand(new[] { "Slash", "NotARealCard" });

            Assert.AreEqual(1, character.Hand.Cards.Count);
            Assert.AreEqual("Slash", character.Hand.Cards[0].Data.CardName);
        }

        [Test]
        public void RebuildHand_Null_EmptiesHand()
        {
            character.Hand.AddCard(new CardInstance(MakeCard("Slash")));

            character.RebuildHand(null);

            Assert.AreEqual(0, character.Hand.Cards.Count);
        }

        [Test]
        public void RebuildHand_EmptyArray_EmptiesHand()
        {
            character.Hand.AddCard(new CardInstance(MakeCard("Slash")));

            character.RebuildHand(new string[0]);

            Assert.AreEqual(0, character.Hand.Cards.Count);
        }

        [Test]
        public void RebuildHand_PreservesHandInstance()
        {
            Hand before = character.Hand;

            character.RebuildHand(new[] { "Slash" });

            Assert.AreSame(before, character.Hand);
        }

        [Test]
        public void ExplicitTeamId0_IsAllyEvenWithOwnerPlayerId1()
        {
            var ally = TestHelper.CreateCharacter(1, 0, 0, teamId: 0);
            try
            {
                Assert.AreEqual(0, ally.TeamId);
                Assert.AreEqual(Team.Ally, ally.Team);
            }
            finally
            {
                Object.DestroyImmediate(ally.CurrentCell.gameObject);
                Object.DestroyImmediate(ally.gameObject);
            }
        }

        [Test]
        public void DefaultTeamId_DerivedFromOwnerPlayerId()
        {
            var player = TestHelper.CreateCharacter(0, 0, 0);
            var enemy = TestHelper.CreateCharacter(1, 0, 0);
            try
            {
                Assert.AreEqual(0, player.TeamId);
                Assert.AreEqual(Team.Ally, player.Team);
                Assert.AreEqual(1, enemy.TeamId);
                Assert.AreEqual(Team.Enemy, enemy.Team);
            }
            finally
            {
                Object.DestroyImmediate(player.CurrentCell.gameObject);
                Object.DestroyImmediate(player.gameObject);
                Object.DestroyImmediate(enemy.CurrentCell.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        [Test]
        public void IsAllyOf_SameTeamId_IsAlly()
        {
            var other = TestHelper.CreateCharacter(1, 0, 0, teamId: 0);
            try
            {
                Assert.IsTrue(character.IsAllyOf(other));
                Assert.IsFalse(character.IsEnemyOf(other));
            }
            finally
            {
                Object.DestroyImmediate(other.CurrentCell.gameObject);
                Object.DestroyImmediate(other.gameObject);
            }
        }

        [Test]
        public void IsAllyOf_DifferentTeamId_IsEnemy()
        {
            var other = TestHelper.CreateCharacter(1, 0, 0, teamId: 1);
            try
            {
                Assert.IsTrue(character.IsEnemyOf(other));
                Assert.IsFalse(character.IsAllyOf(other));
            }
            finally
            {
                Object.DestroyImmediate(other.CurrentCell.gameObject);
                Object.DestroyImmediate(other.gameObject);
            }
        }

        [Test]
        public void IsAllyOf_PvPDistinctTeams_AreMutualEnemies()
        {
            var p1 = TestHelper.CreateCharacter(0, 0, 0, teamId: 1);
            var p2 = TestHelper.CreateCharacter(1, 0, 0, teamId: 2);
            try
            {
                Assert.IsTrue(p1.IsEnemyOf(p2));
                Assert.IsTrue(p2.IsEnemyOf(p1));
                Assert.IsFalse(p1.IsAllyOf(p2));
            }
            finally
            {
                Object.DestroyImmediate(p1.CurrentCell.gameObject);
                Object.DestroyImmediate(p1.gameObject);
                Object.DestroyImmediate(p2.CurrentCell.gameObject);
                Object.DestroyImmediate(p2.gameObject);
            }
        }

        [Test]
        public void IsAllyOf_Self_SameTeam()
        {
            Assert.IsTrue(character.IsAllyOf(character));
            Assert.IsFalse(character.IsEnemyOf(character));
        }

        [Test]
        public void SetName_SetsCharacterName()
        {
            character.SetName("Bob");
            Assert.AreEqual("Bob", character.Stats.CharacterName);
        }

        [Test]
        public void SetName_IgnoresNullOrEmpty()
        {
            string before = character.Stats.CharacterName;
            character.SetName(null);
            Assert.AreEqual(before, character.Stats.CharacterName);
            character.SetName("");
            Assert.AreEqual(before, character.Stats.CharacterName);
        }

        [Test]
        public void SetPlayerColor_SetsPlayerColor()
        {
            Assert.IsFalse(character.PlayerColor.HasValue);
            character.SetPlayerColor(Color.red);
            Assert.IsTrue(character.PlayerColor.HasValue);
            Assert.AreEqual(Color.red, character.PlayerColor.Value);
        }

        [Test]
        public void SetName_FiresOnStatsChanged()
        {
            bool fired = false;
            Action<CharacterStats> handler = (s) => fired = true;
            character.Stats.OnStatsChanged += handler;
            try
            {
                character.SetName("Bob");
            }
            finally
            {
                character.Stats.OnStatsChanged -= handler;
            }

            Assert.IsTrue(fired);
        }

        [Test]
        public void SetName_Null_DoesNotFireOnStatsChanged()
        {
            bool fired = false;
            Action<CharacterStats> handler = (s) => fired = true;
            character.Stats.OnStatsChanged += handler;
            try
            {
                character.SetName(null);
            }
            finally
            {
                character.Stats.OnStatsChanged -= handler;
            }

            Assert.IsFalse(fired);
        }

        [Test]
        public void SetPlayerColor_FiresOnStatsChanged()
        {
            bool fired = false;
            Action<CharacterStats> handler = (s) => fired = true;
            character.Stats.OnStatsChanged += handler;
            try
            {
                character.SetPlayerColor(Color.blue);
            }
            finally
            {
                character.Stats.OnStatsChanged -= handler;
            }

            Assert.IsTrue(fired);
        }
    }
}
