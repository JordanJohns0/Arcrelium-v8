using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Deal damage to a targeted enemy character within range.
    /// Damage is defined per card, not by character stats.
    /// </summary>
    [CreateAssetMenu(fileName = "AttackEffect", menuName = "/Effects/Attack")]
    public class AttackEffect : CardEffectSO
    {
        public int Damage = 1;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCharacter == null) return false;
            if (!context.TargetCharacter.Stats.IsAlive) return false;

            int distance = context.Source.CurrentCell.Coordinates.DistanceTo(
                context.TargetCharacter.CurrentCell.Coordinates);
            return distance <= context.Range;
        }

        public override void Execute(CardContext context)
        {
            context.TargetCharacter.TakeDamage(Damage);
        }
    }
}
