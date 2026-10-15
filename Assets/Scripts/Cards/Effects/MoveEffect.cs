using System.Collections.Generic;
using UnityEngine;
using Characters;
using HexGrid;
using TileEffects;

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

            if (context.Grid != null)
            {
                // Planned with the same slide rules as the move itself: the target must be a cell the
                // character can end on, which an oil tile it slides off of is not. Slides don't count
                // as steps, so a target can be farther away than the range in straight-line distance.
                return MovePlanner.FindRoute(context.Grid, context.Source.CurrentCell,
                    context.TargetCell, context.EffectiveRange) != null;
            }

            int distance = context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates);
            return distance <= context.EffectiveRange;
        }

        /// <summary>
        /// Validates an explicitly supplied move path as it will be walked (oil slides included):
        /// every hop is to a walkable neighbour of where the character is at that point, the steps
        /// fit the effective range, and the last cell is the requested target.
        /// </summary>
        private static bool IsValidPath(CardContext context)
        {
            if (context.Source.CurrentCell == null || context.TargetCell == null) return false;

            if (context.Grid != null)
            {
                MoveRoute route = MovePlanner.Validate(context.Grid, context.Source.CurrentCell,
                    context.MovePath, context.EffectiveRange);
                return route != null && context.MovePath[context.MovePath.Count - 1] == context.TargetCell;
            }

            // Without a board there are no slides: plain adjacency and length.
            if (context.MovePath.Count > context.EffectiveRange) return false;
            HexCoordinates previous = context.Source.CurrentCell.Coordinates;
            foreach (HexCell cell in context.MovePath)
            {
                if (cell == null || previous.DistanceTo(cell.Coordinates) != 1 || !cell.IsWalkable) return false;
                previous = cell.Coordinates;
            }
            return previous == context.TargetCell.Coordinates;
        }

        public override void Execute(CardContext context)
        {
            HexCell target = context.TargetCell;
            if (target == null) return;

            // Without an explicit route (AI, or a play that only names a target) plan one with the
            // same slide rules the validation used, so the character ends on the target.
            List<HexCell> path = context.MovePath;
            if ((path == null || path.Count == 0) && context.Grid != null)
            {
                MoveRoute route = MovePlanner.FindRoute(context.Grid, context.Source.CurrentCell, target,
                    context.EffectiveRange);
                path = route?.Cells;
            }

            // Step through each cell in the path so OnMovedToCell fires per step,
            // allowing equipment triggers (like Null Amulet) to activate on each move.
            // With a TileEffectSystem present, movement routes through it so tile effects
            // (fire on entry, oil slipping) resolve per step and the returned route reflects
            // where the character actually ends up.
            TileEffectSystem environment = TileEffectSystem.Instance;
            List<HexCell> resolved = path;

            if (path != null && path.Count > 0)
            {
                if (environment != null)
                {
                    resolved = environment.MoveCharacter(context.Source, path);
                }
                else
                {
                    foreach (HexCell cell in path)
                    {
                        context.Source.SetCellOnly(cell);
                    }
                }

                if (Application.isPlaying)
                    context.Source.AnimateAlongPath(resolved);
            }
            else
            {
                if (environment != null)
                    environment.WalkInto(context.Source, target);
                else
                    context.Source.SetCellOnly(target);

                if (Application.isPlaying)
                    context.Source.AnimateToCell(context.Source.CurrentCell);
            }
        }
    }
}
