using FantasyBrawl.Characters;
using FantasyBrawl.HexGrid;
using FantasyBrawl.Game;

namespace FantasyBrawl.Equipment
{
    /// <summary>
    /// Everything an equipment trigger needs to resolve its effect: who owns the equipment,
    /// the board, and the current game state. Not every field will be populated — depends
    /// on the trigger timing.
    /// </summary>
    public struct EquipmentContext
    {
        public Character Source;
        public HexGridManager Grid;
        public TurnManager TurnManager;
        public TriggerTiming Timing;
        public int Amount;
    }
}
