using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game;

namespace UI
{
    public class PauseMenu : MonoBehaviour
    {
        private static readonly float[] SpeedValues = { 1f, 1.5f, 2f };
        private static readonly string[] SpeedLabels = { "Watch Speed: Normal", "Watch Speed: Fast", "Watch Speed: 2x" };

        [Header("Prefab")]
        [SerializeField] private GameObject uiPrefab;

        [Header("Canvas")]
        [SerializeField] private int sortingOrder = 300;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);

        [Header("Overlay & Panel")]
        [SerializeField] private Color overlayColor = new Color(0, 0, 0, 0.6f);
        [SerializeField] private Vector2 panelSize = new Vector2(350, 430);
        [SerializeField] private Color panelColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private float panelSpacing = 20f;
        [SerializeField] private int paddingLeft = 30;
        [SerializeField] private int paddingRight = 30;
        [SerializeField] private int paddingTop = 60;
        [SerializeField] private int paddingBottom = 30;

        [Header("Title")]
        [SerializeField] private string titleLabel = "Paused";
        [SerializeField] private int titleFontSize = 40;
        [SerializeField] private Color titleColor = Color.white;
        [SerializeField] private Vector2 titleSize = new Vector2(290, 60);

        [Header("Buttons")]
        [SerializeField] private Vector2 buttonSize = new Vector2(250, 55);
        [SerializeField] private Color buttonColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color buttonHighlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color buttonPressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        [SerializeField] private int buttonFontSize = 26;
        [SerializeField] private Color buttonTextColor = Color.white;
        [SerializeField] private string resumeLabel = "Resume";
        [SerializeField] private string mainMenuLabel = "Main Menu";

        private InputAction toggleAction;
        private GameObject canvasRoot;
        private bool isVisible;
        private static PauseMenu instance;
        private static GameObject staticPrefab;
        private TextMeshProUGUI speedButtonText;

        private void Awake()
        {
            if (staticPrefab == null && uiPrefab != null) staticPrefab = uiPrefab;
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            CreateUI();
            canvasRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (toggleAction == null) return;
            toggleAction.Enable();
            toggleAction.performed += OnToggle;
        }

        private void OnDisable()
        {
            if (toggleAction != null)
            {
                toggleAction.performed -= OnToggle;
                toggleAction.Disable();
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void Initialize(InputActionAsset actions)
        {
            if (actions == null) return;
            var uiMap = actions.FindActionMap("UI");
            if (uiMap != null)
            {
                toggleAction = uiMap.FindAction("TogglePauseMenu");
                if (toggleAction != null && isActiveAndEnabled)
                {
                    toggleAction.Enable();
                    toggleAction.performed += OnToggle;
                }
            }
        }

        private void OnToggle(InputAction.CallbackContext ctx)
        {
            if (isVisible) Hide(); else Show();
        }

        private void CreateUI()
        {
            canvasRoot = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);

            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;

            var overlay = new GameObject("Overlay", typeof(Image)).GetComponent<RectTransform>();
            overlay.SetParent(canvasRoot.transform, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            var overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = overlayColor;

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            panel.SetParent(canvasRoot.transform, false);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = panelSize;
            panel.anchoredPosition = Vector2.zero;
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = panelColor;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = panelSpacing;
            layout.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);

            var titleObj = new GameObject("Title", typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(panel, false);
            var title = titleObj.GetComponent<TextMeshProUGUI>();
            title.text = titleLabel;
            title.fontSize = titleFontSize;
            title.fontStyle = FontStyles.Bold;
            title.color = titleColor;
            title.alignment = TextAlignmentOptions.Center;
            title.rectTransform.sizeDelta = titleSize;

            CreateButton(panel, resumeLabel, Hide);
            CreateSpeedButton(panel);
            CreateButton(panel, mainMenuLabel, GoToMainMenu);
        }

        /// <summary>
        /// Adds the "Watch Speed" cycle button. It steps through the manual override
        /// multiplier applied on top of the automatic enemy-count scaling, persists it,
        /// and applies it immediately (see <see cref="GamePace"/>).
        /// </summary>
        private void CreateSpeedButton(Transform parent)
        {
            GameObject btnObj = CreateButton(parent, SpeedLabels[CurrentSpeedIndex()], CycleSpeed);
            speedButtonText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        }

        private static int CurrentSpeedIndex()
        {
            float current = GamePace.ManualMultiplier;
            for (int i = 0; i < SpeedValues.Length; i++)
            {
                if (Mathf.Approximately(SpeedValues[i], current)) return i;
            }
            return 0;
        }

        private void CycleSpeed()
        {
            int next = (CurrentSpeedIndex() + 1) % SpeedValues.Length;
            GamePace.ManualMultiplier = SpeedValues[next];
            if (speedButtonText != null) speedButtonText.text = SpeedLabels[next];
        }

        private GameObject CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject(label + "Button", typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.sizeDelta = buttonSize;

            var btnImage = btnObj.GetComponent<Image>();
            btnImage.color = buttonColor;

            var textObj = new GameObject("Text", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = buttonFontSize;
            text.color = buttonTextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = buttonSize;

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);

            ColorBlock colors = btn.colors;
            colors.highlightedColor = buttonHighlightedColor;
            colors.pressedColor = buttonPressedColor;
            btn.colors = colors;

            return btnObj;
        }

        public void Show()
        {
            if (canvasRoot == null) return;
            isVisible = true;
            canvasRoot.SetActive(true);
            Time.timeScale = 0;
        }

        public void Hide()
        {
            if (canvasRoot == null) return;
            isVisible = false;
            canvasRoot.SetActive(false);
            Time.timeScale = 1;
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene("MainMenu");
        }

        public static PauseMenu GetOrCreate(InputActionAsset actions)
        {
            if (instance != null)
            {
                instance.Initialize(actions);
                return instance;
            }
            var existing = FindAnyObjectByType<PauseMenu>();
            if (existing != null)
            {
                instance = existing;
                existing.Initialize(actions);
                return existing;
            }
            if (staticPrefab != null)
            {
                instance = Object.Instantiate(staticPrefab).GetComponent<PauseMenu>();
            }
            else
            {
                new GameObject("PauseMenu", typeof(PauseMenu));
            }
            instance.Initialize(actions);
            return instance;
        }
    }
}
