using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using Game;
using Object = UnityEngine.Object;

namespace Tests
{
    [TestFixture]
    public class AICardScoringTests
    {
        private Character source;
        private Character target;
        private readonly List<ScriptableObject> trackedSops = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            source = TestHelper.CreateCharacter(0, 0, 0);
            target = TestHelper.CreateCharacter(100, 3, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(source.CurrentCell.gameObject);
            Object.DestroyImmediate(source.gameObject);
            Object.DestroyImmediate(target.CurrentCell.gameObject);
            Object.DestroyImmediate(target.gameObject);
            foreach (var so in trackedSops)
            {
                if (so != null)
                    Object.DestroyImmediate(so);
            }
            trackedSops.Clear();
        }

        private T MakeEffect<T>() where T : CardEffectSO
        {
            var effect = ScriptableObject.CreateInstance<T>();
            trackedSops.Add(effect);
            return effect;
        }

        private CardData MakeCard(string name, CardEffectSO effect, TargetType targetType = TargetType.Self, int cost = 0)
        {
            var card = TestHelper.MakeCard(name, targetType, cost, effect);
            trackedSops.Add(card);
            return card;
        }

        private CardData MakeAttack(int damage)
        {
            var effect = MakeEffect<AttackEffect>();
            effect.Damage = CardValue.MakeConstant(damage);
            return MakeCard($"Attack {damage}", effect, TargetType.EnemyCharacter);
        }

        [Test]
        public void EffectiveHpToKill_IncludesBlockArmorAndHealth()
        {
            target.Stats.Block = 2;
            target.Stats.Armor = 3;
            target.Stats.CurrentHealth = 4;

            Assert.AreEqual(9, AICardScoring.EffectiveHpToKill(target));
        }

        [Test]
        public void Score_NullCard_ReturnsMinValue()
        {
            Assert.AreEqual(float.MinValue, AICardScoring.Score(null, source, target, 0));
        }

        [Test]
        public void Score_LethalDamage_AddsLethalBonus()
        {
            target.Stats.CurrentHealth = 5;

            float score = AICardScoring.Score(MakeAttack(5), source, target, 1);

            Assert.AreEqual(5 * AICardScoring.DamagePerHp + AICardScoring.LethalBonus, score, 0.0001f);
        }

        [Test]
        public void Score_DamageScalesWithHpDealt()
        {
            target.Stats.CurrentHealth = 10;

            float score = AICardScoring.Score(MakeAttack(3), source, target, 1);

            Assert.AreEqual(3 * AICardScoring.DamagePerHp, score, 0.0001f);
        }

        [Test]
        public void Score_NearDeathTarget_GetsWeakTargetBonus()
        {
            target.Stats.CurrentHealth = AICardScoring.NearDeathThreshold;

            float score = AICardScoring.Score(MakeAttack(1), source, target, 1);

            Assert.AreEqual(
                AICardScoring.DamagePerHp + AICardScoring.WeakTargetBonus, score, 0.0001f);
        }

        [Test]
        public void Score_Heal_ScaledByMissingHealth()
        {
            source.Stats.CurrentHealth = 5;
            var effect = MakeEffect<HealEffect>();
            effect.Amount = CardValue.MakeConstant(2);

            float score = AICardScoring.Score(MakeCard("Heal", effect), source, null, 0);

            Assert.AreEqual(2 * AICardScoring.HealPerPoint, score, 0.0001f);
        }

        [Test]
        public void Score_HealNearDeath_AddsNearDeathBonus()
        {
            source.Stats.CurrentHealth = 1;
            var effect = MakeEffect<HealEffect>();
            effect.Amount = CardValue.MakeConstant(2);

            float score = AICardScoring.Score(MakeCard("Heal", effect), source, null, 0);

            Assert.AreEqual(2 * AICardScoring.HealPerPoint + AICardScoring.NearDeathHealBonus, score, 0.0001f);
        }

        [Test]
        public void Score_HealAtFullHealth_ScoresZero()
        {
            var effect = MakeEffect<HealEffect>();
            effect.Amount = CardValue.MakeConstant(2);

            float score = AICardScoring.Score(MakeCard("Heal", effect), source, null, 0);

            Assert.AreEqual(0f, score, 0.0001f);
        }

        [Test]
        public void Score_BlockThreatened_OutranksIdle()
        {
            var effect = MakeEffect<GainBlockEffect>();
            effect.Amount = CardValue.MakeConstant(4);

            float threatened = AICardScoring.Score(MakeCard("Block", effect), source, null, AICardScoring.ThreatRange);
            float idle = AICardScoring.Score(MakeCard("Block", effect), source, null, -1);

            Assert.Greater(threatened, idle);
        }

        [Test]
        public void Score_DrawWithSpace_ScoresPerCard()
        {
            var effect = MakeEffect<DrawCardsEffect>();
            effect.Amount = CardValue.MakeConstant(2);

            float score = AICardScoring.Score(MakeCard("Draw", effect), source, null, -1);

            Assert.AreEqual(2 * AICardScoring.DrawPerCard, score, 0.0001f);
        }

        [Test]
        public void Score_DrawAtFullHand_Penalized()
        {
            for (int i = 0; i < source.Hand.MaxCapacity; i++)
                source.Hand.AddCard(new CardInstance(MakeCard($"Fill {i}", MakeEffect<HealEffect>())));

            var effect = MakeEffect<DrawCardsEffect>();
            effect.Amount = CardValue.MakeConstant(2);

            float score = AICardScoring.Score(MakeCard("Draw", effect), source, null, -1);

            Assert.AreEqual(AICardScoring.NoHandSpacePenalty, score, 0.0001f);
        }

        [Test]
        public void Score_Composite_AccumulatesSubEffects()
        {
            source.Stats.CurrentHealth = 5;

            var attack = MakeEffect<AttackEffect>();
            attack.Damage = CardValue.MakeConstant(2);
            var heal = MakeEffect<HealEffect>();
            heal.Amount = CardValue.MakeConstant(1);
            var block = MakeEffect<GainBlockEffect>();
            block.Amount = CardValue.MakeConstant(1);
            var draw = MakeEffect<DrawCardsEffect>();
            draw.Amount = CardValue.MakeConstant(1);

            var composite = MakeEffect<CompositeEffect>();
            composite.Effects = new List<CardEffectSO> { attack, heal, block, draw };

            float score = AICardScoring.Score(MakeCard("All", composite, TargetType.EnemyCharacter), source, target, 1);

            Assert.AreEqual(
                2 * AICardScoring.DamagePerHp
                + 1 * AICardScoring.HealPerPoint
                + 1 * AICardScoring.BlockThreatened
                + 1 * AICardScoring.DrawPerCard, score, 0.0001f);
        }

        [Test]
        public void Score_CostPenalty_ReducesScore()
        {
            target.Stats.CurrentHealth = 10;

            float free = AICardScoring.Score(MakeAttack(3), source, target, 1);
            var costlyEffect = MakeEffect<AttackEffect>();
            costlyEffect.Damage = CardValue.MakeConstant(3);
            var costly = MakeCard("Costly", costlyEffect, TargetType.EnemyCharacter, cost: 2);
            float costlyScore = AICardScoring.Score(costly, source, target, 1);

            Assert.AreEqual(free - 2 * AICardScoring.CostPenalty, costlyScore, 0.0001f);
        }

        [Test]
        public void ScoreMove_RewardsClosingDistance()
        {
            Assert.AreEqual((4 - 1) * AICardScoring.MovePerHex, AICardScoring.ScoreMove(4, 1), 0.0001f);
        }
    }
}
