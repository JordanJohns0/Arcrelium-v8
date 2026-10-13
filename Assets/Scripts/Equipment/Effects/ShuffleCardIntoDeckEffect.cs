using UnityEngine;
using Characters;
using Cards;

namespace Equipment.Effects
{
    /// <summary>
    /// Shuffles a specific card into a character's deck. When the trigger that fired this
    /// involved another character (e.g. the victim of damage you dealt), the card goes into
    /// THAT character's deck, and only when they are an enemy of the owner; otherwise it goes
    /// into the owner's own deck.
    /// </summary>
    [CreateAssetMenu(fileName = "ShuffleCardIntoDeckEffect", menuName = "Arcrelium/Equipment Effects/Shuffle Card Into Deck")]
    public class ShuffleCardIntoDeckEffect : EquipmentEffect
    {
        [Tooltip("The card to shuffle into the deck.")]
        public CardData Card;

        public override void Execute(EquipmentContext context)
        {
            if (Card == null) return;

            Character target = context.Other;
            if (target != null)
            {
                if (context.Source == null) return;
                if (!target.IsEnemyOf(context.Source)) return;
            }
            else
            {
                target = context.Source;
            }

            if (target == null) return;
            target.Deck.ShuffleIn(Card);
        }
    }
}
