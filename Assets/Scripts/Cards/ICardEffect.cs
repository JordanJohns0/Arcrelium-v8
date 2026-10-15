using System.Collections.Generic;
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
    /// Mutable class so equipment effects can modify BonusRange/BonusDamage before execution.
    /// </summary>
    public class CardContext
    {
        public Character Source;
        public HexGridManager Grid;
        public HexCell TargetCell;
        public Character TargetCharacter;
        public int Range;
        public int BonusRange;
        public int BonusDamage;

        /// <summary>The multiplier applied to base damage for this card play.</summary>
        public float DamageMultiplier = 1f;

        /// <summary>
        /// The exact cells to walk when this is a movement play (starts adjacent to Source's
        /// current cell, ends on TargetCell). When set, the move step walks this route rather
        /// than re-pathfinding a shortest route, so the player picks exactly where they move.
        /// </summary>
        public List<HexCell> MovePath;

        /// <summary>
        /// The card instance this context resolves (the played card, the card just drawn for
        /// OnDrawnModifiers, or the just-discarded card). Lets effects mutate an instance's own
        /// state, e.g. "when this card is drawn, set its cost to 0, 1, or 2 at random".
        /// </summary>
        public CardInstance Card;

        /// <summary>
        /// The card options attached to the step being resolved (CardEffectStep.CardModifiers);
        /// applied to every card that step's effect produces (e.g. set drawn cards' cost to 1).
        /// </summary>
        public List<CardModifierSO> StepModifiers;

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
