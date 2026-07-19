using UnityEngine;

namespace FantasyBrawl.Cards.Effects
{
    /// <summary>
    /// Example effect: deal damage to a targeted enemy character within range.
    /// Uses the source's BaseAttack stat as the damage amount — swap in a fixed
    /// card-defined amount instead if that fits your design better.
    /// </summary>
    [CreateAssetMenu(fileName = "AttackEffect", menuName = "FantasyBrawl/Effects/Attack")]
    public class AttackEffect : CardEffectSO
    {
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
            context.TargetCharacter.TakeDamage(context.Source.Stats.BaseAttack);
        }
    }
}
