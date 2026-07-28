using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game;

namespace UI
{
    public class VictoryScreen : MonoBehaviour
    {
        private Canvas canvas;
        private TMP_Text victoryText;

        private void Awake()
        {
            CreateUI();
            Hide();
        }

        private void CreateUI()
        {
            var root = new GameObject("VictoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var bg = new GameObject("Background", typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);
            var bgRect = bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var panel = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.15f, 0.15f, 0.15f, 1);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(400, 250);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20;

            var textObj = new GameObject("VictoryText", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(panel.transform, false);
            victoryText = textObj.GetComponent<TextMeshProUGUI>();
            victoryText.text = $"Victory! Level {GameState.CurrentLevel} Complete!";
            victoryText.fontSize = 28;
            victoryText.alignment = TextAlignmentOptions.Center;
            victoryText.color = Color.white;
            victoryText.rectTransform.sizeDelta = new Vector2(360, 80);

            var btnObj = new GameObject("NextLevelButton", typeof(Image), typeof(Button));
            btnObj.transform.SetParent(panel.transform, false);
            var button = btnObj.GetComponent<Button>();
            var btnImage = btnObj.GetComponent<Image>();
            btnImage.color = new Color(0.2f, 0.5f, 0.2f, 1);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(200, 50);

            var btnTextObj = new GameObject("Text", typeof(TextMeshProUGUI));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnText = btnTextObj.GetComponent<TextMeshProUGUI>();
            btnText.text = "Next Level";
            btnText.fontSize = 22;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;
            btnText.rectTransform.anchorMin = Vector2.zero;
            btnText.rectTransform.anchorMax = Vector2.one;
            btnText.rectTransform.sizeDelta = Vector2.zero;

            button.onClick.AddListener(GameState.LoadNextLevel);
        }

        public void Show()
        {
            victoryText.text = $"Victory! Level {GameState.CurrentLevel} Complete!";
            canvas.enabled = true;
        }

        public void Hide()
        {
            canvas.enabled = false;
        }
    }
}
