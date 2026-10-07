using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Card option: sets the cost of every card the owning effect produces.
    /// Attach to a draw effect for "Draw a card. Set its cost to 1."
    /// </summary>
    [CreateAssetMenu(fileName = "SetCardCostModifier", menuName = "Arcrelium/Card Modifiers/Set Card Cost")]
    public class SetCardCostModifier : CardModifierSO
    {
        [Tooltip("Cost assigned to every card this option touches.")]
        public int Cost = 1;

        public override void Modify(CardInstance card, CardContext context)
        {
            if (card == null) return;
            card.Cost = Cost;
        }
    }
}
