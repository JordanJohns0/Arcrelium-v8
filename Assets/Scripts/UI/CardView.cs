using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Cards;

namespace UI
{
    /// <summary>
    /// Visual representation of a single card in hand. Purely a display + click-reporter —
    /// it doesn't know about play/cost/target logic, that's HandView/TurnFlowUIController's job.
    /// Attach to a card prefab with an Image (artwork), TextMeshProUGUI fields for
    /// name/description/cost, plus a selection-highlight Image (e.g. a colored outline/border)
    /// that's toggled on/off.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image artworkImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text costText;
        [Tooltip("Enabled while this card is selected (as the card being played, or as a cost payment).")]
        [SerializeField] private GameObject selectionHighlight;

        [Header("Hover")]
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float hoverLift = -40f;
        [SerializeField] private float hoverDuration = 0.12f;

        private Button button;
        private Coroutine scaleRoutine;
        private bool hovered;
        private Vector3 basePosition;

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
            basePosition = transform.localPosition;
        }

        public void SetSelected(bool selected)
        {
            if (selectionHighlight != null) selectionHighlight.SetActive(selected);
        }

        /// <summary>Dim/disable the card when it can't legally be picked right now (e.g. it IS the card being played, so it can't also pay its own cost).</summary>
        public void SetInteractable(bool interactable)
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.interactable = interactable;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null && !button.interactable) return;
            hovered = true;
            AnimateHover(hoverScale, hoverLift);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            AnimateHover(1f, 0f);
        }

        private void AnimateHover(float targetScale, float targetLift)
        {
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(HoverCoroutine(targetScale, targetLift));
        }

        private IEnumerator HoverCoroutine(float targetScale, float targetLift)
        {
            Vector3 startScale = transform.localScale;
            Vector3 endScale = new Vector3(targetScale, targetScale, 1f);
            float startY = transform.localPosition.y;
            float endY = basePosition.y - targetLift;
            float elapsed = 0f;

            while (elapsed < hoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / hoverDuration);
                transform.localScale = Vector3.Lerp(startScale, endScale, t);
                Vector3 pos = transform.localPosition;
                pos.y = Mathf.Lerp(startY, endY, t);
                transform.localPosition = pos;
                yield return null;
            }

            transform.localScale = endScale;
            Vector3 final = transform.localPosition;
            final.y = endY;
            transform.localPosition = final;
            scaleRoutine = null;
        }
    }
}
