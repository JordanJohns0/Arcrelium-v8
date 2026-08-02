using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Heal the source character. Does not require a target.
    /// Amount can be a constant or derived from a stat.
    /// </summary>
    [CreateAssetMenu(fileName = "HealEffect", menuName = "Arcrelium/Effects/Heal")]
    public class HealEffect : CardEffectSO
    {
        public CardValue Amount = CardValue.MakeConstant(2);

        public override bool CanExecute(CardContext context)
        {
            if (context.Source == null) return false;
            if (!context.Source.Stats.IsAlive) return false;
            return context.Source.Stats.CurrentHealth < context.Source.Stats.MaxHealth;
        }

        public override void Execute(CardContext context)
        {
            context.Source.Stats.Heal(Amount.Evaluate(context.Source));
        }
    }
}
