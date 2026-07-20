using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Grants the source character block equal to a fixed amount.
    /// Block absorbs damage before health.
    /// </summary>
    [CreateAssetMenu(fileName = "GainBlockEffect", menuName = "/Effects/Gain Block")]
    public class GainBlockEffect : CardEffectSO
    {
        public int Amount = 1;

        public override void Execute(CardContext context)
        {
            context.Source.Stats.GainBlock(Amount);
        }
    }
}
