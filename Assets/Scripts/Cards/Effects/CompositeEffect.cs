using System.Collections.Generic;
using UnityEngine;

namespace FantasyBrawl.Cards.Effects
{
    /// <summary>
    /// Combines multiple card effects into one. Assign any number of CardEffectSO assets
    /// and they all execute in order when this card is played.
    /// Example: "Deal 2 damage, gain 3 block" = AttackEffect(2) + GainBlockEffect(3).
    /// </summary>
    [CreateAssetMenu(fileName = "CompositeEffect", menuName = "FantasyBrawl/Effects/Composite")]
    public class CompositeEffect : CardEffectSO
    {
        [Tooltip("Effects to execute in order when this card is played.")]
        public List<CardEffectSO> Effects = new List<CardEffectSO>();

        public override bool CanExecute(CardContext context)
        {
            foreach (CardEffectSO effect in Effects)
            {
                if (effect != null && !effect.CanExecute(context))
                    return false;
            }
            return true;
        }

        public override void Execute(CardContext context)
        {
            foreach (CardEffectSO effect in Effects)
            {
                if (effect != null)
                    effect.Execute(context);
            }
        }
    }
}
