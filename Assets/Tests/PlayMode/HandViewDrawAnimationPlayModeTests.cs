using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
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

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in owned)
            {
                if (obj != null) Object.Destroy(obj);
            }
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator DrawnCard_FadesInAndSettlesFullyOpaque()
        {
            var character = TestHelper.CreateCharacter(0, 0, 0, 0);
            owned.Add(character.gameObject);
            owned.Add(character.CurrentCell.gameObject);

            var prefabGo = new GameObject("CardPrefab", typeof(RectTransform), typeof(Button), typeof(CardView));
            prefabGo.SetActive(false);
            owned.Add(prefabGo);

            var handGo = new GameObject("HandView", typeof(RectTransform), typeof(HandView));
            owned.Add(handGo);
            var hand = handGo.GetComponent<HandView>();

            var containerGo = new GameObject("CardContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            owned.Add(containerGo);

            typeof(HandView).GetField("cardViewPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hand, prefabGo.GetComponent<CardView>());
            typeof(HandView).GetField("cardContainer", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hand, containerGo.transform);
            hand.SetCharacter(character);

            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = "Drawn";
            owned.Add(data);
            character.Hand.AddCard(new CardInstance(data));

            // Let the animation begin (it waits for the layout to place the card first).
            yield return null;

            var views = (List<CardView>)typeof(HandView)
                .GetField("spawnedViews", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(hand);
            Assert.AreEqual(1, views.Count, "The drawn card must be shown in the hand.");

            var group = views[0].GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "A drawn card must get a CanvasGroup to fade in with.");
            Assert.Less(group.alpha, 1f, "A drawn card starts faded out and fades in.");

            yield return new WaitForSeconds(1f);

            Assert.AreEqual(1f, group.alpha, 0.001f, "The drawn card must finish fully opaque.");
        }
    }
}
