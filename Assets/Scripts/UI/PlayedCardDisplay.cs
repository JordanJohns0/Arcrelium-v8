using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cards;
using Equipment;

namespace UI
{
    /// <summary>
    /// Shows a floating card visual whenever a card is played, so the play is visible on
    /// every machine. The host displays its own and AI plays directly and broadcasts a
    /// CardPlayedDisplay message so clients show them too.
    ///
    /// Plays are queued and shown strictly one at a time: each played card pops up, stays
    /// on screen while its action resolves, then fades out before the next play appears,
    /// so rapid plays (e.g. an enemy playing several cards) never overlap.
    /// </summary>
    public class PlayedCardDisplay : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject uiPrefab;
        [SerializeField] private GameObject cardPrefab;

        [Header("Canvas")]
        [SerializeField] private int sortingOrder = 60;

        [Header("Placement")]
        [SerializeField] private Vector2 basePosition = new Vector2(0, 170);
        [SerializeField] private Vector2 endPosition = new Vector2(0, 250);
        [SerializeField] private float baseScale = 0.8f;
        [SerializeField] private float startScale = 0.7f;

        [Header("Animation Timing")]
        [SerializeField] private float riseTime = 0.2f;
        [SerializeField] private float holdTime = 0.8f;
        [SerializeField] private float fadeTime = 0.35f;

        [Header("Caption")]
        [SerializeField] private int captionFontSize = 20;
        [SerializeField] private Color captionColor = Color.white;
        [SerializeField] private Vector2 captionOffset = new Vector2(0, 8);
        [SerializeField] private Vector2 captionSize = new Vector2(340, 26);

        [Header("Fallback Card")]
        [SerializeField] private Color fallbackBackground = new Color(0.12f, 0.12f, 0.14f, 0.95f);
        [SerializeField] private Vector2 fallbackSize = new Vector2(248, 346);
        [SerializeField] private int fallbackFontSize = 22;
        [SerializeField] private Color fallbackTextColor = new Color(1f, 0.85f, 0.3f, 1f);

        private static PlayedCardDisplay instance;
        private static GameObject staticPrefab;

        private Canvas canvas;
        private readonly Queue<DisplayRequest> queue = new Queue<DisplayRequest>();
        private bool playing;

        private struct DisplayRequest
        {
            public readonly string CardName;
            public readonly string ActorName;

            public DisplayRequest(string cardName, string actorName)
            {
                CardName = cardName;
                ActorName = actorName;
            }
        }

        /// <summary>
        /// Enqueues a played card to be shown. <paramref name="cardName"/> is resolved to
        /// CardData via the GameDataLibrary; if this machine lacks the card it falls back to
        /// a plain card showing just the name so every player still sees what was played.
        /// </summary>
        public static void Show(string cardName, string actorName)
        {
            if (instance == null)
            {
                var existing = FindAnyObjectByType<PlayedCardDisplay>();
                if (existing != null)
                {
                    instance = existing;
                }
                else if (staticPrefab != null)
                {
                    instance = Object.Instantiate(staticPrefab).GetComponent<PlayedCardDisplay>();
                }
                else
                {
                    var go = new GameObject("PlayedCardDisplay");
                    instance = go.AddComponent<PlayedCardDisplay>();
                }
            }
            instance.Display(cardName, actorName);
        }

        private void Awake()
        {
            if (staticPrefab == null && uiPrefab != null) staticPrefab = uiPrefab;
            var root = new GameObject("PlayedCardCanvas", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Display(string cardName, string actorName)
        {
            queue.Enqueue(new DisplayRequest(cardName, actorName));
            if (!playing)
                StartCoroutine(PlayQueue());
        }

        private IEnumerator PlayQueue()
        {
            playing = true;
            while (queue.Count > 0)
            {
                DisplayRequest request = queue.Dequeue();
                yield return ShowOne(request);
            }
            playing = false;
        }

        private IEnumerator ShowOne(DisplayRequest request)
        {
            CardData data = null;
            var library = GameDataLibrary.Instance;
            if (library != null) data = library.GetCardByName(request.CardName);

            string name = data != null && !string.IsNullOrEmpty(data.CardName) ? data.CardName : request.CardName;
            if (string.IsNullOrEmpty(name)) yield break;

            GameObject card;
            if (data != null)
            {
                card = cardPrefab != null ? Instantiate(cardPrefab, canvas.transform) : BuildFallbackCard(name);
                card.name = "PlayedCard";

                var view = card.GetComponent<CardView>();
                if (view != null)
                {
                    view.Setup(new CardInstance(data));
                    var button = card.GetComponent<Button>();
                    if (button != null) button.interactable = false;
                }
            }
            else
            {
                card = BuildFallbackCard(name);
            }

            if (!string.IsNullOrEmpty(request.ActorName))
                AttachCaption(card, $"{request.ActorName} played");

            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = basePosition;
            rect.localScale = Vector3.one * baseScale;

            CanvasGroup group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.AddComponent<CanvasGroup>();

            yield return Animate(rect, group);
            Destroy(card);
        }

        private IEnumerator Animate(RectTransform rect, CanvasGroup group)
        {
            Vector2 from = basePosition;
            Vector2 to = endPosition;

            group.alpha = 0f;
            rect.anchoredPosition = from;
            rect.localScale = Vector3.one * baseScale;

            float t = 0f;
            while (t < riseTime)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / riseTime);
                group.alpha = Mathf.Lerp(0f, 1f, p);
                rect.anchoredPosition = Vector2.Lerp(from, to, p);
                rect.localScale = Vector3.one * Mathf.Lerp(startScale, baseScale, p);
                yield return null;
            }

            group.alpha = 1f;
            rect.anchoredPosition = to;
            rect.localScale = Vector3.one * baseScale;

            yield return new WaitForSeconds(holdTime);

            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / fadeTime));
                yield return null;
            }
        }

        private void AttachCaption(GameObject card, string text)
        {
            var obj = new GameObject("ActorCaption", typeof(TextMeshProUGUI));
            obj.transform.SetParent(card.transform, false);
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = captionFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = captionColor;
            tmp.raycastTarget = false;
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = captionOffset;
            rt.sizeDelta = captionSize;
        }

        /// <summary>
        /// Simple name-only card used when the prefab can't be found or the card's CardData
        /// isn't on this machine — the play is still announced rather than silently dropped.
        /// </summary>
        private GameObject BuildFallbackCard(string name)
        {
            var card = new GameObject("PlayedCard", typeof(Image));
            card.transform.SetParent(canvas.transform, false);
            var image = card.GetComponent<Image>();
            image.color = fallbackBackground;
            image.raycastTarget = false;
            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = fallbackSize;

            var label = new GameObject("Name", typeof(TextMeshProUGUI));
            label.transform.SetParent(card.transform, false);
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = name;
            tmp.fontSize = fallbackFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = fallbackTextColor;
            tmp.raycastTarget = false;
            var labelRect = tmp.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return card;
        }
    }
}
