using System.Collections.Generic;
using UnityEngine;

namespace Equipment
{
    /// <summary>One authored shop listing: an item and the price it sells for.</summary>
    [System.Serializable]
    public class ShopEntry
    {
        public EquipmentData item;
        [Min(0)] public int price;
    }

    /// <summary>An item offered for sale in the shop, with its resolved price.</summary>
    public struct ShopOffer
    {
        public readonly EquipmentData Item;
        public readonly int Price;

        public ShopOffer(EquipmentData item, int price)
        {
            Item = item;
            Price = price;
        }
    }

    /// <summary>
    /// Catalogue the end-of-level shop draws its stock from. Authored listings win; when the
    /// shop has no explicit stock (or no ShopPool asset exists) it falls back to the legacy
    /// <see cref="RewardPool"/> with derived prices, so the shop always has something to sell.
    ///
    /// Offers are filtered to items the player does not already own and picked with an
    /// optional seed so a given level shows a stable set. Each machine rolls locally —
    /// the host never sees another player's inventory or offers.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopPool", menuName = "Arcrelium/Shop Pool")]
    public class ShopPool : ScriptableObject
    {
        public List<ShopEntry> stock = new List<ShopEntry>();

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
        /// Builds up to <paramref name="count"/> offers from the main shop pool, falling back
        /// to the RewardPool when the main pool is missing or empty.
        /// </summary>
        public static List<ShopOffer> Offers(PlayerInventory inventory, int count, System.Random rng)
        {
            return Build(Main, inventory, count, rng);
        }

        private static List<ShopOffer> Build(ShopPool pool, PlayerInventory inventory, int count, System.Random rng)
        {
            var candidates = new List<ShopOffer>();

            if (pool != null && pool.stock != null)
            {
                foreach (var entry in pool.stock)
                {
                    if (entry == null || entry.item == null) continue;
                    int price = entry.price > 0 ? entry.price : ShopPricing.PriceFor(entry.item);
                    candidates.Add(new ShopOffer(entry.item, price));
                }
            }

            if (candidates.Count == 0)
            {
                var rewards = RewardPool.Main;
                if (rewards != null && rewards.rewards != null)
                {
                    foreach (var item in rewards.rewards)
                    {
                        if (item == null) continue;
                        candidates.Add(new ShopOffer(item, ShopPricing.PriceFor(item)));
                    }
                }
            }

            var eligible = new List<ShopOffer>();
            foreach (var offer in candidates)
            {
                if (inventory != null && Owns(inventory, offer.Item)) continue;
                eligible.Add(offer);
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
