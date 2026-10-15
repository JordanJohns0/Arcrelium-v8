using System.Collections.Generic;
using UnityEngine;
using Characters;
using Equipment;
using Game;
using HexGrid;

namespace TileEffects
{
    /// <summary>
    /// Host-authoritative manager for persistent tile effects (fire, oil, rocks...).
    ///
    /// What it does:
    ///  - Holds placed effects per cell (stored on HexCell.Effects).
    ///  - Reacts to character movement: entry effects run, and oil slides the character
    ///    one tile in the direction they were moving.
    ///  - Reacts to turn starts: whatever the active character is standing on applies.
    ///  - Ticks effect durations once per full round (first character's turn start).
    ///
    /// Drop this on the board scene; it auto-binds to the HexGridManager and TurnManager.
    /// All card-driven movement routes through MoveCharacter so tile effects see every move.
    /// </summary>
    public class TileEffectSystem : MonoBehaviour
    {
        /// <summary>Maximum chained slides in a single entry (oil tile after oil tile).</summary>
        public const int MaxSlipChain = 3;

        public static TileEffectSystem Instance { get; private set; }

        [SerializeField] private HexGridManager grid;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private GameObject fireVisualPrefab;
        [SerializeField] private GameObject oilVisualPrefab;

        private readonly HashSet<Character> subscribed = new HashSet<Character>();
        private readonly Dictionary<TileEffectInstance, GameObject> visuals = new Dictionary<TileEffectInstance, GameObject>();
        private bool handlingEntry;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TileEffects] Duplicate TileEffectSystem in the scene; keeping the first.");
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            if (grid == null) grid = FindFirstObjectByType<HexGridManager>();
            if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();

            var library = GameDataLibrary.Instance;
            if (library != null)
            {
                if (fireVisualPrefab == null) fireVisualPrefab = library.fireEffectVisualPrefab;
                if (oilVisualPrefab == null) oilVisualPrefab = library.oilEffectVisualPrefab;
            }

            Character.OnCharacterCreated += SubscribeCharacter;
            Character.OnCharacterDestroyed += UnsubscribeCharacter;
            if (turnManager != null) turnManager.OnTurnStarted += HandleTurnStartedBroadcast;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        /// <summary>
        /// Unsubscribes every event this system registered, mirroring OnDestroy. Edit-mode
        /// tests can't rely on DestroyImmediate invoking OnDestroy, so fixtures call this
        /// explicitly to keep static Character events from leaking between tests.
        /// </summary>
        public void Shutdown()
        {
            if (Instance == this) Instance = null;
            Character.OnCharacterCreated -= SubscribeCharacter;
            Character.OnCharacterDestroyed -= UnsubscribeCharacter;
            if (turnManager != null) turnManager.OnTurnStarted -= HandleTurnStartedBroadcast;
            foreach (Character character in subscribed) character.OnMovedToCell -= OnCharacterMoved;
            subscribed.Clear();
            foreach (GameObject go in visuals.Values)
            {
                if (go != null)
                {
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                }
            }
            visuals.Clear();
        }

        /// <summary>Bindings for tests; mirrors what Awake resolves in a real scene.</summary>
        public void InitializeForTests(HexGridManager testGrid, TurnManager testTurnManager)
        {
            Instance = this;
            grid = testGrid;
            turnManager = testTurnManager;
            Character.OnCharacterCreated += SubscribeCharacter;
            Character.OnCharacterDestroyed += UnsubscribeCharacter;
            if (turnManager != null) turnManager.OnTurnStarted += HandleTurnStartedBroadcast;
        }

        /// <summary>Test seam for supplying the effect visuals; mirrors the GameDataLibrary resolution in Awake.</summary>
        public void BindVisualPrefabs(GameObject firePrefab, GameObject oilPrefab)
        {
            fireVisualPrefab = firePrefab;
            oilVisualPrefab = oilPrefab;
        }

        private void SubscribeCharacter(Character character)
        {
            if (character != null && subscribed.Add(character))
                character.OnMovedToCell += OnCharacterMoved;
        }

        private void UnsubscribeCharacter(Character character)
        {
            if (character == null) return;
            character.OnMovedToCell -= OnCharacterMoved;
            subscribed.Remove(character);
        }

        private void HandleTurnStartedBroadcast(Character active)
        {
            bool roundStart = turnManager != null && turnManager.CurrentTurnIndex == 0;
            HandleTurnStarted(active, roundStart);
        }

        // ------------------------------------------------------------ public API

        /// <summary>
        /// Places an effect on a cell. Blocking effects (rocks) are refused unless the tile is
        /// currently walkable. Fire placed on oil detonates via OnEffectAddedToCell.
        /// </summary>
        public TileEffectInstance PlaceEffect(HexCell cell, TileEffectSO definition)
        {
            if (cell == null || definition == null) return null;

            if (definition.BlocksMovement && !cell.IsWalkable)
            {
                Debug.LogWarning($"[TileEffects] Cannot place {definition.EffectName} on {cell.Coordinates}: the tile is not walkable.");
                return null;
            }

            var instance = new TileEffectInstance
            {
                Data = definition,
                TurnsRemaining = definition.Duration <= 0 ? TileEffectInstance.Permanent : definition.Duration,
                Cell = cell
            };
            cell.AddEffectInstance(instance);
            definition.OnEffectAddedToCell(MakeContext(cell, cell.Occupant, instance));

            // The add-trigger may have consumed the effect itself (fire igniting oil) —
            // don't leave a visual floating over a hex that no longer holds the effect.
            if (cell.Effects.Contains(instance))
                SpawnVisual(instance);
            return instance;
        }

        public void RemoveEffect(HexCell cell, TileEffectInstance instance)
        {
            if (cell != null) cell.RemoveEffectInstance(instance);
            DestroyVisual(instance);
        }

        /// <summary>
        /// Resolves a card movement: walks the character through each path cell, applying entry
        /// effects (fire burns, oil slides), and returns the true routed cells for animation so
        /// the model ends where the logic says it is.
        /// </summary>
        public List<HexCell> MoveCharacter(Character character, List<HexCell> path)
        {
            var resolved = new List<HexCell>();
            if (character == null || path == null || path.Count == 0) return resolved;

            foreach (HexCell step in path)
            {
                if (step == null) break;

                // A cell the character already stands on was reached by an oil slide during the
                // previous step; it costs nothing and the walk carries on from there.
                if (step == character.CurrentCell) continue;
                if (!step.IsWalkable) break;

                WalkInto(character, step);
                resolved.Add(character.CurrentCell);
            }

            return resolved;
        }

        /// <summary>Moves the character onto the cell logically, then resolves entry effects (slides included).</summary>
        public HexCell WalkInto(Character character, HexCell target)
        {
            if (character == null || target == null) return character != null ? character.CurrentCell : null;

            SubscribeCharacter(character);
            character.SetCellOnly(target);
            return character.CurrentCell;
        }

        /// <summary>
        /// Applies start-of-turn tile effects for the active character, then ticks the whole board
        /// down one full round if this is a round start (the first character's turn).
        /// </summary>
        public void HandleTurnStarted(Character active, bool isRoundStart)
        {
            if (active == null) return;

            HexCell cell = active.CurrentCell;
            if (cell != null)
            {
                foreach (TileEffectInstance effect in cell.Effects)
                    effect.Data.OnCharacterTurnStarted(MakeContext(cell, active, effect));
            }

            if (isRoundStart) TickRound();
        }

        /// <summary>Counts every non-permanent effect's duration down one round, removing anything that hit zero.</summary>
        public void TickRound()
        {
            if (grid == null) return;

            var expired = new List<(HexCell cell, TileEffectInstance effect)>();
            foreach (HexCell cell in grid.AllCells)
            {
                foreach (TileEffectInstance effect in cell.Effects)
                {
                    if (effect.TurnsRemaining == TileEffectInstance.Permanent) continue;
                    effect.TurnsRemaining--;
                    if (effect.TurnsRemaining <= 0) expired.Add((cell, effect));
                }
            }

            foreach ((HexCell cell, TileEffectInstance effect) in expired)
            {
                cell.RemoveEffectInstance(effect);
                DestroyVisual(effect);
                Debug.Log($"[TileEffects] {effect.Data.EffectName} on {cell.Coordinates} burnt out.");
            }
        }

        /// <summary>Deals AoE damage to the occupant of the center tile and everyone within radius.</summary>
        public void DamageRadius(HexCell center, int amount, int radius)
        {
            if (grid == null || center == null) return;

            foreach (HexCell cell in grid.GetCellsInRange(center.Coordinates, radius))
            {
                if (cell.Occupant == null || !cell.Occupant.Stats.IsAlive) continue;
                cell.Occupant.TakeDamage(amount);
            }
        }

        /// <summary>Fire meeting oil: consumes every oil and fire effect on the tile and detonates an AoE.</summary>
        public void IgniteOil(HexCell cell, int amount, int radius)
        {
            if (cell == null) return;

            for (int i = cell.Effects.Count - 1; i >= 0; i--)
            {
                TileEffectInstance consumed = cell.Effects[i];
                if (consumed.Data.Slippery || consumed.Data is FireTileEffectSO)
                {
                    cell.RemoveEffectInstance(consumed);
                    DestroyVisual(consumed);
                }
            }

            DamageRadius(cell, amount, radius);
            Debug.Log($"[TileEffects] Fire ignited oil on {cell.Coordinates} — explosion for {amount} damage!");
        }

        // ------------------------------------------------------------ internals

        private void SpawnVisual(TileEffectInstance effect)
        {
            if (effect == null || effect.Cell == null) return;
            GameObject prefab = null;
            if (effect.Data is FireTileEffectSO) prefab = fireVisualPrefab;
            else if (effect.Data is OilTileEffectSO) prefab = oilVisualPrefab;
            if (prefab == null) return;

            GameObject go = Instantiate(prefab, effect.Cell.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            visuals[effect] = go;
        }

        private void DestroyVisual(TileEffectInstance effect)
        {
            if (effect == null || !visuals.TryGetValue(effect, out GameObject go)) return;
            visuals.Remove(effect);
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private void OnCharacterMoved(HexCell cell)
        {
            if (cell == null) return;
            HandleEntry(cell.Occupant, cell);
        }

        /// <summary>
        /// Runs a character's entry into a tile: each effect's OnCharacterEntered, then oil slides
        /// the character forward (capped chain). Guarded so the slides' own OnMovedToCell calls
        /// don't re-enter this handler recursively.
        /// </summary>
        private void HandleEntry(Character character, HexCell cell)
        {
            if (character == null || cell == null || handlingEntry) return;
            handlingEntry = true;
            try
            {
                int budget = MaxSlipChain;
                HexCell current = cell;
                while (current != null && budget-- > 0)
                {
                    foreach (TileEffectInstance effect in new List<TileEffectInstance>(current.Effects))
                        effect.Data.OnCharacterEntered(MakeContext(current, character, effect));

                    if (character != null && !character.Stats.IsAlive) break;

                    HexCell slide = ResolveSlideTarget(character, current);
                    if (slide == null || slide == current || !slide.IsWalkable) break;

                    character.SetCellOnly(slide);
                    current = slide;
                }
            }
            finally
            {
                handlingEntry = false;
            }
        }

        /// <summary>
        /// For a slippery tile, picks where the character slides: one tile past in the direction
        /// they were moving (opposite their entry direction). Falls back to the first walkable
        /// neighbor if the entry direction is unknown. Null means no slide.
        /// </summary>
        private HexCell ResolveSlideTarget(Character character, HexCell current)
        {
            if (current == null || grid == null || !current.HasEffect(effect => effect.Slippery)) return null;

            if (character != null && character.LastCell != null && character.LastCell != current
                && character.LastCell.Coordinates.DistanceTo(current.Coordinates) == 1)
            {
                // Same rule the move planner uses, so a planned route and the real slide agree.
                return MovePlanner.SlideDestination(grid, character.LastCell, current);
            }

            foreach (HexCell neighbor in grid.GetNeighbors(current.Coordinates))
            {
                if (neighbor.IsWalkable) return neighbor;
            }

            return null;
        }

        private TileEffectContext MakeContext(HexCell cell, Character character, TileEffectInstance effect)
        {
            return new TileEffectContext { System = this, Cell = cell, Character = character, Effect = effect };
        }
    }
}