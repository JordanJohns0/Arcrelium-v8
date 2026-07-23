using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Draws cards for the source character when played.
    /// Amount can be a constant or derived from a stat.
    /// </summary>
    [CreateAssetMenu(fileName = "DrawCardsEffect", menuName = "/Effects/Draw Cards")]
    public class DrawCardsEffect : CardEffectSO
    {
        public CardValue Amount = CardValue.MakeConstant(1);

        public override void Execute(CardContext context)
        {
            context.Source.DrawCards(Amount.Evaluate(context.Source));
        }
    }
}
