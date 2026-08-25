using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Cards;

namespace UI
{
    /// <summary>
    /// Lists every card in the player's deck (all cards from the equipped loadout), with
    /// duplicate cards condensed into "Nx Name" entries, sorted by cost ascending then
    /// alphabetically on ties. Built entirely in code as a child of the equipment UI, so no
    /// scene wiring is needed. Hovering a row previews that card as a real Card prefab
    /// instance (see CardPreview).
    /// </summary>
    public class DeckListUI : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject uiPrefab;

        [Header("Background")]
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        [Header("Header")]
        [SerializeField] private string headerLabel = "Deck";
        [SerializeField] private int headerFontSize = 24;
        [SerializeField] private Vector2 headerOffset = new Vector2(0, -6);
        [SerializeField] private float headerHeight = 30f;

        [Header("List")]
        [SerializeField] private Vector2 listOffsetMin = new Vector2(8, 8);
        [SerializeField] private Vector2 listOffsetMax = new Vector2(-8, -40);
        [SerializeField] private float rowSpacing = 4f;
        [SerializeField] private int listPadding = 4;

        [Header("Rows")]
        [SerializeField] private Color rowColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private float rowHeight = 52f;

        // Upper bound for row text auto-sizing so labels fill the doubled row height.
        [SerializeField] private int rowFontSize = 32;
        [SerializeField] private Color rowTextColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private float rowTextPadding = 8f;

        private RectTransform listContent;
        private readonly List<GameObject> rows = new List<GameObject>();
        private CardView cardPrefab;
        private static GameObject staticPrefab;

        /// <summary>
        /// Card prefab used for hover previews. Normally injected by EquipmentUI from the
        /// inventory slot prefab so there is exactly one place the Card prefab is assigned.
        /// </summary>
        public void SetCardPrefab(CardView prefab)
        {
            cardPrefab = prefab;
        }

        private void Awake()
        {
            if (staticPrefab == null && uiPrefab != null) staticPrefab = uiPrefab;
        }

        /// <summary>A condensed deck entry: one card plus how many copies are in the deck.</summary>
        public struct DeckEntry
        {
            public CardData Card;
            public int Count;
        }

        /// <summary>
        /// Instantiates the deck list from the UI prefab, falling back to a plain GameObject
        /// if the prefab is missing.
        /// </summary>
        public static DeckListUI Create()
        {
            var existing = FindAnyObjectByType<DeckListUI>();
            if (existing != null)
                return existing;
            if (staticPrefab != null)
                return Object.Instantiate(staticPrefab).GetComponent<DeckListUI>();
            var go = new GameObject("Deck List", typeof(RectTransform), typeof(DeckListUI));
            return go.GetComponent<DeckListUI>();
        }

        /// <summary>
        /// Condenses a card set into unique entries (counting copies of the same card name),
        /// sorted by cost ascending, then by name (case-insensitive ordinal) on ties.
        /// Null cards are dropped. Extracted as a static helper so it's unit-testable.
        /// </summary>
        public static List<DeckEntry> CondenseDeck(IEnumerable<CardData> cards)
        {
            if (cards == null) return new List<DeckEntry>();
            return cards
                .Where(c => c != null)
                .GroupBy(c => c.CardName)
                .Select(g => new DeckEntry { Card = g.First(), Count = g.Count() })
                .OrderBy(e => e.Card.Cost)
                .ThenBy(e => e.Card.CardName, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void Refresh(List<CardData> cards)
        {
            if (listContent == null) BuildUI();

            foreach (GameObject row in rows)
            {
                if (row != null) Destroy(row);
            }
            rows.Clear();

            foreach (DeckEntry entry in CondenseDeck(cards))
                rows.Add(CreateRow(entry.Card, entry.Count));
        }

        private void BuildUI()
        {
            if (GetComponent<Image>() == null)
            {
                var bg = gameObject.AddComponent<Image>();
                bg.color = backgroundColor;
                bg.raycastTarget = true;
            }

            var headerObj = new GameObject("Header", typeof(TextMeshProUGUI));
            headerObj.transform.SetParent(transform, false);
            var header = headerObj.GetComponent<TextMeshProUGUI>();
            header.text = headerLabel;
            header.fontSize = headerFontSize;
            header.fontStyle = FontStyles.Bold;
            header.alignment = TextAlignmentOptions.Center;
            header.color = Color.white;
            var headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = headerOffset;
            headerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headerHeight);

            var listObj = new GameObject("Cards", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listObj.transform.SetParent(transform, false);
            listContent = listObj.GetComponent<RectTransform>();
            listContent.anchorMin = new Vector2(0, 0);
            listContent.anchorMax = new Vector2(1, 1);
            listContent.offsetMin = listOffsetMin;
            listContent.offsetMax = listOffsetMax;

            var layout = listObj.GetComponent<VerticalLayoutGroup>();
            layout.spacing = rowSpacing;
            layout.padding = new RectOffset(listPadding, listPadding, listPadding, listPadding);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
        }

        private GameObject CreateRow(CardData card, int count)
        {
            // RectTransform must come first — adding a UGUI component before it makes
            // Unity auto-create a RectTransform and then reject the explicit one.
            var row = new GameObject("CardRow", typeof(RectTransform), typeof(Image), typeof(RowHoverHandler));
            row.transform.SetParent(listContent, false);

            var image = row.GetComponent<Image>();
            image.color = rowColor;
            image.raycastTarget = true;

            var rt = row.GetComponent<RectTransform>();
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);

            var textObj = new GameObject("Text", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(row.transform, false);
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = $"{count}x {card.CardName}";
            // Auto-size so the label fills the row height regardless of length.
            text.enableAutoSizing = true;
            text.fontSizeMin = 10;
            text.fontSizeMax = rowFontSize;
            text.alignment = TextAlignmentOptions.Left;
            text.color = rowTextColor;
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(rowTextPadding, 0);
            textRect.offsetMax = new Vector2(-rowTextPadding, 0);

            row.GetComponent<RowHoverHandler>().Setup(card, cardPrefab);
            return row;
        }

        /// <summary>
        /// Hovering or right-clicking a deck entry previews that card as a real Card
        /// prefab instance shown beside the row.
        /// </summary>
        private class RowHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
            IPointerClickHandler
        {
            private CardData card;
            private CardView cardPrefab;

            public void Setup(CardData card, CardView cardPrefab)
            {
                this.card = card;
                this.cardPrefab = cardPrefab;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                ShowPreview();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                CardPreview.Hide(this);
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Right) return;
                ShowPreview();
            }

            private void ShowPreview()
            {
                if (card == null)
                {
                    Debug.LogWarning("[DeckPreview] Row preview skipped: row has no card.");
                    return;
                }
                // Anchor to the row itself so the preview sits beside it instead of
                // following the cursor.
                CardPreview.Show(cardPrefab, new[] { card }, (RectTransform)transform, this);
            }
        }
    }
}
