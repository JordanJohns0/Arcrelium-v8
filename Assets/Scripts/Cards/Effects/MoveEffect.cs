using UnityEngine;

namespace FantasyBrawl.Cards.Effects
{
    /// <summary>
    /// Example effect: move the source character to the target cell, if it's within the
    /// card's Range and walkable. This is a reference implementation showing the pattern —
    /// duplicate/adapt this for Attack, Heal, Buff, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "MoveEffect", menuName = "FantasyBrawl/Effects/Move")]
    public class MoveEffect : CardEffectSO
    {
        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null || !context.TargetCell.IsWalkable) return false;

            int distance = context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates);
            return distance <= context.Range;
        }

        public override void Execute(CardContext context)
        {
            context.Source.PlaceOnCell(context.TargetCell);
        }
    }
}
