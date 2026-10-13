using Characters;
using UnityEngine;

namespace Cards.Effects
{
    /// <summary>Enemy cannot target the caster on its next turn.</summary>
    [CreateAssetMenu(fileName = "BlockTargetingNextTurnEffect", menuName = "Arcrelium/Effects/Block Targeting Next Turn")]
    public class BlockTargetingNextTurnEffect : StatusCardEffectSO
    {
        protected override CharacterStatus CreateStatus(Character source, Character target, int duration)
        {
            return new CannotTargetStatus { Name = "Smoked", Duration = duration, Protected = source };
        }
    }
}