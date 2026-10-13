using UnityEngine;

namespace Cards
{
    /// <summary>
    /// An option that can be attached to a card's STEP (CardEffectStep.CardModifiers), applied
    /// to every card that step's effect produces — drawn, returned, etc. Example: "set cost to 1".
    /// Designers pick these from the card's inspector like the Overwatch Workshop action list.
    /// </summary>
    public abstract class CardModifierSO : ScriptableObject
    {
        /// <summary>Called once per card the owning effect produced.</summary>
        public abstract void Modify(CardInstance card, CardContext context);
    }
}
