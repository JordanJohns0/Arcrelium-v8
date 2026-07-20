using UnityEngine;

namespace FantasyBrawl.Cards.Effects
{
    /// <summary>
    /// Grants the source character block equal to a fixed amount.
    /// Block absorbs damage before health and resets at the start of each turn.
    /// </summary>
    [CreateAssetMenu(fileName = "GainBlockEffect", menuName = "FantasyBrawl/Effects/Gain Block")]
    public class GainBlockEffect : CardEffectSO
    {
        public int Amount = 1;

        public override void Execute(CardContext context)
        {
            context.Source.Stats.GainBlock(Amount);
        }
    }
}
