using UnityEngine;

namespace Cards.Effects
{
    [CreateAssetMenu(fileName = "ConditionalEffect", menuName = "Arcrelium/Effects/Conditional")]
    public class ConditionalEffect : CardEffectSO
    {
        public CardConditionSO Condition;
        public CardEffectSO TrueEffect;
        public CardEffectSO FalseEffect;

        public override bool CanExecute(CardContext context)
        {
            if (Condition == null) return true;

            if (Condition.Evaluate(context))
                return TrueEffect != null && TrueEffect.CanExecute(context);

            return FalseEffect == null || FalseEffect.CanExecute(context);
        }

        public override void Execute(CardContext context)
        {
            if (Condition == null || Condition.Evaluate(context))
                TrueEffect?.Execute(context);
            else
                FalseEffect?.Execute(context);
        }
    }
}
