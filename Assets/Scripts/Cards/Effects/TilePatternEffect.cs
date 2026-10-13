using System.Collections.Generic;
using UnityEngine;
using HexGrid;
using TileEffects;

namespace Cards.Effects
{
    /// <summary>
    /// Places a persistent tile effect on a pattern of hexes defined as offsets from the
    /// picked target cell, in the reference frame where the target sits due-east (direction 0)
    /// of the caster. When the picked cell is an adjacent neighbor the whole pattern rotates to
    /// face the caster-to-target direction — a three-tile fire wall is just
    /// "{target, flank-left, flank-right}". This is the generic replacement for the old
    /// bespoke FireWallEffect and can express any shaped glyph.
    /// </summary>
    [CreateAssetMenu(fileName = "TilePatternEffect", menuName = "Arcrelium/Effects/Tile Pattern")]
    public class TilePatternEffect : CardEffectSO
    {
        public TileEffectSO Definition;

        [Tooltip("Pattern cells as offsets from the picked cell, in the reference frame where the " +
                 "target is due-east (direction 0) of the caster. Rotated to face the caster-to-target " +
                 "direction when the picked cell is an adjacent neighbor.")]
        public List<HexCoordinates> Offsets = new List<HexCoordinates> { new HexCoordinates(0, 0) };

        [Tooltip("When true, the picked cell must be directly adjacent to the caster.")]
        public bool RequireAdjacent = false;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null) return false;
            if (context.Source == null || context.Source.CurrentCell == null) return false;

            int distance = context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates);
            if (distance > context.EffectiveRange) return false;
            if (RequireAdjacent) return distance == 1;
            return true;
        }

        public override void Execute(CardContext context)
        {
            if (context.TargetCell == null || Definition == null) return;
            var system = TileEffectSystem.Instance;
            if (system == null) return;

            foreach (HexCoordinates coordinates in Cells(context.Source.CurrentCell, context.TargetCell))
            {
                HexCell cell = context.Grid != null ? context.Grid.GetCell(coordinates) : null;
                if (cell == null) continue;
                if (cell.HasEffect(Definition)) continue;
                system.PlaceEffect(cell, Definition);
            }
        }

        /// <summary>
        /// The hexes this pattern affects when placed from <paramref name="sourceCell"/> onto
        /// <paramref name="target"/>. The offset math is pure (no grid needed) so the AI can
        /// value a pattern without a live board. Falls back to unrotated offsets when the
        /// target is not a direct neighbor. Shared by execution and the caster AI.
        /// </summary>
        public List<HexCoordinates> Cells(HexCell sourceCell, HexCell target)
        {
            var result = new List<HexCoordinates>();
            if (target == null) return result;
            result.Add(target.Coordinates);

            if (sourceCell == null) return result;

            int direction = -1;
            for (int dir = 0; dir < 6; dir++)
            {
                if (sourceCell.Coordinates.Neighbor(dir) == target.Coordinates)
                {
                    direction = dir;
                    break;
                }
            }

            int rotations = direction < 0 ? 0 : (6 - direction) % 6;
            foreach (HexCoordinates offset in Offsets)
            {
                if (offset.Q == 0 && offset.R == 0) continue; // the picked cell itself is already in the list
                HexCoordinates rotated = Rotate(offset, rotations);
                result.Add(new HexCoordinates(target.Coordinates.Q + rotated.Q, target.Coordinates.R + rotated.R));
            }
            return result;
        }

        /// <summary>Rotates an axial offset by <paramref name="rotations"/> 60-degree steps.</summary>
        private static HexCoordinates Rotate(HexCoordinates cell, int rotations)
        {
            HexCoordinates rotated = cell;
            for (int i = 0; i < rotations; i++)
                rotated = new HexCoordinates(-rotated.R, rotated.Q + rotated.R);
            return rotated;
        }
    }
}