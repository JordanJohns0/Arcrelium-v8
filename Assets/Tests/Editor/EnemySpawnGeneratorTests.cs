using System.Collections.Generic;
using System.Linq;
using Enemies;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Enemies
{
    [TestFixture]
    public class EnemySpawnGeneratorTests
    {
        private static EnemyType Type(string enemyName, int pointCost, int minCount)
        {
            var type = ScriptableObject.CreateInstance<EnemyType>();
            type.EnemyName = enemyName;
            type.PointCost = pointCost;
            type.MinCount = minCount;
            return type;
        }

        private static EnemyGroup Group(params EnemyType[] members)
        {
            var group = ScriptableObject.CreateInstance<EnemyGroup>();
            group.MemberTypes = members.ToList();
            return group;
        }

        private static EncounterPlan Generate(EnemyGroup group, int budget, int seed)
        {
            return EnemySpawnGenerator.Generate(group, budget, new System.Random(seed));
        }

        private static int Count(EncounterPlan plan, EnemyType type)
        {
            CountedEnemy found = plan.Enemies.FirstOrDefault(e => e.Type == type);
            return found?.Count ?? 0;
        }

        [Test]
        public void BudgetForLevel_ScalesLinearlyFromBaseDifficulty()
        {
            Assert.AreEqual(0, EnemySpawnGenerator.BudgetForLevel(0, 20, 1));
            Assert.AreEqual(40, EnemySpawnGenerator.BudgetForLevel(40, 20, 1));
            Assert.AreEqual(60, EnemySpawnGenerator.BudgetForLevel(40, 20, 2));
            Assert.AreEqual(100, EnemySpawnGenerator.BudgetForLevel(40, 20, 4));
        }

        [Test]
        public void BudgetForLevel_ClampsLevelAndBudget()
        {
            Assert.AreEqual(40, EnemySpawnGenerator.BudgetForLevel(40, 20, 0));
            Assert.AreEqual(40, EnemySpawnGenerator.BudgetForLevel(40, 20, -3));
            Assert.AreEqual(40, EnemySpawnGenerator.BudgetForLevel(0, 20, 3));
            Assert.AreEqual(0, EnemySpawnGenerator.BudgetForLevel(-10, 20, 1));
        }

        [Test]
        public void ExactBudget_FillsPerfectly_RespectingMins()
        {
            EnemyType melee = Type("Melee Kobold", 10, 1);
            EnemyType ranged = Type("Ranged Kobold", 15, 1);
            EncounterPlan plan = Generate(Group(melee, ranged), 60, 12345);

            Assert.AreEqual(60, plan.TotalCost);
            Assert.AreEqual(60, plan.Budget);
            Assert.IsFalse(plan.Overshot);
            Assert.AreEqual(3, Count(plan, melee));
            Assert.AreEqual(2, Count(plan, ranged));
            Assert.AreEqual(5, plan.TotalCount);
        }

        [Test]
        public void NeverExceedsBudget_SpendsAsMuchAsPossible()
        {
            EnemyType melee = Type("Melee Kobold", 10, 1);
            EnemyType ranged = Type("Ranged Kobold", 15, 1);
            EncounterPlan plan = Generate(Group(melee, ranged), 57, 999);

            Assert.LessOrEqual(plan.TotalCost, 57);
            Assert.AreEqual(55, plan.TotalCost);
            Assert.IsFalse(plan.Overshot);
            Assert.GreaterOrEqual(Count(plan, melee), 1);
            Assert.GreaterOrEqual(Count(plan, ranged), 1);
        }

        [Test]
        public void SmallBudget_StillSpawnsMins()
        {
            EnemyType melee = Type("Melee Kobold", 10, 1);
            EnemyType ranged = Type("Ranged Kobold", 15, 1);
            EncounterPlan plan = Generate(Group(melee, ranged), 40, 1);

            Assert.AreEqual(40, plan.TotalCost);
            Assert.AreEqual(1, Count(plan, melee));
            Assert.AreEqual(2, Count(plan, ranged));
        }

        [Test]
        public void MinsExceedingBudget_SpawnMinsAnyway_Overshot()
        {
            EnemyType melee = Type("Melee Kobold", 10, 1);
            EnemyType ranged = Type("Ranged Kobold", 15, 1);
            EncounterPlan plan = Generate(Group(melee, ranged), 5, 1);

            Assert.IsTrue(plan.Overshot);
            Assert.AreEqual(25, plan.TotalCost);
            Assert.AreEqual(1, Count(plan, melee));
            Assert.AreEqual(1, Count(plan, ranged));
        }

        [Test]
        public void MinsEqualBudget_NoOvershoot()
        {
            EnemyType melee = Type("Melee Kobold", 10, 1);
            EnemyType ranged = Type("Ranged Kobold", 15, 1);
            EncounterPlan plan = Generate(Group(melee, ranged), 25, 1);

            Assert.IsFalse(plan.Overshot);
            Assert.AreEqual(25, plan.TotalCost);
            Assert.AreEqual(1, Count(plan, melee));
            Assert.AreEqual(1, Count(plan, ranged));
        }

        [Test]
        public void SingleType_FillsAsCloseAsPossible()
        {
            EnemyType kobold = Type("Kobold", 10, 0);
            EncounterPlan plan = Generate(Group(kobold), 25, 1);

            Assert.AreEqual(20, plan.TotalCost);
            Assert.AreEqual(2, Count(plan, kobold));
        }

        [Test]
        public void ZeroBudget_WithoutMins_SpawnsNothing()
        {
            EnemyType kobold = Type("Kobold", 10, 0);
            EncounterPlan plan = Generate(Group(kobold), 0, 1);

            Assert.AreEqual(0, plan.Enemies.Count);
            Assert.AreEqual(0, plan.TotalCount);
            Assert.AreEqual(0, plan.TotalCost);
        }

        [Test]
        public void EmptyGroup_SpawnsNothing()
        {
            EncounterPlan plan = Generate(Group(), 60, 1);

            Assert.AreEqual(0, plan.Enemies.Count);
            Assert.AreEqual(0, plan.TotalCount);
            Assert.AreEqual(60, plan.Budget);
        }

        [Test]
        public void NullGroup_SpawnsNothing()
        {
            EncounterPlan plan = EnemySpawnGenerator.Generate(null, 60, new System.Random(1));

            Assert.AreEqual(0, plan.Enemies.Count);
            Assert.AreEqual(0, plan.TotalCount);
        }

        [Test]
        public void UnboundAndZeroCostMembers_AreSkipped()
        {
            EnemyType cheap = Type("Swarmer", 0, 0);
            EnemyType kobold = Type("Kobold", 10, 0);
            EncounterPlan plan = Generate(Group(null, cheap, kobold), 15, 1);

            Assert.AreEqual(10, plan.TotalCost);
            Assert.AreEqual(1, Count(plan, kobold));
            Assert.AreEqual(0, Count(plan, cheap));
        }

        [Test]
        public void SameSeed_ProducesIdenticalComposition()
        {
            EnemyType melee = Type("Melee Kobold", 10, 1);
            EnemyType ranged = Type("Ranged Kobold", 15, 1);
            EncounterPlan a = Generate(Group(melee, ranged), 57, 4242);
            EncounterPlan b = Generate(Group(melee, ranged), 57, 4242);

            Assert.AreEqual(Count(a, melee), Count(b, melee));
            Assert.AreEqual(Count(a, ranged), Count(b, ranged));
        }

        [Test]
        public void OptimalTies_AreResolvedAcrossSeeds_AmongClosestFills()
        {
            EnemyType melee = Type("Melee Kobold", 10, 1);
            EnemyType ranged = Type("Ranged Kobold", 15, 1);

            var meleeCounts = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                EncounterPlan plan = Generate(Group(melee, ranged), 57, seed);
                Assert.AreEqual(55, plan.TotalCost, "seed " + seed);
                Assert.That(new[] { 1, 4 }.Contains(Count(plan, melee)), Is.True, "seed " + seed);
                meleeCounts.Add(Count(plan, melee));
            }

            Assert.AreEqual(2, meleeCounts.Count, "both optimal compositions should surface across seeds");
        }
    }
}