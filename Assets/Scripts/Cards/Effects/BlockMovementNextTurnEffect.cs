using Characters;
using UnityEngine;

namespace Cards.Effects
{
    /// <summary>Enemy cannot move on its next turn.</summary>
    [CreateAssetMenu(fileName = "BlockMovementNextTurnEffect", menuName = "Arcrelium/Effects/Block Movement Next Turn")]
    public class BlockMovementNextTurnEffect : StatusCardEffectSO
    {
        protected override CharacterStatus CreateStatus(Character source, Character target, int duration)
        {
            return new CannotMoveStatus { Name = "Pinned", Duration = duration };
        }
    }
}