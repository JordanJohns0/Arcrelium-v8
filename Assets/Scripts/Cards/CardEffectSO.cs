using UnityEngine;

namespace FantasyBrawl.Cards
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
    }
}
