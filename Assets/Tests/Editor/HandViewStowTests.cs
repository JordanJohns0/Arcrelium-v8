using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cards;
using UI;

namespace Tests.UI
{
    [TestFixture]
    public class HandViewStowTests
    {
        private static readonly FieldInfo containerField =
            typeof(HandView).GetField("cardContainer", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo spawnedViewsField =
            typeof(HandView).GetField("spawnedViews", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo stowedOffsetField =
            typeof(HandView).GetField("stowedOffset", BindingFlags.NonPublic | BindingFlags.Instance);

        private const float StowOffset = 100f;

        private readonly List<GameObject> owned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in owned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            owned.Clear();
            foreach (Object asset in ownedAssets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            ownedAssets.Clear();
        }

        [Test]
        public void Tick_NoHover_HandSlidesDown()
        {
            HandView hand = CreateHand();
            RectTransform container = ContainerOf(hand);
            float startY = container.anchoredPosition.y;

            hand.TickHandSlide(1f / 60f);

            Assert.Less(container.anchoredPosition.y, startY,
                "Without a hovered card the hand must slide down into its stowed spot.");
        }

        [Test]
        public void Tick_ConvergesToFullyStowed()
        {
            HandView hand = CreateHand();
            RectTransform container = ContainerOf(hand);

            TickMany(hand, 300);

            Assert.AreEqual(-StowOffset, container.anchoredPosition.y, 0.5f,
                "The hand must come to rest exactly stowedOffset pixels below its normal spot.");
        }

        [Test]
        public void Tick_CardHovered_HandReveals()
        {
            HandView hand = CreateHand();
            RectTransform container = ContainerOf(hand);
            CardView card = CreateCard();
            InjectViews(hand, card);
            TickMany(hand, 60); // settle stowed first

            Hover(card);
            TickMany(hand, 300);

            Assert.AreEqual(0f, container.anchoredPosition.y, 0.5f,
                "While a card is hovered the hand must rise back to its normal spot.");
        }

        [Test]
        public void Tick_DescendsRightAfterHoverStops()
        {
            HandView hand = CreateHand();
            RectTransform container = ContainerOf(hand);
            CardView card = CreateCard();
            InjectViews(hand, card);

            Hover(card);
            TickMany(hand, 1);
            float revealedY = container.anchoredPosition.y;

            Unhover(card);
            hand.TickHandSlide(1f / 60f);

            Assert.Less(container.anchoredPosition.y, revealedY,
                "The hand must start descending on the very first tick after no card is hovered.");
        }

        [Test]
        public void Tick_LayoutShiftWhileRevealed_IsRespected()
        {
            HandView hand = CreateHand();
            RectTransform container = ContainerOf(hand);
            CardView card = CreateCard();
            InjectViews(hand, card);

            Hover(card);
            TickMany(hand, 60); // settle revealed at the normal spot

            // Someone else repositions the container (layout/safe-area change mid-play).
            container.anchoredPosition = new Vector2(0f, 80f);

            TickMany(hand, 300);

            Assert.AreEqual(80f, container.anchoredPosition.y, 0.5f,
                "The slide must track the container's current resting spot, not a stale cached base.");
        }

        // ---- helpers ----

        private HandView CreateHand()
        {
            var go = new GameObject("HandView", typeof(RectTransform), typeof(HandView));
            var containerGo = new GameObject("CardContainer", typeof(RectTransform));
            owned.Add(go);
            owned.Add(containerGo);

            HandView hand = go.GetComponent<HandView>();
            containerField.SetValue(hand, containerGo.transform);
            stowedOffsetField.SetValue(hand, StowOffset); // deterministic, not the Inspector default
            return hand;
        }

        private RectTransform ContainerOf(HandView hand)
        {
            return (RectTransform)((Transform)containerField.GetValue(hand));
        }

        private void InjectViews(HandView hand, params CardView[] views)
        {
            spawnedViewsField.SetValue(hand, new List<CardView>(views));
        }

        private CardView CreateCard()
        {
            var go = new GameObject("CardView", typeof(RectTransform), typeof(Button), typeof(CardView));
            owned.Add(go);

            CardView view = go.GetComponent<CardView>();
            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = "Peek Test";
            ownedAssets.Add(data);

            view.Setup(new CardInstance(data));
            return view;
        }

        private readonly List<Object> ownedAssets = new List<Object>();

        private static void Hover(CardView view)
        {
            view.OnPointerEnter(new PointerEventData(EventSystem.current));
        }

        private static void Unhover(CardView view)
        {
            view.OnPointerEnter(new PointerEventData(EventSystem.current));
            view.OnPointerExit(new PointerEventData(EventSystem.current));
        }

        private static void TickMany(HandView hand, int frames)
        {
            float dt = 1f / 60f;
            for (int i = 0; i < frames; i++)
                hand.TickHandSlide(dt);
        }
    }
}
