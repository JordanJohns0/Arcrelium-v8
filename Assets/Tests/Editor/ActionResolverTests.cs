using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Characters;
using HexGrid;
using Game;

namespace Tests.Game
{
    using Cards = global::Cards;

    [TestFixture]
    public class ActionResolverTests
    {
        private ActionResolver resolver;
        private Character source;
        private Character target;
        private CardData attackCardData;
        private CardData selfCardData;

        [SetUp]
        public void SetUp()
        {
            var resolverGo = new GameObject("Resolver");
            resolver = resolverGo.AddComponent<ActionResolver>();

            source = TestHelper.CreateCharacter(0, 0, 0);
            target = TestHelper.CreateCharacter(1, 1, 0);

            var attackEffect = ScriptableObject.CreateInstance<Cards.Effects.AttackEffect>();
            attackEffect.Damage = CardValue.MakeConstant(3);
            attackCardData = ScriptableObject.CreateInstance<CardData>();
            attackCardData.CardName = "Attack";
            attackCardData.TargetType = TargetType.EnemyCharacter;
            attackCardData.Range = 5;
            attackCardData.Cost = 0;
            attackCardData.Effect = attackEffect;

            var selfEffect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();
            selfEffect.Amount = CardValue.MakeConstant(2);
            selfCardData = ScriptableObject.CreateInstance<CardData>();
            selfCardData.CardName = "Defend";
            selfCardData.TargetType = TargetType.Self;
            selfCardData.Range = 0;
            selfCardData.Cost = 0;
            selfCardData.Effect = selfEffect;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(resolver.gameObject);
            Object.DestroyImmediate(source.CurrentCell.gameObject);
            Object.DestroyImmediate(source.gameObject);
            Object.DestroyImmediate(target.CurrentCell.gameObject);
            Object.DestroyImmediate(target.gameObject);
        }

        [Test]
        public void PlayCard_NullSource_ReturnsFalse()
        {
            var card = new CardInstance(attackCardData);
            Assert.IsFalse(resolver.PlayCard(null, card));
        }

        [Test]
        public void PlayCard_CardNotInHand_ReturnsFalse()
        {
            var card = new CardInstance(attackCardData);
            Assert.IsFalse(resolver.PlayCard(source, card));
        }

        [Test]
        public void PlayCard_SuccessfulAttack()
        {
            source.Hand.AddCard(new CardInstance(attackCardData));
            var card = source.Hand.Cards[0];

            bool played = resolver.PlayCard(source, card, targetCharacter: target);
            Assert.IsTrue(played);
        }

        [Test]
        public void PlayCard_RemovesCardFromHand()
        {
            var card = new CardInstance(attackCardData);
            source.Hand.AddCard(card);

            resolver.PlayCard(source, card, targetCharacter: target);
            Assert.IsFalse(source.Hand.Contains(card));
        }

        [Test]
        public void PlayCard_CardWithCost_DiscardsCostCards()
        {
            var expensiveCardData = ScriptableObject.CreateInstance<CardData>();
            expensiveCardData.CardName = "Expensive";
            expensiveCardData.TargetType = TargetType.Self;
            expensiveCardData.Cost = 1;
            expensiveCardData.Effect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();

            var expensive = new CardInstance(expensiveCardData);
            var costCard = new CardInstance(attackCardData);

            source.Hand.AddCard(expensive);
            source.Hand.AddCard(costCard);

            resolver.PlayCard(source, expensive, costCards: new List<CardInstance> { costCard });

            Assert.IsFalse(source.Hand.Contains(costCard));
            Assert.IsFalse(source.Hand.Contains(expensive));
        }

        [Test]
        public void PlayCard_WrongCostCount_ReturnsFalse()
        {
            var expensiveCardData = ScriptableObject.CreateInstance<CardData>();
            expensiveCardData.CardName = "Expensive";
            expensiveCardData.TargetType = TargetType.Self;
            expensiveCardData.Cost = 2;
            expensiveCardData.Effect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();

            var expensive = new CardInstance(expensiveCardData);
            var costCard = new CardInstance(attackCardData);

            source.Hand.AddCard(expensive);
            source.Hand.AddCard(costCard);

            bool played = resolver.PlayCard(source, expensive, costCards: new List<CardInstance> { costCard });
            Assert.IsFalse(played);
        }

        [Test]
        public void PlayCard_SelfCard_NoTargetNeeded()
        {
            var card = new CardInstance(selfCardData);
            source.Hand.AddCard(card);

            bool played = resolver.PlayCard(source, card);
            Assert.IsTrue(played);
        }

        [Test]
        public void PlayCard_NullEffect_ReturnsFalse()
        {
            var noEffectData = ScriptableObject.CreateInstance<CardData>();
            noEffectData.Cost = 0;
            noEffectData.TargetType = TargetType.None;
            noEffectData.Effect = null;

            var card = new CardInstance(noEffectData);
            source.Hand.AddCard(card);

            Assert.IsFalse(resolver.PlayCard(source, card));
        }

        [Test]
        public void PlayCard_FiresOnCardPlayed()
        {
            bool fired = false;
            resolver.OnCardPlayed += (s, d) => fired = true;

            var card = new CardInstance(selfCardData);
            source.Hand.AddCard(card);

            resolver.PlayCard(source, card);
            Assert.IsTrue(fired);
        }

        [Test]
        public void PlayCard_FailedPlay_DoesNotDiscard()
        {
            var card = new CardInstance(attackCardData);
            source.Hand.AddCard(card);

            resolver.PlayCard(source, card, targetCharacter: null);
            Assert.IsTrue(source.Hand.Contains(card));
        }

        [Test]
        public void PlayCard_EnemyTargetCard_RejectsAllyTarget()
        {
            var ally = TestHelper.CreateCharacter(2, 2, 0, teamId: 0);
            try
            {
                source.Hand.AddCard(new CardInstance(attackCardData));
                var card = source.Hand.Cards[0];

                bool played = resolver.PlayCard(source, card, targetCharacter: ally);

                Assert.IsFalse(played);
                Assert.IsTrue(source.Hand.Contains(card));
            }
            finally
            {
                Object.DestroyImmediate(ally.CurrentCell.gameObject);
                Object.DestroyImmediate(ally.gameObject);
            }
        }

        [Test]
        public void PlayCard_AllyTargetCard_RejectsEnemyTarget()
        {
            var allyTargetCardData = ScriptableObject.CreateInstance<CardData>();
            allyTargetCardData.CardName = "Buff";
            allyTargetCardData.TargetType = TargetType.AllyCharacter;
            allyTargetCardData.Range = 5;
            allyTargetCardData.Cost = 0;
            allyTargetCardData.Effect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();

            source.Hand.AddCard(new CardInstance(allyTargetCardData));
            var card = source.Hand.Cards[0];

            bool played = resolver.PlayCard(source, card, targetCharacter: target);

            Assert.IsFalse(played);
            Assert.IsTrue(source.Hand.Contains(card));
        }

        [Test]
        public void PlayCard_DuplicateCostCard_ReturnsFalse()
        {
            var expensiveCardData = ScriptableObject.CreateInstance<CardData>();
            expensiveCardData.CardName = "Expensive";
            expensiveCardData.TargetType = TargetType.Self;
            expensiveCardData.Cost = 2;
            expensiveCardData.Effect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();

            var expensive = new CardInstance(expensiveCardData);
            var costCard = new CardInstance(attackCardData);

            source.Hand.AddCard(expensive);
            source.Hand.AddCard(costCard);

            bool played = resolver.PlayCard(source, expensive, costCards: new List<CardInstance> { costCard, costCard });
            Assert.IsFalse(played);
            Assert.IsTrue(source.Hand.Contains(expensive), "Card must stay in hand when cost payment is rejected.");
        }

        [Test]
        public void PlayCard_CostCardNotInHand_ReturnsFalse()
        {
            var expensiveCardData = ScriptableObject.CreateInstance<CardData>();
            expensiveCardData.CardName = "Expensive";
            expensiveCardData.TargetType = TargetType.Self;
            expensiveCardData.Cost = 1;
            expensiveCardData.Effect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();

            var expensive = new CardInstance(expensiveCardData);
            var notInHand = new CardInstance(attackCardData);

            source.Hand.AddCard(expensive);

            bool played = resolver.PlayCard(source, expensive, costCards: new List<CardInstance> { notInHand });
            Assert.IsFalse(played);
            Assert.IsTrue(source.Hand.Contains(expensive), "Card must stay in hand when a cost card is not in hand.");
        }

        [Test]
        public void PlayCard_PlayedCardAsCost_ReturnsFalse()
        {
            var expensiveCardData = ScriptableObject.CreateInstance<CardData>();
            expensiveCardData.CardName = "Expensive";
            expensiveCardData.TargetType = TargetType.Self;
            expensiveCardData.Cost = 1;
            expensiveCardData.Effect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();

            var card = new CardInstance(expensiveCardData);
            source.Hand.AddCard(card);

            bool played = resolver.PlayCard(source, card, costCards: new List<CardInstance> { card });
            Assert.IsFalse(played);
            Assert.IsTrue(source.Hand.Contains(card), "The played card itself must not be usable as a cost card.");
        }

        [Test]
        public void PlayCard_HexCellCard_RejectsNullCell()
        {
            var moveCardData = ScriptableObject.CreateInstance<CardData>();
            moveCardData.CardName = "Walk";
            moveCardData.TargetType = TargetType.HexCell;
            moveCardData.Range = 1;
            moveCardData.Cost = 0;
            moveCardData.Effect = ScriptableObject.CreateInstance<Cards.Effects.MoveEffect>();

            var card = new CardInstance(moveCardData);
            source.Hand.AddCard(card);

            bool played = resolver.PlayCard(source, card);
            Assert.IsFalse(played);
            Assert.IsTrue(source.Hand.Contains(card), "HexCell card without a target cell must be rejected.");
        }

        [Test]
        public void PlayCard_EnemyCard_RejectsDeadTarget()
        {
            target.Stats.CurrentHealth = 0;

            var card = new CardInstance(attackCardData);
            source.Hand.AddCard(card);

            bool played = resolver.PlayCard(source, card, targetCharacter: target);
            Assert.IsFalse(played);
            Assert.IsTrue(source.Hand.Contains(card), "Card must stay in hand when the target is dead.");
        }

        [Test]
        public void PlayCard_AnyTargetCard_AcceptsEnemyAndAlly()
        {
            var anyCardData = ScriptableObject.CreateInstance<CardData>();
            anyCardData.CardName = "Aid";
            anyCardData.TargetType = TargetType.AnyCharacter;
            anyCardData.Range = 5;
            anyCardData.Cost = 0;
            anyCardData.Effect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();

            var ally = TestHelper.CreateCharacter(2, 2, 0, teamId: 0);
            try
            {
                source.Hand.AddCard(new CardInstance(anyCardData));
                bool playedOnEnemy = resolver.PlayCard(source, source.Hand.Cards[source.Hand.Cards.Count - 1], targetCharacter: target);

                source.Hand.AddCard(new CardInstance(anyCardData));
                bool playedOnAlly = resolver.PlayCard(source, source.Hand.Cards[source.Hand.Cards.Count - 1], targetCharacter: ally);

                Assert.IsTrue(playedOnEnemy);
                Assert.IsTrue(playedOnAlly);
            }
            finally
            {
                Object.DestroyImmediate(ally.CurrentCell.gameObject);
                Object.DestroyImmediate(ally.gameObject);
            }
        }

        [Test]
        public void PlayCard_FiresOnBeforeCardPlay()
        {
            bool fired = false;
            resolver.OnBeforeCardPlay += (CardContext context) => fired = true;

            var card = new CardInstance(selfCardData);
            source.Hand.AddCard(card);

            resolver.PlayCard(source, card);
            Assert.IsTrue(fired);
        }
    }
}
