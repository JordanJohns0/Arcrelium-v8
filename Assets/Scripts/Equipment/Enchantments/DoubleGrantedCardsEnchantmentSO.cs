using System.Collections.Generic;
using Cards;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Doubles every card the enchanted equipment would grant (both its own card list and any
    /// cards added by other enchantments on the same piece, such as Inspiring).
    /// </summary>
    [CreateAssetMenu(fileName = "DoubleGrantedCardsEnchantment", menuName = "Arcrelium/Enchantments/Double Granted Cards")]
    public class DoubleGrantedCardsEnchantmentSO : EquipmentEnchantmentSO
    {
        [Tooltip("How many copies of each granted card to add, on top of the original. 1 = double.")]
        public int ExtraCopies = 1;

        public override void ModifyGrantedCards(EquipmentInstance equipment, List<CardData> slotCards)
        {
            if (slotCards == null || slotCards.Count == 0 || ExtraCopies < 1) return;

            int originalCount = slotCards.Count;
            for (int copy = 0; copy < ExtraCopies; copy++)
            {
                for (int i = 0; i < originalCount; i++)
                    slotCards.Add(slotCards[i]);
            }
        }
    }
}
