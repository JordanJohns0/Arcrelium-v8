using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Characters;
using Cards;
using Cards.Effects;
using HexGrid;
using TileEffects;

namespace Game
{
    public class AIController : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private ActionResolver actionResolver;
        [SerializeField] private HexGridManager grid;
        [SerializeField] private float aiDelay = 0.4f;

        /// <summary>
        /// Minimum real seconds between AI card plays so everyone can follow each
        /// resolution. Human players are never throttled by this.
        /// </summary>
        internal const float MinPlayIntervalSeconds = 1f;

        [SerializeField] private float aiPlayInterval = 1f;

        private struct Candidate
        {
            public CardInstance Card;
            public List<CardInstance> Cost;
            public HexCell Cell;
            public Character Target;
            public float Score;
        }

        private void OnEnable()
        {
            turnManager.OnTurnStarted += HandleTurnStarted;
        }

        private void OnDisable()
        {
            turnManager.OnTurnStarted -= HandleTurnStarted;
        }

        private void HandleTurnStarted(Character character)
        {
            if (character == null) return;
            if (GameState.ActiveMode == GameMode.Client) return;
            // Only characters owned by the AI are driven here. Human players (including
            // every player in PvP) control their own turns through input.
            if (character.OwnerPlayerId < GameState.EnemyOwnerPlayerId) return;
            if (character.Stats == null || !character.Stats.IsAlive) return;

            StartCoroutine(PlayAITurn(character));
        }

        private IEnumerator PlayAITurn(Character character)
        {
            while (turnManager.Busy)
                yield return null;

            yield return new WaitForSeconds(aiDelay);

            while (true)
            {
                Candidate best = ChooseBestPlay(character);
                if (best.Card == null || best.Score <= AICardScoring.PassThreshold)
                {
                    // The previous card's animation must finish before passing, otherwise the
                    // Pass would be rejected while the turn is busy and the turn would stall.
                    while (turnManager.Busy)
                        yield return null;
                    turnManager.Pass();
                    yield break;
                }

                if (!turnManager.PlayCard(best.Card, best.Cost, best.Cell, best.Target))
                {
                    // Every candidate is pre-validated, so a rejection means the turn
                    // is no longer playable (e.g. a state change mid-AI). End the turn
                    // cleanly instead of soft-locking on an unplayable hand.
                    while (turnManager.Busy)
                        yield return null;
                    turnManager.Pass();
                    yield break;
                }

                // Pace the turn: wait for this card's animation to finish (and keep a readable
                // minimum gap between plays) before the next one is submitted.
                float minGapEnd = Time.time + Mathf.Max(MinPlayIntervalSeconds, aiPlayInterval);
                while (Time.time < minGapEnd || turnManager.Busy)
                    yield return null;
            }
        }

        private Candidate ChooseBestPlay(Character character)
        {
            List<CardInstance> hand = new List<CardInstance>(character.Hand.Cards);
            List<Character> enemies = FindAliveEnemies(character);

            Candidate best = default;
            best.Score = float.MinValue;

            int nearestEnemyDistance = DistanceToNearest(enemies, character.CurrentCell.Coordinates);

            foreach (CardInstance card in hand)
            {
                if (card == null || card.Data == null || card.Data.Steps == null || card.Data.Steps.Count == 0) continue;
                if (card.Cost > hand.Count - 1) continue;
                List<CardInstance> cost = PickCostCards(hand, card);

                switch (card.Data.TargetType)
                {
                    case TargetType.None:
                    case TargetType.Self:
                    {
                        if (!AllStepsCanExecute(character, card.Data, null, null)) break;
                        AddCandidate(ref best, card, cost, null, null,
                            AICardScoring.Score(card.Data, character, null, nearestEnemyDistance, card.Cost));
                        break;
                    }

                    case TargetType.HexCell:
                    {
                        foreach (HexCell cell in grid.GetCellsInRange(character.CurrentCell.Coordinates, card.Data.Range))
                        {
                            if (cell == character.CurrentCell) continue;
                            if (card.Data.CellConstraint == CellConstraint.Empty && cell.Occupant != null) continue;
                            if (!AllStepsCanExecute(character, card.Data, cell, null)) continue;

                            int before = nearestEnemyDistance;
                            int after = DistanceToNearest(enemies, cell.Coordinates);
                            float score = AICardScoring.Score(card.Data, character, null, after, card.Cost)
                                + AICardScoring.ScoreMove(before, after)
                                + AreaDamageValue(card.Data, character, enemies, cell)
                                + TileEffectValue(card.Data, character, cell);
                            AddCandidate(ref best, card, cost, cell, null, score);
                        }
                        break;
                    }

                    case TargetType.EnemyCharacter:
                    case TargetType.AllyCharacter:
                    case TargetType.AnyCharacter:
                    {
                        foreach (Character target in GetTargetsInRange(character, card.Data))
                        {
                            if (!AllStepsCanExecute(character, card.Data, null, target)) continue;
                            AddCandidate(ref best, card, cost, null, target,
                                AICardScoring.Score(card.Data, character, target, nearestEnemyDistance, card.Cost));
                        }
                        break;
                    }
                }
            }

            return best;
        }

        private static void AddCandidate(ref Candidate best, CardInstance card, List<CardInstance> cost,
            HexCell cell, Character target, float score)
        {
            if (score <= best.Score) return;
            best.Card = card;
            best.Cost = cost;
            best.Cell = cell;
            best.Target = target;
            best.Score = score;
        }

        /// <summary>
        /// Every step of the card must be executable against its own context (per-step range
        /// and target type) for the candidate to be valid — same all-or-nothing rule as the
        /// resolver, so a multi-step card is only played if it fully resolves.
        /// </summary>
        private bool AllStepsCanExecute(Character source, CardData card, HexCell cell, Character target)
        {
            foreach (CardEffectStep step in card.Steps)
            {
                if (step == null || step.Effect == null) return false;
                CardContext ctx = MakeContext(source, card, step, cell, target);
                if (!step.Effect.CanExecute(ctx)) return false;
            }
            return true;
        }

        private CardContext MakeContext(Character source, CardData card, CardEffectStep step, HexCell cell, Character target)
        {
            return new CardContext
            {
                Source = source,
                Grid = grid,
                TargetCell = cell,
                TargetCharacter = target,
                Range = step.Range
            };
        }

        /// <summary>
        /// Extra value for enemy-targeting area-damage steps: damage × enemies caught in the
        /// blast around the candidate cell (capped by each enemy's effective HP, plus a lethal
        /// bonus per kill). Gives AoE plays real value in the AI's decision, not just movement.
        /// </summary>
        private float AreaDamageValue(CardData card, Character source, List<Character> enemies, HexCell cell)
        {
            float value = 0f;
            if (card.Steps == null) return value;

            foreach (CardEffectStep step in card.Steps)
            {
                if (step?.Effect is not AreaDamageEffect area) continue;
                if (area.Faction != AreaFaction.Enemies) continue;

                foreach (Character enemy in enemies)
                {
                    if (enemy.Stats == null || !enemy.Stats.IsAlive) continue;
                    int distance = cell.Coordinates.DistanceTo(enemy.CurrentCell.Coordinates);
                    if (distance > area.Radius) continue;

                    int damage = area.DamageAt(source, distance);
                    if (damage <= 0) continue;

                    int needed = AICardScoring.EffectiveHpToKill(enemy);
                    value += Mathf.Min(damage, needed) * AICardScoring.DamagePerHp;
                    if (damage >= needed)
                        value += AICardScoring.LethalBonus;
                }
            }
            return value;
        }

        /// <summary>
        /// Control value for cards that manipulate tile effects directly: a fire wall is worth
        /// the burn damage of every enemy standing in it, and mould earth displacing or crushing
        /// a standing enemy has matchup value. Grease (oil placement) has no direct payoff so it
        /// scores nothing on its own.
        /// </summary>
        private float TileEffectValue(CardData card, Character source, HexCell cell)
        {
            if (card.Steps == null || grid == null) return 0f;
            float value = 0f;

            foreach (CardEffectStep step in card.Steps)
            {
                switch (step?.Effect)
                {
                    case FireWallEffect wall:
                    {
                        int burn = (wall.FireDefinition as FireTileEffectSO)?.FireDamage ?? 0;
                        if (burn <= 0) break;
                        foreach (HexCell burned in wall.BurningCells(grid, source.CurrentCell, cell))
                        {
                            if (burned?.Occupant == null || burned.Occupant.IsAllyOf(source)) continue;
                            if (burned.Occupant.Stats == null || !burned.Occupant.Stats.IsAlive) continue;
                            int needed = AICardScoring.EffectiveHpToKill(burned.Occupant);
                            value += Mathf.Min(burn, needed) * AICardScoring.DamagePerHp;
                            if (burn >= needed) value += AICardScoring.LethalBonus;
                        }
                        break;
                    }
                    case PushOccupantsEffect when cell.Occupant != null && cell.Occupant.IsEnemyOf(source):
                    {
                        bool hasExit = false;
                        foreach (HexCell neighbor in grid.GetNeighbors(cell.Coordinates))
                        {
                            if (neighbor != null && neighbor.IsWalkable)
                            {
                                hasExit = true;
                                break;
                            }
                        }
                        value += hasExit ? 3f : AICardScoring.LethalBonus;
                        break;
                    }
                    case SpawnRockEffect:
                        // A rising rock walls adjacent enemies into place; worth a bit for each one caged.
                        foreach (HexCell neighbor in grid.GetNeighbors(cell.Coordinates))
                        {
                            Character victim = neighbor == null ? null : neighbor.Occupant;
                            if (victim != null && victim.IsEnemyOf(source)
                                && victim.Stats != null && victim.Stats.IsAlive && !victim.IsStructure)
                                value += AICardScoring.MovePerHex * 0.5f;
                        }
                        break;
                }
            }

            return value;
        }

        /// <summary>
        /// Every character in range of the card that matches its target type (enemy/ally
        /// decided by TeamId so PvP treats each player as a lone team).
        /// </summary>
        private IEnumerable<Character> GetTargetsInRange(Character source, CardData card)
        {
            foreach (HexCell cell in grid.GetCellsInRange(source.CurrentCell.Coordinates, card.Range))
            {
                Character occupant = cell.Occupant;
                if (occupant == null || occupant == source) continue;
                if (occupant.Stats == null || !occupant.Stats.IsAlive) continue;

                switch (card.TargetType)
                {
                    case TargetType.EnemyCharacter when !occupant.IsEnemyOf(source):
                    case TargetType.AllyCharacter when !occupant.IsAllyOf(source):
                        continue;
                }

                yield return occupant;
            }
        }

        private static List<Character> FindAliveEnemies(Character source)
        {
            var enemies = new List<Character>();
            foreach (Character c in FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                if (c == null || c == source || c.Stats == null || !c.Stats.IsAlive) continue;
                if (c.IsEnemyOf(source)) enemies.Add(c);
            }
            return enemies;
        }

        private static int DistanceToNearest(List<Character> enemies, HexCoordinates from)
        {
            int best = -1;
            foreach (Character enemy in enemies)
            {
                int distance = from.DistanceTo(enemy.CurrentCell.Coordinates);
                if (best < 0 || distance < best) best = distance;
            }
            return best;
        }

        /// <summary>
        /// Pays a card's cost by discarding the least valuable hand cards first, keeping
        /// expensive plays in hand.
        /// </summary>
        private static List<CardInstance> PickCostCards(List<CardInstance> hand, CardInstance played)
        {
            var pool = new List<CardInstance>(hand);
            pool.Remove(played);
            pool.Sort((a, b) => a.Cost.CompareTo(b.Cost));

            var result = new List<CardInstance>();
            for (int i = 0; i < played.Cost && i < pool.Count; i++)
                result.Add(pool[i]);
            return result;
        }
    }
}
