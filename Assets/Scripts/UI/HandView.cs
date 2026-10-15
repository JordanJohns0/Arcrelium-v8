using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
    ///
    /// Motion: newly drawn cards fly in from the draw source (or below the hand), and cards that
    /// leave the hand (played or discarded) fly out to the discard pile.
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

        [Header("Discard Animation")]
        [Tooltip("Optional. Cards that are played or discarded fly to this object (the discard pile). Leave empty to drop them below the hand.")]
        [SerializeField] private RectTransform discardTarget;
        [Tooltip("Seconds a played or discarded card takes to fly out.")]
        [SerializeField] private float discardDuration = 0.3f;
        [Tooltip("Scale a card shrinks to by the time it reaches the discard pile.")]
        [SerializeField] private float discardScale = 0.6f;

        private Character boundCharacter;
        private Character viewedCharacter;
        private readonly List<CardView> spawnedViews = new List<CardView>();
        private readonly List<CardView> flyingCards = new List<CardView>();

        // Real time each card's draw animation starts. Kept per card (not per view) so a rebuilt
        // hand carries on animating instead of restarting or dropping the animation.
        private readonly Dictionary<CardInstance, float> drawStartTimes = new Dictionary<CardInstance, float>();
        private int drawAnimationsStarted;
        private float baseSpacing;
        private bool baseSpacingCaptured;

        private bool handSubscribed;
        private float currentOffset;
        private float slideFrom;
        private float slideTarget;
        private float slideElapsed = Mathf.Infinity;

        public event System.Action<CardInstance> OnCardClicked;

        /// <summary>The CardView prefab spawned for hand cards. Always wired in-scene, so the
        /// played-card popup can fall back to it before any deck UI injects its own preview prefab.</summary>
        internal CardView ViewPrefab => cardViewPrefab;

        /// <summary>Number of draw animations started for newly drawn cards. Public for tests.</summary>
        internal int DrawAnimationsStarted => drawAnimationsStarted;

        private void OnEnable()
        {
            Character.OnCharacterCreated += HandleCharacterCreated;
            SubscribeToHand();
        }

        private void OnDisable()
        {
            Character.OnCharacterCreated -= HandleCharacterCreated;
            UnsubscribeFromHand();
            // A hidden hand stops its animations, so any card still flying out must not be left
            // behind on screen.
            ClearFlyingCards();
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
            // Cards of the same hand that are no longer in it were played or discarded: they fly
            // out to the discard pile. Views of a different character's hand just go.
            bool sameOwner = viewedCharacter == boundCharacter;
            viewedCharacter = boundCharacter;

            foreach (CardView view in spawnedViews)
            {
                if (view == null) continue;
                bool leaving = sameOwner && view.Instance != null && !IsInHand(view.Instance);
                if (leaving && isActiveAndEnabled) SendToDiscard(view);
                else DestroyView(view.gameObject);
            }
            spawnedViews.Clear();

            // Forget draw times of cards that left, so a copy drawn again later animates again.
            foreach (CardInstance card in new List<CardInstance>(drawStartTimes.Keys))
            {
                if (!IsInHand(card)) drawStartTimes.Remove(card);
            }

            if (boundCharacter == null) return;

            ApplyHandSpacing(boundCharacter.Hand.Cards.Count);

            float now = Time.realtimeSinceStartup;
            int drawIndex = 0;
            foreach (CardInstance card in boundCharacter.Hand.Cards)
            {
                CardView view = Instantiate(cardViewPrefab, cardContainer);
                view.Setup(card);
                view.OnClicked += HandleCardViewClicked;
                spawnedViews.Add(view);

                // A card new to the hand is drawn now, staggered within this draw. A card already
                // mid-draw keeps its original start time, so a rebuild never restarts it.
                bool newlyDrawn = !drawStartTimes.ContainsKey(card);
                if (newlyDrawn)
                    drawStartTimes[card] = now + drawIndex++ * drawStagger;

                float start = drawStartTimes[card];
                if (isActiveAndEnabled && now < start + drawDuration)
                {
                    var group = view.GetComponent<CanvasGroup>();
                    if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0f; // hidden right away, before the fade starts
                    view.SetEntering(true); // hover waits until the card has landed

                    if (newlyDrawn) drawAnimationsStarted++;
                    StartCoroutine(DrawIn(view, start));
                }
            }
        }

        /// <summary>
        /// Keeps the hand within the panel's width. While the cards fit, the layout's normal
        /// spacing is used. Once they would overflow, the spacing becomes negative so the cards
        /// overlap and the hand fills exactly the panel's width instead of spilling out.
        /// </summary>
        private void ApplyHandSpacing(int count)
        {
            var group = cardContainer != null ? cardContainer.GetComponent<HorizontalLayoutGroup>() : null;
            if (group == null) return;

            if (!baseSpacingCaptured)
            {
                baseSpacing = group.spacing;
                baseSpacingCaptured = true;
            }

            var containerRect = cardContainer as RectTransform;
            var cardRect = cardViewPrefab != null ? cardViewPrefab.GetComponent<RectTransform>() : null;
            if (containerRect == null || cardRect == null || count < 2) { group.spacing = baseSpacing; return; }

            float cardWidth = cardRect.sizeDelta.x;
            float available = containerRect.rect.width - group.padding.horizontal;
            if (available <= 0f || cardWidth <= 0f) { group.spacing = baseSpacing; return; }

            float natural = count * cardWidth + (count - 1) * baseSpacing;
            group.spacing = natural <= available
                ? baseSpacing
                : (available - count * cardWidth) / (count - 1);
        }

        private bool IsInHand(CardInstance card)
        {
            return boundCharacter != null && boundCharacter.Hand.Contains(card);
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

        /// <summary>
        /// Flies one newly drawn card from its start point into its laid-out place while fading
        /// it in. <paramref name="startTime"/> is the real time the draw began (it may be a few
        /// frames in the past when the hand was rebuilt mid-draw). The card's hover is held for
        /// the whole flight, so nothing else moves it until it has landed.
        /// </summary>
        private IEnumerator DrawIn(CardView view, float startTime)
        {
            // Wait a frame so the layout group has placed the card before reading its rest spot.
            yield return null;
            if (view == null) yield break;

            var rt = view.GetComponent<RectTransform>();
            var group = view.GetComponent<CanvasGroup>();
            if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();

            Vector2 target = rt.anchoredPosition;
            Vector2 offset = DrawOffset(rt);

            while (true)
            {
                if (view == null) yield break;

                float elapsed = Time.realtimeSinceStartup - startTime;
                if (elapsed >= drawDuration) break;

                float t = SmoothStep01(Mathf.Clamp01(elapsed / drawDuration));
                rt.anchoredPosition = Vector2.LerpUnclamped(target + offset, target, t);
                group.alpha = t;
                yield return null;
            }

            if (view == null) yield break;
            rt.anchoredPosition = target;
            group.alpha = 1f;
            view.SetEntering(false); // landed: hover may now act on the card
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

        /// <summary>
        /// Takes a card that just left the hand out of the layout and flies it to the discard
        /// pile, shrinking and fading as it goes. The card is parented to the root so the hand's
        /// layout closes the gap at once while the card is still on screen.
        /// </summary>
        private void SendToDiscard(CardView view)
        {
            var rt = view.GetComponent<RectTransform>();
            var group = view.GetComponent<CanvasGroup>();
            if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Vector3 start = rt.position;
            rt.SetParent(transform.root, true);
            rt.SetAsLastSibling();

            flyingCards.Add(view);
            StartCoroutine(FlyToDiscard(view, rt, group, start));
        }

        private IEnumerator FlyToDiscard(CardView view, RectTransform rt, CanvasGroup group, Vector3 start)
        {
            Vector3 end = discardTarget != null
                ? discardTarget.position
                : start + new Vector3(0f, -drawFromDistance, 0f);
            Vector3 startScale = rt.localScale;
            Vector3 endScale = startScale * discardScale;

            float startTime = Time.realtimeSinceStartup;
            while (view != null)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                if (elapsed >= discardDuration) break;

                float t = SmoothStep01(Mathf.Clamp01(elapsed / discardDuration));
                rt.position = Vector3.LerpUnclamped(start, end, t);
                rt.localScale = Vector3.Lerp(startScale, endScale, t);
                group.alpha = 1f - t;
                yield return null;
            }

            flyingCards.Remove(view);
            if (view != null) DestroyView(view.gameObject);
        }

        private static float SmoothStep01(float x)
        {
            return x * x * (3f - 2f * x);
        }

        private void OnDestroy()
        {
            UnsubscribeFromHand();
            ClearFlyingCards();
        }

        private void ClearFlyingCards()
        {
            foreach (CardView view in flyingCards)
            {
                if (view != null) DestroyView(view.gameObject);
            }
            flyingCards.Clear();
        }

        private static void DestroyView(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
