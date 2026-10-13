using UnityEngine;
using Characters;

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
            int amount = Amount.Evaluate(context.Source);
            if (amount > 0)
                context.Source.GainBlock(amount);
        }
    }
}
