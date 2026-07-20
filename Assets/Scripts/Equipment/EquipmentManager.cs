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
    /// Attach to the same GameObject as Character. TurnManager and HexGridManager are
    /// found at runtime — no Inspector assignment needed.
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        [Header("Active Equipment")]
        [SerializeField] private EquipmentData headEquipment;
        [SerializeField] private EquipmentData chestEquipment;
        [SerializeField] private EquipmentData feetEquipment;
        [SerializeField] private EquipmentData leftHandEquipment;
        [SerializeField] private EquipmentData rightHandEquipment;
        [SerializeField] private EquipmentData trinket1;
        [SerializeField] private EquipmentData trinket2;
        [SerializeField] private EquipmentData trinket3;

        private Character character;
        private TurnManager turnManager;
        private HexGridManager grid;

        private EquipmentInstance headSlot;
        private EquipmentInstance chestSlot;
        private EquipmentInstance feetSlot;
        private EquipmentInstance leftHandSlot;
        private EquipmentInstance rightHandSlot;
        private readonly List<EquipmentInstance> trinketSlots = new List<EquipmentInstance>();

        public Character Character => character;

        public event Action<EquipmentInstance> OnEquipmentChanged;

        public void Initialize(Character character)
        {
            this.character = character;

            turnManager = FindAnyObjectByType<TurnManager>();
            grid = FindAnyObjectByType<HexGridManager>();

            LoadFromInspector();

            if (turnManager != null)
            {
                turnManager.OnTurnStarted += HandleTurnStarted;
                turnManager.OnTurnEnded += HandleTurnEnded;
            }
        }

        private void LoadFromInspector()
        {
            if (headEquipment != null) Equip(headEquipment);
            if (chestEquipment != null) Equip(chestEquipment);
            if (feetEquipment != null) Equip(feetEquipment);
            if (leftHandEquipment != null) Equip(leftHandEquipment, EquipmentSlotType.Hand, 0);
            if (rightHandEquipment != null) Equip(rightHandEquipment, EquipmentSlotType.Hand, 1);
            if (trinket1 != null) Equip(trinket1);
            if (trinket2 != null) Equip(trinket2);
            if (trinket3 != null) Equip(trinket3);
        }

        public bool Equip(EquipmentData data, EquipmentSlotType? forcedSlot = null, int? forcedIndex = null)
        {
            if (data == null) return false;

            EquipmentSlotType slot = forcedSlot ?? data.SlotType;
            int index = forcedIndex ?? 0;

            if (!CanEquip(data, slot, index)) return false;

            var instance = new EquipmentInstance(data);

            switch (slot)
            {
                case EquipmentSlotType.Head:
                    headSlot = instance;
                    break;
                case EquipmentSlotType.Chest:
                    chestSlot = instance;
                    break;
                case EquipmentSlotType.Feet:
                    feetSlot = instance;
                    break;
                case EquipmentSlotType.Hand:
                    if (forcedIndex == 1 || (forcedIndex == null && leftHandSlot != null))
                        rightHandSlot = instance;
                    else
                        leftHandSlot = instance;
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
                case EquipmentSlotType.Feet:
                    removed = feetSlot;
                    feetSlot = null;
                    break;
                case EquipmentSlotType.Hand:
                    if (index == 0) { removed = leftHandSlot; leftHandSlot = null; }
                    else if (index == 1) { removed = rightHandSlot; rightHandSlot = null; }
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

        public bool CanEquip(EquipmentData data, EquipmentSlotType slot, int index = 0)
        {
            if (data == null) return false;
            if (data.SlotType != slot) return false;

            switch (slot)
            {
                case EquipmentSlotType.Head:
                    return headSlot == null;
                case EquipmentSlotType.Chest:
                    return chestSlot == null;
                case EquipmentSlotType.Feet:
                    return feetSlot == null;
                case EquipmentSlotType.Hand:
                    return index == 0 ? leftHandSlot == null : rightHandSlot == null;
                case EquipmentSlotType.Trinket:
                    return trinketSlots.Count < 3;
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
            AddCardsFromSlot(feetSlot, cards);
            AddCardsFromSlot(leftHandSlot, cards);
            AddCardsFromSlot(rightHandSlot, cards);

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
            AddTriggersFromSlot(feetSlot, timing, triggers);
            AddTriggersFromSlot(leftHandSlot, timing, triggers);
            AddTriggersFromSlot(rightHandSlot, timing, triggers);

            foreach (var slot in trinketSlots)
                AddTriggersFromSlot(slot, timing, triggers);

            return triggers;
        }

        public EquipmentInstance GetEquipped(EquipmentSlotType slot, int index = 0)
        {
            switch (slot)
            {
                case EquipmentSlotType.Head:
                    return headSlot;
                case EquipmentSlotType.Chest:
                    return chestSlot;
                case EquipmentSlotType.Feet:
                    return feetSlot;
                case EquipmentSlotType.Hand:
                    return index == 0 ? leftHandSlot : rightHandSlot;
                case EquipmentSlotType.Trinket:
                    return index >= 0 && index < trinketSlots.Count ? trinketSlots[index] : null;
                default:
                    return null;
            }
        }

        public IReadOnlyList<EquipmentInstance> GetEquippedInSlot(EquipmentSlotType slot)
        {
            switch (slot)
            {
                case EquipmentSlotType.Head:
                    return headSlot != null ? new[] { headSlot } : Array.Empty<EquipmentInstance>();
                case EquipmentSlotType.Chest:
                    return chestSlot != null ? new[] { chestSlot } : Array.Empty<EquipmentInstance>();
                case EquipmentSlotType.Feet:
                    return feetSlot != null ? new[] { feetSlot } : Array.Empty<EquipmentInstance>();
                case EquipmentSlotType.Hand:
                    if (leftHandSlot != null && rightHandSlot != null)
                        return new[] { leftHandSlot, rightHandSlot };
                    if (leftHandSlot != null)
                        return new[] { leftHandSlot };
                    if (rightHandSlot != null)
                        return new[] { rightHandSlot };
                    return Array.Empty<EquipmentInstance>();
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
