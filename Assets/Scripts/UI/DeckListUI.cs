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
    /// scene wiring is needed. Right-clicking a row opens a tooltip with that card's cost
    /// and description.
    /// </summary>
    public class DeckListUI : MonoBehaviour
    {
        private RectTransform listContent;
        private readonly List<GameObject> rows = new List<GameObject>();

        /// <summary>A condensed deck entry: one card plus how many copies are in the deck.</summary>
        public struct DeckEntry
        {
            public CardData Card;
            public int Count;
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
                bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
                bg.raycastTarget = true;
            }

            var headerObj = new GameObject("Header", typeof(TextMeshProUGUI));
            headerObj.transform.SetParent(transform, false);
            var header = headerObj.GetComponent<TextMeshProUGUI>();
            header.text = "Deck";
            header.fontSize = 24;
            header.fontStyle = FontStyles.Bold;
            header.alignment = TextAlignmentOptions.Center;
            header.color = Color.white;
            var headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = new Vector2(0, -6);
            headerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 30);

            var listObj = new GameObject("Cards", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listObj.transform.SetParent(transform, false);
            listContent = listObj.GetComponent<RectTransform>();
            listContent.anchorMin = new Vector2(0, 0);
            listContent.anchorMax = new Vector2(1, 1);
            listContent.offsetMin = new Vector2(8, 8);
            listContent.offsetMax = new Vector2(-8, -40);

            var layout = listObj.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
        }

        private GameObject CreateRow(CardData card, int count)
        {
            var row = new GameObject("CardRow", typeof(Image), typeof(RectTransform), typeof(RowClickHandler));
            row.transform.SetParent(listContent, false);

            var image = row.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            image.raycastTarget = true;

            var rt = row.GetComponent<RectTransform>();
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 26);

            var textObj = new GameObject("Text", typeof(TextMeshProUGUI));
            textObj.transform.SetParent(row.transform, false);
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = $"{count}x {card.CardName} ({card.Cost})";
            text.fontSize = 17;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);

            row.GetComponent<RowClickHandler>().Setup(card);
            return row;
        }

        /// <summary>Right-clicking a deck entry opens the card tooltip (name, cost, description).</summary>
        private class RowClickHandler : MonoBehaviour, IPointerClickHandler
        {
            private CardData card;

            public void Setup(CardData card)
            {
                this.card = card;
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Right) return;
                if (card == null) return;
                Tooltip.ShowCard(card, eventData.position);
            }
        }
    }
}
