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
    public class CardViewHoverTests
    {
        private readonly System.Collections.Generic.List<GameObject> owned =
            new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in owned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            owned.Clear();
        }

        private CardView CreateCardView()
        {
            var go = new GameObject("CardView", typeof(RectTransform), typeof(Button), typeof(CardView));
            owned.Add(go);

            // CardView has serialized fields we need to set via reflection.
            var view = go.GetComponent<CardView>();
            return view;
        }

        private CardInstance MakeCardInstance()
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = "Test Card";
            data.Cost = 1;
            data.Steps = new System.Collections.Generic.List<CardEffectStep>();
            return new CardInstance(data);
        }

        [Test]
        public void CardView_ImplementsPointerEnterAndExit()
        {
            var go = new GameObject("CardView", typeof(Button), typeof(CardView));
            owned.Add(go);

            var view = go.GetComponent<CardView>();
            Assert.IsNotNull(view as IPointerEnterHandler,
                "CardView must implement IPointerEnterHandler.");
            Assert.IsNotNull(view as IPointerExitHandler,
                "CardView must implement IPointerExitHandler.");
        }

        [Test]
        public void OnPointerEnter_SetsHoveredFlag()
        {
            var view = CreateCardView();
            var instance = MakeCardInstance();
            view.Setup(instance);

            var hoveredField = typeof(CardView).GetField("hovered",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsFalse((bool)hoveredField.GetValue(view));

            var eventData = new PointerEventData(EventSystem.current);
            view.OnPointerEnter(eventData);

            Assert.IsTrue((bool)hoveredField.GetValue(view),
                "hovered must be true after OnPointerEnter.");
        }

        [Test]
        public void OnPointerExit_ClearsHoveredFlag()
        {
            var view = CreateCardView();
            var instance = MakeCardInstance();
            view.Setup(instance);

            var hoveredField = typeof(CardView).GetField("hovered",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var eventData = new PointerEventData(EventSystem.current);
            view.OnPointerEnter(eventData);
            Assert.IsTrue((bool)hoveredField.GetValue(view));

            view.OnPointerExit(eventData);
            Assert.IsFalse((bool)hoveredField.GetValue(view),
                "hovered must be false after OnPointerExit.");
        }

        [Test]
        public void OnPointerEnter_ScaleEqualsDefault()
        {
            var view = CreateCardView();
            var instance = MakeCardInstance();
            view.Setup(instance);

            // Before hover, scale must be unit.
            Assert.AreEqual(Vector3.one, view.transform.localScale,
                "Card should start at unit scale.");
        }

        [Test]
        public void OnPointerEnter_WhenDisabled_DoesNotSetHovered()
        {
            var view = CreateCardView();
            var instance = MakeCardInstance();
            view.Setup(instance);
            view.SetInteractable(false);

            var hoveredField = typeof(CardView).GetField("hovered",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var eventData = new PointerEventData(EventSystem.current);
            view.OnPointerEnter(eventData);

            Assert.IsFalse((bool)hoveredField.GetValue(view),
                "Hover must be ignored when the card is not interactable.");
        }

        [Test]
        public void OnPointerEnter_StoresBasePosition()
        {
            var view = CreateCardView();
            var instance = MakeCardInstance();
            view.Setup(instance);

            var basePosField = typeof(CardView).GetField("basePosition",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var basePos = (Vector3)basePosField.GetValue(view);

            Assert.AreEqual(view.transform.localPosition, basePos,
                "basePosition must match local position at Setup time.");
        }

        [Test]
        public void OnPointerEnter_StartsHoverCoroutine()
        {
            var view = CreateCardView();
            var instance = MakeCardInstance();
            view.Setup(instance);

            var routineField = typeof(CardView).GetField("scaleRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var eventData = new PointerEventData(EventSystem.current);
            view.OnPointerEnter(eventData);

            Assert.IsNotNull(routineField.GetValue(view),
                "A hover coroutine must be running after OnPointerEnter.");
        }

        [Test]
        public void OnPointerEnter_ReplacesExistingCoroutine()
        {
            var view = CreateCardView();
            var instance = MakeCardInstance();
            view.Setup(instance);

            var routineField = typeof(CardView).GetField("scaleRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var eventData = new PointerEventData(EventSystem.current);
            view.OnPointerEnter(eventData);
            var first = routineField.GetValue(view);

            view.OnPointerEnter(eventData);
            var second = routineField.GetValue(view);

            Assert.AreNotSame(first, second,
                "A new coroutine must replace the previous one on repeated enters.");
        }

        [Test]
        public void OnPointerExit_StartsReturnCoroutine()
        {
            var view = CreateCardView();
            var instance = MakeCardInstance();
            view.Setup(instance);

            var routineField = typeof(CardView).GetField("scaleRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var eventData = new PointerEventData(EventSystem.current);

            view.OnPointerEnter(eventData);
            view.OnPointerExit(eventData);

            Assert.IsNotNull(routineField.GetValue(view),
                "A coroutine must be running after OnPointerExit (return animation).");
        }
    }
}
