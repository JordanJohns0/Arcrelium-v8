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
    ///   1. Turn start — draw up to the character's max hand size
    ///   2. Active character plays any number of cards, or ends turn
    ///   3. costs are paid by discarding other cards from hand
    ///   4. a card's effects resolve when its play is committed; a play submitted while an
    ///      earlier card is still resolving is queued and committed once the board is idle
    ///   5. ending the turn -> discard the hand, advance to the next character
    ///
    /// Each step has a configurable delay so actions are visible to the player.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        [SerializeField] private List<Character> turnOrder = new List<Character>();
        [SerializeField] private ActionResolver actionResolver;

        [Header("Action Timing")]
        [SerializeField] private float actionDelay = 0.33f;
        [SerializeField] private float turnEndDelay = 0.2f;

        private int currentIndex = 0;
        private Coroutine turnCoroutine;
        private readonly HashSet<Character> discardSubscribed = new HashSet<Character>();
        private readonly HashSet<CardInstance> discardPlaying = new HashSet<CardInstance>();

        // Plays submitted by a player while an earlier card is still resolving. They resolve
        // strictly in order (see PumpPendingPlays), one per idle frame, so effects and their
        // animations/popups happen in the order they were played rather than all at once.
        private readonly Queue<PendingPlay> pendingPlays = new Queue<PendingPlay>();
        private bool resolvingPlay;

        public Character ActiveCharacter => turnOrder.Count > 0 ? turnOrder[currentIndex] : null;
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Act;
        public bool Busy => turnCoroutine != null
            || pendingPlays.Count > 0
            || (ActiveCharacter != null && ActiveCharacter.IsAnimating);
        public bool HasQueuedPlays => pendingPlays.Count > 0;
        public int CurrentTurnIndex => currentIndex;

        private struct PendingPlay
        {
            public CardInstance Card;
            public Character Actor;
            public List<CardInstance> CostCards;
            public HexCell TargetCell;
            public Character TargetCharacter;
            public List<HexCell> MovePath;
            public bool IgnoreCost;
            public bool IgnoreHandMembership;
            public bool CardsRemoved;
        }

        public event System.Action<Character> OnTurnStarted;
        public event System.Action<Character> OnTurnEnded;
        public event System.Action<Character> OnPlayerPassed;
        public event System.Action OnActionResolved;
        public event System.Action OnPlayQueued;
        public event System.Action<CardInstance, Character> OnCardPlayed;

        public void SetTurnOrder(List<Character> characters)
        {
            turnOrder = characters;
            currentIndex = 0;

            foreach (Character character in turnOrder)
            {
                if (character == null || !discardSubscribed.Add(character)) continue;
                character.OnCardDiscarded += HandleCardDiscarded;
            }
        }

        /// <summary>
        /// Host-driven turn sync for clients: advances to the given index and notifies the UI.
        /// </summary>
        public void SetTurn(int index)
        {
            if (turnOrder.Count == 0) { currentIndex = 0; return; }
            currentIndex = ((index % turnOrder.Count) + turnOrder.Count) % turnOrder.Count;
            OnTurnStarted?.Invoke(ActiveCharacter);
        }

        public void StartMatch()
        {
            currentIndex = 0;
            BeginTurn();
        }

        private void BeginTurn()
        {
            CurrentPhase = TurnPhase.Act;
            pendingPlays.Clear();
            ActiveCharacter?.TickStatusesAtTurnStart();
            ActiveCharacter?.DrawToHandLimit();
            OnTurnStarted?.Invoke(ActiveCharacter);
        }

        public bool PlayCard(
            CardInstance card,
            List<CardInstance> costCards = null,
            HexCell targetCell = null,
            Character targetCharacter = null,
            List<HexCell> movePath = null,
            bool ignoreCost = false,
            bool ignoreHandMembership = false)
        {
            if (CurrentPhase != TurnPhase.Act) return false;

            var play = new PendingPlay
            {
                Card = card,
                Actor = ActiveCharacter,
                CostCards = costCards,
                TargetCell = targetCell,
                TargetCharacter = targetCharacter,
                MovePath = movePath,
                IgnoreCost = ignoreCost,
                IgnoreHandMembership = ignoreHandMembership
            };

            // While an earlier card is still resolving (animating, or its queue is draining) a
            // submitted play is queued and resolves once the board is idle again. The AI and
            // discard-triggers always wait for Busy to clear before submitting, so only direct
            // player input reaches this path with a play to queue.
            if (Busy)
            {
                if (!actionResolver.CanPlay(play.Actor, card, costCards, targetCell, targetCharacter,
                        movePath, ignoreCost, ignoreHandMembership))
                    return false;

                // Take the card (and its cost cards) out of the hand right away so the hand
                // updates the instant the player clicks, exactly like a normal play. Only the
                // effect is deferred until the queue reaches it.
                if (!ignoreHandMembership)
                {
                    RemoveCardsForQueuedPlay(play.Actor, card, costCards);
                    play.CardsRemoved = true;
                }

                pendingPlays.Enqueue(play);

                // The hand just changed without an effect resolving; let listeners (the host)
                // re-broadcast so clients drop the card from their hand right away too.
                if (play.CardsRemoved)
                    OnPlayQueued?.Invoke();

                return true;
            }

            return ResolvePlay(play);
        }

        /// <summary>
        /// Discards the played card and its cost cards for a queued play without resolving it.
        /// resolvingPlay is set so "when discarded, play it" triggers still fire (and queue up)
        /// exactly as they would during a normal play.
        /// </summary>
        private void RemoveCardsForQueuedPlay(Character actor, CardInstance card, List<CardInstance> costCards)
        {
            if (actor == null || card == null) return;

            resolvingPlay = true;
            try
            {
                if (costCards != null)
                {
                    foreach (CardInstance costCard in costCards)
                        actor.DiscardFromHand(costCard);
                }
                actor.DiscardFromHand(card);
            }
            finally
            {
                resolvingPlay = false;
            }
        }

        /// <summary>
        /// Resolves one queued play per frame once the board is idle: no turn transition and no
        /// animation still playing. Queued plays run strictly in order, each waiting for the
        /// previous card's animation before the next begins.
        /// </summary>
        internal void PumpPendingPlays()
        {
            if (CurrentPhase != TurnPhase.Act) return;
            if (turnCoroutine != null) return;
            if (pendingPlays.Count == 0) return;
            if (ActiveCharacter != null && ActiveCharacter.IsAnimating) return;

            PendingPlay play = pendingPlays.Dequeue();
            if (!ResolvePlay(play))
            {
                // The board or hand changed while this play waited, so it is no longer legal.
                Debug.Log($"[TurnManager] Queued play '{play.Card?.Data?.CardName}' could not resolve and was skipped.");
                OnActionResolved?.Invoke();
            }
        }

        /// <summary>
        /// Commits a single play: faces the target, runs the card through the resolver (which
        /// applies its effects), starts the attack swing if it is an offensive card, then fires
        /// the played/action-resolved events. Returns false (changing nothing) if the play is
        /// no longer legal.
        /// </summary>
        private bool ResolvePlay(PendingPlay play)
        {
            Character actor = play.Actor;
            if (actor == null) return false;

            if (play.TargetCharacter != null)
                actor.FaceTarget(play.TargetCharacter);
            else if (play.TargetCell != null)
                actor.FaceTarget(play.TargetCell);

            // A discard-triggered play ("when discarded, play it") may fire while this play's
            // cost cards are discarded; allow it through the Busy gate for the duration.
            resolvingPlay = true;
            bool played;
            try
            {
                played = actionResolver.PlayCard(actor, play.Card, play.CostCards, play.TargetCell,
                    play.TargetCharacter, play.MovePath, play.IgnoreCost, play.IgnoreHandMembership,
                    play.CardsRemoved);
            }
            finally
            {
                resolvingPlay = false;
            }

            if (!played) return false;

            // Only offensive cards swing: move/block/heal cards may have a target but shouldn't
            // play the attack animation.
            if (play.Card.Data != null && play.Card.Data.IsAttack)
                actor.PlayAttack();

            OnCardPlayed?.Invoke(play.Card, actor);
            OnActionResolved?.Invoke();
            return true;
        }

        private void Update()
        {
            PumpPendingPlays();
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
            ActiveCharacter?.DiscardHand();
            OnTurnEnded?.Invoke(ActiveCharacter);
            ActiveCharacter?.TickStatusesAtTurnEnd();
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

        /// <summary>
        /// "When this card is discarded, play it." A card leaving the hand into the discard
        /// pile is re-played for free while the owner's Act phase is live. Cards that need a
        /// board pick are left discarded (nothing can ask for a target mid-discard), and the
        /// card is tracked so its own resolve can't loop. Host-authoritative: clients only
        /// display what the host broadcasts.
        /// </summary>
        private void HandleCardDiscarded(Character character, CardInstance card)
        {
            if (character == null || card == null || card.Data == null) return;
            if (!card.Data.PlaysOnDiscard) return;
            if (GameState.ActiveMode == GameMode.Client) return;
            if (CurrentPhase != TurnPhase.Act) return;
            // A play being resolved right now is allowed to spawn its own discard-triggered play;
            // otherwise we wait until the board is idle so it isn't layered onto an animation.
            if (Busy && !resolvingPlay) return;
            if (card.Data.NeedsPick) return;
            if (!discardPlaying.Add(card)) return;

            PlayCard(card, ignoreCost: true, ignoreHandMembership: true);

            discardPlaying.Remove(card);
        }
    }
}
