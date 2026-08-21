using System.Collections.Generic;
using Cards;
using Characters;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Adds a special card to the wearer's deck while equipped and removes it again
    /// on unequip. Copies already drawn into hand stay playable until used.
    /// </summary>
    [CreateAssetMenu(fileName = "GrantCardEnchantment", menuName = "Arcrelium/Enchantments/Grant Card")]
    public class GrantCardEnchantmentSO : EquipmentEnchantmentSO
    {
        [Tooltip("Card injected into the deck while equipped.")]
        public CardData GrantedCard;

        public override void OnEquipped(EquipmentInstance equipment, Character wearer)
        {
            if (GrantedCard == null) return;
            wearer?.Deck?.AddCard(GrantedCard);
        }

        public override void OnUnequipped(EquipmentInstance equipment, Character wearer)
        {
            if (GrantedCard == null) return;
            wearer?.Deck?.RemoveCard(GrantedCard);
        }

        public override void CollectGrantedCards(List<CardData> cards)
        {
            if (GrantedCard != null) cards.Add(GrantedCard);
        }
    }
}
