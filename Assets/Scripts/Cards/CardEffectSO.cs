using UnityEngine;

namespace Cards
{
    /// <summary>
    /// Base class for card effects as ScriptableObject assets, so designers can create
    /// e.g. "Move_Effect.asset", "Attack_Effect.asset" in the editor and drop them onto
    /// CardData without touching code. Subclass this for each behavior (movement, attack,
    /// heal, status effect, etc).
    /// </summary>
    public abstract class CardEffectSO : ScriptableObject, ICardEffect
    {
        public virtual bool CanExecute(CardContext context) => true;

        public abstract void Execute(CardContext context);

        /// <summary>
        /// Whether this effect represents an offensive attack (deals damage to one or more
        /// enemies). Used to decide cosmetic behavior such as playing the attack animation:
        /// move/block/heal cards that merely have a target should not swing.
        /// </summary>
        public virtual bool IsAttack => false;
    }
}
