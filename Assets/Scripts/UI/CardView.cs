using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FantasyBrawl.Cards;

namespace FantasyBrawl.UI
{
    /// <summary>
    /// Visual representation of a single card in hand. Purely a display + click-reporter —
    /// it doesn't know about play/cost/target logic, that's HandView/TurnFlowUIController's job.
    /// Attach to a card prefab with an Image (artwork), TextMeshProUGUI fields for
    /// name/description/cost, plus a selection-highlight Image (e.g. a colored outline/border)
    /// that's toggled on/off.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CardView : MonoBehaviour
    {
        [SerializeField] private Image artworkImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text costText;
        [Tooltip("Enabled while this card is selected (as the card being played, or as a cost payment).")]
        [SerializeField] private GameObject selectionHighlight;

        private Button button;

        public CardInstance Instance { get; private set; }
        public CardData Card => Instance?.Data;

        public event System.Action<CardView> OnClicked;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(() => OnClicked?.Invoke(this));
        }

        public void Setup(CardInstance instance)
        {
            Instance = instance;
            CardData data = instance.Data;

            if (artworkImage != null) artworkImage.sprite = data.Artwork;
            if (nameText != null) nameText.text = data.CardName;
            if (descriptionText != null) descriptionText.text = data.Description;
            // Cost of 0 still displays "0" — free cards are common, this isn't hidden.
            if (costText != null) costText.text = data.Cost.ToString();

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectionHighlight != null) selectionHighlight.SetActive(selected);
        }

        /// <summary>Dim/disable the card when it can't legally be picked right now (e.g. it IS the card being played, so it can't also pay its own cost).</summary>
        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
        }
    }
}
