using Characters;
using Cards;
using HexGrid;
using Game;

namespace Equipment
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
        public HexCell TargetCell;
        public CardContext CardContext;

        /// <summary>
        /// The secondary character involved in the trigger. For OnEnemyTurnEnded this is the
        /// enemy whose turn just ended; for OnDamageTaken it could be a future damage source.
        /// Left null when the effect only concerns the wearer (Source).
        /// </summary>
        public Character Other;
    }
}
