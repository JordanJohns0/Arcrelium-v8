using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Sets the cost of every card drawn during this effect chain (see CardContext.DrawnCards),
    /// e.g. "Draw a card. Set its cost to 1." Compose with a DrawCardsEffect in the same step.
    /// </summary>
    [CreateAssetMenu(fileName = "SetDrawnCardsCostEffect", menuName = "Arcrelium/Effects/Set Drawn Cards Cost")]
    public class SetDrawnCardsCostEffect : CardEffectSO
    {
        public int CostValue = 1;

        public override void Execute(CardContext context)
        {
            if (context?.DrawnCards == null || context.Source == null) return;

            foreach (CardInstance card in context.DrawnCards)
            {
                if (card != null)
                    card.Cost = CostValue;
            }

            context.Source.Hand?.NotifyChanged();
        }
    }
}