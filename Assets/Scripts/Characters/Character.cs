using System;
using System.Collections.Generic;
using UnityEngine;
using HexGrid;
using Cards;
using Equipment;

namespace Characters
{
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

        public event Action<Character> OnDefeated;
        public event Action<HexCell> OnMovedToCell;

        private Renderer[] renderers;
        private readonly List<(Renderer renderer, Color originalColor)> originalColors = new List<(Renderer, Color)>();
        private static readonly int colorID = Shader.PropertyToID("_Color");
        private static readonly int baseColorID = Shader.PropertyToID("_BaseColor");

        public void Initialize(int ownerPlayerId, HexCell startingCell, int startingHandSize = 3)
        {
            OwnerPlayerId = ownerPlayerId;
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
        }

        public void SetHighlighted(Color highlightColor)
        {
            ClearHighlight();
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();

            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Color original = r.material.color;
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
                block.SetColor(colorID, entry.originalColor);
                block.SetColor(baseColorID, entry.originalColor);
                entry.renderer.SetPropertyBlock(block);
            }
            originalColors.Clear();
        }
    }
}
