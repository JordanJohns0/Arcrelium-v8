using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Characters;
using Cards;
using Cards.Effects;
using HexGrid;

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
                    turnManager.Pass();
                    yield break;
                }

                if (!turnManager.PlayCard(best.Card, best.Cost, best.Cell, best.Target))
                {
                    // Every candidate is pre-validated, so a rejection means the turn
                    // is no longer playable (e.g. a state change mid-AI). End the turn
                    // cleanly instead of soft-locking on an unplayable hand.
                    turnManager.Pass();
                    yield break;
                }

                // Pace the turn: each card resolves visibly for at least one second
                // before the next play goes in.
                yield return new WaitForSeconds(Mathf.Max(MinPlayIntervalSeconds, aiPlayInterval));

                if (turnManager.Busy)
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
                if (card.Data.Cost > hand.Count - 1) continue;
                List<CardInstance> cost = PickCostCards(hand, card);

                switch (card.Data.TargetType)
                {
                    case TargetType.None:
                    case TargetType.Self:
                    {
                        if (!AllStepsCanExecute(character, card.Data, null, null)) break;
                        AddCandidate(ref best, card, cost, null, null,
                            AICardScoring.Score(card.Data, character, null, nearestEnemyDistance));
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
                            float score = AICardScoring.Score(card.Data, character, null, after)
                                + AICardScoring.ScoreMove(before, after)
                                + AreaDamageValue(card.Data, character, enemies, cell);
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
                                AICardScoring.Score(card.Data, character, target, nearestEnemyDistance));
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
            pool.Sort((a, b) => (a.Data != null ? a.Data.Cost : 0).CompareTo(b.Data != null ? b.Data.Cost : 0));

            var result = new List<CardInstance>();
            for (int i = 0; i < played.Data.Cost && i < pool.Count; i++)
                result.Add(pool[i]);
            return result;
        }
    }
}
