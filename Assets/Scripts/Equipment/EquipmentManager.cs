using System;
using System.Collections.Generic;
using UnityEngine;
using Characters;
using Cards;
using HexGrid;
using Game;

namespace Equipment
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
        private ActionResolver actionResolver;
        private HexGridManager grid;

        private EquipmentInstance headSlot;
        private EquipmentInstance chestSlot;
        private EquipmentInstance feetSlot;
        private EquipmentInstance leftHandSlot;
        private EquipmentInstance rightHandSlot;
        private readonly List<EquipmentInstance> trinketSlots = new List<EquipmentInstance>();

        // Scratch buffers so trigger collection never allocates per fire.
        private readonly List<EquipmentTrigger> triggerScratch = new List<EquipmentTrigger>();

        // Once-per-turn / once-per-match budgets. Trigger references are stable, so a trigger
        // marked OncePerTurn fires at most once per wearer-turn, and OncePerMatch once overall.
        private readonly HashSet<EquipmentTrigger> firedThisTurn = new HashSet<EquipmentTrigger>();
        private readonly HashSet<EquipmentTrigger> firedThisMatch = new HashSet<EquipmentTrigger>();

        // Guards against OnShuffled re-entering itself (draw -> empty deck -> reshuffle -> draw...).
        private bool shufflingDeck;

        public Character Character => character;

        public event Action<EquipmentInstance> OnEquipmentChanged;

        public void Initialize(Character character)
        {
            this.character = character;

            turnManager = FindAnyObjectByType<TurnManager>();
            actionResolver = FindAnyObjectByType<ActionResolver>();
            grid = FindAnyObjectByType<HexGridManager>();

            LoadFromInspector();

            if (turnManager != null)
            {
                turnManager.OnTurnStarted += HandleTurnStarted;
                turnManager.OnTurnEnded += HandleTurnEnded;
            }

            if (actionResolver != null)
            {
                actionResolver.OnBeforeCardPlay += HandleBeforeCardPlay;
            }

            character.OnMovedToCell += HandleMovedToCell;
            character.OnLeftCell += HandleLeftCell;
            character.OnDamageTaken += HandleDamageTaken;
            character.OnDamageDealt += HandleDamageDealt;
            character.OnBlockGained += HandleBlockGained;

            if (character.Deck != null)
                character.Deck.OnShuffled += HandleDeckShuffled;
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

        public bool Equip(EquipmentData data, EquipmentSlotType? forcedSlot = null, int? forcedIndex = null,
            IEnumerable<EquipmentEnchantmentSO> enchantments = null)
        {
            if (data == null) return false;

            EquipmentSlotType slot = forcedSlot ?? data.SlotType;
            int index = forcedIndex ?? 0;

            // Auto-assign to the first free slot for multi-slot equipment (hands,
            // trinkets) so equipping without an explicit index fills empty slots in order.
            if (forcedIndex == null)
            {
                switch (slot)
                {
                    case EquipmentSlotType.Hand:
                        index = leftHandSlot == null ? 0 : 1;
                        break;
                    case EquipmentSlotType.Trinket:
                        index = -1;
                        for (int i = 0; i < 3; i++)
                        {
                            if (trinketSlots.Count <= i || trinketSlots[i] == null)
                            {
                                index = i;
                                break;
                            }
                        }
                        break;
                }
            }

            if (!CanEquip(data, slot, index)) return false;

            var instance = new EquipmentInstance(data);
            instance.AddEnchantments(enchantments);

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
                    if (index == 1) rightHandSlot = instance;
                    else leftHandSlot = instance;
                    break;
                case EquipmentSlotType.Trinket:
                    while (trinketSlots.Count <= index)
                        trinketSlots.Add(null);
                    trinketSlots[index] = instance;
                    break;
            }

            OnEquipmentChanged?.Invoke(instance);
            FireEnchantmentLifecycle(instance, equipping: true);
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
                FireEnchantmentLifecycle(removed, equipping: false);
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
                    if (index < 0 || index > 2) return false;
                    if (trinketSlots.Count <= index) return true;
                    return trinketSlots[index] == null;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Collects all CardData from every equipped item, including cards granted by
        /// enchantments. Used to build the character's deck.
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

        /// <summary>
        /// Snapshots the currently equipped items into a fixed-slot loadout.
        /// </summary>
        public EquipmentLoadout GetLoadout()
        {
            var loadout = new EquipmentLoadout
            {
                Head = headSlot?.Data,
                Chest = chestSlot?.Data,
                Feet = feetSlot?.Data,
                LeftHand = leftHandSlot?.Data,
                RightHand = rightHandSlot?.Data
            };

            loadout.Trinket1 = trinketSlots.Count > 0 && trinketSlots[0] != null ? trinketSlots[0].Data : null;
            loadout.Trinket2 = trinketSlots.Count > 1 && trinketSlots[1] != null ? trinketSlots[1].Data : null;
            loadout.Trinket3 = trinketSlots.Count > 2 && trinketSlots[2] != null ? trinketSlots[2].Data : null;

            CopyEnchantments(headSlot, loadout);
            CopyEnchantments(chestSlot, loadout);
            CopyEnchantments(feetSlot, loadout);
            CopyEnchantments(leftHandSlot, loadout);
            CopyEnchantments(rightHandSlot, loadout);
            foreach (var slot in trinketSlots)
                CopyEnchantments(slot, loadout);

            return loadout;
        }

        private static void CopyEnchantments(EquipmentInstance instance, EquipmentLoadout loadout)
        {
            if (instance == null) return;
            foreach (var enchantment in instance.ExtraEnchantments)
                loadout.AddEnchantment(instance.Data, enchantment);
        }

        /// <summary>
        /// Clears every slot and equips the given loadout's items at their fixed slot
        /// positions. Used at match start so a character's deck/triggers come from the
        /// player's chosen loadout rather than the Inspector defaults.
        /// </summary>
        public void ApplyLoadout(EquipmentLoadout loadout)
        {
            ClearAllSlots();
            if (loadout == null) return;

            Equip(loadout.Head, EquipmentSlotType.Head, 0, loadout.GetEnchantments(loadout.Head));
            Equip(loadout.Chest, EquipmentSlotType.Chest, 0, loadout.GetEnchantments(loadout.Chest));
            Equip(loadout.Feet, EquipmentSlotType.Feet, 0, loadout.GetEnchantments(loadout.Feet));
            Equip(loadout.LeftHand, EquipmentSlotType.Hand, 0, loadout.GetEnchantments(loadout.LeftHand));
            Equip(loadout.RightHand, EquipmentSlotType.Hand, 1, loadout.GetEnchantments(loadout.RightHand));
            Equip(loadout.Trinket1, EquipmentSlotType.Trinket, 0, loadout.GetEnchantments(loadout.Trinket1));
            Equip(loadout.Trinket2, EquipmentSlotType.Trinket, 1, loadout.GetEnchantments(loadout.Trinket2));
            Equip(loadout.Trinket3, EquipmentSlotType.Trinket, 2, loadout.GetEnchantments(loadout.Trinket3));
        }

        private void ClearAllSlots()
        {
            // Revert enchantment effects (stat bonuses, granted deck cards) before dropping references.
            FireEnchantmentLifecycle(headSlot, equipping: false);
            FireEnchantmentLifecycle(chestSlot, equipping: false);
            FireEnchantmentLifecycle(feetSlot, equipping: false);
            FireEnchantmentLifecycle(leftHandSlot, equipping: false);
            FireEnchantmentLifecycle(rightHandSlot, equipping: false);

            foreach (var slot in trinketSlots)
                FireEnchantmentLifecycle(slot, equipping: false);

            headSlot = null;
            chestSlot = null;
            feetSlot = null;
            leftHandSlot = null;
            rightHandSlot = null;
            trinketSlots.Clear();
        }

        /// <summary>
        /// Runs every enchantment on the given equipment's OnEquipped/OnUnequipped hook.
        /// </summary>
        private void FireEnchantmentLifecycle(EquipmentInstance instance, bool equipping)
        {
            if (instance == null || character == null) return;

            foreach (var enchantment in instance.AllEnchantments)
            {
                if (enchantment == null) continue;
                if (equipping) enchantment.OnEquipped(instance, character);
                else enchantment.OnUnequipped(instance, character);
            }
        }

        /// <summary>
        /// Total bonus damage added to <paramref name="playedCard"/> by enchantments on
        /// currently worn equipment. Consulted by ActionResolver when a card is played.
        /// </summary>
        public int GetTotalCardDamageBonus(CardData playedCard)
        {
            int total = 0;

            CollectEnchantmentDamageBonus(headSlot, playedCard, ref total);
            CollectEnchantmentDamageBonus(chestSlot, playedCard, ref total);
            CollectEnchantmentDamageBonus(feetSlot, playedCard, ref total);
            CollectEnchantmentDamageBonus(leftHandSlot, playedCard, ref total);
            CollectEnchantmentDamageBonus(rightHandSlot, playedCard, ref total);

            foreach (var slot in trinketSlots)
                CollectEnchantmentDamageBonus(slot, playedCard, ref total);

            return total;
        }

        private static void CollectEnchantmentDamageBonus(EquipmentInstance slot, CardData playedCard, ref int total)
        {
            if (slot == null) return;

            foreach (var enchantment in slot.AllEnchantments)
            {
                if (enchantment != null)
                    total += enchantment.GetDamageBonus(slot, playedCard);
            }
        }

        /// <summary>
        /// Product of every damage multiplier contributed by enchantments on worn equipment
        /// for <paramref name="playedCard"/>. 1.0 means unchanged. Consulted by ActionResolver.
        /// </summary>
        public float GetTotalCardDamageMultiplier(CardData playedCard)
        {
            float multiplier = 1f;

            CollectEnchantmentDamageMultiplier(headSlot, playedCard, ref multiplier);
            CollectEnchantmentDamageMultiplier(chestSlot, playedCard, ref multiplier);
            CollectEnchantmentDamageMultiplier(feetSlot, playedCard, ref multiplier);
            CollectEnchantmentDamageMultiplier(leftHandSlot, playedCard, ref multiplier);
            CollectEnchantmentDamageMultiplier(rightHandSlot, playedCard, ref multiplier);

            foreach (var slot in trinketSlots)
                CollectEnchantmentDamageMultiplier(slot, playedCard, ref multiplier);

            return multiplier;
        }

        private static void CollectEnchantmentDamageMultiplier(EquipmentInstance slot, CardData playedCard, ref float multiplier)
        {
            if (slot == null) return;

            foreach (var enchantment in slot.AllEnchantments)
            {
                if (enchantment != null)
                    multiplier *= enchantment.GetDamageMultiplier(slot, playedCard);
            }
        }

        /// <summary>
        /// Summed cost delta (e.g. -1) applied to <paramref name="card"/>, contributed by
        /// enchantments on worn equipment. Consulted when the deck creates a card instance.
        /// </summary>
        public int GetCardCostDelta(CardData card)
        {
            int total = 0;
            if (card == null) return 0;

            CollectEnchantmentCostDelta(headSlot, card, ref total);
            CollectEnchantmentCostDelta(chestSlot, card, ref total);
            CollectEnchantmentCostDelta(feetSlot, card, ref total);
            CollectEnchantmentCostDelta(leftHandSlot, card, ref total);
            CollectEnchantmentCostDelta(rightHandSlot, card, ref total);

            foreach (var slot in trinketSlots)
                CollectEnchantmentCostDelta(slot, card, ref total);

            return total;
        }

        private static void CollectEnchantmentCostDelta(EquipmentInstance slot, CardData card, ref int total)
        {
            if (slot == null) return;

            foreach (var enchantment in slot.AllEnchantments)
            {
                if (enchantment != null)
                    total += enchantment.GetCardCostDelta(slot, card);
            }
        }

        /// <summary>
        /// True when any enchantment on worn equipment makes the wearer immune to the given
        /// damage source (e.g. fire or fire-oil explosions). Consulted by Character.TakeDamage.
        /// </summary>
        public bool IsImmuneTo(DamageType type)
        {
            if (type == DamageType.None) return false;

            return SlotImmuneTo(headSlot, type)
                || SlotImmuneTo(chestSlot, type)
                || SlotImmuneTo(feetSlot, type)
                || SlotImmuneTo(leftHandSlot, type)
                || SlotImmuneTo(rightHandSlot, type)
                || AnyTrinketImmuneTo(type);
        }

        private bool AnyTrinketImmuneTo(DamageType type)
        {
            foreach (var slot in trinketSlots)
            {
                if (SlotImmuneTo(slot, type)) return true;
            }
            return false;
        }

        private static bool SlotImmuneTo(EquipmentInstance slot, DamageType type)
        {
            if (slot == null) return false;

            foreach (var enchantment in slot.AllEnchantments)
            {
                if (enchantment != null && enchantment.IsImmuneTo(type)) return true;
            }
            return false;
        }

        private void HandleTurnStarted(Character turnCharacter)
        {
            if (turnCharacter != character) return;
            firedThisTurn.Clear();
            FireTriggers(TriggerTiming.StartOfTurn);
        }

        private void HandleTurnEnded(Character turnCharacter)
        {
            if (turnCharacter == character)
            {
                FireTriggers(TriggerTiming.EndOfTurn);
                return;
            }

            // An enemy's turn ended. Fire OnEnemyTurnEnded with that enemy as Other so
            // effects like Terrifying Mask can react to adjacent enemies.
            if (turnCharacter.IsEnemyOf(character))
            {
                var context = new EquipmentContext
                {
                    Source = character,
                    Grid = grid,
                    TurnManager = turnManager,
                    Timing = TriggerTiming.OnEnemyTurnEnded,
                    Other = turnCharacter
                };

                RunTriggers(TriggerTiming.OnEnemyTurnEnded, context);
            }
        }

        private void HandleBeforeCardPlay(CardContext cardContext)
        {
            if (cardContext.Source != character) return;
            var context = new EquipmentContext
            {
                Source = character,
                Grid = grid,
                TurnManager = turnManager,
                Timing = TriggerTiming.OnBeforeCardPlay,
                TargetCell = cardContext.TargetCell,
                CardContext = cardContext
            };

            RunTriggers(TriggerTiming.OnBeforeCardPlay, context);
        }

        private void HandleMovedToCell(HexCell cell)
        {
            var context = new EquipmentContext
            {
                Source = character,
                Grid = grid,
                TurnManager = turnManager,
                Timing = TriggerTiming.OnMovedToCell,
                TargetCell = cell
            };

            RunTriggers(TriggerTiming.OnMovedToCell, context);
        }

        private void HandleLeftCell(HexCell cell)
        {
            if (cell == null) return;

            var context = new EquipmentContext
            {
                Source = character,
                Grid = grid,
                TurnManager = turnManager,
                Timing = TriggerTiming.OnLeftCell,
                TargetCell = cell
            };

            RunTriggers(TriggerTiming.OnLeftCell, context);
        }

        private void HandleDamageTaken(int amount)
        {
            var context = new EquipmentContext
            {
                Source = character,
                Grid = grid,
                TurnManager = turnManager,
                Timing = TriggerTiming.OnDamageTaken
            };

            RunTriggers(TriggerTiming.OnDamageTaken, context);
        }

        private void HandleDamageDealt(int amount, Character target)
        {
            var context = new EquipmentContext
            {
                Source = character,
                Grid = grid,
                TurnManager = turnManager,
                Timing = TriggerTiming.OnDealDamage,
                Amount = amount,
                Other = target
            };

            RunTriggers(TriggerTiming.OnDealDamage, context);
        }

        private void HandleBlockGained(int amount)
        {
            var context = new EquipmentContext
            {
                Source = character,
                Grid = grid,
                TurnManager = turnManager,
                Timing = TriggerTiming.OnGainBlock,
                Amount = amount
            };

            RunTriggers(TriggerTiming.OnGainBlock, context);
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

            RunTriggers(timing, context);
        }

        /// <summary>
        /// Fires every trigger (from equipment AND enchantments) for the timing, honouring
        /// OnlyOnWearersTurn, per-trigger conditions, and once-per-turn/once-per-match budgets.
        /// </summary>
        private void RunTriggers(TriggerTiming timing, EquipmentContext context)
        {
            foreach (EquipmentTrigger trigger in GetTriggers(timing))
            {
                if (trigger.Effect == null) continue;
                if (trigger.OnlyOnWearersTurn && turnManager != null &&
                    turnManager.ActiveCharacter != character) continue;
                if (trigger.Condition != null && !trigger.Condition.Evaluate(context)) continue;

                if (trigger.OncePerMatch && firedThisMatch.Contains(trigger)) continue;
                if (trigger.OncePerTurn && firedThisTurn.Contains(trigger)) continue;

                trigger.Effect.Execute(context);

                if (trigger.OncePerMatch) firedThisMatch.Add(trigger);
                if (trigger.OncePerTurn) firedThisTurn.Add(trigger);
            }
        }

        /// <summary>
        /// Fires OnShuffle triggers when the owner's discard pile is shuffled back into the
        /// draw pile mid-match. Guarded so a draw effect that empties the deck again cannot
        /// recursively re-enter this handler.
        /// </summary>
        private void HandleDeckShuffled()
        {
            if (shufflingDeck) return;
            shufflingDeck = true;
            try
            {
                var context = new EquipmentContext
                {
                    Source = character,
                    Grid = grid,
                    TurnManager = turnManager,
                    Timing = TriggerTiming.OnShuffle
                };
                RunTriggers(TriggerTiming.OnShuffle, context);
            }
            finally
            {
                shufflingDeck = false;
            }
        }

        /// <summary>
        /// Builds the per-slot card list (equipment's own cards plus enchantment-granted ones),
        /// lets enchantments transform it (e.g. double granted cards), then appends it.
        /// </summary>
        private void AddCardsFromSlot(EquipmentInstance slot, List<CardData> cards)
        {
            if (slot == null) return;

            var slotCards = new List<CardData>();
            if (slot.Data.Cards != null)
                slotCards.AddRange(slot.Data.Cards);

            foreach (var enchantment in slot.AllEnchantments)
                enchantment?.CollectGrantedCards(slotCards);

            foreach (var enchantment in slot.AllEnchantments)
                enchantment?.ModifyGrantedCards(slot, slotCards);

            cards.AddRange(slotCards);
        }

        /// <summary>
        /// Collects all trigger effects from equipped items for the given timing,
        /// including triggers contributed by enchantments.
        /// </summary>
        private void AddTriggersFromSlot(EquipmentInstance slot, TriggerTiming timing, List<EquipmentTrigger> triggers)
        {
            if (slot == null) return;

            if (slot.Data.Triggers != null)
            {
                foreach (var trigger in slot.Data.Triggers)
                {
                    if (trigger != null && trigger.Timing == timing)
                        triggers.Add(trigger);
                }
            }

            foreach (var enchantment in slot.AllEnchantments)
            {
                if (enchantment == null) continue;
                triggerScratch.Clear();
                enchantment.CollectTriggers(triggerScratch);
                foreach (var trigger in triggerScratch)
                {
                    if (trigger != null && trigger.Timing == timing)
                        triggers.Add(trigger);
                }
            }
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted -= HandleTurnStarted;
                turnManager.OnTurnEnded -= HandleTurnEnded;
            }

            if (actionResolver != null)
            {
                actionResolver.OnBeforeCardPlay -= HandleBeforeCardPlay;
            }

            if (character != null)
            {
                character.OnMovedToCell -= HandleMovedToCell;
                character.OnLeftCell -= HandleLeftCell;
                character.OnDamageTaken -= HandleDamageTaken;
                character.OnDamageDealt -= HandleDamageDealt;
                character.OnBlockGained -= HandleBlockGained;

                if (character.Deck != null)
                    character.Deck.OnShuffled -= HandleDeckShuffled;
            }
        }
    }
}
