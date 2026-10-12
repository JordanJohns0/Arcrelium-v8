using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;
using Cards;
using Equipment;
using UI;

namespace Tests.UI
{
    public class CardPreviewTests
    {
        private static readonly FieldInfo spawnedField =
            typeof(CardPreview).GetField("spawned", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo canvasRootField =
            typeof(CardPreview).GetField("canvasRoot", BindingFlags.NonPublic | BindingFlags.Static);

        private readonly List<GameObject> owned = new List<GameObject>();
        private readonly List<Object> ownedAssets = new List<Object>();
        private int ownerCounter;

        [SetUp]
        public void SetUp()
        {
            // Tests may leave state behind on failure; always start clean.
            CardPreview.Hide();
        }

        [TearDown]
        public void TearDown()
        {
            CardPreview.Hide();
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
        public void Show_CreatesOneCardViewPerGrantedCard()
        {
            var prefab = CreateCardPrefab();
            var cards = new List<CardData> { MakeCard("A"), MakeCard("B") };

            CardPreview.Show(prefab, cards, CreateAnchor(new Vector2(60f, 200f)), NextOwner());

            Assert.AreEqual(3, SpawnedCount(), "Two card instances plus the column container.");
        }

        [Test]
        public void Show_DisablesRaycastsSoItNeverBlocksInput()
        {
            var prefab = CreateCardPrefab();

            CardPreview.Show(prefab, new List<CardData> { MakeCard("A") },
                CreateAnchor(new Vector2(60f, 200f)), NextOwner());

            Assert.IsNotNull(GetCanvasRoot(), "Showing a preview must create the overlay canvas.");
            foreach (Graphic graphic in GetCanvasRoot().GetComponentsInChildren<Graphic>(true))
            {
                Assert.IsFalse(graphic.raycastTarget,
                    $"{graphic.name} must not intercept pointer input.");
            }
        }

        [Test]
        public void Show_EmptyCardList_ShowsNothing()
        {
            var prefab = CreateCardPrefab();

            CardPreview.Show(prefab, new List<CardData>(), CreateAnchor(new Vector2(60f, 200f)), NextOwner());

            Assert.AreEqual(0, SpawnedCount());
        }

        [Test]
        public void Show_NullPrefab_ShowsNothing()
        {
            CardPreview.Show(null, new List<CardData> { MakeCard("A") },
                CreateAnchor(new Vector2(60f, 200f)), NextOwner());

            Assert.AreEqual(0, SpawnedCount());
        }

        [Test]
        public void Show_PositionsBesideAnchor_VerticallyCentred_WithoutCoveringIt()
        {
            var prefab = CreateCardPrefab();
            // 160x48 box centred at (60, 200): right edge x=140, vertical centre y=200.
            RectTransform anchor = CreateAnchor(new Vector2(60f, 200f), 160f, 48f);

            CardPreview.Show(prefab, new List<CardData> { MakeCard("A") }, anchor, NextOwner());

            RectTransform column = GetColumnRect();
            Assert.IsNotNull(column, "Showing must spawn the preview column.");
            Assert.AreEqual(new Vector2(0f, 0.5f), column.pivot,
                "Default placement pivots off the anchor's RIGHT edge.");

            Vector3[] corners = new Vector3[4];
            column.GetWorldCorners(corners);
            Assert.GreaterOrEqual(corners[0].x, 140f,
                "The preview must start at or beyond the hovered row's right edge.");
            Assert.AreEqual(200f, (corners[0].y + corners[2].y) * 0.5f, 0.5f,
                "The preview must be vertically centred on the row.");
        }

        [Test]
        public void Show_AnchorAtRightEdge_FlipsToLeftWithoutCoveringIt()
        {
            var prefab = CreateCardPrefab();
            float anchorLeftX = Screen.width - 100f;
            // Box whose right edge sits past the screen: right-side placement can't fit.
            RectTransform anchor =
                CreateAnchor(new Vector2(Screen.width - 20f, 200f), 160f, 48f);

            CardPreview.Show(prefab, new List<CardData> { MakeCard("A") }, anchor, NextOwner());

            RectTransform column = GetColumnRect();
            Assert.IsNotNull(column);
            Assert.AreEqual(new Vector2(1f, 0.5f), column.pivot,
                "Near the screen edge the preview must flip to the anchor's LEFT edge.");

            Vector3[] corners = new Vector3[4];
            column.GetWorldCorners(corners);
            Assert.LessOrEqual(corners[2].x, anchorLeftX,
                "The flipped preview must end before the hovered row's left edge.");
            Assert.AreEqual(200f, (corners[0].y + corners[2].y) * 0.5f, 0.5f,
                "The flipped preview must still be vertically centred on the row.");
        }

        [Test]
        public void Show_AnchorNearBottom_ClampsPreviewInsideScreen()
        {
            var prefab = CreateCardPrefab();
            // Row hugging the bottom edge: a purely centred preview hangs off-screen.
            RectTransform anchor = CreateAnchor(new Vector2(60f, 20f), 160f, 48f);

            CardPreview.Show(prefab, new List<CardData> { MakeCard("A") }, anchor, NextOwner());

            RectTransform column = GetColumnRect();
            Assert.IsNotNull(column);
            Vector3[] corners = new Vector3[4];
            column.GetWorldCorners(corners);
            // ScreenMargin is a private const of 8 — keep this in sync if it changes.
            Assert.GreaterOrEqual(corners[0].y, 7.5f,
                "A preview beside a bottom-edge row must stay fully on-screen.");
        }

        [Test]
        public void Show_AnchorNearTop_ClampsPreviewInsideScreen()
        {
            var prefab = CreateCardPrefab();
            RectTransform anchor = CreateAnchor(new Vector2(60f, Screen.height - 20f), 160f, 48f);

            CardPreview.Show(prefab, new List<CardData> { MakeCard("A") }, anchor, NextOwner());

            RectTransform column = GetColumnRect();
            Assert.IsNotNull(column);
            Vector3[] corners = new Vector3[4];
            column.GetWorldCorners(corners);
            Assert.LessOrEqual(corners[2].y, Screen.height - 7.5f,
                "A preview beside a top-edge row must stay fully on-screen.");
        }

        [Test]
        public void Hide_ByOwner_DestroysPreview()
        {
            var prefab = CreateCardPrefab();
            Object owner = NextOwner();
            CardPreview.Show(prefab, new List<CardData> { MakeCard("A") },
                CreateAnchor(new Vector2(60f, 200f)), owner);

            CardPreview.Hide(owner);

            Assert.AreEqual(0, SpawnedCount());
        }

        [Test]
        public void Hide_ByDifferentOwner_PreviewStaysUp()
        {
            var prefab = CreateCardPrefab();
            Object owner = NextOwner();
            CardPreview.Show(prefab, new List<CardData> { MakeCard("A") },
                CreateAnchor(new Vector2(60f, 200f)), owner);

            CardPreview.Hide(NextOwner()); // another slot's exit must not dismiss it

            Assert.AreEqual(2, SpawnedCount(),
                "A slot leaving hover must not close the preview another slot just opened.");
        }

        [Test]
        public void DeckRow_Hover_ShowsCardPreview()
        {
            var prefab = CreateCardPrefab();
            var deckGo = new GameObject("DeckList", typeof(RectTransform), typeof(DeckListUI));
            owned.Add(deckGo);
            var deckList = deckGo.GetComponent<DeckListUI>();
            deckList.SetCardPrefab(prefab);
            deckList.Refresh(new List<CardData> { MakeCard("Deck Card") });

            var handler = FindRowHandler(deckGo);
            Assert.IsNotNull(handler, "Each deck row must have a hover handler.");

            ((IPointerEnterHandler)handler).OnPointerEnter(new PointerEventData(EventSystem.current));
            Assert.AreEqual(2, SpawnedCount(), "Hovering a deck row must preview its card.");

            ((IPointerExitHandler)handler).OnPointerExit(new PointerEventData(EventSystem.current));
            Assert.AreEqual(0, SpawnedCount(), "Leaving the row must dismiss the preview.");
        }

        [Test]
        public void DeckRow_RightClick_ShowsCardPreview()
        {
            var prefab = CreateCardPrefab();
            var deckGo = new GameObject("DeckList", typeof(RectTransform), typeof(DeckListUI));
            owned.Add(deckGo);
            var deckList = deckGo.GetComponent<DeckListUI>();
            deckList.SetCardPrefab(prefab);
            deckList.Refresh(new List<CardData> { MakeCard("Deck Card") });

            var handler = FindRowHandler(deckGo);
            Assert.IsNotNull(handler, "Each deck row must have a hover handler.");

            var click = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Right
            };
            ((IPointerClickHandler)handler).OnPointerClick(click);

            Assert.AreEqual(2, SpawnedCount(), "Right-clicking a deck row must preview its card.");
        }

        [Test]
        public void InventorySlot_RightClick_DoesNotShowCardPreview()
        {
            var prefab = CreateCardPrefab();
            var slotGo = new GameObject("Slot", typeof(RectTransform), typeof(InventorySlotUI));
            owned.Add(slotGo);
            InventorySlotUI slot = slotGo.GetComponent<InventorySlotUI>();

            // Prefab is available, so any preview would be a real behavior, not a null guard.
            typeof(InventorySlotUI)
                .GetField("cardPreviewPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(slot, prefab);

            var item = ScriptableObject.CreateInstance<EquipmentData>();
            item.Cards = new List<CardData> { MakeCard("Item Card") };
            ownedAssets.Add(item);
            slot.Setup(item, null);

            var click = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Right
            };
            ((IPointerClickHandler)slot).OnPointerClick(click);

            Assert.AreEqual(0, SpawnedCount(),
                "Inventory slots use the text tooltip; card previews belong to deck rows only.");
        }

        [Test]
        public void InventorySlotPrefab_CardPreviewPrefabResolves()
        {
            // Loads the real asset so a mis-wired YAML reference (child object, root
            // GameObject instead of the CardView component, missing guid, ...) resolves
            // to null here instead of silently killing previews at runtime.
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Inventory Slot.prefab");
            try
            {
                var slot = root.GetComponent<InventorySlotUI>();
                Assert.IsNotNull(slot, "The prefab must carry an InventorySlotUI.");
                Assert.IsNotNull(slot.CardPreviewPrefab,
                    "cardPreviewPrefab must resolve to a CardView component. " +
                    "For component-typed fields the YAML fileID must be the component's id, " +
                    "not the GameObject's.");

                CardView view = slot.CardPreviewPrefab;
                Assert.AreSame(view.transform, view.transform.root,
                    "cardPreviewPrefab must point at the CardView on its prefab's ROOT object, not a child.");
                string assetPath = AssetDatabase.GetAssetPath(view.gameObject);
                Assert.True(assetPath.EndsWith("Card.prefab"),
                    $"cardPreviewPrefab must come from the Card prefab, but resolved from '{assetPath}'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void EquipmentUI_ForwardsCardPrefabToDeckList()
        {
            CardView cardView = CreateCardPrefab();
            var slotPrefabGo = new GameObject("SlotPrefab", typeof(RectTransform), typeof(InventorySlotUI));
            owned.Add(slotPrefabGo);
            InventorySlotUI slotPrefab = slotPrefabGo.GetComponent<InventorySlotUI>();
            typeof(InventorySlotUI)
                .GetField("cardPreviewPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(slotPrefab, cardView);

            var uiGo = new GameObject("EquipmentUI", typeof(RectTransform), typeof(EquipmentUI));
            owned.Add(uiGo);
            // Inactive so Awake's SetActive(false)/OnEnable's InitializeSlots don't run
            // against unassigned slot references.
            uiGo.SetActive(false);
            EquipmentUI ui = uiGo.AddComponent<EquipmentUI>();
            typeof(EquipmentUI)
                .GetField("inventorySlotPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(ui, slotPrefab);

            // The deck list lives inside the "Decklist" page (which ships with a grid layout).
            var deckPage = new GameObject("Decklist", typeof(RectTransform), typeof(GridLayoutGroup));
            deckPage.transform.SetParent(uiGo.transform, false);
            owned.Add(deckPage);

            typeof(EquipmentUI)
                .GetMethod("EnsureDeckList", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(ui, null);

            Transform deckTransform = deckPage.transform.Find("Deck List");
            Assert.IsNotNull(deckTransform, "EnsureDeckList must create the Deck List under the Decklist page.");
            Assert.IsFalse(deckPage.GetComponent<GridLayoutGroup>().enabled,
                "The Decklist page's grid layout must be disabled so the deck list can lay itself out.");
            var deckList = deckTransform.GetComponent<DeckListUI>();
            Assert.IsNotNull(deckList);

            var cardPrefabField = typeof(DeckListUI).GetField("cardPrefab",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreSame(cardView, cardPrefabField.GetValue(deckList),
                "EquipmentUI must forward the slot prefab's Card view to the deck list.");

            // The played-card notification display must receive the same Card prefab so
            // plays render as real cards instead of the name-only fallback. Its field
            // stores the prefab's GameObject (it instantiates it directly).
            PlayedCardDisplay popup = Object.FindAnyObjectByType<PlayedCardDisplay>();
            Assert.IsNotNull(popup, "EnsureDeckList must make the played-card display available.");
            owned.Add(popup.gameObject);
            var popupPrefabField = typeof(PlayedCardDisplay).GetField("cardPrefab",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreSame(cardView.gameObject, popupPrefabField.GetValue(popup),
                "EquipmentUI must forward the slot prefab's Card view to the played-card display.");
        }

        private static MonoBehaviour FindRowHandler(GameObject deckGo)
        {
            foreach (MonoBehaviour behaviour in deckGo.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IPointerEnterHandler && behaviour.GetType().Name == "RowHoverHandler")
                    return behaviour;
            }
            return null;
        }

        // ---- helpers ----

        private CardView CreateCardPrefab()
        {
            var go = new GameObject("CardPrefab", typeof(RectTransform), typeof(Button), typeof(CardView));
            var art = new GameObject("Artwork", typeof(Image));
            art.transform.SetParent(go.transform, false);
            owned.Add(go);

            CardView view = go.GetComponent<CardView>();
            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = "Preview Source";
            ownedAssets.Add(data);

            view.Setup(new CardInstance(data));
            return view;
        }

        private CardData MakeCard(string name)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = name;
            data.Steps = new List<CardEffectStep>();
            ownedAssets.Add(data);
            return data;
        }

        private Object NextOwner()
        {
            var go = new GameObject($"Owner{ownerCounter++}");
            owned.Add(go);
            return go;
        }

        /// <summary>
        /// A screen-space box standing in for a hovered row. Anchored to the origin so its
        /// world corners map 1:1 onto screen pixels in edit mode.
        /// </summary>
        private RectTransform CreateAnchor(Vector2 centre, float width = 160f, float height = 48f)
        {
            var go = new GameObject("Anchor", typeof(RectTransform));
            owned.Add(go);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = centre;
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private RectTransform GetColumnRect()
        {
            GameObject root = GetCanvasRoot();
            return root == null ? null : root.transform.Find("CardPreview") as RectTransform;
        }

        private GameObject GetCanvasRoot() => (GameObject)canvasRootField.GetValue(null);

        private int SpawnedCount()
        {
            return ((List<Object>)spawnedField.GetValue(null)).Count;
        }
    }
}
