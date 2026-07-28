using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Characters;
using Cards;
using HexGrid;

namespace Game
{
    public enum TurnPhase
    {
        Act,
        Draw,
        End
    }

    /// <summary>
    /// Drives whose turn it is and enforces the turn structure:
    ///   1. Turn start — draw 2 cards
    ///   2. Active character plays ONE card, or passes
    ///   3. (if playing) cost is paid by discarding other cards from hand
    ///   4. the card's effect resolves
    ///   5. end of turn -> advance to the next character
    ///
    /// Each step has a configurable delay so actions are visible to the player.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        [SerializeField] private List<Character> turnOrder = new List<Character>();
        [SerializeField] private ActionResolver actionResolver;
        [SerializeField] private int cardsDrawnPerTurn = 2;

        [Header("Action Timing")]
        [SerializeField] private float actionDelay = 0.5f;
        [SerializeField] private float turnEndDelay = 0.3f;

        private int currentIndex = 0;
        private Coroutine turnCoroutine;

        public Character ActiveCharacter => turnOrder.Count > 0 ? turnOrder[currentIndex] : null;
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Act;
        public bool Busy => turnCoroutine != null;

        public event System.Action<Character> OnTurnStarted;
        public event System.Action<Character> OnTurnEnded;
        public event System.Action<Character> OnPlayerPassed;
        public event System.Action OnActionResolved;

        public void SetTurnOrder(List<Character> characters)
        {
            turnOrder = characters;
            currentIndex = 0;
        }

        public void StartMatch()
        {
            currentIndex = 0;
            BeginTurn();
        }

        private void BeginTurn()
        {
            CurrentPhase = TurnPhase.Act;
            ActiveCharacter?.DrawCards(cardsDrawnPerTurn);
            OnTurnStarted?.Invoke(ActiveCharacter);
        }

        public bool PlayCard(
            CardInstance card,
            List<CardInstance> costCards = null,
            HexCell targetCell = null,
            Character targetCharacter = null)
        {
            if (CurrentPhase != TurnPhase.Act) return false;
            if (Busy) return false;

            bool played = actionResolver.PlayCard(ActiveCharacter, card, costCards, targetCell, targetCharacter);
            if (!played) return false;

            OnActionResolved?.Invoke();
            turnCoroutine = StartCoroutine(AdvanceToDrawThenEndCoroutine());
            return true;
        }

        public void Pass()
        {
            if (CurrentPhase != TurnPhase.Act) return;
            if (Busy) return;

            OnPlayerPassed?.Invoke(ActiveCharacter);
            turnCoroutine = StartCoroutine(AdvanceToDrawThenEndCoroutine());
        }

        private IEnumerator AdvanceToDrawThenEndCoroutine()
        {
            yield return new WaitForSeconds(actionDelay);

            CurrentPhase = TurnPhase.End;
            OnTurnEnded?.Invoke(ActiveCharacter);
            yield return new WaitForSeconds(turnEndDelay);

            AdvanceToNextCharacter();
            turnCoroutine = null;
            BeginTurn();
        }

        private void AdvanceToNextCharacter()
        {
            if (turnOrder.Count == 0) return;

            int attempts = 0;
            do
            {
                currentIndex = (currentIndex + 1) % turnOrder.Count;
                attempts++;
            }
            while (!turnOrder[currentIndex].Stats.IsAlive && attempts <= turnOrder.Count);
        }

        public void RemoveFromTurnOrder(Character character)
        {
            turnOrder.Remove(character);
            if (currentIndex >= turnOrder.Count) currentIndex = 0;
        }
    }
}
