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
                Assert.Greater(card.Steps.Count, 0, $"Card '{card.CardName}' has no steps.");
                foreach (CardEffectStep step in card.Steps)
                {
                    Assert.IsNotNull(step, $"Card '{card.CardName}' has a null step.");
                    AssertEffectBound(step.Effect, card.CardName, 0);
                }
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
        public void NoEquipmentAssetUsesPlaceholderName()
        {
            var offenders = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:EquipmentData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var equipment = AssetDatabase.LoadAssetAtPath<EquipmentData>(path);
                if (equipment != null && equipment.EquipmentName == "New Equipment")
                    offenders.Add(path);
            }

            Assert.IsEmpty(offenders, "Equipment assets still using the default 'New Equipment' name: " + string.Join(", ", offenders));
        }

        [Test]
        [TestCaseSource(nameof(RegisteredCardNames))]
        public void Card_PlaysSuccessfully(string cardName)
        {
            CardData data = Resolve(cardName);
            ExpectedOutcome expected = SpecFor(data);
            using (Setup setup = BuildSetup(data))
            {
                setup.TargetPickCell = expected.TargetPickCell;
                int handBefore = setup.Source.Hand.Cards.Count;
                bool played = setup.Play();

                Assert.IsTrue(played, $"Card '{cardName}' could not be played with a valid target setup.");
                Assert.IsFalse(setup.Source.Hand.Contains(setup.Card), $"Card '{cardName}' was not consumed from hand.");
                foreach (CardInstance cost in setup.CostCards)
                    Assert.IsFalse(setup.Source.Hand.Contains(cost), $"Cost card was not consumed for '{cardName}'.");

                int drawn = Mathf.Min(TotalDrawAmount(data), 5);
                Assert.AreEqual(handBefore - (1 + data.Cost) + drawn, setup.Source.Hand.Cards.Count,
                    $"'{cardName}': unexpected hand size after play.");
            }
        }

        [Test]
        [TestCaseSource(nameof(RegisteredCardNames))]
        public void Card_Outcome_MatchesDesign(string cardName)
        {
            CardData data = Resolve(cardName);
            ExpectedOutcome expected = SpecFor(data);
            using (Setup setup = BuildSetup(data))
            {
                setup.TargetPickCell = expected.TargetPickCell;
                Assert.IsTrue(setup.Play(), $"Card '{cardName}' could not be played with a valid target setup.");

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

        /// <summary>
        /// Fireball's description: "Deal 3 damage to the character there and 1 damage to each
        /// adjacent character." This test asserts exactly that.
        /// </summary>
        [Test]
        public void Fireball_MatchesDescription()
        {
            CardData fireball = Resolve("Fireball");
            using (Setup setup = BuildSetup(fireball))
            {
                // Reset source to full HP so "source is not a target" is unambiguous.
                setup.Source.Stats.Initialize();

                // Put the primary target ON the picked hex and a victim on an adjacent hex.
                HexCoordinates center = new HexCoordinates(2, 0); // range 2 from source (0,0)
                setup.Enemy.PlaceOnCell(setup.Grid.GetCell(center));
                Character adjacent = TestHelper.CreateCharacterOnGrid(setup.Grid, 1, 3, 0);  // 1 hex away
                Character far = TestHelper.CreateCharacterOnGrid(setup.Grid, 1, 4, 0);       // 2 hexes away

                try
                {
                    setup.TargetPickCell = center;
                    Assert.IsTrue(setup.Play(), "Fireball should play against a valid hex.");

                    Assert.AreEqual(7, setup.Enemy.Stats.CurrentHealth,
                        "Character on the picked hex should take 3 damage.");
                    Assert.AreEqual(9, adjacent.Stats.CurrentHealth,
                        "Adjacent characters should take 1 damage.");
                    Assert.AreEqual(10, far.Stats.CurrentHealth,
                        "Characters 2+ hexes away should take no damage.");
                    Assert.AreEqual(10, setup.Source.Stats.CurrentHealth,
                        "Source is 2 hexes away and must not be hit by its own Fireball.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(adjacent.gameObject);
                    UnityEngine.Object.DestroyImmediate(far.gameObject);
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

        private static int TotalDrawAmount(CardData card)
        {
            if (card.Steps == null) return 0;
            int total = 0;
            foreach (CardEffectStep step in card.Steps)
            {
                if (step != null) total += TotalDrawAmount(step.Effect);
            }
            return total;
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
                case "Bulwark":
                    return SourceBlock(8);
                case "Defend":
                case "Inspire":
                    return SourceBlockDraw(4, 1);
                case "Restore":
                    return SourceHeal(9);
                case "Painful Slash":
                    return SelfDamageAttack(1, 4);
                case "Walk":
                case "Only the enemy has this card":
                    return MoveTo(1);
                case "Jog":
                    return MoveTo(2);
                case "Run":
                    return MoveTo(3);
                case "Sprint":
                    return MoveTo(4);
                case "Fireball":
                    // Description: "Deal 3 damage to the character there and 1 damage to each
                    // adjacent character." In the generic fixture the enemy sits adjacent to the
                    // picked hex, so it should take 1 and the caster (2 hexes out) 0.
                    return new ExpectedOutcome
                    {
                        SourceHealth = 7,
                        SourceBlock = 3,
                        EnemyHealth = 9,
                        TargetPickCell = new HexCoordinates(2, -1)
                    };
                case "Flame Bolt":
                    return RangedAttack(2);
                case "Mystic Bolt":
                    return RangedAttack(3);
                case "Foretell":
                    // "Draw a card. Set its cost to 1." Cost 0: net hand unchanged (played one, drew one).
                    return new ExpectedOutcome { SourceHealth = 7, SourceBlock = 3, EnemyHealth = 10, HandCount = 1 };
                case "Transmute":
                    // "Take 1 damage. Draw a card." Block absorbs the self-damage.
                    return new ExpectedOutcome { SourceHealth = 7, SourceBlock = 2, EnemyHealth = 10, HandCount = 1 };
                case "Prepare":
                    // "Decrease the cost of cards in your hand by 1." Cost 1, no other state change.
                    return new ExpectedOutcome { SourceHealth = 7, SourceBlock = 3, EnemyHealth = 10, HandCount = 0 };
                case "Painful Sprint":
                    return MoveAndSelfDamage(2, 1);
                case "Disarming Slash":
                    return Attack(2);
                case "Heavy Slash":
                    return Attack(4);
                case "Parry":
                    return new ExpectedOutcome { SourceHealth = 7, SourceBlock = 5, EnemyHealth = 10 };
                case "Grease":
                case "Fire Wall":
                case "Mould Earth":
                    // Tile-effect cards: no target standing in the generic fixture and no
                    // TileEffectSystem present in the battery, so state is unchanged.
                    return new ExpectedOutcome { SourceHealth = 7, SourceBlock = 3, EnemyHealth = 10 };
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

        private static ExpectedOutcome SelfDamageAttack(int selfDamage, int enemyDamage)
        {
            return new ExpectedOutcome
            {
                SourceHealth = 7,
                SourceBlock = 3 - selfDamage,
                EnemyHealth = 10 - enemyDamage
            };
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

        private static ExpectedOutcome MoveAndSelfDamage(int range, int selfDamage)
        {
            return new ExpectedOutcome
            {
                SourceHealth = 7,
                SourceBlock = 3 - selfDamage,
                EnemyHealth = 10,
                TargetCellCoordinates = new HexCoordinates(range, 0)
            };
        }

        private static ExpectedOutcome RangedAttack(int damage)
        {
            return new ExpectedOutcome { SourceHealth = 7, SourceBlock = 3, EnemyHealth = 10 - damage };
        }

        private static ExpectedOutcome AreaBlast(int damage, HexCoordinates targetPick)
        {
            return new ExpectedOutcome
            {
                SourceHealth = 7,
                SourceBlock = 3 - damage,
                EnemyHealth = 10 - damage,
                TargetPickCell = targetPick
            };
        }

        [Test]
        public void HexCell_CellConstraintEmpty_RejectsOccupiedCell()
        {
            CardData walk = Resolve("Walk");
            using (Setup setup = BuildSetup(walk))
            {
                Character blocker = TestHelper.CreateCharacterOnGrid(setup.Grid, 0, 1, 0);

                int handBefore = setup.Source.Hand.Cards.Count;
                int sourceHp = setup.Source.Stats.CurrentHealth;

                HexCell targetCell = setup.Grid.GetCell(new HexCoordinates(1, 0));
                bool played = setup.Resolver.PlayCard(setup.Source, setup.Card, setup.CostCards, targetCell, null);

                Assert.IsFalse(played);
                Assert.AreEqual(handBefore, setup.Source.Hand.Cards.Count);
                Assert.AreEqual(sourceHp, setup.Source.Stats.CurrentHealth);

                UnityEngine.Object.DestroyImmediate(blocker.gameObject);
            }
        }

        /// <summary>
        /// Parry's conditional effect: "If an adjacent enemy exists, gain 2 block."
        /// Tests both branches — condition true (enemy adjacent) and false (no adjacent enemy).
        /// </summary>
        [Test]
        public void Parry_ConditionalBranches()
        {
            CardData parry = Resolve("Parry");
            using (Setup setup = BuildSetup(parry))
            {
                // Branch 1 — enemy IS adjacent (1,-1). Condition true → gain 2 block.
                Assert.IsTrue(setup.Play(), "Parry should play with an adjacent enemy.");
                Assert.AreEqual(5, setup.Source.Stats.Block,
                    "Parry should grant 2 block when an enemy is adjacent (3 starting + 2 = 5).");
                Assert.AreEqual(10, setup.Enemy.Stats.CurrentHealth,
                    "Parry must not damage the enemy.");

                // Re-add card and cost cards to hand for branch 2.
                setup.Source.Hand.AddCard(setup.Card);
                foreach (CardInstance cost in setup.CostCards)
                    setup.Source.Hand.AddCard(cost);

                // Reset stats and move enemy out of adjacency.
                setup.Source.Stats.Initialize();
                setup.Source.Stats.ApplyDamage(3);
                setup.Source.Stats.GainBlock(3);
                setup.Enemy.PlaceOnCell(setup.Grid.GetCell(new HexCoordinates(2, -1)));

                // Branch 2 — no enemy adjacent. Condition false → no block gain.
                Assert.IsTrue(setup.Play(), "Parry should play even without an adjacent enemy.");
                Assert.AreEqual(3, setup.Source.Stats.Block,
                    "Parry must not grant block when no enemy is adjacent.");
                Assert.AreEqual(10, setup.Enemy.Stats.CurrentHealth,
                    "Parry must not damage the enemy.");
            }
        }

        private struct ExpectedOutcome
        {
            public int SourceHealth;
            public int SourceBlock;
            public int EnemyHealth;
            public int HandCount;
            public HexCoordinates? TargetCellCoordinates;
            /// <summary>If set, the Play method picks this cell instead of (Range, 0).</summary>
            public HexCoordinates? TargetPickCell;
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

            public HexCoordinates? TargetPickCell;

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
                        if (TargetPickCell.HasValue)
                            targetCell = Grid.GetCell(TargetPickCell.Value);
                        else
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
