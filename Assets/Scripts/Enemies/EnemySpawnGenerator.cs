using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    public class CountedEnemy
    {
        public readonly EnemyType Type;
        public readonly int Count;

        public CountedEnemy(EnemyType type, int count)
        {
            Type = type;
            Count = count;
        }
    }

    /// <summary>The composition one encounter will spawn.</summary>
    public class EncounterPlan
    {
        public readonly List<CountedEnemy> Enemies = new List<CountedEnemy>();
        public int TotalCost;
        public int Budget;
        public bool Overshot;
        public int TotalCount;

        internal void Add(EnemyType type, int count)
        {
            if (count <= 0) return;
            Enemies.Add(new CountedEnemy(type, count));
            TotalCost += count * type.PointCost;
            TotalCount += count;
        }
    }

    /// <summary>
    /// Turns an <see cref="EnemyGroup"/> and a difficulty budget into a concrete encounter.
    /// Every member's MinCount is always spawned (even if that alone exceeds the budget); the
    /// remaining budget is spent as completely as possible without going over, and ties between
    /// equally-close compositions are broken randomly.
    /// </summary>
    public static class EnemySpawnGenerator
    {
        /// <summary>Difficulty budget for an encounter at <paramref name="level"/>. Level 1 yields the base.</summary>
        public static int BudgetForLevel(int baseDifficulty, int perLevelDifficulty, int level)
        {
            int safeLevel = Mathf.Max(1, level);
            return Mathf.Max(0, baseDifficulty + (safeLevel - 1) * perLevelDifficulty);
        }

        public static EncounterPlan Generate(EnemyGroup group, int budget, System.Random rng)
        {
            var plan = new EncounterPlan { Budget = Mathf.Max(0, budget) };
            if (group == null || group.MemberTypes == null) return plan;

            var members = new List<EnemyType>();
            for (int i = 0; i < group.MemberTypes.Count; i++)
            {
                EnemyType type = group.MemberTypes[i];
                if (type == null) continue;
                if (type.PointCost < 1)
                {
                    Debug.LogWarning($"[EnemySpawnGenerator] '{TypeName(type)}' has PointCost below 1 and was skipped.", type);
                    continue;
                }
                members.Add(type);
            }

            if (members.Count == 0) return plan;

            int minimumCost = 0;
            var minima = new List<CountedEnemy>();
            for (int i = 0; i < members.Count; i++)
            {
                EnemyType type = members[i];
                minimumCost += type.MinCount * type.PointCost;
                if (type.MinCount > 0) minima.Add(new CountedEnemy(type, type.MinCount));
            }

            plan.Overshot = minimumCost > plan.Budget;
            if (minimumCost >= plan.Budget)
            {
                for (int i = 0; i < minima.Count; i++) plan.Add(minima[i].Type, minima[i].Count);
                return plan;
            }

            int[] extras = MaxFillExtras(members, plan.Budget - minimumCost, rng);
            for (int i = 0; i < members.Count; i++)
                plan.Add(members[i], members[i].MinCount + extras[i]);

            return plan;
        }

        /// <summary>
        /// Computes a count-per-member whose total cost is as close as possible to (but never above) the
        /// remaining budget, choosing uniformly at random between equally-good compositions.
        /// </summary>
        private static int[] MaxFillExtras(List<EnemyType> members, int remaining, System.Random rng)
        {
            int m = members.Count;
            int[] costs = new int[m];
            for (int i = 0; i < m; i++) costs[i] = members[i].PointCost;

            // suffix[i][s] = sum s is reachable using only types i..m-1 (unbounded copies).
            bool[][] suffix = new bool[m + 1][];
            suffix[m] = new bool[remaining + 1];
            suffix[m][0] = true;
            for (int i = m - 1; i >= 0; i--)
            {
                bool[] cur = new bool[remaining + 1];
                bool[] rest = suffix[i + 1];
                for (int s = 0; s <= remaining; s++)
                {
                    bool reachable = rest[s];
                    if (s >= costs[i] && cur[s - costs[i]]) reachable = true;
                    cur[s] = reachable;
                }
                suffix[i] = cur;
            }

            int target = 0;
            for (int s = remaining; s >= 0; s--)
            {
                if (suffix[0][s]) { target = s; break; }
            }

            var extras = new int[m];
            int left = target;
            for (int i = 0; i < m; i++)
            {
                int maxN = left / costs[i];
                var valid = new List<int>();
                for (int n = 0; n <= maxN; n++)
                {
                    int rest = left - n * costs[i];
                    if (suffix[i + 1][rest]) valid.Add(n);
                }

                int chosen = valid.Count > 0 ? valid[Pick(rng, valid.Count)] : 0;
                extras[i] = chosen;
                left -= chosen * costs[i];
            }

            return extras;
        }

        private static int Pick(System.Random rng, int count)
        {
            return rng != null ? rng.Next(count) : Random.Range(0, count);
        }

        private static string TypeName(EnemyType type)
        {
            return string.IsNullOrEmpty(type.name) ? "(unnamed)" : type.name;
        }
    }
}