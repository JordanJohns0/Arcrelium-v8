using UnityEngine;
using Characters;

namespace Cards
{
    public enum CardValueType
    {
        Constant,
        CurrentBlock,
        CurrentHealth,
        MaxHealth,
        MissingHealth
    }

    [System.Serializable]
    public struct CardValue
    {
        public CardValueType Type;
        public int Constant;

        public int Evaluate(Character source)
        {
            if (source == null) return Constant;

            switch (Type)
            {
                case CardValueType.Constant:
                    return Constant;
                case CardValueType.CurrentBlock:
                    return source.Stats.Block;
                case CardValueType.CurrentHealth:
                    return source.Stats.CurrentHealth;
                case CardValueType.MaxHealth:
                    return source.Stats.MaxHealth;
                case CardValueType.MissingHealth:
                    return source.Stats.MaxHealth - source.Stats.CurrentHealth;
                default:
                    return Constant;
            }
        }

        public static CardValue MakeConstant(int value)
        {
            return new CardValue { Type = CardValueType.Constant, Constant = value };
        }

        public static CardValue MakeBlock()
        {
            return new CardValue { Type = CardValueType.CurrentBlock };
        }

        public static CardValue MakeHealth()
        {
            return new CardValue { Type = CardValueType.CurrentHealth };
        }

        public static CardValue MakeMissingHealth()
        {
            return new CardValue { Type = CardValueType.MissingHealth };
        }
    }
}
