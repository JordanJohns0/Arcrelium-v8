using System;
using System.Collections.Generic;
using UnityEngine;
using HexGrid;
using Cards;
using Equipment;

namespace Characters
{
    public enum Team { Ally, Enemy }

    /// <summary>
    /// A character on the board. Holds its stats, current cell, and its card deck/hand.
    /// Characters never act on their own — every action comes from a card being played
    /// through this character (see ActionResolver).
    /// </summary>
    public class Character : MonoBehaviour
    {
        [SerializeField] private CharacterStats stats = new CharacterStats();
        [SerializeField] private Deck deck;
        [SerializeField] private EquipmentManager equipmentManager;

        public CharacterStats Stats => stats;
        public Deck Deck => deck;
        public EquipmentManager Equipment => equipmentManager;
        public Hand Hand { get; private set; }
        public HexCell CurrentCell { get; private set; }
        public int OwnerPlayerId { get; private set; }
        public int TeamId { get; private set; }
        public Team Team => TeamId == 0 ? Team.Ally : Team.Enemy;
        public Color? PlayerColor { get; private set; }

        public event Action<Character> OnDefeated;
        public event Action<HexCell> OnMovedToCell;

        public static event Action<Character> OnCharacterCreated;
        public static event Action<Character> OnCharacterDestroyed;

        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<(Renderer renderer, Color originalColor)> originalColors = new List<(Renderer, Color)>();
        private static readonly int colorID = Shader.PropertyToID("_Color");
        private static readonly int baseColorID = Shader.PropertyToID("_BaseColor");

        public void Initialize(int ownerPlayerId, HexCell startingCell, int startingHandSize = 1, int teamId = -1)
        {
            OwnerPlayerId = ownerPlayerId;
            TeamId = teamId >= 0 ? teamId : (ownerPlayerId == 0 ? 0 : 1);
            stats.Initialize();
            Hand = new Hand();

            PlaceOnCell(startingCell);

            if (equipmentManager != null)
                equipmentManager.Initialize(this);

            if (deck != null)
            {
                List<CardData> equipmentCards = equipmentManager != null
                    ? equipmentManager.GetAllCards()
                    : new List<CardData>();

                deck.SetStartingCards(equipmentCards);
                deck.Shuffle();
                DrawCards(startingHandSize);
            }

            OnCharacterCreated?.Invoke(this);
        }

        public void PlaceOnCell(HexCell cell)
        {
            if (CurrentCell != null)
            {
                CurrentCell.ClearOccupant();
            }

            CurrentCell = cell;
            cell.SetOccupant(this);
            transform.position = cell.transform.position;
        }

        /// <summary>
        /// Updates cell occupancy and logical position without moving the transform.
        /// Used by animated movement effects.
        /// </summary>
        public void SetCellOnly(HexCell cell)
        {
            if (CurrentCell != null)
            {
                CurrentCell.ClearOccupant();
            }

            CurrentCell = cell;
            cell.SetOccupant(this);
            OnMovedToCell?.Invoke(cell);
        }

        public void DrawCards(int count)
        {
            if (deck == null) return;
            for (int i = 0; i < count; i++)
            {
                if (Hand.IsFull) break;
                CardInstance card = deck.Draw();
                if (card != null)
                {
                    Hand.AddCard(card);
                }
            }
        }

        public void TakeDamage(int amount)
        {
            stats.ApplyDamage(amount);
            if (!stats.IsAlive)
            {
                Die();
            }
        }

        private void Die()
        {
            CurrentCell?.ClearOccupant();
            OnDefeated?.Invoke(this);
            if (Application.isPlaying)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            OnCharacterDestroyed?.Invoke(this);
        }

        public void SetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            stats.CharacterName = name;
            stats.NotifyChanged();
        }

        /// <summary>
        /// Replaces the hand with freshly created cards matching the given names,
        /// preserving the Hand instance so bound UI refreshes in place.
        /// </summary>
        public void RebuildHand(string[] cardNames)
        {
            foreach (CardInstance card in new List<CardInstance>(Hand.Cards))
                Hand.RemoveCard(card);

            if (cardNames == null || deck == null) return;
            foreach (string name in cardNames)
            {
                CardData data = deck.GetCardDataByName(name);
                if (data != null)
                    Hand.AddCard(new CardInstance(data));
            }
        }

        public void SetPlayerColor(Color color)
        {
            PlayerColor = color;
            if (renderers.Count == 0) renderers.AddRange(GetComponentsInChildren<Renderer>());
            ApplyColor(color);
            stats.NotifyChanged();
        }

        private void ApplyColor(Color color)
        {
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor(colorID, color);
                block.SetColor(baseColorID, color);
                r.SetPropertyBlock(block);
            }
        }

        public void SetHighlighted(Color highlightColor)
        {
            ClearHighlight();
            if (renderers.Count == 0) renderers.AddRange(GetComponentsInChildren<Renderer>());

            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Color original = PlayerColor ?? r.material.color;
                originalColors.Add((r, original));

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor(colorID, highlightColor);
                block.SetColor(baseColorID, highlightColor);
                r.SetPropertyBlock(block);
            }
        }

        public void ClearHighlight()
        {
            foreach (var entry in originalColors)
            {
                if (entry.renderer == null) continue;
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                entry.renderer.GetPropertyBlock(block);
                Color restore = PlayerColor ?? entry.originalColor;
                block.SetColor(colorID, restore);
                block.SetColor(baseColorID, restore);
                entry.renderer.SetPropertyBlock(block);
            }
            originalColors.Clear();
        }
    }
}
