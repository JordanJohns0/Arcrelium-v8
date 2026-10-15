using System.Collections.Generic;
using UnityEngine;

namespace Equipment
{
    /// <summary>An item the post-victory shop can offer the player.</summary>
    public struct ShopOffer
    {
        public readonly EquipmentData Item;

        public ShopOffer(EquipmentData item)
        {
            Item = item;
        }
    }

    /// <summary>
    /// Catalogue the end-of-level shop draws its offers from. The player picks ONE of the
    /// offers for free; there is no gold. Authored stock wins; when it is empty (or no
    /// ShopPool asset exists) it falls back to <see cref="RewardPool"/>.
    ///
    /// Offers are filtered to items the player does not already own and picked with a
    /// seed so a given run+level shows a stable set, while a fresh run rolls new stock.
    /// Each machine rolls locally — the host never sees another player's inventory.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopPool", menuName = "Arcrelium/Shop Pool")]
    public class ShopPool : ScriptableObject
    {
        public List<EquipmentData> stock = new List<EquipmentData>();

        private static ShopPool main;
        private static bool mainSearched;

        public static ShopPool Main
        {
            get
            {
                if (main == null && !mainSearched)
                {
                    mainSearched = true;
                    main = Resources.Load<ShopPool>("ShopPool");
                }
                return main;
            }
        }

        /// <summary>Test seam: installs an explicit pool and disables the Resources fallback.</summary>
        public static void SetMain(ShopPool pool)
        {
            main = pool;
            mainSearched = pool != null;
        }

        public List<ShopOffer> BuildOffers(PlayerInventory inventory, int count, System.Random rng)
        {
            return Build(this, inventory, count, rng);
        }

        /// <summary>
        /// Builds up to <paramref name="count"/> distinct offers the player does not already
        /// own, from the main shop pool (falling back to the RewardPool when it is empty).
        /// </summary>
        public static List<ShopOffer> Offers(PlayerInventory inventory, int count, System.Random rng)
        {
            return Build(Main, inventory, count, rng);
        }

        private static List<ShopOffer> Build(ShopPool pool, PlayerInventory inventory, int count, System.Random rng)
        {
            var candidates = new List<EquipmentData>();

            if (pool != null && pool.stock != null)
                candidates.AddRange(pool.stock);

            if (candidates.Count == 0)
            {
                var rewards = RewardPool.Main;
                if (rewards != null && rewards.rewards != null)
                    candidates.AddRange(rewards.rewards);
            }

            var eligible = new List<ShopOffer>();
            var seen = new HashSet<EquipmentData>();
            foreach (var item in candidates)
            {
                if (item == null || !seen.Add(item)) continue;
                if (inventory != null && Owns(inventory, item)) continue;
                eligible.Add(new ShopOffer(item));
            }

            Shuffle(eligible, rng);

            if (count >= 0 && eligible.Count > count)
                eligible.RemoveRange(count, eligible.Count - count);
            return eligible;
        }

        private static bool Owns(PlayerInventory inventory, EquipmentData item)
        {
            if (inventory.IsUnlocked(item)) return true;
            return inventory.Loadout != null && inventory.Loadout.ContainsItem(item);
        }

        private static void Shuffle(List<ShopOffer> offers, System.Random rng)
        {
            for (int i = offers.Count - 1; i > 0; i--)
            {
                int j = rng != null ? rng.Next(i + 1) : Random.Range(0, i + 1);
                (offers[i], offers[j]) = (offers[j], offers[i]);
            }
        }
    }
}
