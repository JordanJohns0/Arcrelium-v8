using System.Collections.Generic;
using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Steals the top card of the target character's deck into the source's hand, applying
    /// the step's card options (e.g. "reduce its cost by 1"). The victim's draw pile simply
    /// loses the card — it is never shuffled back in from the discard pile.
    /// </summary>
    [CreateAssetMenu(fileName = "StealTopCardEffect", menuName = "Arcrelium/Effects/Steal Top Card")]
    public class StealTopCardEffect : CardTargetingEffectSO
    {
        public override bool CanExecute(CardContext context)
        {
            if (context.Source == null || context.TargetCharacter == null) return false;
            if (context.Source.CurrentCell == null || context.TargetCharacter.CurrentCell == null) return false;
            if (context.TargetCharacter.Stats == null || !context.TargetCharacter.Stats.IsAlive) return false;
            if (context.Source.Hand == null || context.Source.Hand.IsFull) return false;
            if (context.TargetCharacter.Deck == null || context.TargetCharacter.Deck.DrawPile.Count == 0) return false;

            return context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCharacter.CurrentCell.Coordinates)
                <= context.EffectiveRange;
        }

        public override void Execute(CardContext context)
        {
            if (context.Source == null || context.TargetCharacter == null) return;
            if (context.Source.Hand == null || context.Source.Hand.IsFull) return;
            CardInstance stolen = context.TargetCharacter.Deck?.TakeTopCard();
            if (stolen == null) return;

            context.Source.Hand.AddCard(stolen);
            ApplyCardModifiers(context, new List<CardInstance> { stolen });
        }
    }
}