using UnityEngine;

namespace Cards
{
    /// <summary>
    /// Definition of a single card. Create as an asset (right-click > Create >  > Card).
    /// Deliberately generic right now — Effect + TargetType + Range cover most action types
    /// (move, attack, ability). Add fields here as your design needs grow.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCard", menuName = "/Card")]
    public class CardData : ScriptableObject
    {
        public string CardName = "New Card";
        [TextArea] public string Description;
        public Sprite Artwork;

        public TargetType TargetType = TargetType.HexCell;

        [Tooltip("Range in hexes, if the effect needs one (movement distance, attack reach, etc).")]
        public int Range = 1;

        [Tooltip("The only resource cost in the game: how many OTHER cards must be discarded " +
                 "from hand to play this card. Shown top-left on the card face.")]
        public int Cost = 0;

        [Tooltip("The behavior this card performs when played.")]
        public CardEffectSO Effect;
    }
}