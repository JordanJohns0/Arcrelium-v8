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

        private Character boundCharacter;
        private readonly List<CardView> spawnedViews = new List<CardView>();
        private bool handSubscribed;

        public event System.Action<CardInstance> OnCardClicked;

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
                if (view != null) Destroy(view.gameObject);
            }
            spawnedViews.Clear();

            if (boundCharacter == null) return;

            foreach (CardInstance card in boundCharacter.Hand.Cards)
            {
                CardView view = Instantiate(cardViewPrefab, cardContainer);
                view.Setup(card);
                view.OnClicked += HandleCardViewClicked;
                spawnedViews.Add(view);
            }
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

        private void OnDestroy()
        {
            UnsubscribeFromHand();
        }
    }
}
