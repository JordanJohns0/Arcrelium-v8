using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Lowers the cost of every card currently in the source's hand, never below MinCost.
    /// e.g. "Decrease the cost of cards in your hand by 1."
    /// </summary>
    [CreateAssetMenu(fileName = "DecreaseHandCostsEffect", menuName = "Arcrelium/Effects/Decrease Hand Costs")]
    public class DecreaseHandCostsEffect : CardEffectSO
    {
        public int Amount = 1;
        public int MinCost = 0;

        public override void Execute(CardContext context)
        {
            if (context?.Source?.Hand == null) return;

            Hand hand = context.Source.Hand;
            foreach (CardInstance card in hand.Cards)
            {
                if (card != null)
                    card.Cost = Mathf.Max(MinCost, card.Cost - Amount);
            }

            hand.NotifyChanged();
        }
    }
}