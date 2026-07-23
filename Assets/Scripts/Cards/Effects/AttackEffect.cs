using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Deal damage to a targeted enemy character within range.
    /// Damage can be a constant or derived from a stat (e.g. your current block).
    /// </summary>
    [CreateAssetMenu(fileName = "AttackEffect", menuName = "/Effects/Attack")]
    public class AttackEffect : CardEffectSO
    {
        public CardValue Damage = CardValue.MakeConstant(1);

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
            context.TargetCharacter.TakeDamage(Damage.Evaluate(context.Source));
        }
    }
}
