using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using Equipment;
using UI;
using Game;

namespace Tests.PlayMode
{
    public class PlayedCardDisplayPlayModeTests
    {
        private PlayedCardDisplay display;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.SetFloat(GamePace.PrefKey, 1f);
            GamePace.ResetManualCache();
            GamePace.Reset();

            var go = new GameObject("PlayedCardDisplay");
            display = go.AddComponent<PlayedCardDisplay>();
        }

        [TearDown]
        public void TearDown()
        {
            GameDataLibrary.SetInstance(null);
            if (display != null) Object.Destroy(display.gameObject);
            foreach (var asset in trackAssets)
            {
                if (asset != null) Object.Destroy(asset);
            }
            trackAssets.Clear();
        }

        private GameObject VisibleCard()
        {
            Canvas canvas = display.GetComponentInChildren<Canvas>();
            if (canvas == null) return null;
            Transform card = canvas.transform.Find("PlayedCard");
            return card != null && card.gameObject.activeInHierarchy ? card.gameObject : null;
        }

        private string VisibleCardName()
        {
            GameObject card = VisibleCard();
            if (card == null) return null;
            Transform label = card.transform.Find("Name");
            return label != null ? label.GetComponent<TextMeshProUGUI>().text : null;
        }

        [UnityTest]
        public IEnumerator RapidPlays_PlaySequentially_OneAtATime()
        {
            // A burst of plays must show one notification at a time — never stacked.
            PlayedCardDisplay.Show("Alpha", "Player");
            PlayedCardDisplay.Show("Beta", "Enemy");

            for (int i = 0; i < 30; i++)
            {
                int count = 0;
                Canvas canvas = display.GetComponentInChildren<Canvas>();
                foreach (Transform child in canvas.transform)
                {
                    if (child.name == "PlayedCard") count++;
                }
                Assert.LessOrEqual(count, 1, "Notifications must never overlap on screen.");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RapidPlays_FirstCardStaysUp_MinimumSecond_BeforeSecondAppears()
        {
            PlayedCardDisplay.Show("Alpha", "Player");
            PlayedCardDisplay.Show("Beta", "Enemy");

            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual("Alpha", VisibleCardName(),
                "The first card must still be showing shortly after a second play arrives.");

            // Just past the 1s minimum the first card must STILL be up...
            yield return new WaitForSeconds(0.85f);
            Assert.AreEqual("Alpha", VisibleCardName(),
                "Each card must stay visible for at least one full second.");

            // ...then the second card takes its turn.
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual("Beta", VisibleCardName(),
                "After the first card finishes, the next queued play is shown.");

            // Both cards' lifetimes later, nothing remains.
            yield return new WaitForSeconds(1.8f);
            Assert.IsNull(VisibleCard(), "All notifications must eventually clear.");
        }

        [UnityTest]
        public IEnumerator Show_WithCardData_RendersPrefabWithoutLibraryLookup()
        {
            // The CardData comes straight from the play event — no GameDataLibrary
            // registration — so every card (present and future) renders as the real
            // card instead of the name-only fallback.
            var prefabGo = new GameObject("CardPrefab", typeof(RectTransform), typeof(Button), typeof(CardView));
            var art = new GameObject("Artwork", typeof(Image));
            art.transform.SetParent(prefabGo.transform, false);
            CardView view = prefabGo.GetComponent<CardView>();
            var data = ScriptableObject.CreateInstance<Cards.CardData>();
            data.CardName = "Unregistered Future Card";
            trackAssets.Add(data);
            view.Setup(new Cards.CardInstance(data));

            GameDataLibrary.SetInstance(null); // no library available at all

            PlayedCardDisplay.SetCardPrefab(view);
            PlayedCardDisplay.Show(data, "Foe");
            yield return null;

            GameObject card = VisibleCard();
            Assert.IsNotNull(card);
            Assert.IsNotNull(card.GetComponent<CardView>(),
                "Direct CardData plays must render as the real card with no name lookup.");
        }

        [UnityTest]
        public IEnumerator DisplayedCard_NeverBlocksRaycasts()
        {
            PlayedCardDisplay.Show("Brace", "Player");
            yield return null;

            Canvas canvas = display.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas);
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                Assert.IsFalse(graphic.raycastTarget,
                    $"{graphic.name} must not intercept clicks while the notification shows.");
            }
        }

        [UnityTest]
        public IEnumerator Show_RendersInjectedCardPrefab_NotFallback()
        {
            var prefabGo = new GameObject("CardPrefab", typeof(RectTransform), typeof(Button), typeof(CardView));
            var art = new GameObject("Artwork", typeof(Image));
            art.transform.SetParent(prefabGo.transform, false);
            CardView view = prefabGo.GetComponent<CardView>();
            var data = ScriptableObject.CreateInstance<Cards.CardData>();
            data.CardName = "Popup Source";
            trackAssets.Add(data);
            view.Setup(new Cards.CardInstance(data));

            // The popup resolves names through the library before using the prefab;
            // register the card there like a real match would have it.
            var library = ScriptableObject.CreateInstance<GameDataLibrary>();
            library.allCards.Add(data);
            trackAssets.Add(library);
            GameDataLibrary.SetInstance(library);

            PlayedCardDisplay.SetCardPrefab(view);
            PlayedCardDisplay.Show("Popup Source", "Foe");
            yield return null;

            GameObject card = VisibleCard();
            Assert.IsNotNull(card);
            var shownView = card.GetComponent<CardView>();
            Assert.IsNotNull(shownView,
                "With a prefab injected the notification must show the real card, not the fallback.");
            Assert.AreEqual("Popup Source", shownView.Instance.Data.CardName,
                "The displayed card must carry the played card's data.");
        }

        [UnityTest]
        public IEnumerator BroadcastPayload_RendersAccurateStandIn_WhenNameUnknown()
        {
            // A name that exists in NO library (the real Resources one included) plus the
            // broadcast's cost/description: the popup must render an accurate stand-in
            // card, never a blank or wrong fallback.
            var prefabGo = new GameObject("CardPrefab", typeof(RectTransform), typeof(Button), typeof(CardView));
            CardView view = prefabGo.GetComponent<CardView>();
            view.Setup(new Cards.CardInstance(ScriptableObject.CreateInstance<Cards.CardData>()));

            GameDataLibrary.SetInstance(null); // worst case: no resolvable library

            PlayedCardDisplay.SetCardPrefab(view);
            PlayedCardDisplay.Show("Totally Unknown Card", "Foe", 3, "Deal 2 damage.");
            yield return null;

            GameObject card = VisibleCard();
            Assert.IsNotNull(card);
            var shownView = card.GetComponent<CardView>();
            Assert.IsNotNull(shownView,
                "With the broadcast payload present the popup must be a real card, not the name-only fallback.");
            Assert.AreEqual("Totally Unknown Card", shownView.Instance.Data.CardName);
            Assert.AreEqual(3, shownView.Instance.Data.Cost);
            Assert.AreEqual("Deal 2 damage.", shownView.Instance.Data.Description);

            Cards.CardData standIn = shownView.Instance.Data;
            yield return new WaitForSeconds(2.5f);
            Assert.IsTrue(standIn == null,
                "The transient stand-in CardData must be destroyed after display, never leak.");
        }

        private readonly List<ScriptableObject> trackAssets = new List<ScriptableObject>();
    }
}
