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

        public event System.Action<CardInstance> OnCardClicked;

        private void OnEnable()
        {
            Character.OnCharacterCreated += HandleCharacterCreated;
        }

        private void OnDisable()
        {
            Character.OnCharacterCreated -= HandleCharacterCreated;
        }

        private void HandleCharacterCreated(Character character)
        {
            if (character.OwnerPlayerId == GameState.LocalPlayerId)
                SetCharacter(character);
        }

        /// <summary>Switch which character's hand this view is showing (e.g. on turn change).</summary>
        public void SetCharacter(Character character)
        {
            if (boundCharacter != null)
            {
                boundCharacter.Hand.OnHandChanged -= Refresh;
            }

            boundCharacter = character;

            if (boundCharacter != null)
            {
                boundCharacter.Hand.OnHandChanged += Refresh;
            }

            Refresh();
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

        /// <summary>Highlights exactly the given cards (used to show the pending play + selected cost cards) and dims everything else.</summary>
        public void SetHighlighted(IReadOnlyCollection<CardInstance> highlighted, CardInstance nonInteractable = null)
        {
            foreach (CardView view in spawnedViews)
            {
                bool isHighlighted = highlighted != null && Contains(highlighted, view.Instance);
                view.SetSelected(isHighlighted);
                view.SetInteractable(view.Instance != nonInteractable);
            }
        }

        public void ClearHighlights()
        {
            foreach (CardView view in spawnedViews)
            {
                view.SetSelected(false);
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
            if (boundCharacter != null)
            {
                boundCharacter.Hand.OnHandChanged -= Refresh;
            }
        }
    }
}
