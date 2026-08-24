using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Characters;
using UI;

namespace Tests.UI
{
    public class StatEntryClickTests
    {
        private GameObject entryGo;
        private StatEntry entry;
        private Character character;

        [TearDown]
        public void TearDown()
        {
            if (entryGo != null) Object.DestroyImmediate(entryGo);
            if (character != null)
            {
                if (character.CurrentCell != null) Object.DestroyImmediate(character.CurrentCell.gameObject);
                Object.DestroyImmediate(character.gameObject);
            }
        }

        private TMP_Text CreateNameLabel()
        {
            var labelGo = new GameObject("Name", typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(entryGo.transform, false);
            TMP_Text label = labelGo.GetComponent<TextMeshProUGUI>();
            label.raycastTarget = true;
            return label;
        }

        [SetUp]
        public void SetUp()
        {
            entryGo = new GameObject("StatEntry", typeof(StatEntry));
            entry = entryGo.GetComponent<StatEntry>();
            character = TestHelper.CreateCharacter(100, 0, 0);
        }

        [Test]
        public void ClickingNameLabel_RaisesNameClicked()
        {
            TMP_Text label = CreateNameLabel();
            entry.nameLabel = label;
            entry.linkedCharacter = character;

            StatEntry clicked = null;
            entry.OnNameClicked += e => clicked = e;

            var eventData = new PointerEventData(EventSystem.current)
            {
                pointerCurrentRaycast = new RaycastResult { gameObject = label.gameObject }
            };
            entry.OnPointerClick(eventData);

            Assert.AreSame(entry, clicked,
                "A click on the name label must raise the name-click event.");
        }

        [Test]
        public void ClickingNonLabelChild_DoesNotRaiseNameClicked()
        {
            CreateNameLabel();
            entry.nameLabel = entryGo.GetComponentInChildren<TextMeshProUGUI>();

            var segmentGo = new GameObject("Segment", typeof(Image));
            segmentGo.transform.SetParent(entryGo.transform, false);
            segmentGo.GetComponent<Image>().raycastTarget = true;

            StatEntry clicked = null;
            entry.OnNameClicked += e => clicked = e;

            var eventData = new PointerEventData(EventSystem.current)
            {
                pointerCurrentRaycast = new RaycastResult { gameObject = segmentGo }
            };
            entry.OnPointerClick(eventData);

            Assert.IsNull(clicked,
                "Only the name label targets the character; HP bars must not.");
        }

        [Test]
        public void ClickingNameLabel_WithoutRaycastHit_DoesNothing()
        {
            TMP_Text label = CreateNameLabel();
            entry.nameLabel = label;
            entry.linkedCharacter = character;

            StatEntry clicked = null;
            entry.OnNameClicked += e => clicked = e;

            var eventData = new PointerEventData(EventSystem.current);
            entry.OnPointerClick(eventData);

            Assert.IsNull(clicked, "No raycast hit means no target selection.");
        }
    }
}
