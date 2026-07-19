using System.Collections.Generic;
using UnityEngine;
using FantasyBrawl.Characters;
using FantasyBrawl.Cards;
using FantasyBrawl.HexGrid;

namespace FantasyBrawl.Game
{
    public enum TurnPhase
    {
        /// <summary>Waiting for the active character to either PlayCard or Pass. Exactly one of these per turn.</summary>
        Act,
        Draw,
        End
    }

    /// <summary>
    /// Drives whose turn it is and enforces the turn structure:
    ///   1. Turn start
    ///   2. Active character plays ONE card, or passes
    ///   3. (if playing) cost is paid by discarding other cards from hand
    ///   4. the card's effect resolves
    ///   5. draw a card
    ///   6. end of turn -> advance to the next character
    ///
    /// This class owns the sequencing; ActionResolver only knows how to resolve a single
    /// card play. Call PlayCard or Pass here (not on ActionResolver directly) so the
    /// draw/end steps always happen automatically afterward.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        [SerializeField] private List<Character> turnOrder = new List<Character>();
        [SerializeField] private ActionResolver actionResolver;
        [SerializeField] private int cardsDrawnPerTurn = 1;

        private int currentIndex = 0;

        public Character ActiveCharacter => turnOrder.Count > 0 ? turnOrder[currentIndex] : null;
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Act;

        public event System.Action<Character> OnTurnStarted;
        public event System.Action<Character> OnTurnEnded;
        public event System.Action<Character> OnPlayerPassed;

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
            OnTurnStarted?.Invoke(ActiveCharacter);
        }

        /// <summary>
        /// Step 2/3/4: play a card (paying its discard cost) and resolve its effect.
        /// On success this automatically continues through draw and end-of-turn.
        /// Returns false and leaves the turn in the Act phase if the play was invalid
        /// (wrong phase, card not playable, bad cost/target) — nothing is consumed.
        /// </summary>
        public bool PlayCard(
            CardInstance card,
            List<CardInstance> costCards = null,
            HexCell targetCell = null,
            Character targetCharacter = null)
        {
            if (CurrentPhase != TurnPhase.Act) return false;

            bool played = actionResolver.PlayCard(ActiveCharacter, card, costCards, targetCell, targetCharacter);
            if (!played) return false;

            AdvanceToDrawThenEnd();
            return true;
        }

        /// <summary>Step 2 alternative: skip playing a card entirely and go straight to draw.</summary>
        public void Pass()
        {
            if (CurrentPhase != TurnPhase.Act) return;

            OnPlayerPassed?.Invoke(ActiveCharacter);
            AdvanceToDrawThenEnd();
        }

        private void AdvanceToDrawThenEnd()
        {
            // Step 5: draw a card.
            CurrentPhase = TurnPhase.Draw;
            ActiveCharacter?.DrawCards(cardsDrawnPerTurn);

            // Step 6: end of turn.
            CurrentPhase = TurnPhase.End;
            OnTurnEnded?.Invoke(ActiveCharacter);

            AdvanceToNextCharacter();
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
