using System.Collections.Generic;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Pool of equipment that can be earned as a reward. Rolls are filtered to items the
    /// player does not already own, so every reward is a fresh unlock. Each machine rolls
    /// its own pool locally — the host never sees another player's inventory or reward.
    ///
    /// Create once per project (right-click > Create >  > Reward Pool) and place it in a
    /// Resources folder so it loads as RewardPool.Main.
    /// </summary>
    [CreateAssetMenu(fileName = "RewardPool", menuName = "Arcrelium/Reward Pool")]
    public class RewardPool : ScriptableObject
    {
        public List<EquipmentData> rewards = new List<EquipmentData>();

        private static RewardPool main;
        private static bool mainSearched;

        public static RewardPool Main
        {
            get
            {
                if (main == null && !mainSearched)
                {
                    mainSearched = true;
                    main = Resources.Load<RewardPool>("RewardPool");
                }
                return main;
            }
        }

        /// <summary>Test seam: installs an explicit pool and disables the Resources fallback.</summary>
        public static void SetMain(RewardPool pool)
        {
            main = pool;
            mainSearched = pool != null;
        }

        public EquipmentData RollUnlocked(PlayerInventory inventory)
        {
            return RollUnlocked(inventory, null);
        }

        /// <summary>
        /// Rolls a reward the given inventory does not already own. Returns null when the
        /// inventory owns everything in the pool (or the pool is empty/null). A seeded
        /// System.Random makes rolls deterministic for tests.
        /// </summary>
        public EquipmentData RollUnlocked(PlayerInventory inventory, System.Random rng)
        {
            if (inventory == null || rewards == null || rewards.Count == 0) return null;

            var eligible = new List<EquipmentData>();
            foreach (var r in rewards)
            {
                if (r == null || inventory.IsUnlocked(r)) continue;
                eligible.Add(r);
            }
            if (eligible.Count == 0) return null;
            if (rng != null) return eligible[rng.Next(eligible.Count)];
            return eligible[Random.Range(0, eligible.Count)];
        }
    }
}
