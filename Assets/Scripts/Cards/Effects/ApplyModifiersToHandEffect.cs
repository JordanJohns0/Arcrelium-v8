using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Applies the STEP's attached CardModifiers (CardEffectStep.CardModifiers) to every card
    /// currently in the source's hand — the generic "modify my whole hand" verb. The actual
    /// options live on the step, so Prepare is just "ApplyToHand + DecreaseCardCost", and a
    /// future "everything in your hand costs 0" is the same effect with a different modifier.
    /// </summary>
    [CreateAssetMenu(fileName = "ApplyModifiersToHandEffect", menuName = "Arcrelium/Effects/Apply Modifiers To Hand")]
    public class ApplyModifiersToHandEffect : CardEffectSO
    {
        public override void Execute(CardContext context)
        {
            if (context?.Source?.Hand == null) return;
            if (context.StepModifiers == null || context.StepModifiers.Count == 0) return;

            bool applied = false;
            foreach (CardInstance card in context.Source.Hand.Cards)
            {
                if (card == null) continue;
                foreach (CardModifierSO modifier in context.StepModifiers)
                {
                    if (modifier == null) continue;
                    modifier.Modify(card, context);
                    applied = true;
                }
            }

            if (applied)
                context.Source.Hand.NotifyChanged();
        }
    }
}