using System.Collections.Generic;
using UnityEngine;
using FantasyBrawl.Characters;
using FantasyBrawl.Cards;
using FantasyBrawl.HexGrid;

namespace FantasyBrawl.Game
{
    /// <summary>
    /// Handles automatic turns for AI-controlled characters. On each turn start, picks a
    /// random affordable card with a valid target and plays it; passes if nothing is viable.
    /// Assign humanPlayerId in the Inspector to 0 so that player 0 waits for human input
    /// and all other player IDs are handled by the AI.
    /// </summary>
    public class AIController : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private ActionResolver actionResolver;
        [SerializeField] private HexGridManager grid;
        [SerializeField] private int humanPlayerId = 0;

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
            if (character.OwnerPlayerId == humanPlayerId) return;

            PlayRandomCard(character);
        }

        private void PlayRandomCard(Character character)
        {
            Hand hand = character.Hand;
            List<CardInstance> handCards = new List<CardInstance>(hand.Cards);
            var playable = new List<(CardInstance card, List<CardInstance> cost, HexCell cell, Character target)>();

            foreach (CardInstance card in handCards)
            {
                if (card.Data.Cost > handCards.Count - 1) continue;

                List<CardInstance> costPool = new List<CardInstance>(handCards);
                costPool.Remove(card);
                List<CardInstance> costCards = PickRandom(costPool, card.Data.Cost);

                switch (card.Data.TargetType)
                {
                    case TargetType.None:
                    case TargetType.Self:
                        playable.Add((card, costCards, null, null));
                        break;

                    case TargetType.HexCell:
                    {
                        HexCell cell = FindRandomValidCell(character, card.Data);
                        if (cell != null) playable.Add((card, costCards, cell, null));
                        break;
                    }

                    case TargetType.EnemyCharacter:
                    case TargetType.AllyCharacter:
                    case TargetType.AnyCharacter:
                    {
                        Character target = FindRandomValidCharacter(character, card.Data);
                        if (target != null) playable.Add((card, costCards, null, target));
                        break;
                    }
                }
            }

            if (playable.Count > 0)
            {
                var chosen = playable[Random.Range(0, playable.Count)];
                turnManager.PlayCard(chosen.card, chosen.cost, chosen.cell, chosen.target);
            }
            else
            {
                turnManager.Pass();
            }
        }

        private HexCell FindRandomValidCell(Character source, CardData card)
        {
            var valid = new List<HexCell>();
            foreach (HexCell cell in grid.GetCellsInRange(source.CurrentCell.Coordinates, card.Range))
            {
                if (cell == source.CurrentCell) continue;
                if (card.TargetType == TargetType.HexCell && !cell.IsWalkable) continue;
                valid.Add(cell);
            }
            return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
        }

        private Character FindRandomValidCharacter(Character source, CardData card)
        {
            var valid = new List<Character>();
            foreach (HexCell cell in grid.GetCellsInRange(source.CurrentCell.Coordinates, card.Range))
            {
                if (cell.Occupant == null) continue;
                if (!cell.Occupant.Stats.IsAlive) continue;
                if (cell.Occupant == source) continue;

                bool isEnemy = cell.Occupant.OwnerPlayerId != source.OwnerPlayerId;
                bool isAlly = cell.Occupant.OwnerPlayerId == source.OwnerPlayerId;

                if (card.TargetType == TargetType.EnemyCharacter && !isEnemy) continue;
                if (card.TargetType == TargetType.AllyCharacter && !isAlly) continue;

                valid.Add(cell.Occupant);
            }
            return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
        }

        private static List<T> PickRandom<T>(List<T> source, int count)
        {
            var pool = new List<T>(source);
            var result = new List<T>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int idx = Random.Range(0, pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return result;
        }
    }
}
