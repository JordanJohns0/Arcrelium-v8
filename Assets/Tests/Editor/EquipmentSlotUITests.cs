using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Equipment;
using UI;

namespace Tests.UI
{
    [TestFixture]
    public class EquipmentSlotUITests
    {
        private static readonly FieldInfo backgroundField =
            typeof(EquipmentSlotUI).GetField("background", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo filledBackgroundField =
            typeof(EquipmentSlotUI).GetField("filledBackgroundSprite", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo tooltipInstanceField =
            typeof(Tooltip).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo tooltipCreateUiMethod =
            typeof(Tooltip).GetMethod("CreateUI", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo tooltipEnchantTextField =
            typeof(Tooltip).GetField("enchantText", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly System.Collections.Generic.List<Object> owned = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in owned)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            owned.Clear();
        }

        private Sprite MakeSprite()
        {
            var texture = new Texture2D(4, 4);
            owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            owned.Add(sprite);
            return sprite;
        }

        private EquipmentSlotUI MakeSlot(Image background, Sprite typeSprite, Sprite filledSprite)
        {
            var go = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(EquipmentSlotUI));
            owned.Add(go);
            var ui = go.GetComponent<EquipmentSlotUI>();
            backgroundField.SetValue(ui, background);
            filledBackgroundField.SetValue(ui, filledSprite);
            background.sprite = typeSprite;
            return ui;
        }

        [Test]
        public void EmptySlot_ShowsTypeArt_EquippedSlot_ShowsFilledFrame()
        {
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            owned.Add(bgGo);
            var background = bgGo.GetComponent<Image>();
            var typeSprite = MakeSprite();
            var filledSprite = MakeSprite();

            var ui = MakeSlot(background, typeSprite, filledSprite);
            ui.Setup(EquipmentSlotType.Feet, 0, null);
            Assert.AreSame(typeSprite, background.sprite, "An empty slot shows its own type art.");

            var data = ScriptableObject.CreateInstance<EquipmentData>();
            owned.Add(data);
            data.SlotType = EquipmentSlotType.Feet;
            data.EquipmentName = "Boots";
            ui.SetEquipment(data);
            Assert.AreSame(filledSprite, background.sprite, "An equipped slot shows the generic filled frame.");

            ui.Clear();
            Assert.AreSame(typeSprite, background.sprite, "Unequipping restores the slot's type art.");
        }

        [Test]
        public void NoFilledFrameConfigured_KeepsTypeArt()
        {
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            owned.Add(bgGo);
            var background = bgGo.GetComponent<Image>();
            var typeSprite = MakeSprite();

            var ui = MakeSlot(background, typeSprite, null);
            ui.Setup(EquipmentSlotType.Head, 0, null);

            var data = ScriptableObject.CreateInstance<EquipmentData>();
            owned.Add(data);
            data.SlotType = EquipmentSlotType.Head;
            data.EquipmentName = "Helm";
            ui.SetEquipment(data);
            Assert.AreSame(typeSprite, background.sprite, "Without a filled frame the slot keeps its type art.");
        }

        [Test]
        public void RightClick_ShowsTooltipWithTheEquippedItemsEnchantments()
        {
            var tooltipGo = new GameObject("Tooltip", typeof(Tooltip));
            owned.Add(tooltipGo);
            var tooltip = tooltipGo.GetComponent<Tooltip>();
            tooltipInstanceField.SetValue(null, tooltip);
            // Awake/CreateUI don't run automatically in edit mode — build the tooltip UI directly.
            tooltipCreateUiMethod.Invoke(tooltip, null);

            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            owned.Add(inventory);

            var data = ScriptableObject.CreateInstance<EquipmentData>();
            owned.Add(data);
            data.SlotType = EquipmentSlotType.Trinket;
            data.EquipmentName = "Lucky Charm";

            var enchantment = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            enchantment.EnchantmentName = "Sparkly";
            enchantment.Description = "Glows in the dark";
            owned.Add(enchantment);
            inventory.Loadout.AddEnchantment(data, enchantment);

            System.Func<PlayerInventory> previous = EquipmentSlotUI.LocalInventory;
            EquipmentSlotUI.LocalInventory = () => inventory;
            try
            {
                var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
                owned.Add(bgGo);
                var background = bgGo.GetComponent<Image>();
                var ui = MakeSlot(background, MakeSprite(), null);
                ui.Setup(EquipmentSlotType.Trinket, 0, null);
                ui.SetEquipment(data);

                var eventData = new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Right,
                    position = new Vector2(50f, 50f)
                };
                ui.OnPointerClick(eventData);

                var enchantText = (TMPro.TMP_Text)tooltipEnchantTextField.GetValue(tooltip);
                Assert.IsNotNull(enchantText, "Tooltip must build its enchantment text.");
                StringAssert.Contains("Sparkly", enchantText.text,
                    "Right-clicking an equipped item must show its enchantments on the tooltip.");
            }
            finally
            {
                EquipmentSlotUI.LocalInventory = previous;
                tooltipInstanceField.SetValue(null, null);
            }
        }
    }
}
