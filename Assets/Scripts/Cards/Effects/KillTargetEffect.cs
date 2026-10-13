using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Instantly kills the target character when it has Threshold or fewer remaining HP.
    /// The kill ignores block (an execution is not damage), so armored targets with low
    /// health still die. Playable as a no-op against healthy targets.
    /// </summary>
    [CreateAssetMenu(fileName = "KillTargetEffect", menuName = "Arcrelium/Effects/Kill Target")]
    public class KillTargetEffect : CardEffectSO
    {
        public CardValue Threshold = CardValue.MakeConstant(3);

        public override bool IsAttack => true;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCharacter == null) return false;
            if (context.TargetCharacter.Stats == null || !context.TargetCharacter.Stats.IsAlive) return false;
            if (context.Source == null || context.Source.CurrentCell == null || context.TargetCharacter.CurrentCell == null) return false;

            return context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCharacter.CurrentCell.Coordinates)
                <= context.EffectiveRange;
        }

        public override void Execute(CardContext context)
        {
            if (context.TargetCharacter == null || context.TargetCharacter.Stats == null) return;
            if (!context.TargetCharacter.Stats.IsAlive) return;
            if (context.TargetCharacter.Stats.CurrentHealth > Threshold.Evaluate(context.Source)) return;

            context.TargetCharacter.Kill();
        }
    }
}