using Characters;
using UnityEngine;

namespace Cards.Effects
{
    /// <summary>Enemy draws 1 fewer card on its next turn.</summary>
    [CreateAssetMenu(fileName = "ReduceDrawNextTurnEffect", menuName = "Arcrelium/Effects/Reduce Draw Next Turn")]
    public class ReduceDrawNextTurnEffect : StatusCardEffectSO
    {
        public int Amount = 1;

        protected override CharacterStatus CreateStatus(Character source, Character target, int duration)
        {
            return new ReducedDrawStatus { Name = "Fuddled", Duration = duration, Amount = Amount };
        }
    }
}