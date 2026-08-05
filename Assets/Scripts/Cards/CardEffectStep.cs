using System;

namespace Cards
{
    /// <summary>
    /// When a step targets a hex cell, controls whether the cell must be empty or may be occupied.
    /// </summary>
    public enum CellConstraint
    {
        Any,   // empty or occupied (e.g. AoE blast)
        Empty  // must have no occupant (e.g. movement)
    }

    /// <summary>
    /// One effect in a card's play sequence, paired with the target type and range that
    /// THIS step needs. A card with multiple steps can express e.g. "deal 4 damage to an
    /// enemy (EnemyCharacter, range 1) and take 1 yourself (Self, range 0)".
    /// </summary>
    [Serializable]
    public class CardEffectStep
    {
        public CardEffectSO Effect;
        public TargetType TargetType = TargetType.Self;
        public int Range = 0;
        public CellConstraint CellConstraint = CellConstraint.Any;
    }
}
