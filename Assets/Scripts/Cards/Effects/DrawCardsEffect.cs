using System.Collections.Generic;
using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Draws cards for the source character when played.
    /// Amount can be a constant or derived from a stat.
    /// Card options attached to the STEP (CardEffectStep.CardModifiers, e.g. "set cost to 1")
    /// are applied to every card it draws.
    /// </summary>
    [CreateAssetMenu(fileName = "DrawCardsEffect", menuName = "Arcrelium/Effects/Draw Cards")]
    public class DrawCardsEffect : CardTargetingEffectSO
    {
        public CardValue Amount = CardValue.MakeConstant(1);

        public override void Execute(CardContext context)
        {
            List<CardInstance> drawn = context.Source.DrawCards(Amount.Evaluate(context.Source));
            if (drawn == null || drawn.Count == 0) return;

            ApplyCardModifiers(context, drawn);
        }
    }
}
