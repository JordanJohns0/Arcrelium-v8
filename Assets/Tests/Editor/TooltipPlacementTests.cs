using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Equipment;
using UI;

namespace Tests.UI
{
    /// <summary>
    /// Tooltips must never run off-screen: equipment rows near the bottom or right edge
    /// used to push the fixed-size panel past the display bounds.
    /// </summary>
    public class TooltipPlacementTests
    {
        private static readonly FieldInfo instanceField =
            typeof(Tooltip).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo createUiMethod =
            typeof(Tooltip).GetMethod("CreateUI", BindingFlags.NonPublic | BindingFlags.Instance);

        private GameObject tooltipGo;
        private Tooltip tooltip;
        private readonly System.Collections.Generic.List<Object> ownedAssets =
            new System.Collections.Generic.List<Object>();

        [SetUp]
        public void SetUp()
        {
            tooltipGo = new GameObject("Tooltip", typeof(Tooltip));
            tooltip = tooltipGo.GetComponent<Tooltip>();
            instanceField.SetValue(null, tooltip);
            // Awake/CreateUI don't run automatically in edit mode — build the UI directly.
            createUiMethod.Invoke(tooltip, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (tooltipGo != null) Object.DestroyImmediate(tooltipGo);
            instanceField.SetValue(null, null);
            foreach (Object asset in ownedAssets)
                if (asset != null) Object.DestroyImmediate(asset);
            ownedAssets.Clear();
        }

        private EquipmentData MakeEquipment()
        {
            var data = ScriptableObject.CreateInstance<EquipmentData>();
            data.EquipmentName = "Test Gear";
            data.Description = "A clamp test.";
            ownedAssets.Add(data);
            return data;
        }

        private Rect PanelScreenRect()
        {
            RectTransform panel = null;
            foreach (RectTransform rt in tooltipGo.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == "TooltipPanel") { panel = rt; break; }
            Assert.IsNotNull(panel, "Tooltip panel must exist after Show.");
            Vector3[] corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [Test]
        public void Show_NearBottomEdge_ClampsPanelOnScreen()
        {
            Tooltip.Show(MakeEquipment(), new Vector2(100f, 20f));

            Rect rect = PanelScreenRect();
            Assert.GreaterOrEqual(rect.yMin, 7.5f,
                "A tooltip opened near the bottom edge must stay fully on-screen.");
        }

        [Test]
        public void Show_NearRightEdge_ClampsPanelOnScreen()
        {
            Tooltip.Show(MakeEquipment(), new Vector2(Screen.width - 10f, 300f));

            Rect rect = PanelScreenRect();
            Assert.LessOrEqual(rect.xMax, Screen.width - 7.5f,
                "A tooltip opened near the right edge must stay fully on-screen.");
        }

        [Test]
        public void Show_MidScreen_KeepsOffsetPosition()
        {
            var requested = new Vector2(200f, 500f);
            Tooltip.Show(MakeEquipment(), requested);

            Rect rect = PanelScreenRect();
            Assert.AreEqual(requested.x + 15f, rect.xMin, 0.5f,
                "Mid-screen tooltips keep their cursor offset.");
            Assert.AreEqual(requested.y - 10f, rect.yMax, 0.5f,
                "Mid-screen tooltips keep their cursor offset.");
        }
    }
}
