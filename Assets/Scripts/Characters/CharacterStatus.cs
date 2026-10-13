namespace Characters
{
    /// <summary>
    /// Base class for a temporary condition on a character that lasts a number of the
    /// affected character's turns. A status is dormant when it is applied (usually during
    /// someone else's turn) and only takes effect once its owner's turn begins; it is then
    /// ticked down when that turn ends. Subclasses override only the hooks they care about
    /// (draw count, movement, targeting).
    /// </summary>
    public abstract class CharacterStatus
    {
        public string Name = "Status";

        /// <summary>Remaining number of the owner's turns this status applies to.</summary>
        public int Duration;

        /// <summary>True only while the owner's current turn is underway.</summary>
        public bool ActiveThisTurn { get; private set; }

        public bool Expired => Duration <= 0;

        /// <summary>Adjusts how many cards the owner draws at the start of their turn.</summary>
        public virtual int ModifyDrawCount(int baseCount) => baseCount;

        /// <summary>Whether the owner is allowed to move this turn.</summary>
        public virtual bool AllowsMovement => true;

        /// <summary>Whether the owner is allowed to target the given character this turn.</summary>
        public virtual bool AllowsTargeting(Character target) => true;

        /// <summary>Called when the owner's turn begins; the status becomes active for that turn.</summary>
        public virtual void OnTurnStarted() => ActiveThisTurn = true;

        /// <summary>Called when the owner's turn ends; an active status is spent.</summary>
        public virtual void OnTurnEnded()
        {
            if (ActiveThisTurn) Duration--;
            ActiveThisTurn = false;
        }
    }

    /// <summary>Owner draws one fewer card on their next turn (never below zero).</summary>
    public class ReducedDrawStatus : CharacterStatus
    {
        public int Amount = 1;

        public override int ModifyDrawCount(int baseCount)
        {
            return baseCount - Amount;
        }
    }

    /// <summary>Owner cannot move on their next turn.</summary>
    public class CannotMoveStatus : CharacterStatus
    {
        public override bool AllowsMovement => false;
    }

    /// <summary>
    /// Owner cannot target <see cref="Protected"/> on their next turn ("No one can target
    /// that character this turn" style effects).
    /// </summary>
    public class CannotTargetStatus : CharacterStatus
    {
        public Character Protected;

        public override bool AllowsTargeting(Character target)
        {
            return target == null || target != Protected;
        }
    }
}
