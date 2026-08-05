using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Characters;
using Cards;
using Cards.Effects;
using Equipment;
using Game;
using HexGrid;

namespace Tests.Cards
{
    /// <summary>
    /// Data-driven battery over every card registered in GameDataLibrary. New cards added
    /// to the library are covered automatically by these tests.
    /// </summary>
    [TestFixture]
    public class CardExecutionTests
    {
        private static readonly FieldInfo resolverGridField =
            typeof(ActionResolver).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance);

        private static IEnumerable<string> RegisteredCardNames()
        {
            GameDataLibrary library = GameDataLibrary.Instance;
            if (library == null) yield break;
            foreach (CardData card in library.allCards)
            {
                if (card != null) yield return card.CardName;
            }
        }

        private static CardData Resolve(string cardName)
        {
            CardData card = GameDataLibrary.Instance.GetCardByName(cardName);
            Assert.IsNotNull(card, $"Card '{cardName}' not found in GameDataLibrary.");
            return card;
        }

        /// <summary>
        /// Walks a card's effect tree asserting no node is null. Catches missing-script
        /// ScriptableObject children inside composites, which silently do nothing.
        /// </summary>
        private static void AssertEffectBound(CardEffectSO effect, string cardName, int depth)
        {
            Assert.IsNotNull(effect, $"Card '{cardName}' has a null (missing-script) effect node.");
            if (depth >= 16 || !(effect is CompositeEffect composite)) return;
            for (int i = 0; i < composite.Effects.Count; i++)
            {
                AssertEffectBound(composite.Effects[i], cardName, depth + 1);
            }
        }

        [Test]
        public void AllRegisteredCards_HaveValidData()
        {
            GameDataLibrary library = GameDataLibrary.Instance;
            Assert.IsNotNull(library, "GameDataLibrary asset missing from Resources.");
            Assert.Greater(library.allCards.Count, 0, "No cards registered.");

            var seenNames = new HashSet<string>();
            foreach (CardData card in library.allCards)
            {
                if (card == null)
                {
                    Assert.Fail("allCards contains a null (broken) reference.");
                    continue;
                }
                Assert.False(string.IsNullOrEmpty(card.CardName), "Registered card has no name.");
                Assert.IsTrue(seenNames.Add(card.CardName),
                    $"Duplicate card name in GameDataLibrary: '{card.CardName}' (names must be unique).");
                AssertEffectBound(card.Effect, card.CardName, 0);
                Assert.GreaterOrEqual(card.Cost, 0, $"Card '{card.CardName}' has negative cost.");
                Assert.LessOrEqual(card.Cost, 4, $"Card '{card.CardName}' costs {card.Cost} — cost plus the card itself must fit a 5-card hand.");
                Assert.GreaterOrEqual(card.Range, 0, $"Card '{card.CardName}' has negative range.");
                Assert.IsTrue(Enum.IsDefined(typeof(TargetType), card.TargetType), $"Card '{card.CardName}' has invalid TargetType.");
            }
        }

        [Test]
        public void EveryCardAssetInProject_IsRegisteredInLibrary()
        {
            GameDataLibrary library = GameDataLibrary.Instance;
            Assert.IsNotNull(library, "GameDataLibrary asset missing from Resources.");

            var registered = new HashSet<CardData>();
            foreach (CardData card in library.allCards)
                if (card != null) registered.Add(card);

            var missing = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card != null && !registered.Contains(card))
                    missing.Add(card.CardName);
            }

            Assert.IsEmpty(missing, "Card assets not registered in GameDataLibrary: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryEquipmentAssetInProject_IsRegisteredInLibrary()
        {
            GameDataLibrary library = GameDataLibrary.Instance;
            Assert.IsNotNull(library, "GameDataLibrary asset missing from Resources.");

            var registered = new HashSet<EquipmentData>();
            foreach (EquipmentData equipment in library.allEquipment)
                if (equipment != null) registered.Add(equipment);

            var missing = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:EquipmentData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var equipment = AssetDatabase.LoadAssetAtPath<EquipmentData>(path);
                if (equipment != null && !registered.Contains(equipment))
                    missing.Add(equipment.EquipmentName);
            }

            Assert.IsEmpty(missing, "Equipment assets not registered in GameDataLibrary: " + string.Join(", ", missing));
        }

        [Test]
        [TestCaseSource(nameof(RegisteredCardNames))]
        public void Card_PlaysSuccessfully(string cardName)
        {
            CardData data = Resolve(cardName);
            using (Setup setup = BuildSetup(data))
            {
                int handBefore = setup.Source.Hand.Cards.Count;
                bool played = setup.Play();

                Assert.IsTrue(played, $"Card '{cardName}' could not be played with a valid target setup.");
                Assert.IsFalse(setup.Source.Hand.Contains(setup.Card), $"Card '{cardName}' was not consumed from hand.");
                foreach (CardInstance cost in setup.CostCards)
                    Assert.IsFalse(setup.Source.Hand.Contains(cost), $"Cost card was not consumed for '{cardName}'.");

                int drawn = Mathf.Min(TotalDrawAmount(data.Effect), 5);
                Assert.AreEqual(handBefore - (1 + data.Cost) + drawn, setup.Source.Hand.Cards.Count,
                    $"'{cardName}': unexpected hand size after play.");
            }
        }

        [Test]
        [TestCaseSource(nameof(RegisteredCardNames))]
        public void Card_Outcome_MatchesDesign(string cardName)
        {
            CardData data = Resolve(cardName);
            using (Setup setup = BuildSetup(data))
            {
                Assert.IsTrue(setup.Play(), $"Card '{cardName}' could not be played with a valid target setup.");

                ExpectedOutcome expected = SpecFor(data);
                Assert.AreEqual(expected.SourceHealth, setup.Source.Stats.CurrentHealth, $"'{cardName}': source health.");
                Assert.AreEqual(expected.SourceBlock, setup.Source.Stats.Block, $"'{cardName}': source block.");
                Assert.AreEqual(expected.EnemyHealth, setup.Enemy.Stats.CurrentHealth, $"'{cardName}': enemy health.");
                Assert.AreEqual(expected.HandCount, setup.Source.Hand.Cards.Count, $"'{cardName}': hand count.");
                if (expected.TargetCellCoordinates.HasValue)
                {
                    Assert.AreEqual(expected.TargetCellCoordinates.Value, setup.Source.CurrentCell.Coordinates,
                        $"'{cardName}': did not move to the expected cell.");
                }
            }
        }

        [Test]
        public void PlayCard_InvalidTarget_ReturnsFalse_AndMutatesNothing()
        {
            CardData slash = Resolve("Slash");
            using (Setup setup = BuildSetup(slash))
            {
                int handBefore = setup.Source.Hand.Cards.Count;
                int sourceHp = setup.Source.Stats.CurrentHealth;
                int enemyHp = setup.Enemy.Stats.CurrentHealth;

                bool played = setup.Resolver.PlayCard(setup.Source, setup.Card, setup.CostCards, null, null);

                Assert.IsFalse(played);
                Assert.AreEqual(handBefore, setup.Source.Hand.Cards.Count, "Hand must be untouched on rejection.");
                Assert.IsTrue(setup.Source.Hand.Contains(setup.Card), "Card must stay in hand on rejection.");
                Assert.AreEqual(sourceHp, setup.Source.Stats.CurrentHealth, "Source health changed on rejection.");
                Assert.AreEqual(enemyHp, setup.Enemy.Stats.CurrentHealth, "Enemy health changed on rejection.");
            }
        }

        [Test]
        public void PlayCard_OutOfRange_ReturnsFalse_AndMutatesNothing()
        {
            CardData slash = Resolve("Slash");
            using (Setup setup = BuildSetup(slash))
            {
                setup.Enemy.PlaceOnCell(setup.Grid.GetCell(new HexCoordinates(2, -1)));
                int handBefore = setup.Source.Hand.Cards.Count;
                int enemyHp = setup.Enemy.Stats.CurrentHealth;

                bool played = setup.Resolver.PlayCard(setup.Source, setup.Card, setup.CostCards, null, setup.Enemy);

                Assert.IsFalse(played);
                Assert.AreEqual(handBefore, setup.Source.Hand.Cards.Count, "Hand must be untouched on rejection.");
                Assert.AreEqual(enemyHp, setup.Enemy.Stats.CurrentHealth, "Enemy health changed on rejection.");
            }
        }

        [Test]
        public void PlayCard_WrongCostCount_ReturnsFalse_AndMutatesNothing()
        {
            CardData slash = Resolve("Slash"); // cost 0
            using (Setup setup = BuildSetup(slash))
            {
                var extra = new CardInstance(TestHelper.MakeCard("Extra " + setup.Source.Hand.Cards.Count));
                setup.Source.Hand.AddCard(extra);
                int handBefore = setup.Source.Hand.Cards.Count;

                // Slash costs 0, so offering 1 cost card is an invalid payment.
                bool played = setup.Resolver.PlayCard(setup.Source, setup.Card, new List<CardInstance> { extra }, null, setup.Enemy);

                Assert.IsFalse(played);
                Assert.AreEqual(handBefore, setup.Source.Hand.Cards.Count, "Hand must be untouched on rejection.");
                Assert.IsTrue(setup.Source.Hand.Contains(setup.Card), "Card must stay in hand on rejection.");
                Assert.IsTrue(setup.Source.Hand.Contains(extra), "Offered cost card must stay in hand on rejection.");
            }
        }

        private static int TotalDrawAmount(CardEffectSO effect)
        {
            switch (effect)
            {
                case DrawCardsEffect draw:
                    return draw.Amount.Evaluate(null);
                case CompositeEffect composite:
                    int total = 0;
                    foreach (CardEffectSO child in composite.Effects)
                    {
                        if (child != null) total += TotalDrawAmount(child);
                    }
                    return total;
                default:
                    return 0;
            }
        }

        private static Setup BuildSetup(CardData data)
        {
            var setup = new Setup { Data = data };

            setup.Grid = TestHelper.CreateTestGrid(5);
            setup.Source = TestHelper.CreateCharacterOnGrid(setup.Grid, 0, 0, 0);
            setup.Enemy = TestHelper.CreateCharacterOnGrid(setup.Grid, 1, 1, -1);
            setup.Ally = TestHelper.CreateCharacterOnGrid(setup.Grid, 0, 2, -2);

            // Source missing HP so heals are legal; block so Shield Bash deals real damage.
            setup.Source.Stats.ApplyDamage(3);
            setup.Source.Stats.GainBlock(3);

            var deckCards = new List<CardData>();
            for (int i = 0; i < 5; i++) deckCards.Add(TestHelper.MakeCard("Dummy " + i));
            setup.Source.Deck.SetStartingCards(deckCards);
            setup.Source.Deck.Shuffle();

            setup.Card = new CardInstance(data);
            setup.Source.Hand.AddCard(setup.Card);
            for (int i = 0; i < data.Cost; i++)
            {
                var cost = new CardInstance(data);
                setup.Source.Hand.AddCard(cost);
                setup.CostCards.Add(cost);
            }

            var resolverGo = new GameObject("CardTestResolver");
            setup.Resolver = resolverGo.AddComponent<ActionResolver>();
            resolverGridField.SetValue(setup.Resolver, setup.Grid);

            setup.owned.Add(resolverGo);
            setup.owned.Add(setup.Source.gameObject);
            setup.owned.Add(setup.Enemy.gameObject);
            setup.owned.Add(setup.Ally.gameObject);
            setup.owned.Add(setup.Grid.gameObject);
            return setup;
        }

        /// <summary>
        /// Expected post-play state for each card, matching its description. Source starts at
        /// 7/10 HP with 3 block; enemy at 10/0 HP.
        /// </summary>
        private static ExpectedOutcome SpecFor(CardData card)
        {
            switch (card.CardName)
            {
                case "Slash":
                case "Bonk":
                case "Poke":
                    return Attack(2);
                case "Excalibur's Justice":
                    return Attack(10);
                case "Shield Bash":
                    return Attack(3);
                case "Brace":
                case "Bad Brace":
                    return SourceBlock(4);
                case "Reinforce":
                    return SourceBlock(5);
                case "Fortify":
                    return SourceBlock(6);
                case "Bulwurk":
                    return SourceBlock(8);
                case "Defend":
                case "Inspire":
                    return SourceBlockDraw(4, 1);
                case "Restore":
                    return SourceHeal(9);
                case "Spiked Handle":
                    return SourceDamageDraw(7, 2);
                case "Walk":
                case "Only the enemy has this card":
                    return MoveTo(1);
                case "Jog":
                    return MoveTo(2);
                case "Run":
                    return MoveTo(3);
                case "Sprint":
                    return MoveTo(4);
                default:
                    Assert.Fail($"No expected outcome defined for card '{card.CardName}' — add a SpecFor row.");
                    return default;
            }
        }

        private static ExpectedOutcome Attack(int damage)
        {
            return new ExpectedOutcome { SourceHealth = 7, SourceBlock = 3, EnemyHealth = 10 - damage };
        }

        private static ExpectedOutcome SourceBlock(int block)
        {
            return new ExpectedOutcome { SourceHealth = 7, SourceBlock = block, EnemyHealth = 10 };
        }

        private static ExpectedOutcome SourceBlockDraw(int block, int draws)
        {
            return new ExpectedOutcome { SourceHealth = 7, SourceBlock = block, EnemyHealth = 10, HandCount = draws };
        }

        private static ExpectedOutcome SourceHeal(int health)
        {
            return new ExpectedOutcome { SourceHealth = health, SourceBlock = 3, EnemyHealth = 10 };
        }

        private static ExpectedOutcome SourceDamageDraw(int health, int draws)
        {
            return new ExpectedOutcome { SourceHealth = health, SourceBlock = 2, EnemyHealth = 10, HandCount = draws };
        }

        private static ExpectedOutcome MoveTo(int range)
        {
            return new ExpectedOutcome
            {
                SourceHealth = 7,
                SourceBlock = 3,
                EnemyHealth = 10,
                TargetCellCoordinates = new HexCoordinates(range, 0)
            };
        }

        private struct ExpectedOutcome
        {
            public int SourceHealth;
            public int SourceBlock;
            public int EnemyHealth;
            public int HandCount;
            public HexCoordinates? TargetCellCoordinates;
        }

        private sealed class Setup : IDisposable
        {
            public HexGridManager Grid;
            public ActionResolver Resolver;
            public Character Source;
            public Character Enemy;
            public Character Ally;
            public CardData Data;
            public CardInstance Card;
            public List<CardInstance> CostCards = new List<CardInstance>();

            public readonly List<GameObject> owned = new List<GameObject>();

            public bool Play()
            {
                Character targetCharacter = null;
                HexCell targetCell = null;
                switch (Data.TargetType)
                {
                    case TargetType.None:
                    case TargetType.Self:
                        break;
                    case TargetType.HexCell:
                        targetCell = Grid.GetCell(new HexCoordinates(Data.Range, 0));
                        break;
                    case TargetType.EnemyCharacter:
                    case TargetType.AnyCharacter:
                        targetCharacter = Enemy;
                        break;
                    case TargetType.AllyCharacter:
                        targetCharacter = Ally;
                        break;
                    default:
                        Assert.Fail($"Card '{Data.CardName}' has unsupported TargetType {Data.TargetType}.");
                        break;
                }

                return Resolver.PlayCard(Source, Card, CostCards, targetCell, targetCharacter);
            }

            public void Dispose()
            {
                if (Grid != null)
                {
                    foreach (Transform child in Grid.GetComponentsInChildren<Transform>(true))
                    {
                        if (child != null && child != Grid.transform)
                            UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                    if (Grid != null) UnityEngine.Object.DestroyImmediate(Grid.gameObject);
                }
                foreach (GameObject go in owned)
                {
                    if (go != null) UnityEngine.Object.DestroyImmediate(go);
                }
                owned.Clear();
            }
        }
    }
}
