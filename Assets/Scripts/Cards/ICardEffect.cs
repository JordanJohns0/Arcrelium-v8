using Characters;
using HexGrid;

namespace Cards
{
    public enum TargetType
    {
        None,           // no target needed (e.g. self-buff)
        Self,
        HexCell,        // an empty/any cell (e.g. movement destination)
        EnemyCharacter,
        AllyCharacter,
        AnyCharacter
    }

    /// <summary>
    /// Everything a played card needs to resolve its effect: who played it,
    /// the board, and whatever target the player selected (cell and/or character).
    /// Not every field will be populated — depends on the card's TargetType.
    /// Mutable class so equipment effects can modify BonusRange before execution.
    /// </summary>
    public class CardContext
    {
        public Character Source;
        public HexGridManager Grid;
        public HexCell TargetCell;
        public Character TargetCharacter;
        public int Range;
        public int BonusRange;

        public int EffectiveRange => Range + BonusRange;
    }

    /// <summary>
    /// Strategy interface for "what does this card actually do". Implement one class
    /// per behavior (Move, Attack, Heal, Buff, etc.) and reference it from CardData.
    /// This is the extension point for adding real card content later.
    /// </summary>
    public interface ICardEffect
    {
        /// <summary>Return false to abort the play (e.g. invalid/out-of-range target) before costs are paid.</summary>
        bool CanExecute(CardContext context);

        void Execute(CardContext context);
    }
}
