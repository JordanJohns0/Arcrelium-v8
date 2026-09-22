using System.Collections.Generic;
using UnityEngine;

namespace Cards
{
    /// <summary>
    /// Definition of a single card. Create as an asset (right-click > Create > Arcrelium > Card).
    /// A card's behavior is a list of Steps, each with its own effect, target type and range, so
    /// a single card can do several things at once (e.g. damage an enemy AND hurt yourself).
    /// </summary>
    [CreateAssetMenu(fileName = "NewCard", menuName = "Arcrelium/Card")]
    public class CardData : ScriptableObject
    {
        public string CardName = "New Card";
        [TextArea] public string Description;
        public Sprite Artwork;

        [Tooltip("The only resource cost in the game: how many OTHER cards must be discarded " +
                 "from hand to play this card. Shown top-left on the card face.")]
        public int Cost = 0;

        [Tooltip("Effects to perform when played, in order. Each step has its own target type and range.")]
        public List<CardEffectStep> Steps = new List<CardEffectStep>();

        /// <summary>
        /// The step whose target the player/AI must pick for this card (the first step that
        /// requires a board target). Null when the card needs no pick (self/untargeted steps only).
        /// </summary>
        public CardEffectStep PrimaryTargetStep
        {
            get
            {
                if (Steps == null) return null;
                foreach (CardEffectStep step in Steps)
                {
                    if (step == null || step.Effect == null) continue;
                    if (step.TargetType != TargetType.None && step.TargetType != TargetType.Self)
                        return step;
                }
                return null;
            }
        }

        /// <summary>The target type of the pick this card requires, or None for untargeted cards.</summary>
        public TargetType TargetType => PrimaryTargetStep != null ? PrimaryTargetStep.TargetType : TargetType.None;

        /// <summary>The range of the pick this card requires.</summary>
        public int Range => PrimaryTargetStep != null ? PrimaryTargetStep.Range : 0;

        /// <summary>Whether the targeted hex cell must be empty (e.g. movement) or may be occupied (e.g. AoE).</summary>
        public CellConstraint CellConstraint => PrimaryTargetStep != null ? PrimaryTargetStep.CellConstraint : CellConstraint.Any;

        /// <summary>
        /// Whether any step in this card deals damage to an enemy (offensive attack). Used for
        /// cosmetic pacing like playing the attack animation, which should not fire for move,
        /// block, heal or other targeted-but-non-attacking cards.
        /// </summary>
        public bool IsAttack
        {
            get
            {
                if (Steps == null) return false;
                foreach (CardEffectStep step in Steps)
                {
                    if (step != null && step.Effect != null && step.Effect.IsAttack)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Whether any step in this card moves the character. Used by the UI to enable
        /// path-building: movement-style picks let the player click a chain of cells with
        /// leftover distance, as opposed to a single-click AoE/attack hex target.
        /// </summary>
        public bool IsMove
        {
            get
            {
                if (Steps == null) return false;
                foreach (CardEffectStep step in Steps)
                {
                    if (step != null && step.Effect != null && step.Effect.IsMove)
                        return true;
                }
                return false;
            }
        }
    }
}
