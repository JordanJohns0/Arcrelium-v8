using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// The stage vote overlay: two options side by side, each an enemy group (first half) and
    /// the reward for beating it (second half), plus a line of who has voted. Built at runtime.
    /// Once the local player votes their choice is locked. When the vote resolves the chosen
    /// option grows and the other fades out, then <see cref="PlayResolution"/> reports back.
    /// </summary>
    public class StageVoteScreen : MonoBehaviour
    {
        /// <summary>What one option shows.</summary>
        public struct OptionLabels
        {
            public string EnemyTitle;
            public string EnemyDetail;
            public string RewardTitle;
            public string RewardDetail;
        }

        [SerializeField] private float resolveGrowDuration = 0.5f;
        [SerializeField] private float resolveGrowScale = 1.25f;
        [SerializeField] private float resolveHoldSeconds = 1.2f;

        /// <summary>Raised when the local player clicks an option (0 or 1).</summary>
        public event Action<int> OptionChosen;

        private const int OptionCount = 2;
        private static readonly Vector2[] OptionPositions = { new Vector2(-250f, -10f), new Vector2(250f, -10f) };

        private readonly RectTransform[] optionRects = new RectTransform[OptionCount];
        private readonly CanvasGroup[] optionGroups = new CanvasGroup[OptionCount];
        private readonly Button[] optionButtons = new Button[OptionCount];
        private readonly TMP_Text[] optionTexts = new TMP_Text[OptionCount];
        private TMP_Text statusText;
        private bool localVoted;
        private Coroutine resolution;

        /// <summary>Creates the overlay under a new root object. The caller destroys it with <see cref="Close"/>.</summary>
        public static StageVoteScreen Create()
        {
            var root = new GameObject("Stage Vote", typeof(StageVoteScreen));
            var screen = root.GetComponent<StageVoteScreen>();
            screen.Build();
            return screen;
        }

        /// <summary>Fills in the two options. Call once after <see cref="Create"/>.</summary>
        public void SetOptions(OptionLabels[] labels)
        {
            for (int i = 0; i < OptionCount; i++)
            {
                if (labels == null || i >= labels.Length) continue;
                OptionLabels label = labels[i];
                optionTexts[i].text =
                    $"<size=36><b>{label.EnemyTitle}</b></size>\n<size=22>{label.EnemyDetail}</size>\n\n" +
                    $"<color=#FFD94D>Reward</color>\n<size=36><b>{label.RewardTitle}</b></size>\n<size=22>{label.RewardDetail}</size>";
            }
        }

        public void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        /// <summary>Enables the options unless the local player has already voted.</summary>
        public void SetCanVote(bool canVote)
        {
            for (int i = 0; i < OptionCount; i++)
                optionButtons[i].interactable = canVote && !localVoted;
        }

        /// <summary>Locks the local player's choice. Their vote cannot be changed after this.</summary>
        public void LockLocalVote(int option)
        {
            localVoted = true;
            SetCanVote(false);
            if (option >= 0 && option < OptionCount)
                optionTexts[option].text += "\n\n<color=#FFD94D><b>Your vote</b></color>";
        }

        /// <summary>
        /// Shows the result: the chosen option grows while the other fades out, then holds briefly
        /// and calls <paramref name="onFinished"/>.
        /// </summary>
        public void PlayResolution(int chosen, Action onFinished)
        {
            if (resolution != null) StopCoroutine(resolution);
            localVoted = true;
            SetCanVote(false);
            resolution = StartCoroutine(ResolveRoutine(chosen, onFinished));
        }

        /// <summary>Destroys the overlay.</summary>
        public void Close()
        {
            if (resolution != null) StopCoroutine(resolution);
            Destroy(gameObject);
        }

        private IEnumerator ResolveRoutine(int chosen, Action onFinished)
        {
            float elapsed = 0f;
            while (elapsed < resolveGrowDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / resolveGrowDuration));
                for (int i = 0; i < OptionCount; i++)
                {
                    if (i == chosen)
                        optionRects[i].localScale = Vector3.one * Mathf.Lerp(1f, resolveGrowScale, t);
                    else
                        optionGroups[i].alpha = 1f - t;
                }
                yield return null;
            }

            if (chosen >= 0 && chosen < OptionCount)
            {
                optionRects[chosen].localScale = Vector3.one * resolveGrowScale;
                for (int i = 0; i < OptionCount; i++)
                {
                    if (i != chosen) optionGroups[i].alpha = 0f;
                }
            }

            yield return new WaitForSecondsRealtime(resolveHoldSeconds);
            resolution = null;
            onFinished?.Invoke();
        }

        private void OnChosen(int option)
        {
            if (localVoted) return;
            localVoted = true;
            SetCanVote(false);
            OptionChosen?.Invoke(option);
        }

        private void Build()
        {
            var canvasGo = new GameObject("StageVoteCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Opaque backdrop so nothing behind (e.g. the loadout screen) shows through.
            var backdrop = NewImage("Backdrop", canvasGo.transform, new Color(0.04f, 0.04f, 0.06f, 0.98f));
            Stretch(backdrop.rectTransform);

            var title = MakeText("Title", canvasGo.transform, 44, FontStyles.Bold, "Choose the next stage");
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -40f);
            title.rectTransform.sizeDelta = new Vector2(1000f, 60f);

            for (int i = 0; i < OptionCount; i++)
            {
                int option = i;
                var panel = NewImage($"Option {i + 1}", canvasGo.transform, new Color(0.14f, 0.14f, 0.18f, 1f));
                var rect = panel.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(440f, 520f);
                rect.anchoredPosition = OptionPositions[i];

                optionRects[i] = rect;
                optionGroups[i] = panel.gameObject.AddComponent<CanvasGroup>();
                optionButtons[i] = panel.gameObject.AddComponent<Button>();
                optionButtons[i].targetGraphic = panel;
                optionButtons[i].onClick.AddListener(() => OnChosen(option));

                optionTexts[i] = MakeText("Text", panel.transform, 26, FontStyles.Normal, "");
                optionTexts[i].alignment = TextAlignmentOptions.Top;
                optionTexts[i].textWrappingMode = TextWrappingModes.Normal;
                Stretch(optionTexts[i].rectTransform, 24f);
            }

            statusText = MakeText("Status", canvasGo.transform, 28, FontStyles.Normal, "");
            statusText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
            statusText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            statusText.rectTransform.sizeDelta = new Vector2(1400f, 50f);
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text MakeText(string name, Transform parent, int fontSize, FontStyles style, string text)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.text = text;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        private static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }
    }
}
