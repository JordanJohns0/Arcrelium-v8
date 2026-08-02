using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Grants the source character block. Amount can be a constant or derived from a stat.
    /// Block absorbs damage before health.
    /// </summary>
    [CreateAssetMenu(fileName = "GainBlockEffect", menuName = "Arcrelium/Effects/Gain Block")]
    public class GainBlockEffect : CardEffectSO
    {
        public CardValue Amount = CardValue.MakeConstant(1);

        public override void Execute(CardContext context)
        {
            context.Source.Stats.GainBlock(Amount.Evaluate(context.Source));
        }
    }
}
