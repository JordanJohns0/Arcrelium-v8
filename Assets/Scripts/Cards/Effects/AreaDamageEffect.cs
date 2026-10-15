using UnityEngine;
using Characters;
using HexGrid;

namespace Cards.Effects
{
    /// <summary>Who an area effect catches in its blast radius.</summary>
    public enum AreaFaction
    {
        Enemies,
        Allies,
        All
    }

    /// <summary>
    /// Deals damage to every matching character within a radius of the SELECTED TILE
    /// (TargetType.HexCell), not a single targeted character. The tile itself just needs
    /// to be within the card's pick range; the blast happens around that tile.
    /// </summary>
    [CreateAssetMenu(fileName = "AreaDamageEffect", menuName = "Arcrelium/Effects/Area Damage")]
    public class AreaDamageEffect : CardEffectSO
    {
        public CardValue Damage = CardValue.MakeConstant(2);

        [Tooltip("Blast radius in hexes around the selected tile.")]
        public int Radius = 2;

        [Tooltip("Who the blast damages: enemies, allies, or everyone.")]
        public AreaFaction Faction = AreaFaction.Enemies;

        [Tooltip("Whether the caster is also hit when standing inside the blast (only relevant for Allies/All).")]
        public bool IncludeSelf = false;

        [Tooltip("Damage lost per hex of distance from the blast center (0 = flat damage across the whole blast).")]
        public int DamagePerHexFalloff = 0;

        [Tooltip("Lowest damage a target can take from the blast, even at the edge (0 = no floor, falls to 0).")]
        public int MinimumDamage = 0;

        public override bool IsAttack => true;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null) return false;
            if (context.Source == null || context.Source.CurrentCell == null) return false;

            int distance = context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates);
            if (distance > context.EffectiveRange) return false;

            // Require at least one victim so the card can't be wasted on an empty blast.
            return context.Grid == null || CountTargets(context) > 0;
        }

        public override void Execute(CardContext context)
        {
            if (context.TargetCell == null || context.Grid == null) return;

            foreach (HexCell cell in context.Grid.GetCellsInRange(context.TargetCell.Coordinates, Radius))
            {
                Character occupant = cell.Occupant;
                if (!MatchesFaction(context.Source, occupant, IncludeSelf)) continue;
                int distance = cell.Coordinates.DistanceTo(context.TargetCell.Coordinates);
                occupant.TakeDamage(DamageAt(context.Source, distance, context.BonusDamage, context.DamageMultiplier), context.Source);
            }
        }

        private int CountTargets(CardContext context)
        {
            int count = 0;
            foreach (HexCell cell in context.Grid.GetCellsInRange(context.TargetCell.Coordinates, Radius))
            {
                Character occupant = cell.Occupant;
                if (!MatchesFaction(context.Source, occupant, IncludeSelf)) continue;
                int distance = cell.Coordinates.DistanceTo(context.TargetCell.Coordinates);
                if (DamageAt(context.Source, distance, context.BonusDamage, context.DamageMultiplier) <= 0) continue; // a 0-damage hit isn't a real victim
                count++;
            }
            return count;
        }

        /// <summary>
        /// Damage dealt to a target at <paramref name="distance"/> hexes from the blast center:
        /// full <see cref="Damage"/> at the center, dropping by <see cref="DamagePerHexFalloff"/>
        /// per hex, floored at <see cref="MinimumDamage"/> (never below 0). Also used by the AI so
        /// it values a blast by the actual damage each enemy would take. Enchantment bonus damage
        /// is added before falloff so it applies across the whole blast.
        /// </summary>
        public int DamageAt(Character source, int distance, int bonusDamage = 0, float multiplier = 1f)
        {
            int baseDamage = Mathf.RoundToInt(Damage.Evaluate(source) * Mathf.Max(0f, multiplier));
            int damage = baseDamage + Mathf.Max(0, bonusDamage) - DamagePerHexFalloff * distance;
            if (MinimumDamage > 0) damage = Mathf.Max(damage, MinimumDamage);
            return Mathf.Max(damage, 0);
        }

        private bool MatchesFaction(Character source, Character occupant, bool includeSelf)
        {
            if (occupant == null || occupant.Stats == null || !occupant.Stats.IsAlive) return false;
            if (occupant == source && !includeSelf) return false;

            switch (Faction)
            {
                case AreaFaction.Enemies:
                    return occupant.IsEnemyOf(source);
                case AreaFaction.Allies:
                    return occupant.IsAllyOf(source);
                default:
                    return true;
            }
        }
    }
}
