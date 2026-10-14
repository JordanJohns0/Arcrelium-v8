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

        /// <summary>The card a hand play is discarding right now (see HandleCardDiscarded).</summary>
        private CardInstance cardBeingPlayedFromHand;

        /// <summary>Set by <see cref="EndMatch"/>: the match is decided and the turn flow is stopped.</summary>
        private bool matchOver;

        // Plays submitted by a player while an earlier card is still resolving. They resolve
        // strictly in order (see PumpPendingPlays), one per idle frame, so effects and their
        // animations/popups happen in the order they were played rather than all at once.
        private readonly Queue<PendingPlay> pendingPlays = new Queue<PendingPlay>();
        private bool resolvingPlay;

        // Ending the turn is queued exactly like a play: submitting End Turn while an earlier
        // card is still resolving takes effect once the board is idle (see PumpPendingPlays).
        private bool passQueued;

        public Character ActiveCharacter => turnOrder.Count > 0 ? turnOrder[currentIndex] : null;
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Act;
        public bool Busy => turnCoroutine != null
            || pendingPlays.Count > 0
            || passQueued
            || (ActiveCharacter != null && ActiveCharacter.IsAnimating);
        public bool HasQueuedPlays => pendingPlays.Count > 0;
        public bool HasQueuedPass => passQueued;
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
            if (matchOver) return;
            CurrentPhase = TurnPhase.Act;
            pendingPlays.Clear();
            passQueued = false;
            ActiveCharacter?.TickStatusesAtTurnStart();
            ActiveCharacter?.DrawCardsForTurn();
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
            if (CurrentPhase != TurnPhase.Act || matchOver) return false;

            // Once the player has queued End Turn the turn is already committed to ending,
            // so no further plays are accepted.
            if (passQueued) return false;

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
            CardInstance previousPlayed = cardBeingPlayedFromHand;
            cardBeingPlayedFromHand = card;
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
                cardBeingPlayedFromHand = previousPlayed;
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
            if (ActiveCharacter != null && ActiveCharacter.IsAnimating) return;

            if (pendingPlays.Count > 0)
            {
                PendingPlay play = pendingPlays.Dequeue();
                if (!ResolvePlay(play))
                {
                    // The board or hand changed while this play waited, so it is no longer legal.
                    Debug.Log($"[TurnManager] Queued play '{play.Card?.Data?.CardName}' could not resolve and was skipped.");
                    OnActionResolved?.Invoke();
                }
                return;
            }

            // Nothing left to resolve: a queued End Turn now takes effect.
            if (passQueued)
                BeginPass();
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
            CardInstance previousPlayed = cardBeingPlayedFromHand;
            cardBeingPlayedFromHand = play.Card;
            bool played;
            try
            {
                played = actionResolver.PlayCard(actor, play.Card, play.CostCards, play.TargetCell,
                    play.TargetCharacter, play.MovePath, play.IgnoreCost, play.IgnoreHandMembership,
                    play.CardsRemoved);
            }
            finally
            {
                cardBeingPlayedFromHand = previousPlayed;
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

        /// <summary>
        /// Stops the turn flow for the rest of the match (called on victory): no more plays,
        /// passes, or turn starts, queued plays are dropped, and discarding does not replay cards.
        /// </summary>
        public void EndMatch()
        {
            matchOver = true;
            pendingPlays.Clear();
            passQueued = false;
        }

        public void Pass()
        {
            if (matchOver) return;
            if (CurrentPhase != TurnPhase.Act) return;
            if (turnCoroutine != null) return;

            // Ending the turn while a card is still resolving (or queued) is accepted and
            // deferred, exactly like a card played mid-resolution: the turn ends once the
            // board is idle. Otherwise it begins right away.
            if (HasQueuedPlays || passQueued || (ActiveCharacter != null && ActiveCharacter.IsAnimating))
            {
                passQueued = true;
                return;
            }

            BeginPass();
        }

        private void BeginPass()
        {
            passQueued = false;
            OnPlayerPassed?.Invoke(ActiveCharacter);
            turnCoroutine = StartCoroutine(AdvanceToDrawThenEndCoroutine());
        }

        private IEnumerator AdvanceToDrawThenEndCoroutine()
        {
            yield return new WaitForSeconds(GamePace.Scale(actionDelay));

            CurrentPhase = TurnPhase.End;
            ActiveCharacter?.DiscardHand();
            OnTurnEnded?.Invoke(ActiveCharacter);
            ActiveCharacter?.TickStatusesAtTurnEnd();
            yield return new WaitForSeconds(GamePace.Scale(turnEndDelay));

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
            if (matchOver) return;
            if (!card.Data.PlaysOnDiscard) return;
            // The card being played from hand is discarded only as part of its own play (the
            // cost of playing it). It resolves once, here, so it must not be replayed on discard.
            if (card == cardBeingPlayedFromHand) return;
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
