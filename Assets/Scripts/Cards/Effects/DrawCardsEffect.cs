using System.Collections.Generic;
using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Draws cards for the source character when played.
    /// Amount can be a constant or derived from a stat.
    /// The cards actually drawn are recorded on CardContext.DrawnCards so later effects in
    /// the same chain can modify them (e.g. "Draw a card. Set its cost to 1").
    /// </summary>
    [CreateAssetMenu(fileName = "DrawCardsEffect", menuName = "Arcrelium/Effects/Draw Cards")]
    public class DrawCardsEffect : CardEffectSO
    {
        public CardValue Amount = CardValue.MakeConstant(1);

        public override void Execute(CardContext context)
        {
            List<CardInstance> drawn = context.Source.DrawCards(Amount.Evaluate(context.Source));
            if (drawn == null || drawn.Count == 0) return;

            context.DrawnCards ??= new List<CardInstance>();
            context.DrawnCards.AddRange(drawn);
        }
    }
}