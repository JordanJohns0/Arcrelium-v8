using Characters;
using UnityEngine;

namespace Cards.Effects
{
    /// <summary>
    /// Base for card effects that apply a temporary next-turn status to the target (e.g.
    /// "draw 1 less card on their next turn", "can't move on their next turn"). Concrete
    /// subclasses build the actual CharacterStatus. Effect assets only ever reference a
    /// concrete subclass, so this base never needs to be instantiated.
    /// </summary>
    public abstract class StatusCardEffectSO : CardEffectSO
    {
        public int Duration = 1;

        protected abstract CharacterStatus CreateStatus(Character source, Character target, int duration);

        public override bool CanExecute(CardContext context)
        {
            if (context.Source == null || context.TargetCharacter == null) return false;
            if (context.Source.CurrentCell == null || context.TargetCharacter.CurrentCell == null) return false;
            if (context.TargetCharacter.Stats == null || !context.TargetCharacter.Stats.IsAlive) return false;

            return context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCharacter.CurrentCell.Coordinates)
                <= context.EffectiveRange;
        }

        public override void Execute(CardContext context)
        {
            if (context.Source == null || context.TargetCharacter == null) return;
            context.TargetCharacter.AddStatus(CreateStatus(context.Source, context.TargetCharacter, Duration));
        }
    }
}