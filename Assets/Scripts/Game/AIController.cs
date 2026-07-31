using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Characters;
using Cards;
using HexGrid;

namespace Game
{
    public class AIController : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private ActionResolver actionResolver;
        [SerializeField] private HexGridManager grid;
        [SerializeField] private float aiDelay = 0.4f;

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
            if (character.Team == Team.Ally) return;

            StartCoroutine(PlayAITurn(character));
        }

        private IEnumerator PlayAITurn(Character character)
        {
            while (turnManager.Busy)
                yield return null;

            yield return new WaitForSeconds(aiDelay);

            while (true)
            {
                var playable = FindPlayableCards(character);
                if (playable.Count == 0)
                {
                    turnManager.Pass();
                    yield break;
                }

                var chosen = playable[Random.Range(0, playable.Count)];
                turnManager.PlayCard(chosen.card, chosen.cost, chosen.cell, chosen.target);

                yield return new WaitForSeconds(0.3f);

                if (turnManager.Busy)
                    yield return null;
            }
        }

        private List<(CardInstance card, List<CardInstance> cost, HexCell cell, Character target)> FindPlayableCards(Character character)
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

            return playable;
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

                bool isEnemy = cell.Occupant.Team != source.Team;
                bool isAlly = cell.Occupant.Team == source.Team;

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
