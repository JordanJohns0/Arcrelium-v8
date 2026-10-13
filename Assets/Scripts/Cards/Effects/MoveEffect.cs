using System.Collections.Generic;
using UnityEngine;
using Characters;
using HexGrid;

namespace Cards.Effects
{
    [CreateAssetMenu(fileName = "MoveEffect", menuName = "Arcrelium/Effects/Move")]
    public class MoveEffect : CardEffectSO
    {
        public override bool IsMove => true;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null) return false;

            // When the play carries an explicit route (path-building UI), honor exactly that
            // route: must be contiguous, walkable, inside effective range, and end on the target.
            if (context.MovePath != null && context.MovePath.Count > 0)
                return IsValidPath(context);

            if (!context.TargetCell.IsWalkable) return false;

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

        /// <summary>
        /// Validates an explicitly supplied move path: starts adjacent to the source's current
        /// cell, every hop is to a neighboring walkable cell, total length fits the effective
        /// range, and the last cell is the requested target.
        /// </summary>
        private static bool IsValidPath(CardContext context)
        {
            if (context.Source.CurrentCell == null) return false;
            if (context.MovePath.Count > context.EffectiveRange) return false;

            HexCoordinates previous = context.Source.CurrentCell.Coordinates;
            for (int i = 0; i < context.MovePath.Count; i++)
            {
                HexCell cell = context.MovePath[i];
                if (cell == null) return false;
                if (previous.DistanceTo(cell.Coordinates) != 1) return false;
                if (!cell.IsWalkable) return false;
                previous = cell.Coordinates;
            }

            return context.TargetCell != null && previous == context.TargetCell.Coordinates;
        }

        public override void Execute(CardContext context)
        {
            HexCell target = context.TargetCell;
            if (target == null) return;

            List<HexCell> path = context.MovePath;
            if (path == null || path.Count == 0)
            {
                if (context.Grid != null)
                {
                    path = context.Grid.FindPath(
                        context.Source.CurrentCell.Coordinates,
                        target.Coordinates,
                        context.EffectiveRange);
                }
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
