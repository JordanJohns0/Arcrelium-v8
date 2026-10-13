using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Characters;
using Cards;
using Cards.Effects;

namespace Tests.Cards
{
    /// <summary>
    /// Execution effects ("kill an enemy with X or less remaining HP"): instant defeat that
    /// bypasses block, is a no-op against healthier targets, and plays the attack stance.
    /// </summary>
    [TestFixture]
    public class KillTargetEffectTests
    {
        private static KillTargetEffect LoadExecuteAsset()
        {
            var effect = AssetDatabase.LoadAssetAtPath<KillTargetEffect>("Assets/CardEffect/Damage/KillTargetEffect_2.asset");
            Assert.IsNotNull(effect, "KillTargetEffect_2 asset missing.");
            return effect;
        }

        private static CardContext Context(Character source, Character target)
        {
            return new CardContext { Source = source, TargetCharacter = target };
        }

        [Test]
        public void ExecuteAsset_ThresholdIsTwo()
        {
            Character source = TestHelper.CreateCharacter(0, 0, 0);
            try
            {
                Assert.AreEqual(2, LoadExecuteAsset().Threshold.Evaluate(source),
                    "Execute should kill targets with exactly 2 or fewer HP.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
            }
        }

        [Test]
        public void ExecuteAsset_IsAnAttack()
        {
            Assert.IsTrue(LoadExecuteAsset().IsAttack,
                "Execution is an offensive play and must use the attack stance.");
        }

        [Test]
        public void Kill_WhenHealthAtOrBelowThreshold()
        {
            Character source = TestHelper.CreateCharacter(0, 0, 0);
            Character target = TestHelper.CreateCharacter(1, 1, 0);
            try
            {
                target.Stats.ApplyDamage(8); // 10 -> 2, exactly at threshold
                LoadExecuteAsset().Execute(Context(source, target));

                Assert.IsFalse(target.Stats.IsAlive, "A target at the threshold should be executed.");
                Assert.AreEqual(0, target.Stats.CurrentHealth, "Executed targets should drop to 0 HP.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void NoOp_WhenTargetAboveThreshold()
        {
            Character source = TestHelper.CreateCharacter(0, 0, 0);
            Character target = TestHelper.CreateCharacter(1, 1, 0);
            try
            {
                target.Stats.ApplyDamage(3); // 7 HP > 2
                LoadExecuteAsset().Execute(Context(source, target));

                Assert.IsTrue(target.Stats.IsAlive, "Healthy targets must survive an execution attempt.");
                Assert.AreEqual(7, target.Stats.CurrentHealth, "The execution should be a no-op.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void Kill_IgnoresBlock()
        {
            Character source = TestHelper.CreateCharacter(0, 0, 0);
            Character target = TestHelper.CreateCharacter(1, 1, 0);
            try
            {
                target.Stats.ApplyDamage(8); // 2 HP (at threshold) but fully armored
                target.Stats.GainBlock(6);
                LoadExecuteAsset().Execute(Context(source, target));

                Assert.IsFalse(target.Stats.IsAlive,
                    "An execution is not damage: block must not save an at-risk target.");
                Assert.AreEqual(0, target.Stats.Block, "The kill should clear the victim's block.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void KillTarget_DoesNotDamageOthers()
        {
            Character source = TestHelper.CreateCharacter(0, 0, 0);
            Character target = TestHelper.CreateCharacter(1, 1, 0);
            Character bystander = TestHelper.CreateCharacter(1, 2, 0);
            try
            {
                target.Stats.ApplyDamage(8); // 2 HP, at threshold
                LoadExecuteAsset().Execute(Context(source, target));

                Assert.AreEqual(10, bystander.Stats.CurrentHealth, "Execution must only hit its target.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(target.gameObject);
                Object.DestroyImmediate(bystander.gameObject);
            }
        }
    }
}