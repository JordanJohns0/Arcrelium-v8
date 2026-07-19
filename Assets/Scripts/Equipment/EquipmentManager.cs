using System;
using System.Collections.Generic;
using UnityEngine;
using FantasyBrawl.Characters;
using FantasyBrawl.Cards;
using FantasyBrawl.HexGrid;
using FantasyBrawl.Game;

namespace FantasyBrawl.Equipment
{
    /// <summary>
    /// Manages a character's equipped items. Handles equipping/unequipping, building the
    /// deck from equipped cards, and firing equipment triggers at the appropriate times.
    /// Attach to the same GameObject as Character.
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private HexGridManager grid;

        [Tooltip("Maximum number of equipment per slot type. Hand and Trinket allow multiple.")]
        [SerializeField] private int maxHandSlots = 2;
        [SerializeField] private int maxTrinketSlots = 3;

        private Character character;

        private EquipmentInstance headSlot;
        private EquipmentInstance chestSlot;
        private EquipmentInstance legsSlot;
        private EquipmentInstance feetSlot;
        private readonly List<EquipmentInstance> handSlots = new List<EquipmentInstance>();
        private readonly List<EquipmentInstance> trinketSlots = new List<EquipmentInstance>();

        public Character Character => character;

        public event Action<EquipmentInstance> OnEquipmentChanged;

        public void Initialize(Character character)
        {
            this.character = character;

            if (turnManager != null)
            {
                turnManager.OnTurnStarted += HandleTurnStarted;
                turnManager.OnTurnEnded += HandleTurnEnded;
            }
        }

        public bool Equip(EquipmentData data, EquipmentSlotType? forcedSlot = null)
        {
            if (data == null) return false;

            EquipmentSlotType slot = forcedSlot ?? data.SlotType;

            if (!CanEquip(data, slot)) return false;

            var instance = new EquipmentInstance(data);

            switch (slot)
            {
                case EquipmentSlotType.Head:
                    headSlot = instance;
                    break;
                case EquipmentSlotType.Chest:
                    chestSlot = instance;
                    break;
                case EquipmentSlotType.Legs:
                    legsSlot = instance;
                    break;
                case EquipmentSlotType.Feet:
                    feetSlot = instance;
                    break;
                case EquipmentSlotType.Hand:
                    handSlots.Add(instance);
                    break;
                case EquipmentSlotType.Trinket:
                    trinketSlots.Add(instance);
                    break;
            }

            OnEquipmentChanged?.Invoke(instance);
            return true;
        }

        public bool Unequip(EquipmentSlotType slot, int index = 0)
        {
            EquipmentInstance removed = null;

            switch (slot)
            {
                case EquipmentSlotType.Head:
                    removed = headSlot;
                    headSlot = null;
                    break;
                case EquipmentSlotType.Chest:
                    removed = chestSlot;
                    chestSlot = null;
                    break;
                case EquipmentSlotType.Legs:
                    removed = legsSlot;
                    legsSlot = null;
                    break;
                case EquipmentSlotType.Feet:
                    removed = feetSlot;
                    feetSlot = null;
                    break;
                case EquipmentSlotType.Hand:
                    if (index >= 0 && index < handSlots.Count)
                    {
                        removed = handSlots[index];
                        handSlots.RemoveAt(index);
                    }
                    break;
                case EquipmentSlotType.Trinket:
                    if (index >= 0 && index < trinketSlots.Count)
                    {
                        removed = trinketSlots[index];
                        trinketSlots.RemoveAt(index);
                    }
                    break;
            }

            if (removed != null)
            {
                OnEquipmentChanged?.Invoke(removed);
                return true;
            }
            return false;
        }

        public bool CanEquip(EquipmentData data, EquipmentSlotType slot)
        {
            if (data == null) return false;
            if (data.SlotType != slot) return false;

            switch (slot)
            {
                case EquipmentSlotType.Head:
                    return headSlot == null;
                case EquipmentSlotType.Chest:
                    return chestSlot == null;
                case EquipmentSlotType.Legs:
                    return legsSlot == null;
                case EquipmentSlotType.Feet:
                    return feetSlot == null;
                case EquipmentSlotType.Hand:
                    return handSlots.Count < maxHandSlots;
                case EquipmentSlotType.Trinket:
                    return trinketSlots.Count < maxTrinketSlots;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Collects all CardData from every equipped item. Used to build the character's deck.
        /// </summary>
        public List<CardData> GetAllCards()
        {
            var cards = new List<CardData>();

            AddCardsFromSlot(headSlot, cards);
            AddCardsFromSlot(chestSlot, cards);
            AddCardsFromSlot(legsSlot, cards);
            AddCardsFromSlot(feetSlot, cards);

            foreach (var slot in handSlots)
                AddCardsFromSlot(slot, cards);

            foreach (var slot in trinketSlots)
                AddCardsFromSlot(slot, cards);

            return cards;
        }

        /// <summary>
        /// Collects all trigger effects from equipped items for the given timing.
        /// </summary>
        public List<EquipmentTrigger> GetTriggers(TriggerTiming timing)
        {
            var triggers = new List<EquipmentTrigger>();

            AddTriggersFromSlot(headSlot, timing, triggers);
            AddTriggersFromSlot(chestSlot, timing, triggers);
            AddTriggersFromSlot(legsSlot, timing, triggers);
            AddTriggersFromSlot(feetSlot, timing, triggers);

            foreach (var slot in handSlots)
                AddTriggersFromSlot(slot, timing, triggers);

            foreach (var slot in trinketSlots)
                AddTriggersFromSlot(slot, timing, triggers);

            return triggers;
        }

        public IReadOnlyList<EquipmentInstance> GetEquippedInSlot(EquipmentSlotType slot)
        {
            switch (slot)
            {
                case EquipmentSlotType.Head:
                    return headSlot != null ? new[] { headSlot } : Array.Empty<EquipmentInstance>();
                case EquipmentSlotType.Chest:
                    return chestSlot != null ? new[] { chestSlot } : Array.Empty<EquipmentInstance>();
                case EquipmentSlotType.Legs:
                    return legsSlot != null ? new[] { legsSlot } : Array.Empty<EquipmentInstance>();
                case EquipmentSlotType.Feet:
                    return feetSlot != null ? new[] { feetSlot } : Array.Empty<EquipmentInstance>();
                case EquipmentSlotType.Hand:
                    return handSlots;
                case EquipmentSlotType.Trinket:
                    return trinketSlots;
                default:
                    return Array.Empty<EquipmentInstance>();
            }
        }

        private void HandleTurnStarted(Character turnCharacter)
        {
            if (turnCharacter != character) return;
            FireTriggers(TriggerTiming.StartOfTurn);
        }

        private void HandleTurnEnded(Character turnCharacter)
        {
            if (turnCharacter != character) return;
            FireTriggers(TriggerTiming.EndOfTurn);
        }

        public void FireTriggers(TriggerTiming timing)
        {
            var context = new EquipmentContext
            {
                Source = character,
                Grid = grid,
                TurnManager = turnManager,
                Timing = timing
            };

            foreach (var trigger in GetTriggers(timing))
            {
                if (trigger.Effect != null)
                    trigger.Effect.Execute(context);
            }
        }

        private static void AddCardsFromSlot(EquipmentInstance slot, List<CardData> cards)
        {
            if (slot == null) return;
            if (slot.Data.Cards != null)
                cards.AddRange(slot.Data.Cards);
        }

        private static void AddTriggersFromSlot(EquipmentInstance slot, TriggerTiming timing, List<EquipmentTrigger> triggers)
        {
            if (slot == null) return;
            if (slot.Data.Triggers == null) return;

            foreach (var trigger in slot.Data.Triggers)
            {
                if (trigger.Timing == timing)
                    triggers.Add(trigger);
            }
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted -= HandleTurnStarted;
                turnManager.OnTurnEnded -= HandleTurnEnded;
            }
        }
    }
}
