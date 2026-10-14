using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Cards;
using Characters;
using UI;

namespace Tests.PlayMode
{
    [TestFixture]
    public class HandViewDrawAnimationPlayModeTests
    {
        private readonly List<Object> owned = new List<Object>();
        private Character character;
        private HandView hand;
        private RectTransform container;

        [SetUp]
        public void SetUp()
        {
            character = TestHelper.CreateCharacter(0, 0, 0, 0);
            owned.Add(character.gameObject);
            owned.Add(character.CurrentCell.gameObject);

            var prefabGo = new GameObject("CardPrefab", typeof(RectTransform), typeof(Button), typeof(CardView));
            prefabGo.SetActive(false); // template only; never shown
            owned.Add(prefabGo);

            var handGo = new GameObject("HandView", typeof(RectTransform), typeof(HandView));
            owned.Add(handGo);
            hand = handGo.GetComponent<HandView>();

            var containerGo = new GameObject("CardContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            owned.Add(containerGo);
            container = containerGo.GetComponent<RectTransform>();

            typeof(HandView).GetField("cardViewPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hand, prefabGo.GetComponent<CardView>());
            typeof(HandView).GetField("cardContainer", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hand, containerGo.transform);
            hand.SetCharacter(character);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in owned)
            {
                if (obj != null) Object.Destroy(obj);
            }
            owned.Clear();
        }

        private CardInstance MakeCard(string name)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = name;
            owned.Add(data);
            return new CardInstance(data);
        }

        private List<CardView> SpawnedViews()
        {
            return (List<CardView>)typeof(HandView)
                .GetField("spawnedViews", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(hand);
        }

        [UnityTest]
        public IEnumerator DrawnCard_FadesInAndSettlesFullyOpaque()
        {
            character.Hand.AddCard(MakeCard("Drawn"));

            // Let the animation begin (it waits for the layout to place the card first).
            yield return null;

            var views = SpawnedViews();
            Assert.AreEqual(1, views.Count, "The drawn card must be shown in the hand.");

            var group = views[0].GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "A drawn card must get a CanvasGroup to fade in with.");
            Assert.Less(group.alpha, 1f, "A drawn card starts faded out and fades in.");

            yield return new WaitForSeconds(1f);

            Assert.AreEqual(1f, group.alpha, 0.001f, "The drawn card must finish fully opaque.");
        }

        [UnityTest]
        public IEnumerator RebuildingTheHandMidDraw_KeepsTheDrawAnimationGoing()
        {
            character.Hand.AddCard(MakeCard("Drawn"));
            yield return null;

            // Turn start rebinds the hand, which rebuilds every card view mid-draw.
            hand.SetCharacter(character);
            yield return null;

            var views = SpawnedViews();
            Assert.AreEqual(1, views.Count);
            Assert.Less(views[0].GetComponent<CanvasGroup>().alpha, 1f,
                "A rebuild during a draw must not cancel the animation: the card is still fading in.");
        }

        [UnityTest]
        public IEnumerator DiscardedCard_FliesOutAndIsRemoved()
        {
            var card = MakeCard("Discarded");
            character.Hand.AddCard(card);
            yield return new WaitForSeconds(1f); // draw finished

            character.Hand.RemoveCard(card);
            yield return null;

            CardView flying = null;
            foreach (CardView view in Object.FindObjectsByType<CardView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view.Instance == card) flying = view;
            }
            Assert.IsNotNull(flying, "A card that leaves the hand must stay on screen while it flies to the discard pile.");
            Assert.AreNotEqual(container, flying.transform.parent,
                "The flying card must leave the hand's layout so the remaining cards close the gap.");
            Assert.Less(flying.GetComponent<CanvasGroup>().alpha, 1f, "The flying card fades out.");
            owned.Add(flying.gameObject);

            yield return new WaitForSeconds(1f);

            Assert.IsTrue(flying == null, "The card is removed once it reaches the discard pile.");
        }

        [UnityTest]
        public IEnumerator HidingTheHand_RemovesCardsStillFlyingOut()
        {
            var card = MakeCard("Played");
            character.Hand.AddCard(card);
            yield return new WaitForSeconds(1f); // draw finished

            character.Hand.RemoveCard(card);
            yield return null;
            Assert.IsNotNull(FindFlyingView(card), "Precondition: the card is still flying out.");

            // Victory hides the hand while the card is in flight.
            hand.gameObject.SetActive(false);
            yield return null;

            Assert.IsNull(FindFlyingView(card),
                "A hidden hand must not leave a flying card stranded on screen over the UI.");
        }

        private static CardView FindFlyingView(CardInstance card)
        {
            foreach (CardView view in Object.FindObjectsByType<CardView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view.Instance == card) return view;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator HoveringDuringTheDraw_HoldsTheHoverUntilTheCardLands()
        {
            character.Hand.AddCard(MakeCard("Drawn"));
            yield return null;

            var view = SpawnedViews()[0];
            view.gameObject.SetActive(true); // spawned cards are active in the game; the template is not
            view.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return new WaitForSeconds(0.1f);

            Assert.IsTrue(view.IsEntering, "The card is still flying in.");
            Assert.AreEqual(1f, view.transform.localScale.x, 0.001f,
                "Hovering a card mid-flight must not start the hover animation.");

            yield return new WaitForSeconds(1f);

            Assert.IsFalse(view.IsEntering, "The card has landed.");
            Assert.Greater(view.transform.localScale.x, 1f,
                "Once the card lands, the hover the pointer is holding plays.");
        }
    }
}
