using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UI;

namespace Tests.UI
{
    [TestFixture]
    public class EquipmentUITests
    {
        private static readonly MethodInfo awakeMethod =
            typeof(EquipmentUI).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);

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

        private GameObject CreateEquipmentUIGo()
        {
            var go = new GameObject("EquipmentUI", typeof(RectTransform), typeof(EquipmentUI));
            owned.Add(go);
            return go;
        }

        [Test]
        public void EquipmentUI_Awake_DeactivatesGameObject()
        {
            var go = CreateEquipmentUIGo();
            Assert.IsTrue(go.activeSelf, "Precondition: GO must start active before Awake runs.");

            awakeMethod.Invoke(go.GetComponent<EquipmentUI>(), null);

            Assert.IsFalse(go.activeSelf,
                "EquipmentUI should deactivate its GameObject in Awake to prevent flashing before LoadoutScreen configures it.");
        }

        [Test]
        public void EquipmentUI_SetActiveTrue_ThenInactive()
        {
            var go = CreateEquipmentUIGo();

            go.SetActive(true);
            Assert.IsTrue(go.activeSelf,
                "EquipmentUI should be activatable via SetActive(true).");

            go.SetActive(false);
            Assert.IsFalse(go.activeSelf,
                "EquipmentUI should be deactivatable via SetActive(false).");
        }

        [Test]
        public void EquipmentUI_Awake_CalledExplicitly_Deactivates()
        {
            var go = CreateEquipmentUIGo();
            var ui = go.GetComponent<EquipmentUI>();

            go.SetActive(true);
            awakeMethod.Invoke(ui, null);

            Assert.IsFalse(go.activeSelf,
                "Explicitly invoking Awake must deactivate the GameObject.");
        }
    }
}
