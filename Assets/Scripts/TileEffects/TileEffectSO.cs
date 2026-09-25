using UnityEngine;

namespace TileEffects
{
    /// <summary>
    /// Definition of a persistent board effect that sits on a tile (oil, fire, rocks...).
    /// Durations count down once per full round (when turns wrap back to the first character);
    /// a Duration of 0 means the effect never expires on its own.
    ///
    /// Subclasses add per-behavior hooks: burning on entry/turn start (Fire), what it means to
    /// be slippery (Oil), how the tile becomes unwalkable (Rock). Drops in as a ScriptableObject
    /// so new environmental effects are data, not code.
    /// </summary>
    public abstract class TileEffectSO : ScriptableObject
    {
        public string EffectName;
        [TextArea] public string Description;

        [Tooltip("Full rounds the effect lasts before expiring. 0 = permanent.")]
        public int Duration;

        [Tooltip("While present, the tile is unwalkable (paths and moves cannot enter it).")]
        public bool BlocksMovement;

        [Tooltip("Slippery ground: a character entering the tile slides one tile past it.")]
        public bool Slippery;

        /// <summary>Called when a character moves onto the tile (or slides onto it mid-chain).</summary>
        public virtual void OnCharacterEntered(TileEffectContext context) { }

        /// <summary>Called when the active character starts their turn while standing on this tile.</summary>
        public virtual void OnCharacterTurnStarted(TileEffectContext context) { }

        /// <summary>Called right after this effect is placed on a tile (interactions: fire igniting oil).</summary>
        public virtual void OnEffectAddedToCell(TileEffectContext context) { }
    }
}