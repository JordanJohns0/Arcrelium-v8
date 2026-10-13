using System.Collections.Generic;
using Characters;
using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Deal damage to a targeted enemy character within range.
    /// Damage can be a constant or derived from a stat (e.g. your current block).
    /// OnHitEffects run against the enemy that was hit, after the damage lands.
    /// </summary>
    [CreateAssetMenu(fileName = "AttackEffect", menuName = "Arcrelium/Effects/Attack")]
    public class AttackEffect : CardEffectSO
    {
        public CardValue Damage = CardValue.MakeConstant(1);

        [Tooltip("Effects applied to the enemy hit by this attack, after the damage lands. " +
                 "They only run while that enemy is still alive.")]
        public List<CardEffectSO> OnHitEffects = new List<CardEffectSO>();

        public override bool IsAttack => true;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCharacter == null) return false;
            if (!context.TargetCharacter.Stats.IsAlive) return false;

            int distance = context.Source.CurrentCell.Coordinates.DistanceTo(
                context.TargetCharacter.CurrentCell.Coordinates);
            return distance <= context.EffectiveRange;
        }

        public override void Execute(CardContext context)
        {
            // Enchantment bonus damage stacks on top of the card's own damage value.
            int damage = Damage.Evaluate(context.Source) + Mathf.Max(0, context.BonusDamage);
            context.TargetCharacter.TakeDamage(damage, context.Source);

            ApplyOnHitEffects(context);
        }

        /// <summary>
        /// Runs every OnHitEffect with the hit enemy as its target (on the enemy's own cell,
        /// so positional effects like tiles land there too). Range carries over so follow-up
        /// range checks pass; enchantment bonus damage does NOT — it already boosted this hit.
        /// </summary>
        private void ApplyOnHitEffects(CardContext context)
        {
            if (OnHitEffects == null || OnHitEffects.Count == 0) return;

            Character victim = context.TargetCharacter;
            if (victim == null || !victim.Stats.IsAlive) return;

            foreach (CardEffectSO effect in OnHitEffects)
            {
                if (effect == null) continue;

                var hitContext = new CardContext
                {
                    Source = context.Source,
                    Grid = context.Grid,
                    Card = context.Card,
                    TargetCell = victim.CurrentCell,
                    TargetCharacter = victim,
                    Range = context.Range,
                    BonusRange = context.BonusRange
                };

                if (effect.CanExecute(hitContext))
                    effect.Execute(hitContext);
            }
        }
    }
}
