using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using Game;

namespace Tests.Cards
{
    [TestFixture]
    public class CardCostEffectsTests
    {
        private Character source;

        [SetUp]
        public void SetUp()
        {
            source = TestHelper.CreateCharacter(0, 0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            if (source != null)
            {
                Object.DestroyImmediate(source.CurrentCell.gameObject);
                Object.DestroyImmediate(source.gameObject);
            }
        }

        private Deck GetDeck()
        {
            return (Deck)typeof(Character).GetField("deck", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(source);
        }

        private static CardData SelfCard(string name, int cost)
        {
            var effect = ScriptableObject.CreateInstance<GainBlockEffect>();
            effect.Amount = CardValue.MakeConstant(1);
            return TestHelper.MakeCard(name, cost, TestHelper.Step(effect, TargetType.Self));
        }

        [Test]
        public void CardInstance_Cost_DefaultsToBaseCost()
        {
            var data = SelfCard("Priced", 3);
            var instance = new CardInstance(data);

            Assert.AreEqual(3, instance.Cost);

            instance.Cost = 0;
            Assert.AreEqual(0, instance.Cost);
            Assert.AreEqual(3, data.Cost, "Mutating the instance must never touch the shared CardData.");
        }

        [Test]
        public void SetRandomCostModifier_ChangesTheContextCardCost()
        {
            var modifier = ScriptableObject.CreateInstance<SetRandomCostModifier>();
            modifier.MinCost = 0;
            modifier.MaxCost = 2;

            var card = new CardInstance(SelfCard("Roll", 9));
            modifier.Modify(card, new CardContext { Card = card });

            Assert.GreaterOrEqual(card.Cost, 0);
            Assert.LessOrEqual(card.Cost, 2);
        }

        [Test]
        public void DrawCards_FiresOnDrawnModifiersBeforeEnteringHand()
        {
            var random = ScriptableObject.CreateInstance<SetRandomCostModifier>();
            random.MinCost = 1;
            random.MaxCost = 1;

            var data = SelfCard("Drawn", 3);
            data.OnDrawnModifiers.Add(random);

            Deck deck = GetDeck();
            deck.SetStartingCards(new List<CardData> { data });
            deck.Shuffle();

            List<CardInstance> drawn = source.DrawCards(1);

            Assert.AreEqual(1, drawn.Count);
            Assert.AreEqual(1, drawn[0].Cost, "OnDrawnModifiers should re-roll the drawn copy's cost.");
            Assert.IsTrue(source.Hand.Contains(drawn[0]));
        }

        [Test]
        public void DrawCards_ReturnsOnlyCardsDrawnWithinHandLimit()
        {
            Deck deck = GetDeck();
            deck.SetStartingCards(new List<CardData> { SelfCard("A", 1), SelfCard("B", 1), SelfCard("C", 1) });
            deck.Shuffle();

            source.Hand.MaxCapacity = 2;
            List<CardInstance> drawn = source.DrawCards(3);

            Assert.AreEqual(2, drawn.Count);
            Assert.AreEqual(2, source.Hand.Cards.Count);
        }

        [Test]
        public void DrawCardsEffect_WithSetCostOption_AppliesCostToDrawnCards()
        {
            var draw = ScriptableObject.CreateInstance<DrawCardsEffect>();
            draw.Amount = CardValue.MakeConstant(2);
            var setCost = ScriptableObject.CreateInstance<SetCardCostModifier>();
            setCost.Cost = 1;

            Deck deck = GetDeck();
            deck.SetStartingCards(new List<CardData> { SelfCard("A", 4), SelfCard("B", 4), SelfCard("C", 4) });
            deck.Shuffle();

            var ctx = new CardContext { Source = source, StepModifiers = new List<CardModifierSO> { setCost } };
            draw.Execute(ctx);

            Assert.AreEqual(2, source.Hand.Cards.Count);
            foreach (CardInstance card in source.Hand.Cards)
                Assert.AreEqual(1, card.Cost, "Cards drawn by the effect should cost 1.");
            Assert.AreEqual(4, source.Hand.Cards[0].Data.Cost,
                "The option must change the instance cost, never the shared CardData.");
        }

        [Test]
        public void ApplyModifiersToHand_Decrease_LowersEveryHandCardAndClampsAtMin()
        {
            var effect = ScriptableObject.CreateInstance<ApplyModifiersToHandEffect>();
            var decrease = ScriptableObject.CreateInstance<DecreaseCardCostModifier>();
            decrease.Amount = 1;
            decrease.MinCost = 0;

            source.Hand.AddCard(new CardInstance(SelfCard("A", 2)));
            source.Hand.AddCard(new CardInstance(SelfCard("B", 0)));

            effect.Execute(new CardContext
            {
                Source = source,
                StepModifiers = new List<CardModifierSO> { decrease }
            });

            Assert.AreEqual(1, source.Hand.Cards[0].Cost);
            Assert.AreEqual(0, source.Hand.Cards[1].Cost);
        }

        [Test]
        public void ActionResolver_UsesInstanceCostWhenPlayed()
        {
            var resolverGo = new GameObject("Resolver");
            try
            {
                var resolver = resolverGo.AddComponent<ActionResolver>();

                var card = new CardInstance(SelfCard("Priced", 2));
                card.Cost = 1; // discounted in hand
                var fuel = new CardInstance(SelfCard("Fuel", 0));
                source.Hand.AddCard(card);
                source.Hand.AddCard(fuel);

                bool played = resolver.PlayCard(source, card, costCards: new List<CardInstance> { fuel });

                Assert.IsTrue(played, "A card with instance cost 1 must be payable with exactly one discard.");
                Assert.IsFalse(source.Hand.Contains(card));
                Assert.IsFalse(source.Hand.Contains(fuel));
            }
            finally
            {
                Object.DestroyImmediate(resolverGo);
            }
        }

        [Test]
        public void ActionResolver_RejectsCostCardCountAgainstInstanceCost()
        {
            var resolverGo = new GameObject("Resolver");
            try
            {
                var resolver = resolverGo.AddComponent<ActionResolver>();

                var card = new CardInstance(SelfCard("Priced", 2));
                var fuel = new CardInstance(SelfCard("Fuel", 0));
                source.Hand.AddCard(card);
                source.Hand.AddCard(fuel);

                bool played = resolver.PlayCard(source, card, costCards: new List<CardInstance> { fuel });

                Assert.IsFalse(played);
                Assert.IsTrue(source.Hand.Contains(card));
            }
            finally
            {
                Object.DestroyImmediate(resolverGo);
            }
        }

        [Test]
        public void Resolver_DecreaseHandCosts_OnlyAffectsCardsStillInHand()
        {
            var resolverGo = new GameObject("Resolver");
            try
            {
                var resolver = resolverGo.AddComponent<ActionResolver>();

                var decrease = ScriptableObject.CreateInstance<DecreaseCardCostModifier>();
                decrease.Amount = 1;
                var apply = ScriptableObject.CreateInstance<ApplyModifiersToHandEffect>();
                var step = TestHelper.Step(apply, TargetType.Self);
                step.CardModifiers = new List<CardModifierSO> { decrease };
                var discountData = TestHelper.MakeCard("Discount", 0, step);
                var discount = new CardInstance(discountData);
                var other = new CardInstance(SelfCard("Other", 3));

                source.Hand.AddCard(discount);
                source.Hand.AddCard(other);

                bool played = resolver.PlayCard(source, discount);

                Assert.IsTrue(played);
                Assert.AreEqual(2, other.Cost, "Remaining hand cards should drop by 1.");
                Assert.IsFalse(source.Hand.Contains(discount), "The played card is gone before effects run.");
            }
            finally
            {
                Object.DestroyImmediate(resolverGo);
            }
        }

        [Test]
        public void DiscardFromHand_FiresOnCardDiscarded()
        {
            var card = new CardInstance(SelfCard("Echo", 0));
            source.Hand.AddCard(card);

            CardInstance discarded = null;
            source.OnCardDiscarded += (c, card) => discarded = card;

            source.DiscardFromHand(card);

            Assert.AreEqual(card, discarded);
            Assert.IsFalse(source.Hand.Contains(card));
        }

        [Test]
        public void TurnManager_DiscardPlaysUntargetedCardForFree()
        {
            var tmGo = new GameObject("TurnManager");
            var arGo = new GameObject("ActionResolver");
            try
            {
                TurnManager turnManager = tmGo.AddComponent<TurnManager>();
                ActionResolver resolver = arGo.AddComponent<ActionResolver>();
                typeof(TurnManager).GetField("actionResolver", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(turnManager, resolver);

                turnManager.SetTurnOrder(new List<Character> { source });
                turnManager.StartMatch();

                var block = ScriptableObject.CreateInstance<GainBlockEffect>();
                block.Amount = CardValue.MakeConstant(3);
                var data = TestHelper.MakeCard("Echo", 0, TestHelper.Step(block, TargetType.Self));
                data.PlaysOnDiscard = true;
                var card = new CardInstance(data);
                source.Hand.AddCard(card);

                int blockBefore = source.Stats.Block;
                source.DiscardFromHand(card);

                Assert.AreEqual(blockBefore + 3, source.Stats.Block,
                    "Discarding the card should play it for free without paying a cost.");
                Assert.IsFalse(source.Hand.Contains(card));
            }
            finally
            {
                Object.DestroyImmediate(arGo);
                Object.DestroyImmediate(tmGo);
            }
        }

        [Test]
        public void TurnManager_DiscardDoesNotPlayTargetedCard()
        {
            var tmGo = new GameObject("TurnManager");
            var arGo = new GameObject("ActionResolver");
            try
            {
                TurnManager turnManager = tmGo.AddComponent<TurnManager>();
                ActionResolver resolver = arGo.AddComponent<ActionResolver>();
                typeof(TurnManager).GetField("actionResolver", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(turnManager, resolver);

                turnManager.SetTurnOrder(new List<Character> { source });
                turnManager.StartMatch();

                var attack = ScriptableObject.CreateInstance<AttackEffect>();
                attack.Damage = CardValue.MakeConstant(2);
                var data = TestHelper.MakeCard("Strike", 0, TestHelper.Step(attack, TargetType.EnemyCharacter));
                data.PlaysOnDiscard = true;
                var card = new CardInstance(data);
                source.Hand.AddCard(card);

                source.DiscardFromHand(card);

                Assert.IsFalse(source.Hand.Contains(card), "The card is discarded, not played.");
            }
            finally
            {
                Object.DestroyImmediate(arGo);
                Object.DestroyImmediate(tmGo);
            }
        }
    }
}