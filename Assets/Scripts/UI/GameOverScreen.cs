using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

using Object = UnityEngine.Object;

namespace UI
{
    /// <summary>
    /// Full-screen overlay shown when every player has died. Blocks the rest of the UI
    /// and offers a single way out: back to the main menu. Built lazily like the other
    /// static overlays so it works without scene wiring.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private int sortingOrder = 400;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);

        [Header("Overlay")]
        [SerializeField] private Color overlayColor = new Color(0, 0, 0, 0.78f);

        [Header("Title")]
        [SerializeField] private string titleLabel = "Game Over";
        [SerializeField] private int titleFontSize = 72;
        [SerializeField] private Color titleColor = Color.white;

        [Header("Button")]
        [SerializeField] private string mainMenuLabel = "Main Menu";
        [SerializeField] private Vector2 buttonSize = new Vector2(260, 62);
        [SerializeField] private int buttonFontSize = 28;
        [SerializeField] private Color buttonColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color buttonHighlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color buttonPressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        private GameObject canvasRoot;
        private static GameOverScreen instance;
        private static GameObject staticPrefab;

        /// <summary>Test seam: observes the menu request without loading scenes.</summary>
        internal Action mainMenuRequested = () => SceneManager.LoadScene("MainMenu");

        public static void Show()
        {
            EnsureInstance();
            instance.ShowInternal();
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;

            var existing = FindAnyObjectByType<GameOverScreen>();
            if (existing != null)
            {
                instance = existing;
            }
            else if (staticPrefab != null)
            {
                instance = Object.Instantiate(staticPrefab).GetComponent<GameOverScreen>();
            }
            else
            {
                instance = new GameObject("GameOverScreen").AddComponent<GameOverScreen>();
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void ShowInternal()
        {
            if (canvasRoot == null) CreateUI();
            canvasRoot.SetActive(true);
            Time.timeScale = 0f;
        }

        private void CreateUI()
        {
            canvasRoot = new GameObject("GameOverCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);

            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;

            // Full-screen blocking overlay: nothing underneath stays clickable.
            var overlay = new GameObject("Overlay", typeof(Image)).GetComponent<RectTransform>();
            overlay.SetParent(canvasRoot.transform, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = overlayColor;

            var titleObj = new GameObject("Title", typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(canvasRoot.transform, false);
            var title = titleObj.GetComponent<TextMeshProUGUI>();
            title.text = titleLabel;
            title.fontSize = titleFontSize;
            title.fontStyle = FontStyles.Bold;
            title.color = titleColor;
            title.alignment = TextAlignmentOptions.Center;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(600, 100);
            titleRect.anchoredPosition = new Vector2(0f, 80f);

            var btnObj = new GameObject(mainMenuLabel + "Button", typeof(Image), typeof(Button));
            btnObj.transform.SetParent(canvasRoot.transform, false);
            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = buttonSize;
            btnRt.anchoredPosition = new Vector2(0f, -40f);

            var btnImage = btnObj.GetComponent<Image>();
            btnImage.color = buttonColor;

            var textObj = new GameObject("Text", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = mainMenuLabel;
            text.fontSize = buttonFontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = buttonSize;

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(GoToMainMenu);

            ColorBlock colors = btn.colors;
            colors.highlightedColor = buttonHighlightedColor;
            colors.pressedColor = buttonPressedColor;
            btn.colors = colors;
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            mainMenuRequested();
        }
    }
}
