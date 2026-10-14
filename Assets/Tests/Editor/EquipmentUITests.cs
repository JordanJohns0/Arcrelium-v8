using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Equipment;
using Game;
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

        private readonly System.Collections.Generic.List<ScriptableObject> ownedSops =
            new System.Collections.Generic.List<ScriptableObject>();

        private readonly System.Collections.Generic.List<Object> ownedAssets =
            new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in owned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            owned.Clear();
            foreach (var so in ownedSops)
            {
                if (so != null) Object.DestroyImmediate(so);
            }
            ownedSops.Clear();
            foreach (var asset in ownedAssets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            ownedAssets.Clear();
        }

        private GameObject CreateEquipmentUIGo()
        {
            var go = new GameObject("EquipmentUI", typeof(RectTransform), typeof(EquipmentUI));
            owned.Add(go);
            return go;
        }

        private PlayerInventory CreateInventory()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            ownedSops.Add(inventory);
            return inventory;
        }

        /// <summary>Mirrors the scene structure: Equipment UI > Gold Display > Gold Number Display.</summary>
        private TMP_Text AddGoldDisplay(GameObject equipmentUiGo)
        {
            var goldGo = new GameObject("Gold Display", typeof(RectTransform));
            goldGo.transform.SetParent(equipmentUiGo.transform);
            owned.Add(goldGo);
            var numberGo = new GameObject("Gold Number Display", typeof(RectTransform), typeof(TextMeshProUGUI));
            numberGo.transform.SetParent(goldGo.transform);
            owned.Add(numberGo);
            return numberGo.GetComponent<TMP_Text>();
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

        [Test]
        public void EquipmentUI_ResolvesSpecialPanelAndGoldNumberDisplay()
        {
            var go = CreateEquipmentUIGo();
            var page = new GameObject("Special Page", typeof(RectTransform));
            page.transform.SetParent(go.transform, false);
            owned.Add(page);
            var special = new GameObject("Special", typeof(RectTransform));
            special.transform.SetParent(page.transform, false);
            owned.Add(special);
            var label = AddGoldDisplay(go);
            var ui = go.GetComponent<EquipmentUI>();

            Assert.IsNotNull(label,
                "Gold Number Display must carry a concrete TMP component.");
            Assert.AreSame(special.transform, ui.ShopPanel,
                "ShopPanel must resolve the nested 'Special' column the shop fills.");
            Assert.AreSame(label, ui.GoldLabel,
                "GoldLabel must resolve the Gold Display's number TMP.");
        }

        [Test]
        public void EquipmentUI_ResolvesSpecialTitleAndDescriptionLabels()
        {
            var go = CreateEquipmentUIGo();
            var page = new GameObject("Special Page", typeof(RectTransform));
            page.transform.SetParent(go.transform, false);
            owned.Add(page);

            var title = new GameObject("Special Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            title.transform.SetParent(page.transform, false);
            owned.Add(title);
            var description = new GameObject("Special Description", typeof(RectTransform), typeof(TextMeshProUGUI));
            description.transform.SetParent(page.transform, false);
            owned.Add(description);

            var ui = go.GetComponent<EquipmentUI>();

            Assert.AreSame(title.GetComponent<TMP_Text>(), ui.SpecialTitleLabel,
                "SpecialTitleLabel must resolve the 'Special Title' TMP under the Special page.");
            Assert.AreSame(description.GetComponent<TMP_Text>(), ui.SpecialDescriptionLabel,
                "SpecialDescriptionLabel must resolve the 'Special Description' TMP under the Special page.");
        }

        [Test]
        public void EquipmentUI_ApplySpecialArea_SwapsSpecialTabIcon()
        {
            var go = CreateEquipmentUIGo();
            var bookmarks = new GameObject("Bookmarks", typeof(RectTransform));
            bookmarks.transform.SetParent(go.transform, false);
            owned.Add(bookmarks);

            var tab = new GameObject("Special Tab", typeof(RectTransform));
            tab.transform.SetParent(bookmarks.transform, false);
            owned.Add(tab);

            var iconGo = new GameObject("Image", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(tab.transform, false);
            owned.Add(iconGo);
            var icon = iconGo.GetComponent<Image>();

            Sprite shop = MakeSprite("Icon_Shop");
            Sprite enchant = MakeSprite("Icon_Enchant");

            var ui = go.GetComponent<EquipmentUI>();
            SetPrivateField(ui, "specialShopIcon", shop);
            SetPrivateField(ui, "specialEnchantIcon", enchant);

            ui.ApplySpecialArea(SpecialArea.Enchant);
            Assert.AreSame(enchant, icon.sprite,
                "The Enchant area must swap the Special tab icon to the enchant sprite.");

            ui.ApplySpecialArea(SpecialArea.Shop);
            Assert.AreSame(shop, icon.sprite,
                "The Shop area must swap the Special tab icon to the shop sprite.");
        }

        [Test]
        public void EquipmentUI_InventoryGoldChange_UpdatesGoldNumberDisplay()
        {
            var go = CreateEquipmentUIGo();
            var label = AddGoldDisplay(go);
            var ui = go.GetComponent<EquipmentUI>();
            var inventory = CreateInventory();
            inventory.AddGold(40);

            ui.SetInventory(inventory);
            inventory.AddGold(10);

            Assert.AreEqual("50", label.text,
                "An inventory change must refresh the gold display to the current balance.");
        }

        [Test]
        public void EquipmentUI_RefreshInventory_PadsToFixedSlotCount()
        {
            var go = CreateEquipmentUIGo();
            var panel = new GameObject("Inventory Panel", typeof(RectTransform));
            panel.transform.SetParent(go.transform, false);
            owned.Add(panel);

            var prefabGo = new GameObject("Slot", typeof(RectTransform), typeof(InventorySlotUI));
            owned.Add(prefabGo);

            var ui = go.GetComponent<EquipmentUI>();
            SetPrivateField(ui, "inventoryPanel", panel.transform);
            SetPrivateField(ui, "inventorySlotPrefab", prefabGo.GetComponent<InventorySlotUI>());

            var inventory = CreateInventory();
            ui.SetInventory(inventory);
            typeof(EquipmentUI)
                .GetMethod("RefreshInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(ui, null);

            Assert.AreEqual(20, panel.transform.childCount,
                "An inventory grid must stay a fixed size, padding the remainder with blank cells.");
        }

        private static void SetPrivateField(EquipmentUI ui, string field, object value)
        {
            typeof(EquipmentUI)
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(ui, value);
        }

        [Test]
        public void EquipmentUI_TabClick_ShowsOnlyItsPageAndSwapsSprites()
        {
            var go = CreateEquipmentUIGo();
            go.SetActive(false);

            var inventoryPage = AddPage(go, "Inventory Slots");
            var deckPage = AddPage(go, "Decklist");
            var specialPage = AddPage(go, "Special Page");

            var bookmarks = new GameObject("Bookmarks", typeof(RectTransform));
            bookmarks.transform.SetParent(go.transform, false);
            owned.Add(bookmarks);

            Sprite invNormal = MakeSprite("Tab01_Right_Normal");
            Sprite invSelected = MakeSprite("Tab01_Right_Selected");
            Sprite deckNormal = MakeSprite("Tab05_Right_Normal");
            Sprite deckSelected = MakeSprite("Tab05_Right_Selected");
            Sprite specialNormal = MakeSprite("Tab03_Right_Normal");
            Sprite specialSelected = MakeSprite("Tab03_Right_Selected");

            GameObject invTab = AddTab(bookmarks, "Inventory Tab", "Inventory Slots", invNormal, invSelected);
            GameObject deckTab = AddTab(bookmarks, "Decklist Tab", "Decklist", deckNormal, deckSelected);
            GameObject specialTab = AddTab(bookmarks, "Special Tab", "Special", specialNormal, specialSelected);

            var ui = go.GetComponent<EquipmentUI>();
            ui.SelectPage("Inventory Slots");

            Assert.IsTrue(inventoryPage.activeSelf, "The selected page must be visible.");
            Assert.IsFalse(deckPage.activeSelf, "Unselected pages must be hidden.");
            Assert.IsFalse(specialPage.activeSelf, "Unselected pages must be hidden.");
            Assert.AreSame(invSelected, invTab.GetComponent<Image>().sprite,
                "The selected tab must show its selected sprite.");
            Assert.AreSame(deckNormal, deckTab.GetComponent<Image>().sprite,
                "Unselected tabs must show their normal sprite.");

            deckTab.GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(deckPage.activeSelf, "Clicking a tab must show its page.");
            Assert.IsFalse(inventoryPage.activeSelf, "Clicking a tab must hide the other pages.");
            Assert.IsFalse(specialPage.activeSelf, "Clicking a tab must hide the other pages.");
            Assert.AreSame(deckSelected, deckTab.GetComponent<Image>().sprite,
                "The clicked tab must switch to its selected sprite.");
            Assert.AreSame(invNormal, invTab.GetComponent<Image>().sprite,
                "The previously selected tab must revert to its normal sprite.");
            Assert.AreSame(specialNormal, specialTab.GetComponent<Image>().sprite);
        }

        [Test]
        public void EquipmentUI_EnsureDeckList_ParentsUnderDecklistPage()
        {
            var go = CreateEquipmentUIGo();
            go.SetActive(false);

            var page = new GameObject("Decklist", typeof(RectTransform), typeof(GridLayoutGroup));
            page.transform.SetParent(go.transform, false);
            owned.Add(page);

            var ui = go.GetComponent<EquipmentUI>();
            typeof(EquipmentUI)
                .GetMethod("EnsureDeckList", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(ui, null);

            Transform deck = page.transform.Find("Deck List");
            Assert.IsNotNull(deck, "EnsureDeckList must create the Deck List inside the Decklist page.");
            Assert.IsNotNull(deck.GetComponent<DeckListUI>());
            Assert.IsFalse(page.GetComponent<GridLayoutGroup>().enabled,
                "The page's grid layout must be disabled so the deck list can lay itself out.");

            var rect = (RectTransform)deck;
            Assert.AreEqual(Vector2.zero, rect.anchorMin, "The deck list must fill the page.");
            Assert.AreEqual(Vector2.one, rect.anchorMax, "The deck list must fill the page.");
        }

        [Test]
        public void TabButton_SetSelected_SwapsNormalAndSelectedSprites()
        {
            var tab = new GameObject("Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            owned.Add(tab);
            var image = tab.GetComponent<Image>();
            Sprite normal = MakeSprite("Tab02_Right_Normal");
            Sprite selected = MakeSprite("Tab02_Right_Selected");
            image.sprite = normal;

            var tabButton = tab.AddComponent<TabButton>();
            SetSpriteField(tabButton, "normalSprite", normal);
            SetSpriteField(tabButton, "selectedSprite", selected);

            tabButton.SetSelected(true);
            Assert.AreSame(selected, image.sprite, "Selecting a tab must show its selected sprite.");
            tabButton.SetSelected(false);
            Assert.AreSame(normal, image.sprite, "Deselecting a tab must show its normal sprite.");
        }

        // ---- helpers ----

        private GameObject AddPage(GameObject parent, string name)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent.transform, false);
            owned.Add(page);
            return page;
        }

        private GameObject AddTab(GameObject bookmarks, string tabName, string pageName,
            Sprite normal, Sprite selected)
        {
            var tab = new GameObject(tabName, typeof(RectTransform), typeof(Image), typeof(Button));
            tab.transform.SetParent(bookmarks.transform, false);
            var image = tab.GetComponent<Image>();
            image.sprite = normal;
            tab.GetComponent<Button>().targetGraphic = image;

            var tabButton = tab.AddComponent<TabButton>();
            tabButton.SetPageName(pageName);
            SetSpriteField(tabButton, "normalSprite", normal);
            SetSpriteField(tabButton, "selectedSprite", selected);

            owned.Add(tab);
            return tab;
        }

        private static void SetSpriteField(TabButton tabButton, string fieldName, Sprite sprite)
        {
            typeof(TabButton)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(tabButton, sprite);
        }

        private Sprite MakeSprite(string name)
        {
            var texture = new Texture2D(2, 2);
            ownedAssets.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            ownedAssets.Add(sprite);
            return sprite;
        }
    }
}
