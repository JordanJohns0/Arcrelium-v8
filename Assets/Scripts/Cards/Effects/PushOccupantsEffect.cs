using System.Collections.Generic;
using UnityEngine;
using Characters;
using HexGrid;
using TileEffects;

namespace Cards.Effects
{
    /// <summary>
    /// Clears the targeted tile before a solid fixture (a rock) rises through it: any enemy
    /// standing there is hurled onto a random walkable adjacent tile. If there is no escape
    /// (every neighbor occupied, rock-stopped, or beyond the board edge), the enemy is
    /// crushed instead. Allies and the caster are never displaced — the card cannot be
    /// played against them.
    /// </summary>
    [CreateAssetMenu(fileName = "PushOccupantsEffect", menuName = "Arcrelium/Effects/Push Occupants")]
    public class PushOccupantsEffect : CardEffectSO
    {
        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null) return false;
            if (context.Source == null || context.Source.CurrentCell == null) return false;

            if (context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates)
                > context.EffectiveRange)
                return false;

            Character occupant = context.TargetCell.Occupant;
            if (occupant != null && occupant.IsStructure) return false;
            return occupant == null || (occupant.Stats.IsAlive && occupant.IsEnemyOf(context.Source));
        }

        public override void Execute(CardContext context)
        {
            if (context.TargetCell == null || context.Grid == null) return;

            Character occupant = context.TargetCell.Occupant;
            if (occupant == null || !occupant.Stats.IsAlive) return;
            if (occupant.IsStructure) return; // fixtures can't be displaced, only targeted directly

            var exits = new List<HexCell>();
            foreach (HexCell neighbor in context.Grid.GetNeighbors(context.TargetCell.Coordinates))
            {
                if (neighbor != null && neighbor.IsWalkable) exits.Add(neighbor);
            }

            if (exits.Count == 0)
            {
                // No way out — the rising stone crushes whoever is on top.
                occupant.TakeDamage(9999);
                return;
            }

            HexCell destination = exits[Random.Range(0, exits.Count)];
            var system = TileEffectSystem.Instance;
            if (system != null)
                system.WalkInto(occupant, destination); // entry effects apply (fire burns, oil slides)
            else
                occupant.SetCellOnly(destination);

            if (Application.isPlaying)
                occupant.AnimateToCell(destination);
        }
    }
}