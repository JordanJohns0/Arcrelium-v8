using Cards;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Makes every card that belongs to the enchanted equipment cost less. The discount is
    /// baked into each card instance at creation, so it survives redraws and is visible in hand.
    /// </summary>
    [CreateAssetMenu(fileName = "DecreaseEquipmentCardCostEnchantment", menuName = "Arcrelium/Enchantments/Decrease Equipment Card Cost")]
    public class DecreaseEquipmentCardCostEnchantmentSO : EquipmentEnchantmentSO
    {
        [Tooltip("How much cheaper the equipment's cards become. 1 = costs 1 less.")]
        public int Amount = 1;

        public override int GetCardCostDelta(EquipmentInstance equipment, CardData card)
        {
            if (card == null || equipment?.Data?.Cards == null) return 0;
            for (int i = 0; i < equipment.Data.Cards.Count; i++)
            {
                if (equipment.Data.Cards[i] == card) return -Mathf.Max(0, Amount);
            }
            return 0;
        }
    }
}
