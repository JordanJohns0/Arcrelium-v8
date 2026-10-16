using System.Collections.Generic;
using Cards;
using Characters;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Adds special card(s) to the wearer's deck while equipped and removes them again
    /// on unequip. Copies already drawn into hand stay playable until used.
    /// </summary>
    [CreateAssetMenu(fileName = "GrantCardEnchantment", menuName = "Arcrelium/Enchantments/Grant Card")]
    public class GrantCardEnchantmentSO : EquipmentEnchantmentSO
    {
        [Tooltip("New style: every card in this list is injected into the deck while equipped.")]
        public List<CardData> GrantedCards = new List<CardData>();

        [Tooltip("Legacy single card. Still honoured (used by older assets); prefer the list above.")]
        public CardData GrantedCard;

        public override void OnEquipped(EquipmentInstance equipment, Character wearer)
        {
            foreach (CardData card in EachGrantedCard())
                wearer?.Deck?.AddCard(card);
        }

        public override void OnUnequipped(EquipmentInstance equipment, Character wearer)
        {
            foreach (CardData card in EachGrantedCard())
                wearer?.Deck?.RemoveCard(card);
        }

        public override void CollectGrantedCards(List<CardData> cards)
        {
            foreach (CardData card in EachGrantedCard())
                cards.Add(card);
        }

        /// <summary>Yields the list entries plus the legacy single card, without duplicates.</summary>
        private IEnumerable<CardData> EachGrantedCard()
        {
            var seen = new HashSet<CardData>();
            if (GrantedCards != null)
            {
                for (int i = 0; i < GrantedCards.Count; i++)
                {
                    var card = GrantedCards[i];
                    if (card != null && seen.Add(card)) yield return card;
                }
            }
            if (GrantedCard != null && seen.Add(GrantedCard)) yield return GrantedCard;
        }
    }
}
