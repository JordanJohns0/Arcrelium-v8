using System.Collections.Generic;
using UnityEngine;
using Characters;
using HexGrid;

namespace Cards.Effects
{
    [CreateAssetMenu(fileName = "MoveEffect", menuName = "Arcrelium/Effects/Move")]
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

            // Step through each cell in the path so OnMovedToCell fires per step,
            // allowing equipment triggers (like Null Amulet) to activate on each move.
            if (path != null && path.Count > 0)
            {
                foreach (HexCell cell in path)
                {
                    context.Source.SetCellOnly(cell);
                }

                if (Application.isPlaying)
                    context.Source.AnimateAlongPath(path);
            }
            else
            {
                context.Source.SetCellOnly(target);

                if (Application.isPlaying)
                    context.Source.AnimateToCell(target);
            }
        }
    }
}
