using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Sets the instance cost of the context's card to a random value within a range.
    /// Intended as a card's OnDrawnEffect: "when this card is drawn, set its cost to 0, 1,
    /// or 2 at random". Applied per instance, so each copy rolls its own cost.
    /// </summary>
    [CreateAssetMenu(fileName = "SetRandomCostEffect", menuName = "Arcrelium/Effects/Set Random Cost")]
    public class SetRandomCostEffect : CardEffectSO
    {
        public int MinCost = 0;
        public int MaxCost = 2;

        public override void Execute(CardContext context)
        {
            if (context?.Card == null) return;
            context.Card.Cost = Random.Range(MinCost, MaxCost + 1);
        }
    }
}