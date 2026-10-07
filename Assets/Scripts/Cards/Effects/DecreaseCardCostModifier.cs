using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Card option: lowers a touched card's cost by Amount, never below MinCost. Composes with
    /// any card-producing effect using the same modifier vocabulary as "set cost to 1" — e.g.
    /// ApplyModifiersToHandEffect + this option = "decrease the cost of cards in your hand by 1".
    /// </summary>
    [CreateAssetMenu(fileName = "DecreaseCardCostModifier", menuName = "Arcrelium/Card Modifiers/Decrease Card Cost")]
    public class DecreaseCardCostModifier : CardModifierSO
    {
        public int Amount = 1;
        public int MinCost = 0;

        public override void Modify(CardInstance card, CardContext context)
        {
            if (card == null) return;
            card.Cost = Mathf.Max(MinCost, card.Cost - Amount);
        }
    }
}