using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cards;
using UI;

namespace Tests.UI
{
    [TestFixture]
    public class CardViewCostColorTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();

        private static readonly Color Increase = new Color(0.9f, 0.2f, 0.2f, 1f);
        private static readonly Color Decrease = new Color(0.2f, 0.85f, 0.2f, 1f);

        [TearDown]
        public void TearDown()
        {
            foreach (var go in owned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            owned.Clear();
        }

        private static void SetField(object target, string name, object value)
        {
            typeof(CardView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }

        private TextMeshProUGUI CreateCostView(out CardView view, out CardData data)
        {
            var go = new GameObject("CardView", typeof(RectTransform), typeof(Button), typeof(CardView));
            owned.Add(go);
            view = go.GetComponent<CardView>();

            var costGo = new GameObject("Cost", typeof(RectTransform), typeof(TextMeshProUGUI));
            costGo.transform.SetParent(go.transform, false);
            var costText = costGo.GetComponent<TextMeshProUGUI>();

            MakeData(out data);
            SetField(view, "costText", costText);
            SetField(view, "costIncreaseColor", Increase);
            SetField(view, "costDecreaseColor", Decrease);
            return costText;
        }

        private static void MakeData(out CardData data)
        {
            data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = "Priced";
            data.Cost = 3;
        }

        [Test]
        public void Setup_ColorsCostByComparisonToBaseCost()
        {
            TextMeshProUGUI costText = CreateCostView(out CardView view, out CardData data);
            costText.color = Color.white;
            var instance = new CardInstance(data);

            view.Setup(instance); // cost == base
            Assert.AreEqual(Color.white, costText.color,
                "A card at its base cost keeps the default number colour.");

            instance.Cost = 4; // above base
            view.Setup(instance);
            Assert.AreEqual(Increase, costText.color, "A cost above base must be red.");

            instance.Cost = 2; // below base
            view.Setup(instance);
            Assert.AreEqual(Decrease, costText.color, "A cost below base must be green.");

            instance.Cost = 3; // back to base
            view.Setup(instance);
            Assert.AreEqual(Color.white, costText.color,
                "Returning to the base cost restores the default number colour.");
        }
    }
}
