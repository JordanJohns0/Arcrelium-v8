using HexGrid;

namespace TileEffects
{
    /// <summary>
    /// A live copy of a TileEffectSO sitting on one particular cell. Carries the countdown:
    /// rounds left before it expires (Permanent never expires on its own).
    /// </summary>
    public class TileEffectInstance
    {
        /// <summary>Sentinel meaning "never expires on its own" (Duration 0 effects).</summary>
        public const int Permanent = -1;

        public TileEffectSO Data;
        public int TurnsRemaining = Permanent;
        public HexCell Cell;
    }
}