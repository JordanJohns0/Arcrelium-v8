using UnityEngine;

namespace Cards
{
    public abstract class CardConditionSO : ScriptableObject
    {
        public abstract bool Evaluate(CardContext context);
    }
}
