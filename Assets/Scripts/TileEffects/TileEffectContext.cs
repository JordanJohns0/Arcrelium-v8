using Characters;
using HexGrid;

namespace TileEffects
{
    /// <summary>
    /// Everything a tile effect needs to do its thing: which tile it lives on, who it is
    /// acting on (the character who entered or whose turn started), and a handle back to the
    /// system so effects can detonate AoE, ignite oil, remove themselves, etc.
    /// </summary>
    public class TileEffectContext
    {
        public TileEffectSystem System;
        public HexCell Cell;
        public Character Character;
        public TileEffectInstance Effect;
    }
}