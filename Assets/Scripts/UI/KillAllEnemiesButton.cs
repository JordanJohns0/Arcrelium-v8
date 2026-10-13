#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game;

namespace UI
{
    /// <summary>
    /// Editor-only debug cheat: a small "Kill All Enemies" button overlaid on the match
    /// scene. It creates itself as soon as a scene containing a <see cref="GameManager"/>
    /// loads in Play mode, so no scene wiring is required. The entire class is compiled out
    /// of player builds, so it never ships.
    /// </summary>
    public class KillAllEnemiesButton : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryCreateFor(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateFor(scene);
        }

        private static void TryCreateFor(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (FindFirstObjectByType<KillAllEnemiesButton>() != null) return;
            if (FindFirstObjectByType<GameManager>() == null) return;
            new GameObject("Kill All Enemies (Debug)", typeof(KillAllEnemiesButton));
        }

        private Button button;

        private void Awake()
        {
            CreateUI();
            button.onClick.AddListener(KillAllEnemies);
        }

        private void KillAllEnemies()
        {
            var manager = FindFirstObjectByType<GameManager>();
            if (manager != null) manager.KillAllEnemies();
        }

        private void CreateUI()
        {
            var canvasGo = new GameObject("KillAllEnemiesCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 350;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var btnGo = new GameObject("Kill All Enemies", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(canvasGo.transform, false);

            var rect = btnGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(210f, 48f);

            var image = btnGo.GetComponent<Image>();
            image.color = new Color(0.6f, 0.15f, 0.15f, 0.95f);

            button = btnGo.GetComponent<Button>();
            button.targetGraphic = image;

            var textGo = new GameObject("Text", typeof(TextMeshProUGUI));
            textGo.transform.SetParent(btnGo.transform, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = "Kill All Enemies";
            text.fontSize = 24f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
        }
    }
}
#endif
