using System.Collections.Generic;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Derives a shop price for an item when no explicit price is authored. Bigger slots
    /// (head/chest) and items that grant more cards, triggers, or enchantments cost more,
    /// so a roughly "stronger" item is also the more expensive one.
    /// </summary>
    public static class ShopPricing
    {
        private const int BasePrice = 10;
        private const int PerCard = 6;
        private const int PerTrigger = 8;
        private const int PerEnchantment = 8;

        public static int PriceFor(EquipmentData item)
        {
            if (item == null) return 0;

            int price = BasePrice + SlotPremium(item.SlotType);
            price += PerCard * Count(item.Cards);
            price += PerTrigger * Count(item.Triggers);
            price += PerEnchantment * Count(item.Enchantments);
            return Mathf.Max(1, price);
        }

        private static int SlotPremium(EquipmentSlotType slot)
        {
            switch (slot)
            {
                case EquipmentSlotType.Head: return 8;
                case EquipmentSlotType.Chest: return 8;
                case EquipmentSlotType.Hand: return 6;
                case EquipmentSlotType.Trinket: return 5;
                case EquipmentSlotType.Feet: return 4;
                default: return 0;
            }
        }

        private static int Count<T>(ICollection<T> list)
        {
            return list == null ? 0 : list.Count;
        }
    }
}
