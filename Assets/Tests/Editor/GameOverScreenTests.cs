using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UI;

namespace Tests.UI
{
    public class GameOverScreenTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            foreach (var go in owned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            owned.Clear();
        }

        private GameObject FindCanvasRoot()
        {
            var screen = Object.FindAnyObjectByType<GameOverScreen>();
            if (screen == null) return null;
            owned.Add(screen.gameObject);
            Transform canvas = screen.transform.Find("GameOverCanvas");
            return canvas != null && canvas.gameObject.activeSelf ? canvas.gameObject : null;
        }

        [Test]
        public void Show_CreatesBlockingOverlayWithGameOverTitle()
        {
            GameOverScreen.Show();
            owned.AddRange(FindAllScreens());

            GameObject root = FindCanvasRoot();
            Assert.IsNotNull(root, "Show must create the overlay canvas.");

            // Full-screen blocking overlay so the dead board stays unclickable.
            RectTransform overlay = root.transform.Find("Overlay") as RectTransform;
            Assert.IsNotNull(overlay);
            Assert.AreEqual(Vector2.zero, overlay.anchorMin);
            Assert.AreEqual(Vector2.one, overlay.anchorMax);
            Assert.IsTrue(overlay.GetComponent<Image>().raycastTarget,
                "The overlay must block clicks into the game underneath.");

            TextMeshProUGUI title = root.GetComponentInChildren<TextMeshProUGUI>();
            Assert.IsNotNull(title);
            Assert.AreEqual("Game Over", title.text);
        }

        [Test]
        public void MainMenuButton_RequestsMainMenuScene()
        {
            GameOverScreen.Show();
            owned.AddRange(FindAllScreens());

            GameObject root = FindCanvasRoot();
            Assert.IsNotNull(root);

            Button menuButton = root.GetComponentInChildren<Button>();
            Assert.IsNotNull(menuButton, "The Game Over screen must offer a Main Menu button.");

            string requested = null;
            var screen = Object.FindAnyObjectByType<GameOverScreen>();
            typeof(GameOverScreen)
                .GetField("mainMenuRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(screen, (System.Action)(() => requested = "MainMenu"));

            menuButton.onClick.Invoke();

            Assert.AreEqual("MainMenu", requested,
                "Clicking the button must navigate back to the main menu.");
        }

        [Test]
        public void Show_PausesGame_MenuRestoreTime()
        {
            GameOverScreen.Show();
            owned.AddRange(FindAllScreens());
            Assert.AreEqual(0f, Time.timeScale, "Game over must pause the game.");

            var screen = Object.FindAnyObjectByType<GameOverScreen>();
            typeof(GameOverScreen)
                .GetField("mainMenuRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(screen, (System.Action)(() => { }));
            typeof(GameOverScreen)
                .GetMethod("GoToMainMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(screen, null);
            Assert.AreEqual(1f, Time.timeScale, "Leaving to the main menu must restore time.");
        }

        private static IEnumerable<GameObject> FindAllScreens()
        {
            foreach (var screen in Object.FindObjectsByType<GameOverScreen>(FindObjectsSortMode.None))
                yield return screen.gameObject;
        }
    }
}
