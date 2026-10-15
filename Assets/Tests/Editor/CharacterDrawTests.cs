using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Equipment;
using Characters;

namespace Tests.Characters
{
    [TestFixture]
    public class CharacterDrawTests
    {
        private readonly List<ScriptableObject> trackedSops = new List<ScriptableObject>();

        private CardData MakeCard(string name)
        {
            var data = TestHelper.MakeCard(name);
            trackedSops.Add(data);
            return data;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in trackedSops)
            {
                if (so != null) Object.DestroyImmediate(so);
            }
            trackedSops.Clear();
        }

        /// <summary>
        /// Builds a character whose deck comes from an equipped item (the real runtime build
        /// path: Initialize -> equipmentManager.GetAllCards -> deck.SetStartingCards), so the
        /// opening hand can actually be observed.
        /// </summary>
        private Character MakeCharacterWithDeck(int deckSize)
        {
            var cell = TestHelper.CreateCell(0, 0);
            var go = new GameObject("Character", typeof(Character));

            var manager = go.AddComponent<EquipmentManager>();
            var equipment = ScriptableObject.CreateInstance<EquipmentData>();
            equipment.EquipmentName = "Test Tome";
            equipment.SlotType = EquipmentSlotType.Head;
            for (int i = 0; i < deckSize; i++)
                equipment.Cards.Add(MakeCard($"Card{i}"));
            trackedSops.Add(equipment);

            typeof(EquipmentManager)
                .GetField("headEquipment", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, equipment);
            typeof(Character)
                .GetField("equipmentManager", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(go.GetComponent<Character>(), manager);

            var character = go.GetComponent<Character>();
            character.Initialize(0, cell);
            return character;
        }

        [Test]
        public void Initialize_DoesNotDrawAnOpeningHand()
        {
            var character = MakeCharacterWithDeck(8);

            Assert.AreEqual(8, character.Deck.DrawPile.Count,
                "The deck must be built from the equipped item's cards.");
            Assert.AreEqual(0, character.Hand.Cards.Count,
                "Initialize must not draw an opening hand; the first turn's draw supplies it.");

            DestroyCharacter(character);
        }

        [Test]
        public void FirstTurn_DrawsExactlyThePerTurnAmount()
        {
            var character = MakeCharacterWithDeck(8);

            character.DrawCardsForTurn();

            Assert.AreEqual(5, character.CardsDrawnPerTurn, "Sanity: the per-turn draw is 5.");
            Assert.AreEqual(5, character.Hand.Cards.Count,
                "The first turn must draw exactly the per-turn amount, with no leftover opening hand.");

            DestroyCharacter(character);
        }

        private static void DestroyCharacter(Character character)
        {
            if (character == null) return;
            var cell = character.CurrentCell;
            Object.DestroyImmediate(character.gameObject);
            if (cell != null) Object.DestroyImmediate(cell.gameObject);
        }
    }
}
