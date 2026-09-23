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
        [Tooltip("Shows the target range of the card's primary targeting step. Hidden for untargeted cards (Range 0).")]
        [SerializeField] private GameObject rangeBadge;
        [SerializeField] private TMP_Text rangeText;
        [Tooltip("Enabled while this card is selected (as the card being played, or as a cost payment).")]
        [SerializeField] private GameObject selectionHighlight;
        [Tooltip("Shown while this card is selected for discard (cost payment). Optional — a red X is generated if left empty.")]
        [SerializeField] private GameObject discardMark;

        [Header("Discard Mark (auto-generated fallback)")]
        [SerializeField] private Color discardMarkColor = new Color(0.85f, 0.1f, 0.1f, 0.95f);
        [SerializeField, Range(1f, 20f)] private float discardMarkThickness = 8f;

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

        /// <summary>True while the pointer is over this card (and it is interactable).</summary>
        public bool Hovered => hovered;

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

            // Range badge: show only for non-movement cards that actually need a target pick.
            // Movement cards don't show a range value — distance is implied by the move step.
            bool showRange = !data.IsMove && data.Range > 0;
            if (rangeBadge != null) rangeBadge.SetActive(showRange);
            if (rangeText != null) rangeText.text = showRange ? data.Range.ToString() : string.Empty;

            SetSelected(false);
            SetDiscardMarked(false);
            basePosition = transform.localPosition;
        }

        public void SetSelected(bool selected)
        {
            if (discardMarkActive && selectionHighlight != null) selected = false;
            if (selectionHighlight != null) selectionHighlight.SetActive(selected);
        }

        private bool discardMarkActive;

        /// <summary>Shows/hides the red X overlay marking this card as selected for discard.</summary>
        public void SetDiscardMarked(bool marked)
        {
            discardMarkActive = marked;

            if (marked)
            {
                EnsureDiscardMark();
                if (discardMark != null) discardMark.SetActive(true);
                // Discard-marked cards use the X, not the normal highlight.
                if (selectionHighlight != null) selectionHighlight.SetActive(false);
            }
            else if (discardMark != null)
            {
                discardMark.SetActive(false);
            }
        }

        private void EnsureDiscardMark()
        {
            if (discardMark != null) return;

            var root = new GameObject("DiscardMark", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            foreach (float angle in new[] { 45f, -45f })
            {
                var barGo = new GameObject("XBar", typeof(RectTransform), typeof(Image));
                barGo.transform.SetParent(root.transform, false);
                RectTransform barRt = (RectTransform)barGo.transform;
                // Stretch horizontally across the card, fixed thickness, rotated to form an X.
                barRt.anchorMin = new Vector2(0f, 0.5f);
                barRt.anchorMax = new Vector2(1f, 0.5f);
                barRt.pivot = new Vector2(0.5f, 0.5f);
                barRt.sizeDelta = new Vector2(0f, discardMarkThickness);
                barRt.localEulerAngles = new Vector3(0f, 0f, angle);

                Image barImage = barGo.GetComponent<Image>();
                barImage.color = discardMarkColor;
                barImage.raycastTarget = false;
            }

            discardMark = root;
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
