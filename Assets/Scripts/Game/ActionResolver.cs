using System.Collections.Generic;
using UnityEngine;
using Characters;
using Cards;
using HexGrid;

namespace Game
{
    /// <summary>
    /// The ONLY way anything happens on the board. Movement, attacks, abilities — all of it
    /// flows through PlayCard. There is deliberately no separate "MoveCharacter" or
    /// "AttackCharacter" API on Character itself, so it's impossible to bypass the card system.
    ///
    /// Cost model: a card's Cost is the number of OTHER cards from the same hand that must be
    /// discarded to play it. There is no separate action-point or mana resource — hand size
    /// itself is the only economy.
    /// </summary>
    public class ActionResolver : MonoBehaviour
    {
        [SerializeField] private HexGridManager grid;

        public event System.Action<Character, CardData> OnCardPlayed;
        public event System.Action<CardContext> OnBeforeCardPlay;

        /// <summary>
        /// Attempts to play a card from the source character's hand against an optional target,
        /// paying its cost by discarding costCards from the same hand.
        /// Returns false (and changes nothing) if the card isn't in hand, the cost payment is
        /// invalid, or the effect rejects the target. Costs are only paid once the play is
        /// confirmed valid — a failed attempt never discards anything.
        /// </summary>
        public bool PlayCard(
            Character source,
            CardInstance card,
            List<CardInstance> costCards = null,
            HexCell targetCell = null,
            Character targetCharacter = null)
        {
            if (source == null || card == null) return false;
            if (!source.Hand.Contains(card)) return false;
            if (card.Data.Effect == null) { Debug.Log($"[ActionResolver] FAIL: card '{card.Data.CardName}' has no effect."); return false; }

            costCards ??= new List<CardInstance>();
            if (!IsCostValid(source, card, costCards)) return false;
            if (!IsTargetValid(card.Data, targetCell, targetCharacter)) return false;

            var context = new CardContext
            {
                Source = source,
                Grid = grid,
                TargetCell = targetCell,
                TargetCharacter = targetCharacter,
                Range = card.Data.Range
            };

            if (!card.Data.Effect.CanExecute(context)) return false;

            OnBeforeCardPlay?.Invoke(context);

            card.Data.Effect.Execute(context);

            // Pay cost: discard the other cards, then discard the played card itself.
            foreach (CardInstance costCard in costCards)
            {
                source.Hand.RemoveCard(costCard);
                source.Deck.Discard(costCard);
            }
            source.Hand.RemoveCard(card);
            source.Deck.Discard(card);

            OnCardPlayed?.Invoke(source, card.Data);
            return true;
        }

        /// <summary>
        /// Cost cards must: be exactly card.Cost in count, all currently in the source's hand,
        /// all distinct, and none of them the card being played.
        /// </summary>
        private bool IsCostValid(Character source, CardInstance card, List<CardInstance> costCards)
        {
            if (costCards.Count != card.Data.Cost) return false;

            var seen = new HashSet<CardInstance>();
            foreach (CardInstance costCard in costCards)
            {
                if (costCard == card) return false;
                if (!source.Hand.Contains(costCard)) return false;
                if (!seen.Add(costCard)) return false; // duplicate reference in the list
            }

            return true;
        }

        private static bool IsTargetValid(CardData card, HexCell targetCell, Character targetCharacter)
        {
            switch (card.TargetType)
            {
                case TargetType.None:
                case TargetType.Self:
                    return true;
                case TargetType.HexCell:
                    return targetCell != null;
                case TargetType.EnemyCharacter:
                case TargetType.AllyCharacter:
                case TargetType.AnyCharacter:
                    return targetCharacter != null;
                default:
                    return false;
            }
        }
    }
}
