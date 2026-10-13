using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Card option: sets the instance cost of a touched card to a random value within a range.
    /// Used as an OnDrawnModifier for "when you draw this, it costs 0, 1, or 2 at random".
    /// </summary>
    [CreateAssetMenu(fileName = "SetRandomCostModifier", menuName = "Arcrelium/Card Modifiers/Set Random Cost")]
    public class SetRandomCostModifier : CardModifierSO
    {
        public int MinCost = 0;
        public int MaxCost = 2;

        public override void Modify(CardInstance card, CardContext context)
        {
            if (card == null) return;
            card.Cost = Random.Range(MinCost, MaxCost + 1);
        }
    }
}