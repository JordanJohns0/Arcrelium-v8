using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    public class PauseMenu : MonoBehaviour
    {
        private InputAction toggleAction;
        private GameObject canvasRoot;
        private bool isVisible;
        private static PauseMenu instance;

        private void Awake()
        {
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
            canvas.sortingOrder = 300;

            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var overlay = new GameObject("Overlay", typeof(Image)).GetComponent<RectTransform>();
            overlay.SetParent(canvasRoot.transform, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            var overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0, 0, 0, 0.6f);

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            panel.SetParent(canvasRoot.transform, false);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(350, 350);
            panel.anchoredPosition = Vector2.zero;
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.12f, 0.12f, 0.12f, 1);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = 20;
            layout.padding = new RectOffset(30, 30, 60, 30);

            var titleObj = new GameObject("Title", typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(panel, false);
            var title = titleObj.GetComponent<TextMeshProUGUI>();
            title.text = "Paused";
            title.fontSize = 40;
            title.fontStyle = FontStyles.Bold;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.Center;
            title.rectTransform.sizeDelta = new Vector2(290, 60);

            CreateButton(panel, "Resume", Hide);
            CreateButton(panel, "Main Menu", GoToMainMenu);
        }

        private static GameObject CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject(label + "Button", typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(250, 55);

            var btnImage = btnObj.GetComponent<Image>();
            btnImage.color = new Color(0.25f, 0.25f, 0.25f, 1);

            var textObj = new GameObject("Text", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 26;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(250, 55);

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);

            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
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
            if (instance != null) return instance;
            if (FindAnyObjectByType<PauseMenu>() != null) return instance;
            new GameObject("PauseMenu", typeof(PauseMenu));
            instance.Initialize(actions);
            return instance;
        }
    }
}
