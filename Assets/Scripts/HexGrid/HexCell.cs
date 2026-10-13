using System;
using System.Collections.Generic;
using UnityEngine;
using Characters;
using TileEffects;

namespace HexGrid
{
    /// <summary>
    /// A single tile on the board. Tracks its coordinates, whatever occupies it, and any
    /// persistent environmental effects sitting on it (fire, oil, rocks...).
    /// Attach to your hex tile prefab.
    /// </summary>
    public class HexCell : MonoBehaviour
    {
        [SerializeField] private bool startBlocked;

        public HexCoordinates Coordinates { get; private set; }
        public Character Occupant { get; private set; }
        public bool IsBlocked { get; private set; }

        public bool IsOccupied => Occupant != null;
        public bool IsWalkable => !IsBlocked && !IsOccupied && !HasBlockingEffect;

        private List<TileEffectInstance> effects;
        public List<TileEffectInstance> Effects => effects ?? (effects = new List<TileEffectInstance>());

        public bool HasBlockingEffect => effects != null && effects.Exists(effect => effect.Data.BlocksMovement);

        public void Initialize(HexCoordinates coordinates)
        {
            Coordinates = coordinates;
            IsBlocked = startBlocked;
        }

        public void SetBlocked(bool blocked)
        {
            IsBlocked = blocked;
        }

        public void SetOccupant(Character character)
        {
            Occupant = character;
        }

        public void ClearOccupant()
        {
            Occupant = null;
        }

        public void AddEffectInstance(TileEffectInstance effect)
        {
            if (effect != null && !Effects.Contains(effect)) Effects.Add(effect);
        }

        public void RemoveEffectInstance(TileEffectInstance effect)
        {
            if (effects != null) effects.Remove(effect);
        }

        public bool HasEffect(TileEffectSO definition)
        {
            return effects != null && effects.Exists(effect => effect.Data == definition);
        }

        public bool HasEffect(Func<TileEffectSO, bool> predicate)
        {
            return effects != null && effects.Exists(effect => predicate(effect.Data));
        }
    }
}