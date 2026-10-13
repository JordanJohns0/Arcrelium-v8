using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Characters;
using Game;

namespace UI
{
    /// <summary>
    /// Draws a compact health bar floating directly above whichever enemy the pointer is
    /// hovering. Hover is driven by <see cref="CharacterSelectable"/>'s global events, so the
    /// bar follows the same raycast the outline highlight uses.
    ///
    /// Only enemies (characters off the local player's team, excluding neutral structures)
    /// get the floating bar — allies already have persistent rows in the <see cref="StatsHUD"/>.
    /// The bar tracks damage/block live via <see cref="CharacterStats.OnStatsChanged"/>.
    /// </summary>
    public class CharacterHoverHealthBar : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("World-space height above the character's origin the bar is anchored to.")]
        [SerializeField] private float headOffset = 2.2f;

        [Header("Size")]
        [SerializeField] private Vector2 barSize = new Vector2(120f, 20f);

        [Header("Colors")]
        [SerializeField] private Color fillColor = new Color(0.82f, 0.18f, 0.18f, 0.95f);
        [SerializeField] private Color emptyColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        [SerializeField] private Color blockColor = new Color(0.73f, 0.73f, 0.73f, 0.95f);
        [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.85f);

        [Header("Label")]
        [SerializeField] private bool showHealthText = true;
        [SerializeField] private Color textColor = Color.white;

        [Header("Canvas")]
        [SerializeField] private int sortingOrder = 5;

        [Header("Camera")]
        [Tooltip("Camera used to project the bar. Defaults to Camera.main when left empty.")]
        [SerializeField] private Camera worldCamera;

        private Canvas canvas;
        private RectTransform barRoot;
        private RectTransform fillRect;
        private RectTransform blockRect;
        private TMP_Text healthLabel;

        private Character hovered;
        private bool built;

        /// <summary>The character the bar is currently tracking, or null when hidden.</summary>
        public Character Hovered => hovered;

        /// <summary>True while the bar is on screen.</summary>
        public bool IsVisible => barRoot != null && barRoot.gameObject.activeSelf;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            CharacterSelectable.AnyCharacterHovered += HandleCharacterHovered;
            CharacterSelectable.AnyCharacterHoverEnded += HandleCharacterHoverEnded;
        }

        private void OnDisable()
        {
            CharacterSelectable.AnyCharacterHovered -= HandleCharacterHovered;
            CharacterSelectable.AnyCharacterHoverEnded -= HandleCharacterHoverEnded;
            Hide();
        }

        private void LateUpdate()
        {
            if (!IsVisible || hovered == null) return;
            Position();
        }

        // ------------------------------------------------------------ public API

        /// <summary>Shows the bar above the given character when it is a valid enemy.</summary>
        public void ShowFor(Character character)
        {
            EnsureBuilt();

            if (character == null || character.IsStructure || character.Stats == null
                || !IsEnemyOfLocalPlayer(character))
            {
                Hide();
                return;
            }

            if (hovered == character)
            {
                Refresh();
                return;
            }

            Detach();
            hovered = character;
            hovered.OnDefeated += HandleHoveredDefeated;
            hovered.Stats.OnStatsChanged += HandleStatsChanged;

            Refresh();
            SetVisible(true);
            Position();
        }

        /// <summary>Hides the bar and stops tracking the current character.</summary>
        public void Hide()
        {
            Detach();
            SetVisible(false);
        }

        // ------------------------------------------------------------ events

        private void HandleCharacterHovered(Character character) => ShowFor(character);

        private void HandleCharacterHoverEnded() => Hide();

        private void HandleHoveredDefeated(Character character) => Hide();

        private void HandleStatsChanged(CharacterStats stats) => Refresh();

        private void Detach()
        {
            if (hovered == null) return;

            hovered.OnDefeated -= HandleHoveredDefeated;
            if (hovered.Stats != null)
                hovered.Stats.OnStatsChanged -= HandleStatsChanged;
            hovered = null;
        }

        // ------------------------------------------------------------ state

        private void Refresh()
        {
            if (hovered == null || hovered.Stats == null) return;

            CharacterStats stats = hovered.Stats;
            int max = Mathf.Max(1, stats.MaxHealth);

            if (fillRect != null)
                SetBarFraction(fillRect, (float)stats.CurrentHealth / max, false);
            if (blockRect != null)
                SetBarFraction(blockRect, (float)stats.Block / max, true);

            if (healthLabel != null)
                healthLabel.text = $"{stats.CurrentHealth} / {max}";
        }

        private void Position()
        {
            if (barRoot == null) return;

            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;

            Vector3 world = hovered.transform.position + Vector3.up * headOffset;
            Vector3 screen = cam.WorldToScreenPoint(world);

            // Behind the camera: park off-screen instead of projecting garbage coordinates.
            barRoot.position = screen.z > 0f
                ? screen
                : new Vector3(-10000f, -10000f, 0f);
        }

        private static void SetBarFraction(RectTransform rt, float fraction, bool fromRight)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fromRight)
            {
                rt.anchorMin = new Vector2(1f - fraction, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(fraction, 1f);
            }
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static int GetLocalTeamId()
        {
            foreach (Character c in UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                if (c != null && c.OwnerPlayerId == GameState.LocalPlayerId)
                    return c.TeamId;
            }
            return 0;
        }

        private static bool IsEnemyOfLocalPlayer(Character character)
        {
            return character != null && character.TeamId != GetLocalTeamId();
        }

        // ------------------------------------------------------------ ui construction

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            BuildUi();
            SetVisible(false);
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("HealthBar Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            barRoot = NewRect("Health Bar", canvas.transform, barSize);

            Image background = NewImage("Background", barRoot, emptyColor);
            Stretch(background.rectTransform);
            var border = background.gameObject.AddComponent<Outline>();
            border.effectColor = borderColor;
            border.effectDistance = new Vector2(2f, -2f);

            Image fill = NewImage("Fill", barRoot, fillColor);
            fillRect = fill.rectTransform;
            Stretch(fillRect);

            Image block = NewImage("Block", barRoot, blockColor);
            blockRect = block.rectTransform;
            Stretch(blockRect);

            if (showHealthText)
            {
                healthLabel = new GameObject("Health Text", typeof(RectTransform), typeof(TextMeshProUGUI))
                    .GetComponent<TMP_Text>();
                healthLabel.transform.SetParent(barRoot, false);
                Stretch(healthLabel.rectTransform);
                healthLabel.alignment = TextAlignmentOptions.Center;
                healthLabel.fontSize = 14f;
                healthLabel.fontStyle = FontStyles.Bold;
                healthLabel.color = textColor;
                healthLabel.raycastTarget = false;
            }
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetVisible(bool value)
        {
            if (barRoot != null) barRoot.gameObject.SetActive(value);
        }
    }
}
