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
        ///
        /// The played card and its cost cards are removed from the hand BEFORE effects resolve,
        /// so draw effects see the freed hand slots instead of being blocked by a full hand.
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
            if (!HasBoundSteps(card.Data)) return false;

            costCards ??= new List<CardInstance>();
            if (!IsCostValid(source, card, costCards)) return false;
            if (!IsTargetValid(source, card.Data, targetCell, targetCharacter)) return false;

            // Validate every step against its own context (its own range and target type)
            // before any step executes, so a multi-step card never partially resolves.
            var executions = new List<(CardEffectStep Step, CardContext Context)>(card.Data.Steps.Count);
            foreach (CardEffectStep step in card.Data.Steps)
            {
                CardContext context = MakeContext(source, card.Data, step, targetCell, targetCharacter);
                if (!step.Effect.CanExecute(context)) return false;
                executions.Add((step, context));
            }

            // Equipment may boost range before execution; the bonus applies to every step.
            CardContext equipmentContext = PrimaryContext(executions);
            OnBeforeCardPlay?.Invoke(equipmentContext);
            int bonusRange = equipmentContext.BonusRange;
            if (bonusRange != 0)
            {
                foreach (var (_, context) in executions)
                    context.BonusRange = bonusRange;
            }

            // Enchantments on worn equipment may add damage to this card; applies to every step.
            int bonusDamage = source.Equipment != null
                ? source.Equipment.GetTotalCardDamageBonus(card.Data)
                : 0;
            if (bonusDamage != 0)
            {
                foreach (var (_, context) in executions)
                    context.BonusDamage = bonusDamage;
            }

            // Pay cost: discard the other cards, then the played card itself. Must happen
            // before effects execute so draw effects see the freed hand slots.
            foreach (CardInstance costCard in costCards)
            {
                source.Hand.RemoveCard(costCard);
                source.Deck.Discard(costCard);
            }
            source.Hand.RemoveCard(card);
            source.Deck.Discard(card);

            foreach (var (step, context) in executions)
                step.Effect.Execute(context);

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

        private static bool HasBoundSteps(CardData card)
        {
            if (card.Steps == null || card.Steps.Count == 0)
            {
                Debug.Log($"[ActionResolver] FAIL: card '{card.CardName}' has no steps.");
                return false;
            }
            foreach (CardEffectStep step in card.Steps)
            {
                if (step == null || step.Effect == null)
                {
                    Debug.Log($"[ActionResolver] FAIL: card '{card.CardName}' has an empty step.");
                    return false;
                }
            }
            return true;
        }

        private CardContext MakeContext(Character source, CardData card, CardEffectStep step,
            HexCell targetCell, Character targetCharacter)
        {
            return new CardContext
            {
                Source = source,
                Grid = grid,
                TargetCell = targetCell,
                TargetCharacter = targetCharacter,
                Range = step.Range
            };
        }

        /// <summary>
        /// The context for the step whose target was picked, or the first step's context for
        /// untargeted cards. This is what equipment sees on OnBeforeCardPlay.
        /// </summary>
        private static CardContext PrimaryContext(List<(CardEffectStep Step, CardContext Context)> executions)
        {
            foreach (var (step, context) in executions)
            {
                if (step.TargetType != TargetType.None && step.TargetType != TargetType.Self)
                    return context;
            }
            return executions.Count > 0 ? executions[0].Context : null;
        }

        private static bool IsTargetValid(Character source, CardData card, HexCell targetCell, Character targetCharacter)
        {
            switch (card.TargetType)
            {
                case TargetType.None:
                case TargetType.Self:
                    return true;
                case TargetType.HexCell:
                    if (targetCell == null) return false;
                    if (card.CellConstraint == CellConstraint.Empty && targetCell.Occupant != null) return false;
                    return true;
                case TargetType.EnemyCharacter:
                    return targetCharacter != null
                        && targetCharacter.Stats.IsAlive
                        && targetCharacter.IsEnemyOf(source);
                case TargetType.AllyCharacter:
                    return targetCharacter != null
                        && targetCharacter.Stats.IsAlive
                        && targetCharacter.IsAllyOf(source);
                case TargetType.AnyCharacter:
                    return targetCharacter != null && targetCharacter.Stats.IsAlive;
                default:
                    return false;
            }
        }
    }
}
