using System.Collections.Generic;
using UnityEngine;
using HexGrid;
using TileEffects;

namespace Cards.Effects
{
    /// <summary>
    /// Lights a wall of three tiles on fire: the chosen adjacent hex plus the two hexes on
    /// its flanks (the neighbors either side of the axis from the caster to the target).
    /// The target must be directly adjacent to the caster.
    /// </summary>
    [CreateAssetMenu(fileName = "FireWallEffect", menuName = "Arcrelium/Effects/Fire Wall")]
    public class FireWallEffect : CardEffectSO
    {
        [Tooltip("The fire effect to place (Environment/Fire by default).")]
        public TileEffectSO FireDefinition;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null) return false;
            if (context.Source == null || context.Source.CurrentCell == null) return false;

            // The pick must be one of the six cells directly around the caster.
            return context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates) == 1;
        }

        public override void Execute(CardContext context)
        {
            if (context.TargetCell == null || FireDefinition == null) return;
            var system = TileEffectSystem.Instance;
            if (system == null) return;

            foreach (HexCell burned in BurningCells(context.Grid, context.Source.CurrentCell, context.TargetCell))
            {
                if (burned == null) continue;
                if (burned.HasEffect(FireDefinition)) continue;
                system.PlaceEffect(burned, FireDefinition);
            }
        }

        /// <summary>
        /// The tiles a Fire Wall ignites: the chosen cell plus the two cells flanking the
        /// caster-to-target axis, kept right next to the caster (one either side of where they
        /// are facing). Shared by execution and the caster AI so it can value a wall by how
        /// many enemies are standing in it.
        /// </summary>
        public List<HexCell> BurningCells(HexGridManager grid, HexCell sourceCell, HexCell target)
        {
            var result = new List<HexCell>();
            if (grid == null || sourceCell == null || target == null)
            {
                result.Add(target);
                return result;
            }

            HexCoordinates from = sourceCell.Coordinates;
            int direction = -1;
            for (int dir = 0; dir < 6; dir++)
            {
                if (from.Neighbor(dir) == target.Coordinates)
                {
                    direction = dir;
                    break;
                }
            }

            if (direction < 0) // not a neighbor — just burn the requested tile
            {
                result.Add(target);
                return result;
            }

            AddIfExists(grid, result, from.Neighbor((direction + 1) % 6));
            AddIfExists(grid, result, from.Neighbor((direction + 5) % 6));
            result.Add(target);
            return result;
        }

        private static void AddIfExists(HexGridManager grid, List<HexCell> cells, HexCoordinates coordinates)
        {
            HexCell cell = grid.GetCell(coordinates);
            if (cell != null) cells.Add(cell);
        }
    }
}