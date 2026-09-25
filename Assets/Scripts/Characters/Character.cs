using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HexGrid;
using Cards;
using Equipment;

namespace Characters
{
    public enum Team { Ally, Enemy }

    /// <summary>
    /// A character on the board. Holds its stats, current cell, and its card deck/hand.
    /// Characters never act on their own — every action comes from a card being played
    /// through this character (see ActionResolver).
    /// </summary>
    public class Character : MonoBehaviour
    {
        [SerializeField] private CharacterStats stats = new CharacterStats();
        [SerializeField] private Deck deck = new Deck();
        [SerializeField] private EquipmentManager equipmentManager;
        [SerializeField] private int maxHandSize = 5;
        [SerializeField] private Animator modelAnimator;

        public CharacterStats Stats => stats;
        public Deck Deck => deck;
        public int MaxHandSize => maxHandSize;
        public EquipmentManager Equipment => equipmentManager;
        public Hand Hand { get; private set; }
        public HexCell CurrentCell { get; private set; }

        /// <summary>The cell the character was on before the current one; used to infer travel
        /// direction (e.g. which way to slide when entering an oil tile).</summary>
        public HexCell LastCell { get; private set; }

        public int OwnerPlayerId { get; private set; }
        public int TeamId { get; private set; }
        public Team Team => TeamId == 0 ? Team.Ally : Team.Enemy;
        public Color? PlayerColor { get; private set; }
        public Color? NameColor { get; private set; }

        /// <summary>
        /// Two characters are allies iff their TeamIds match. In PvE all human players
        /// share team 0 and AI enemies team 1; in PvP every player gets a distinct TeamId,
        /// so each player is only their own ally.
        /// </summary>
        public bool IsAllyOf(Character other)
        {
            return other != null && other.TeamId == TeamId;
        }

        public bool IsEnemyOf(Character other)
        {
            return other != null && other.TeamId != TeamId;
        }

        public event Action<Character> OnDefeated;
        public event Action<HexCell> OnMovedToCell;
        public event Action<int> OnDamageTaken;
        public event Action<int> OnBlockGained;
        public event Action<Character, CardInstance> OnCardDiscarded;

        public static event Action<Character> OnCharacterCreated;
        public static event Action<Character> OnCharacterDestroyed;

        private Coroutine moveAnimation;
        private Coroutine attackAnimationTracker;
        private bool isAttacking; // true while the attack state is playing on the model
        // Walking travel pace (world units/sec). Deliberately slow so the walk stance reads clearly
        // while the transform lerp carries the character cell-to-cell. Chosen so a ~1.3s walk gait
        // cycle lands on roughly one board space, keeping the legs in step with the travel.
        private const float WalkPace = 2f;
        private const float ArrivalThreshold = 0.05f;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<(Renderer renderer, Color originalColor, Color originalBaseColor)> originalColors = new List<(Renderer, Color, Color)>();
        private static readonly int colorID = Shader.PropertyToID("_Color");
        private static readonly int baseColorID = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (modelAnimator == null)
                modelAnimator = GetComponentInChildren<Animator>();

            if (modelAnimator != null)
            {
                // Movement is transform-driven (see StepTowards), so any root motion on the model
                // must stay off. With applyRootMotion on, the Animator (a child Armature) applies
                // root deltas to its own local transform, making the model lunge past the target
                // and snap back when the state returns to Idle.
                modelAnimator.applyRootMotion = false;
            }
        }

        public void Initialize(int ownerPlayerId, HexCell startingCell, int startingHandSize = 1, int teamId = -1, EquipmentLoadout loadout = null)
        {
            OwnerPlayerId = ownerPlayerId;
            TeamId = teamId >= 0 ? teamId : (ownerPlayerId == 0 ? 0 : 1);
            stats.Initialize();
            Hand = new Hand();
            Hand.MaxCapacity = maxHandSize;
            deck ??= new Deck();

            PlaceOnCell(startingCell);

            if (equipmentManager != null)
            {
                equipmentManager.Initialize(this);
                if (loadout != null)
                    equipmentManager.ApplyLoadout(loadout);
            }

            if (deck != null)
            {
                List<CardData> equipmentCards = equipmentManager != null
                    ? equipmentManager.GetAllCards()
                    : new List<CardData>();

                deck.SetStartingCards(equipmentCards);
                deck.Shuffle();
                DrawCards(startingHandSize);
                Debug.Log($"[Character] owner={ownerPlayerId} deckBuilt cards={equipmentCards.Count} hand={Hand.Cards.Count}");
            }
            else
            {
                Debug.LogWarning($"[Character] owner={ownerPlayerId} deck is NULL - no cards built");
            }

            OnCharacterCreated?.Invoke(this);
        }

        public void PlaceOnCell(HexCell cell)
        {
            if (CurrentCell != null)
            {
                CurrentCell.ClearOccupant();
            }

            CurrentCell = cell;
            cell.SetOccupant(this);
            transform.position = cell.transform.position;
        }

        /// <summary>
        /// Updates cell occupancy and logical position without moving the transform.
        /// Used by animated movement effects.
        /// </summary>
        public void SetCellOnly(HexCell cell)
        {
            if (CurrentCell != null)
            {
                CurrentCell.ClearOccupant();
            }

            LastCell = CurrentCell;
            CurrentCell = cell;
            cell.SetOccupant(this);
            OnMovedToCell?.Invoke(cell);
        }

        /// <summary>
        /// Stops any cosmetic movement animation currently running on this character.
        /// </summary>
        public void CancelMoveAnimation()
        {
            if (moveAnimation != null)
            {
                StopCoroutine(moveAnimation);
                moveAnimation = null;
            }
        }

        /// <summary>
        /// Cosmetic move from the character's current transform position to the target cell.
        /// The logical position must already be authoritative (SetCellOnly) before calling.
        /// Cancels any prior move animation.
        /// </summary>
        public void AnimateToCell(HexCell target)
        {
            AnimateToCell(transform.position, target);
        }

        /// <summary>
        /// Cosmetic move from an explicit start position to the target cell.
        /// The logical position must already be authoritative (SetCellOnly) before calling.
        /// Cancels any prior move animation.
        /// </summary>
        public void AnimateToCell(Vector3 start, HexCell target)
        {
            if (target == null) return;
            CancelMoveAnimation();
            transform.position = start;
            moveAnimation = StartCoroutine(AnimateToCellCoroutine(target));
        }

        /// <summary>
        /// Cosmetic move along a path of cells from the character's current transform position.
        /// The logical position must already be authoritative (SetCellOnly) before calling.
        /// Cancels any prior move animation.
        /// </summary>
        public void AnimateAlongPath(List<HexCell> path)
        {
            if (path == null || path.Count == 0) return;
            CancelMoveAnimation();
            moveAnimation = StartCoroutine(AnimateAlongPathCoroutine(path));
        }

        private IEnumerator AnimateToCellCoroutine(HexCell target)
        {
            if (this == null || target == null) yield break;
            Vector3 destination = target.transform.position;

            BeginWalking();
            while (this != null && Vector3.Distance(transform.position, destination) > ArrivalThreshold)
            {
                StepTowards(destination);
                yield return null;
            }

            EndWalking();
            if (this != null)
                transform.position = destination;
            moveAnimation = null;
        }

        private IEnumerator AnimateAlongPathCoroutine(List<HexCell> path)
        {
            if (this == null || path == null) yield break;

            BeginWalking();
            foreach (HexCell cell in path)
            {
                if (this == null || cell == null) break;
                Vector3 destination = cell.transform.position;

                while (this != null && Vector3.Distance(transform.position, destination) > ArrivalThreshold)
                {
                    StepTowards(destination);
                    yield return null;
                }

                if (this != null)
                    transform.position = destination;
            }

            EndWalking();
            if (this != null)
                moveAnimation = null;
        }

        /// <summary>
        /// Advances the character toward the destination. The transform lerp is always the movement
        /// driver so arrival is guaranteed regardless of the model Animator. Walking uses WalkPace
        /// (slow enough to actually read on screen) so the gait keeps in step with the travel.
        /// </summary>
        private void StepTowards(Vector3 destination)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, WalkPace * Time.deltaTime);
        }

        /// <summary>
        /// Turns the walk stance on.
        /// </summary>
        private void BeginWalking()
        {
            if (modelAnimator != null)
                modelAnimator.SetBool("IsWalking", true);
        }

        /// <summary>
        /// Turns the walk stance off.
        /// </summary>
        private void EndWalking()
        {
            if (modelAnimator != null)
                modelAnimator.SetBool("IsWalking", false);
        }

        /// <summary>
        /// Plays the model's attack animation if an Animator is configured. Gameplay-cosmetic only;
        /// damage/effects are resolved through the card system regardless of animation availability.
        /// </summary>
        public void PlayAttack()
        {
            if (modelAnimator == null) return;
            modelAnimator.SetTrigger("Attack");
            isAttacking = true;
            if (attackAnimationTracker != null)
                StopCoroutine(attackAnimationTracker);
            attackAnimationTracker = StartCoroutine(TrackAttackAnimationEnd());
        }

        /// <summary>
        /// Tracks when the model leaves the Attack state and clears the flag, so animation-driven
        /// pacing (TurnManager Busy / AI inter-card waits) uses the real clip length rather than a
        /// hardcoded delay. Timeouts guard against an unbound model halting the turn.
        /// </summary>
        private IEnumerator TrackAttackAnimationEnd()
        {
            float entered = Time.time;
            // The trigger transitions the model into Attack over a frame or two; wait for it.
            while (modelAnimator != null && Time.time - entered < 2f
                && !modelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
                yield return null;

            // Now wait until the swing finishes and the model leaves Attack.
            float deadline = Time.time + 5f;
            while (modelAnimator != null && Time.time < deadline
                && modelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
                yield return null;

            isAttacking = false;
            attackAnimationTracker = null;
        }

        /// <summary>
        /// True while the character's cosmetic animation (walk move-in or attack swing) is still
        /// playing. TurnManager and the AI wait on this before allowing further card plays, so a
        /// card is never submitted on top of an unfinished animation.
        /// </summary>
        public bool IsAnimating => moveAnimation != null || isAttacking;

        /// <summary>
        /// Rotates the character to face the given target (a character or a board cell),
        /// yaw-only so the model stays upright. Cosmetic; does not affect logic.
        /// </summary>
        public void FaceTarget(Character target)
        {
            if (target != null) FaceTarget(target.transform.position);
        }

        public void FaceTarget(HexCell target)
        {
            if (target != null) FaceTarget(target.transform.position);
        }

        public void FaceTarget(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        /// <summary>
        /// Draws cards and returns the instances actually drawn (up to the hand limit).
        /// Each drawn card's OnDrawnEffect runs before the card enters the hand, so draw
        /// triggers can set up the copy (e.g. randomize its cost) before it is displayed.
        /// </summary>
        public List<CardInstance> DrawCards(int count)
        {
            var drawn = new List<CardInstance>();
            if (deck == null) return drawn;
            for (int i = 0; i < count; i++)
            {
                if (Hand.IsFull) break;
                CardInstance card = deck.Draw();
                if (card != null)
                {
                    TriggerOnDrawn(card);
                    Hand.AddCard(card);
                    drawn.Add(card);
                }
            }
            return drawn;
        }

        private void TriggerOnDrawn(CardInstance card)
        {
            if (card == null || card.Data == null || card.Data.OnDrawnEffect == null) return;
            card.Data.OnDrawnEffect.Execute(new CardContext { Source = this, Card = card });
        }

        /// <summary>Draws cards until the hand reaches the character's max hand size.</summary>
        public void DrawToHandLimit()
        {
            DrawCards(maxHandSize);
        }

        /// <summary>Moves every card in the hand to the deck's discard pile.</summary>
        public void DiscardHand()
        {
            if (Hand == null || deck == null) return;
            foreach (CardInstance card in new List<CardInstance>(Hand.Cards))
            {
                DiscardFromHand(card);
            }
        }

        /// <summary>
        /// Removes a card from the hand into the discard pile and fires OnCardDiscarded so
        /// discard-triggers (e.g. "when this card is discarded, play it") can react.
        /// </summary>
        public void DiscardFromHand(CardInstance card)
        {
            if (Hand == null || deck == null) return;
            if (!Hand.RemoveCard(card)) return;
            deck.Discard(card);
            OnCardDiscarded?.Invoke(this, card);
        }

        public void TakeDamage(int amount)
        {
            stats.ApplyDamage(amount);
            OnDamageTaken?.Invoke(amount);
            if (!stats.IsAlive)
            {
                Die();
            }
        }

        /// <summary>
        /// Grants block and fires OnBlockGained so equipment triggers (e.g. Restorative
        /// Shield) can react. Used instead of Stats.GainBlock directly so the event flows
        /// through the same pattern as TakeDamage/OnDamageTaken.
        /// </summary>
        public void GainBlock(int amount)
        {
            stats.GainBlock(amount);
            OnBlockGained?.Invoke(amount);
        }

        private void Die()
        {
            CurrentCell?.ClearOccupant();
            OnDefeated?.Invoke(this);
            if (Application.isPlaying)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            OnCharacterDestroyed?.Invoke(this);
        }

        public void SetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            stats.CharacterName = name;
            stats.NotifyChanged();
        }

        /// <summary>
        /// Replaces the hand with freshly created cards matching the given names,
        /// preserving the Hand instance so bound UI refreshes in place. The character's own
        /// deck is tried first, then the master GameDataLibrary so cards from another
        /// player's loadout (that this machine never equipped) still resolve.
        /// </summary>
        public void RebuildHand(string[] cardNames)
        {
            foreach (CardInstance card in new List<CardInstance>(Hand.Cards))
                Hand.RemoveCard(card);

            if (cardNames == null) return;
            foreach (string name in cardNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                CardData data = deck != null ? deck.GetCardDataByName(name) : null;
                if (data == null)
                {
                    var library = GameDataLibrary.Instance;
                    if (library != null) data = library.GetCardByName(name);
                }
                if (data != null)
                    Hand.AddCard(new CardInstance(data));
            }
        }

        public void SetPlayerColor(Color color)
        {
            PlayerColor = color;
            if (renderers.Count == 0) renderers.AddRange(GetComponentsInChildren<Renderer>());
            ApplyColor(color);
            stats.NotifyChanged();
        }

        /// <summary>
        /// Sets the color used by the HUD (e.g. name label) without tinting the model's renderers.
        /// </summary>
        public void SetNameColor(Color color)
        {
            NameColor = color;
            stats.NotifyChanged();
        }

        private void ApplyColor(Color color)
        {
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor(colorID, color);
                block.SetColor(baseColorID, color);
                r.SetPropertyBlock(block);
            }
        }

        public void SetHighlighted(Color highlightColor)
        {
            ClearHighlight();
            if (renderers.Count == 0) renderers.AddRange(GetComponentsInChildren<Renderer>());

            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Color originalColor = r.material.GetColor(colorID);
                Color originalBaseColor = r.material.GetColor(baseColorID);
                originalColors.Add((r, originalColor, originalBaseColor));

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor(colorID, highlightColor);
                block.SetColor(baseColorID, highlightColor);
                r.SetPropertyBlock(block);
            }
        }

        public void ClearHighlight()
        {
            foreach (var entry in originalColors)
            {
                if (entry.renderer == null) continue;
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                entry.renderer.GetPropertyBlock(block);
                block.SetColor(colorID, entry.originalColor);
                block.SetColor(baseColorID, entry.originalBaseColor);
                entry.renderer.SetPropertyBlock(block);
            }
            originalColors.Clear();
        }
    }
}
