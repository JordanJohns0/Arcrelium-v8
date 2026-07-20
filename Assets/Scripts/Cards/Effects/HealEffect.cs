using UnityEngine;

namespace FantasyBrawl.Cards.Effects
{
    /// <summary>
    /// Heal the source character. Does not require a target.
    /// </summary>
    [CreateAssetMenu(fileName = "HealEffect", menuName = "FantasyBrawl/Effects/Heal")]
    public class HealEffect : CardEffectSO
    {
        public int HealAmount = 2;

        public override bool CanExecute(CardContext context)
        {
            if (context.Source == null) return false;
            if (!context.Source.Stats.IsAlive) return false;
            return context.Source.Stats.CurrentHealth < context.Source.Stats.MaxHealth;
        }

        public override void Execute(CardContext context)
        {
            context.Source.Stats.Heal(HealAmount);
        }
    }
}
