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
    /// Plays made by other players (and AI) are queued and shown strictly one at a
    /// time, each visible for at least MinVisibleSeconds before the next appears — so a
    /// card played right after another still gets read instead of flashing by. The
    /// local player's own plays are never displayed (they just played them). This is
    /// purely visual: playing cards is never blocked or slowed by the queue;
    /// resolution stays instant while the display catches up in the background.
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
        private Coroutine playRoutine;

        /// <summary>Each played card stays on screen at least this long, in seconds.</summary>
        internal const float MinVisibleSeconds = 1f;

        private struct DisplayRequest
        {
            public readonly CardData Data;
            public readonly string CardName;
            public readonly string ActorName;

            public DisplayRequest(CardData data, string cardName, string actorName)
            {
                Data = data;
                CardName = cardName;
                ActorName = actorName;
            }
        }

        /// <summary>
        /// Enqueues a played card to be shown, rendering it with the injected Card prefab.
        /// The CardData is used directly — no name lookup — so every card (present and
        /// future) shows as the real card. Foreign plays (anyone who is not this
        /// machine's player) are queued and shown sequentially, each visible for at
        /// least MinVisibleSeconds.
        /// </summary>
        public static void Show(CardData card, string actorName)
        {
            ShowInternal(card, actorName);
        }

        /// <summary>
        /// Name-only variant used when only the announced name is available (network
        /// broadcasts from other machines). Resolves via GameDataLibrary; unknown names
        /// fall back to a plain name-only card.
        /// </summary>
        public static void Show(string cardName, string actorName)
        {
            ShowInternal(null, actorName, cardName);
        }

        private static void ShowInternal(CardData data, string actorName, string cardName = null)
        {
            EnsureInstance();
            instance.Display(new DisplayRequest(data, cardName, actorName));
        }

        /// <summary>
        /// Injects the Card prefab used to render plays visually (same prefab as the
        /// deck-window hover previews). Without it only the plain name fallback shows.
        /// </summary>
        public static void SetCardPrefab(CardView prefab)
        {
            EnsureInstance();
            instance.cardPrefab = prefab != null ? prefab.gameObject : null;
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;

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

        private void Display(DisplayRequest request)
        {
            // Foreign plays are paced so observers can register each one; actual
            // resolution is never blocked by this — it is purely visual. The local
            // player's own plays are not displayed at all (they just watched themselves
            // play it), which GameManager enforces by not routing them here.
            queue.Enqueue(request);
            if (playRoutine == null)
                playRoutine = StartCoroutine(PlayQueue());
        }

        private IEnumerator PlayQueue()
        {
            while (queue.Count > 0)
            {
                yield return PlayOne(queue.Dequeue());
            }
            playRoutine = null;
        }

        private IEnumerator PlayOne(DisplayRequest request)
        {
            CardData data = request.Data;
            if (data == null && !string.IsNullOrEmpty(request.CardName))
            {
                var library = GameDataLibrary.Instance;
                if (library != null) data = library.GetCardByName(request.CardName);
            }

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

            // The popup is purely informational: it must never eat mouse clicks aimed
            // at the board or hand while it is on screen.
            DisableRaycasts(card);

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

        private static void DisableRaycasts(GameObject root)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        private IEnumerator Animate(RectTransform rect, CanvasGroup group)
        {
            Vector2 from = basePosition;
            Vector2 to = endPosition;

            // Guarantee the minimum visible time even if the serialized timings are
            // tuned shorter: stretch the hold to make up the difference.
            float hold = Mathf.Max(holdTime, MinVisibleSeconds - riseTime - fadeTime);

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

            yield return new WaitForSeconds(hold);

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
