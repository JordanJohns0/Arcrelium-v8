using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cards;
using Equipment;
using Game;

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
    /// local player's own plays are never displayed (they just played them). The queue
    /// itself never blocks card resolution (effects still commit in turn order), but
    /// the AI waits on <see cref="IsBusy"/> so an enemy never plays its next card while
    /// a popup is still on screen — keeping each enemy action and its visual in step.
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
        private GameObject resolvedCardPrefab;

        /// <summary>Each played card stays on screen at least this long, in seconds.</summary>
        internal const float MinVisibleSeconds = 1f;

        /// <summary>
        /// True while a played-card popup is on screen or waiting to be shown. The AI waits on
        /// this before its next play so an enemy never acts while its previous card (or anyone
        /// else's) is still being displayed, keeping the visuals in step with the action.
        /// No instance (headless/tests) means nothing is showing.
        /// </summary>
        public static bool IsBusy => instance != null && (instance.playRoutine != null || instance.queue.Count > 0);

        private struct DisplayRequest
        {
            public readonly CardData Data;
            public readonly string CardName;
            public readonly string ActorName;
            public readonly int Cost;
            public readonly string Description;

            public DisplayRequest(CardData data, string cardName, string actorName,
                int cost = 0, string description = null)
            {
                Data = data;
                CardName = cardName;
                ActorName = actorName;
                Cost = cost;
                Description = description;
            }
        }

        /// <summary>
        /// Enqueues a played card to be shown, rendering it with the injected Card prefab.
        /// The CardData is used directly — no name lookup — so every card (present and
        /// future) shows as the real card. Foreign plays (anyone who is not this
        /// machine's player) are queued and shown sequentially, each visible for at
        /// least MinVisibleSeconds.
        /// </summary>
        public static void Show(CardData card, string actorName, int cost = -1)
        {
            ShowInternal(card, actorName, cost: cost);
        }

        /// <summary>
        /// Name-only variant used when only the announced name is available (network
        /// broadcasts from other machines). Resolves via GameDataLibrary; if that fails,
        /// the cost/description carried by the broadcast render an accurate stand-in so
        /// the popup can never show a wrong or blank card.
        /// </summary>
        public static void Show(string cardName, string actorName, int cost = 0, string description = null)
        {
            ShowInternal(null, actorName, cardName, cost, description);
        }

        private static void ShowInternal(CardData data, string actorName, string cardName = null,
            int cost = -1, string description = null)
        {
            EnsureInstance();
            instance.Display(new DisplayRequest(data, cardName, actorName, cost, description));
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

        /// <summary>
        /// The prefab used to render played cards. Prefers the explicitly injected prefab
        /// (EquipmentUI hands it the deck-window preview) and otherwise borrows the hand's card
        /// prefab, which is always wired in the scene. Without this the popup fell back to a
        /// name-only card whenever no deck UI had opened yet — which is why enemy plays showed
        /// a name instead of the real card "sometimes".
        /// </summary>
        private GameObject ResolveCardPrefab()
        {
            if (cardPrefab != null) return cardPrefab;
            if (resolvedCardPrefab != null) return resolvedCardPrefab;

            HandView hand = FindAnyObjectByType<HandView>(FindObjectsInactive.Include);
            if (hand != null && hand.ViewPrefab != null)
                resolvedCardPrefab = hand.ViewPrefab.gameObject;
            return resolvedCardPrefab;
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
            bool synthetic = false;
            if (data == null && !string.IsNullOrEmpty(request.CardName))
            {
                var library = GameDataLibrary.Instance;
                if (library != null) data = library.GetCardByName(request.CardName);
            }
            if (data == null && !string.IsNullOrEmpty(request.Description))
            {
                // The lookup failed but the host told us exactly what was played: build a
                // transient stand-in carrying the real name/cost/description so the popup
                // is accurate even when this machine can't resolve the asset.
                data = ScriptableObject.CreateInstance<CardData>();
                data.CardName = request.CardName;
                data.Cost = request.Cost;
                data.Description = request.Description;
                synthetic = true;
            }

            string name = data != null && !string.IsNullOrEmpty(data.CardName) ? data.CardName : request.CardName;
            if (string.IsNullOrEmpty(name)) yield break;

            GameObject card;
            if (data != null)
            {
                GameObject prefab = ResolveCardPrefab();
                card = prefab != null ? Instantiate(prefab, canvas.transform) : BuildFallbackCard(name);
                card.name = "PlayedCard";

                var view = card.GetComponent<CardView>();
                if (view != null)
                {
                    var instance = new CardInstance(data);
                    if (request.Cost >= 0)
                        instance.Cost = request.Cost;
                    view.Setup(instance);
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
            if (synthetic) Destroy(data);
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

            // Popups shrink with GamePace so a swarm's worth of enemy plays doesn't hold
            // the turn up. At speed 1 these are exactly the serialized timings.
            float rise = GamePace.Scale(riseTime);
            float fade = GamePace.Scale(fadeTime);
            // Guarantee the minimum visible time even if the serialized timings are
            // tuned shorter: stretch the hold to make up the difference.
            float hold = Mathf.Max(GamePace.Scale(holdTime), GamePace.Scale(MinVisibleSeconds) - rise - fade);

            group.alpha = 0f;
            rect.anchoredPosition = from;
            rect.localScale = Vector3.one * baseScale;

            float t = 0f;
            while (t < rise)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / rise);
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
            while (t < fade)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / fade));
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
