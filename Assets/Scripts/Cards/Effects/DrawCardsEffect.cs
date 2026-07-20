using UnityEngine;

namespace FantasyBrawl.Cards.Effects
{
    /// <summary>
    /// Draws cards for the source character when played.
    /// </summary>
    [CreateAssetMenu(fileName = "DrawCardsEffect", menuName = "FantasyBrawl/Effects/Draw Cards")]
    public class DrawCardsEffect : CardEffectSO
    {
        public int Amount = 1;

        public override void Execute(CardContext context)
        {
            context.Source.DrawCards(Amount);
        }
    }
}
