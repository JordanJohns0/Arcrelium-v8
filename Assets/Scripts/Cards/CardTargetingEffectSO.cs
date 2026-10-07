using System.Collections.Generic;
using UnityEngine;

namespace Cards
{
    /// <summary>
    /// Base for card effects whose outcome is a set of CARD instances (draw a card, return a
    /// card, ...). Card options like "set cost to 1" attach to the STEP on the card itself
    /// (CardEffectStep.CardModifiers) and are applied to every card the effect produces, so one
    /// option works on any card-targeting effect, not just drawing.
    /// </summary>
    public abstract class CardTargetingEffectSO : CardEffectSO
    {
        /// <summary>Applies the step's attached options to every card the effect produced.</summary>
        protected void ApplyCardModifiers(CardContext context, IEnumerable<CardInstance> cards)
        {
            List<CardModifierSO> modifiers = context?.StepModifiers;
            if (modifiers == null || modifiers.Count == 0) return;

            bool applied = false;
            foreach (CardInstance card in cards)
            {
                if (card == null) continue;
                foreach (CardModifierSO modifier in modifiers)
                {
                    if (modifier == null) continue;
                    modifier.Modify(card, context);
                    applied = true;
                }
            }

            if (applied && context.Source?.Hand != null)
                context.Source.Hand.NotifyChanged();
        }
    }
}