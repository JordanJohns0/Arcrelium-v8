using System.Collections.Generic;
using UnityEngine;
using Characters;
using HexGrid;

namespace Cards.Effects
{
    [CreateAssetMenu(fileName = "MoveEffect", menuName = "/Effects/Move")]
    public class MoveEffect : CardEffectSO
    {
        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null || !context.TargetCell.IsWalkable) return false;

            int distance = context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates);
            if (distance > context.EffectiveRange) return false;

            if (context.Grid != null)
            {
                List<HexCell> path = context.Grid.FindPath(
                    context.Source.CurrentCell.Coordinates,
                    context.TargetCell.Coordinates,
                    context.EffectiveRange);
                return path != null;
            }

            return true;
        }

        public override void Execute(CardContext context)
        {
            HexCell target = context.TargetCell;
            if (target == null) return;

            List<HexCell> path = null;
            if (context.Grid != null)
            {
                path = context.Grid.FindPath(
                    context.Source.CurrentCell.Coordinates,
                    target.Coordinates,
                    context.EffectiveRange);
            }

            // Authoritative position updates immediately so a state broadcast right
            // after PlayCard already reflects the final destination. The coroutine
            // below is purely cosmetic.
            context.Source.SetCellOnly(target);

            if (path != null && path.Count > 0)
                context.Source.AnimateAlongPath(path);
            else
                context.Source.AnimateToCell(target);
        }
    }
}
