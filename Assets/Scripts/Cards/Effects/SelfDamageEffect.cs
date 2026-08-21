using UnityEngine;
using Characters;

namespace Cards.Effects
{
    /// <summary>
    /// Deals damage to the source character itself (e.g. "take 1 damage" effects).
    /// Block absorbs it like any other damage. Does not require a target.
    /// Intentionally ignores enchantment BonusDamage — self-harm shouldn't scale with weapon enchants.
    /// </summary>
    [CreateAssetMenu(fileName = "SelfDamageEffect", menuName = "Arcrelium/Effects/Self Damage")]
    public class SelfDamageEffect : CardEffectSO
    {
        public CardValue Damage = CardValue.MakeConstant(1);

        public override bool CanExecute(CardContext context)
        {
            return context.Source != null && context.Source.Stats.IsAlive;
        }

        public override void Execute(CardContext context)
        {
            context.Source.TakeDamage(Damage.Evaluate(context.Source));
        }
    }
}
