using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using Equipment;
using Game;
using Object = UnityEngine.Object;

namespace Tests.AI
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
            var card = TestHelper.MakeCard(name, cost, TestHelper.Step(effect, targetType));
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
        public void EffectiveHpToKill_IncludesBlockAndHealth()
        {
            target.Stats.Block = 2;
            target.Stats.CurrentHealth = 4;

            Assert.AreEqual(6, AICardScoring.EffectiveHpToKill(target));
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
        public void Score_MultipleSteps_AccumulatesAllEffects()
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

            var card = TestHelper.MakeCard("All", 0,
                TestHelper.Step(attack, TargetType.EnemyCharacter),
                TestHelper.Step(heal),
                TestHelper.Step(block),
                TestHelper.Step(draw));
            trackedSops.Add(card);

            float score = AICardScoring.Score(card, source, target, 1);

            Assert.AreEqual(
                2 * AICardScoring.DamagePerHp
                + 1 * AICardScoring.HealPerPoint
                + 1 * AICardScoring.BlockThreatened
                + 1 * AICardScoring.DrawPerCard, score, 0.0001f);
        }

        [Test]
        public void Score_AttackWithOnHitEffect_AccumulatesOnHitDamage()
        {
            target.Stats.CurrentHealth = 10;

            var attack = MakeEffect<AttackEffect>();
            attack.Damage = CardValue.MakeConstant(2);
            var onHit = MakeEffect<AttackEffect>();
            onHit.Damage = CardValue.MakeConstant(3);
            attack.OnHitEffects = new List<CardEffectSO> { onHit };

            float score = AICardScoring.Score(
                MakeCard("Poison Sting", attack, TargetType.EnemyCharacter), source, target, 1);

            Assert.AreEqual(5 * AICardScoring.DamagePerHp, score, 0.0001f,
                "The AI must count damage from effects that fire when the attack lands.");
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

        [Test]
        public void Score_SelfDamage_Penalized()
        {
            var effect = MakeEffect<SelfDamageEffect>();
            effect.Damage = CardValue.MakeConstant(2);

            float score = AICardScoring.Score(MakeCard("Self", effect), source, null, -1);

            Assert.AreEqual(-2 * AICardScoring.SelfDamagePerHp, score, 0.0001f);
        }

        [Test]
        public void Score_SelfDamageLethal_HeavilyPenalized()
        {
            var effect = MakeEffect<SelfDamageEffect>();
            effect.Damage = CardValue.MakeConstant(12);

            float score = AICardScoring.Score(MakeCard("Self", effect), source, null, -1);

            Assert.AreEqual(
                -(10 * AICardScoring.SelfDamagePerHp + AICardScoring.LethalBonus), score, 0.0001f);
        }

        [Test]
        public void Score_SelfDamageNearDeath_ExtraPenalty()
        {
            source.Stats.CurrentHealth = AICardScoring.NearDeathThreshold;
            var effect = MakeEffect<SelfDamageEffect>();
            effect.Damage = CardValue.MakeConstant(1);

            float score = AICardScoring.Score(MakeCard("Self", effect), source, null, -1);

            Assert.AreEqual(
                -(1 * AICardScoring.SelfDamagePerHp + AICardScoring.NearDeathHealBonus), score, 0.0001f);
        }

        [Test]
        public void EveryLibraryEffect_IsHandledByAIScoring()
        {
            GameDataLibrary library = GameDataLibrary.Instance;
            Assert.IsNotNull(library, "GameDataLibrary asset missing from Resources.");

            var handled = new HashSet<System.Type>
            {
                typeof(AttackEffect),
                typeof(HealEffect),
                typeof(GainBlockEffect),
                typeof(DrawCardsEffect),
                typeof(SelfDamageEffect),
                typeof(MoveEffect),
                typeof(AreaDamageEffect),
                typeof(ConditionalEffect),
                typeof(SetRandomCostEffect),
                typeof(DecreaseHandCostsEffect),
                typeof(PlaceTileEffect),
                typeof(FireWallEffect),
                typeof(PushOccupantsEffect),
                typeof(SpawnRockEffect)
            };

            var unhandled = new List<string>();
            foreach (CardData card in library.allCards)
            {
                if (card == null) continue;
                if (card.Steps == null) continue;
                foreach (CardEffectStep step in card.Steps)
                {
                    if (step != null)
                        CollectUnhandled(step.Effect, handled, unhandled, card.CardName, 0);
                }
            }

            Assert.IsEmpty(unhandled, "Effect types the AI cannot score: " + string.Join(", ", unhandled));
        }

        private static void CollectUnhandled(
            CardEffectSO effect,
            HashSet<System.Type> handled,
            List<string> unhandled,
            string cardName,
            int depth)
        {
            if (effect == null) return;
            if (!handled.Contains(effect.GetType()))
                unhandled.Add($"{cardName}:{effect.GetType().Name}");
            if (effect is AttackEffect attack && attack.OnHitEffects != null && depth < 8)
            {
                foreach (CardEffectSO onHit in attack.OnHitEffects)
                {
                    if (onHit != null) CollectUnhandled(onHit, handled, unhandled, cardName, depth + 1);
                }
            }
            if (effect is ConditionalEffect conditional && depth < 8)
            {
                if (conditional.TrueEffect != null)
                    CollectUnhandled(conditional.TrueEffect, handled, unhandled, cardName, depth + 1);
                if (conditional.FalseEffect != null)
                    CollectUnhandled(conditional.FalseEffect, handled, unhandled, cardName, depth + 1);
            }
        }
    }
}
