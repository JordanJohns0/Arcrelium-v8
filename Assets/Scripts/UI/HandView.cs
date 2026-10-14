using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Characters;
using Cards;
using Game;

namespace UI
{
    /// <summary>
    /// Displays the hand of whichever character is currently bound to it (normally the local
    /// player's character, so it stays visible while waiting for other players). Rebuilds its
    /// CardView children whenever the underlying Hand changes (card drawn, played, discarded)
    /// or a new character is bound in.
    /// Put this on a panel with a HorizontalLayoutGroup (or similar) as cardContainer.
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [SerializeField] private Transform cardContainer;
        [SerializeField] private CardView cardViewPrefab;

        [Header("Hand Peek/Stow")]
        [Tooltip("How far down (pixels) the hand tucks below its normal spot while not hovered. A small sliver of the cards stays on screen so they can be hovered.")]
        [SerializeField] private float stowedOffset = 170f;
        [Tooltip("Seconds for the full slide between the normal and stowed spot.")]
        [SerializeField] private float slideDuration = 0.3f;

        [Header("Draw Animation")]
        [Tooltip("Optional. Newly drawn cards fly in from this object (e.g. the deck pile). Leave empty to rise from below the hand.")]
        [SerializeField] private RectTransform drawSource;
        [Tooltip("How far below its spot (pixels) a card starts when there is no draw source.")]
        [SerializeField] private float drawFromDistance = 260f;
        [Tooltip("Seconds each drawn card takes to fly into place.")]
        [SerializeField] private float drawDuration = 0.35f;
        [Tooltip("Delay between consecutive cards in the same draw, so a multi-card draw fans out.")]
        [SerializeField] private float drawStagger = 0.08f;

        private Character boundCharacter;
        private readonly List<CardView> spawnedViews = new List<CardView>();
        private readonly HashSet<CardInstance> displayedCards = new HashSet<CardInstance>();
        private int drawAnimationsStarted;
        private bool handSubscribed;
        private float currentOffset;
        private float slideFrom;
        private float slideTarget;
        private float slideElapsed = Mathf.Infinity;

        public event System.Action<CardInstance> OnCardClicked;

        /// <summary>The CardView prefab spawned for hand cards. Always wired in-scene, so the
        /// played-card popup can fall back to it before any deck UI injects its own preview prefab.</summary>
        internal CardView ViewPrefab => cardViewPrefab;

        private void OnEnable()
        {
            Character.OnCharacterCreated += HandleCharacterCreated;
            SubscribeToHand();
        }

        private void OnDisable()
        {
            Character.OnCharacterCreated -= HandleCharacterCreated;
            UnsubscribeFromHand();
        }

        private void HandleCharacterCreated(Character character)
        {
            // Board fixtures (rocks) are never players — they must not steal the hand view.
            if (character == null || character.IsStructure) return;
            if (character.OwnerPlayerId == GameState.LocalPlayerId)
                SetCharacter(character);
        }

        /// <summary>Switch which character's hand this view is showing (e.g. on turn change).</summary>
        public void SetCharacter(Character character)
        {
            UnsubscribeFromHand();
            boundCharacter = character;
            SubscribeToHand();
            Refresh();
        }

        private void SubscribeToHand()
        {
            if (boundCharacter == null || handSubscribed) return;
            boundCharacter.Hand.OnHandChanged += Refresh;
            handSubscribed = true;
        }

        private void UnsubscribeFromHand()
        {
            if (boundCharacter == null || !handSubscribed) return;
            boundCharacter.Hand.OnHandChanged -= Refresh;
            handSubscribed = false;
        }

        public void Refresh()
        {
            foreach (CardView view in spawnedViews)
            {
                if (view != null) DestroyView(view.gameObject);
            }
            spawnedViews.Clear();

            // Cards that were not in the previous hand were just drawn: animate them in.
            var previous = new HashSet<CardInstance>(displayedCards);
            displayedCards.Clear();
            if (boundCharacter == null) return;

            int drawIndex = 0;
            foreach (CardInstance card in boundCharacter.Hand.Cards)
            {
                bool drawn = !previous.Contains(card);
                CardView view = Instantiate(cardViewPrefab, cardContainer);
                view.Setup(card);
                view.OnClicked += HandleCardViewClicked;
                spawnedViews.Add(view);
                displayedCards.Add(card);

                if (drawn && isActiveAndEnabled)
                {
                    // Hide it now so it never renders fully opaque before the fade starts.
                    var group = view.GetComponent<CanvasGroup>();
                    if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0f;

                    drawAnimationsStarted++;
                    StartCoroutine(DrawIn(view, drawIndex++));
                }
            }
        }

        /// <summary>Number of draw animations started (one per newly drawn card). Public for tests.</summary>
        internal int DrawAnimationsStarted => drawAnimationsStarted;

        /// <summary>
        /// Flies one newly drawn card from its start point into its laid-out place while
        /// fading it in. The layout group places the card first, so the target is read after
        /// the next frame. Stops early if the card is hovered (hover then owns its position)
        /// or the card is rebuilt away.
        /// </summary>
        private IEnumerator DrawIn(CardView view, int index)
        {
            // Wait a frame so the layout group has placed the card before reading its rest spot.
            yield return null;
            if (view == null) yield break;

            var rt = view.GetComponent<RectTransform>();
            var group = view.GetComponent<CanvasGroup>();
            if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();

            Vector2 target = rt.anchoredPosition;
            Vector2 offset = DrawOffset(rt);
            group.alpha = 0f;

            if (index > 0 && drawStagger > 0f)
                yield return new WaitForSecondsRealtime(index * drawStagger);

            // Real time, not accumulated frame deltas, so the fade always completes on schedule.
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < drawDuration)
            {
                if (view == null) yield break;
                if (view.Hovered)
                {
                    group.alpha = 1f;
                    yield break;
                }

                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / drawDuration);
                t = t * t * (3f - 2f * t);
                rt.anchoredPosition = Vector2.LerpUnclamped(target + offset, target, t);
                group.alpha = t;
                yield return null;
            }

            if (view == null) yield break;
            rt.anchoredPosition = target;
            group.alpha = 1f;
        }

        /// <summary>Where a drawn card starts, relative to its resting place in the hand.</summary>
        private Vector2 DrawOffset(RectTransform card)
        {
            Transform parent = card.parent;
            if (drawSource != null && parent != null)
            {
                Vector2 from = parent.InverseTransformPoint(drawSource.position);
                Vector2 rest = parent.InverseTransformPoint(card.position);
                return from - rest;
            }
            return new Vector2(0f, -drawFromDistance);
        }

        private void HandleCardViewClicked(CardView view)
        {
            OnCardClicked?.Invoke(view.Instance);
        }

        /// <summary>
        /// Highlights the pending card (normal highlight), marks discard-cost cards with a red X,
        /// and dims/disables everything else as appropriate.
        /// </summary>
        public void SetHighlighted(IReadOnlyCollection<CardInstance> highlighted, CardInstance nonInteractable = null,
            IReadOnlyCollection<CardInstance> discardMarked = null)
        {
            foreach (CardView view in spawnedViews)
            {
                if (view == null) continue;
                bool isHighlighted = highlighted != null && Contains(highlighted, view.Instance);
                bool isMarked = discardMarked != null && Contains(discardMarked, view.Instance);
                view.SetSelected(isHighlighted);
                view.SetDiscardMarked(isMarked);
                view.SetInteractable(view.Instance != nonInteractable);
            }
        }

        public void ClearHighlights()
        {
            foreach (CardView view in spawnedViews)
            {
                if (view == null) continue;
                view.SetSelected(false);
                view.SetDiscardMarked(false);
                view.SetInteractable(true);
            }
        }

        private static bool Contains(IReadOnlyCollection<CardInstance> collection, CardInstance card)
        {
            foreach (CardInstance c in collection)
            {
                if (c == card) return true;
            }
            return false;
        }

        private void Update()
        {
            TickHandSlide(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Slides the whole hand between its stowed (mostly offscreen) spot and its normal
        /// spot with an ease-in/out curve. Any hovered card reveals the hand; as soon as no
        /// card is hovered it slides back down. Retargeting mid-slide continues smoothly
        /// from the current position. Note this drives cardContainer's Y every frame —
        /// vertical placement of the container belongs to this component. Public for tests.
        /// </summary>
        public void TickHandSlide(float deltaTime)
        {
            if (cardContainer == null) return;
            RectTransform rt = cardContainer as RectTransform;
            if (rt == null) return;

            // Undo our previous displacement to recover the container's resting spot.
            Vector2 basePos = rt.anchoredPosition;
            basePos.y += currentOffset;

            float targetOffset = AnyCardHovered() ? 0f : stowedOffset;
            if (!Mathf.Approximately(targetOffset, slideTarget))
            {
                slideFrom = currentOffset;
                slideTarget = targetOffset;
                slideElapsed = 0f;
            }

            if (slideDuration > 0f && slideElapsed < slideDuration)
            {
                slideElapsed = Mathf.Min(slideDuration, slideElapsed + deltaTime);
                float t = slideElapsed / slideDuration;
                // SmoothStep: zero speed at both ends, fastest mid-travel.
                t = t * t * (3f - 2f * t);
                currentOffset = Mathf.Lerp(slideFrom, slideTarget, t);
            }
            else
            {
                currentOffset = slideTarget;
            }

            basePos.y -= currentOffset;
            rt.anchoredPosition = basePos;
        }

        private bool AnyCardHovered()
        {
            foreach (CardView view in spawnedViews)
            {
                if (view != null && view.Hovered) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            UnsubscribeFromHand();
        }

        private static void DestroyView(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
