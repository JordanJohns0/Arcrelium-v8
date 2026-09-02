using System.Collections.Generic;
using UnityEngine;
using Characters;
using HexGrid;

namespace Equipment.Effects
{
    /// <summary>
    /// Pushes a character (the trigger's Other, typically an enemy that just ended its turn)
    /// one tile away from the equipment owner (Source). Only pushes if it is adjacent to the
    /// owner: it moves to an adjacent walkable tile that does not bring it closer to the owner.
    /// </summary>
    [CreateAssetMenu(fileName = "PushAwayEnemyEffect", menuName = "Arcrelium/Equipment Effects/Push Away Enemy")]
    public class PushAwayEnemyEffect : EquipmentEffect
    {
        public int Distance = 1;

        public override void Execute(EquipmentContext context)
        {
            if (context.Source == null || context.Grid == null) return;
            Character other = context.Other;
            if (other == null || other.CurrentCell == null) return;
            if (context.Source.CurrentCell == null) return;

            HexCoordinates sourceCoords = context.Source.CurrentCell.Coordinates;
            HexCoordinates otherCoords = other.CurrentCell.Coordinates;

            int currentDistance = sourceCoords.DistanceTo(otherCoords);
            if (currentDistance > Distance) return;

            // Candidate pushes: the pushed character's walkable neighbours not already at/under
            // the desired separation that don't move it toward the source.
            var candidates = new List<HexCell>();
            foreach (HexCell neighbor in context.Grid.GetNeighbors(otherCoords))
            {
                if (!neighbor.IsWalkable) continue;
                if (neighbor == context.Source.CurrentCell) continue;

                int newDistance = sourceCoords.DistanceTo(neighbor.Coordinates);
                if (newDistance <= currentDistance && newDistance <= Distance) continue;

                candidates.Add(neighbor);
            }

            // Prefer tiles that push strictly farther from the source; otherwise fall back to any
            // walkable exit (e.g. no cell moves it farther, pick one that keeps separation).
            if (candidates.Count == 0) return;

            HexCell destination = candidates[Random.Range(0, candidates.Count)];
            other.SetCellOnly(destination);

            if (Application.isPlaying)
                other.AnimateToCell(destination);
        }
    }
}