using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cards;
using Characters;
using HexGrid;
using Game;
using Networking;

namespace UI
{
    public class TurnFlowUIController : MonoBehaviour
    {
        private enum SelectionState { Idle, SelectingCost, SelectingTarget }

        [Header("References")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private HandView handView;
        [SerializeField] private int humanPlayerId = 0;

        private bool IsClient => Networking.NetworkManager.Instance != null && !Networking.NetworkManager.Instance.IsHost;

        [Header("UI")]
        [SerializeField] private Button passButton;
        [SerializeField] private Button cancelButton;
        [Tooltip("Shows whose turn it is and what to do next (e.g. 'Select 2 cards to discard').")]
        [SerializeField] private TMP_Text promptText;

        private SelectionState state = SelectionState.Idle;
        private CardInstance pendingCard;
        private readonly List<CardInstance> pendingCostCards = new List<CardInstance>();

        [Header("Board Highlighting")]
        [SerializeField] private HexGridManager grid;
        [SerializeField] private Color validTargetColor = new Color(0.4f, 0.85f, 0.4f, 1f);
        private readonly List<(HexCell cell, Color originalColor)> highlightedCells = new List<(HexCell, Color)>();
        private readonly List<Character> highlightedCharacters = new List<Character>();

        private void OnEnable()
        {
            turnManager.OnTurnStarted += HandleTurnStarted;
            handView.OnCardClicked += HandleCardClicked;
            HexCellSelectable.AnyCellClicked += HandleCellClicked;
            CharacterSelectable.AnyCharacterClicked += HandleCharacterClicked;

            if (passButton != null) passButton.onClick.AddListener(HandlePassClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(ResetSelection);
        }

        private void OnDisable()
        {
            turnManager.OnTurnStarted -= HandleTurnStarted;
            handView.OnCardClicked -= HandleCardClicked;
            HexCellSelectable.AnyCellClicked -= HandleCellClicked;
            CharacterSelectable.AnyCharacterClicked -= HandleCharacterClicked;

            if (passButton != null) passButton.onClick.RemoveListener(HandlePassClicked);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(ResetSelection);

            ClearBoardHighlights();
        }

        private void HandleTurnStarted(Character character)
        {
            if (character == null) return;

            if (character.OwnerPlayerId != humanPlayerId)
            {
                if (promptText != null) promptText.text = $"{character.Stats.CharacterName}'s turn...";
                return;
            }

            handView.SetCharacter(character);
            ResetSelection();
        }

        private void HandlePassClicked()
        {
            if (state != SelectionState.Idle) return;
            if (turnManager.Busy) return;
            if (IsClient)
                NetworkManager.Instance.SendPassRequest();
            else
                turnManager.Pass();
        }

        private void HandleCardClicked(CardInstance card)
        {
            if (turnManager.Busy) return;

            switch (state)
            {
                case SelectionState.Idle:
                    BeginPlaying(card);
                    break;

                case SelectionState.SelectingCost:
                    ToggleCostCard(card);
                    break;

                case SelectionState.SelectingTarget:
                    if (card == pendingCard) ResetSelection();
                    break;
            }
        }

        private void BeginPlaying(CardInstance card)
        {
            pendingCard = card;
            pendingCostCards.Clear();

            if (card.Data.Cost > 0)
            {
                state = SelectionState.SelectingCost;
                UpdatePrompt();
            }
            else if (RequiresBoardTarget(card.Data))
            {
                state = SelectionState.SelectingTarget;
                UpdatePrompt();
                HighlightValidTargets();
            }
            else
            {
                SubmitPlay();
            }

            RefreshHandHighlights();
        }

        private void ToggleCostCard(CardInstance card)
        {
            if (card == pendingCard) return; // can't pay a card's cost with itself

            if (pendingCostCards.Contains(card))
            {
                pendingCostCards.Remove(card);
            }
            else if (pendingCostCards.Count < pendingCard.Data.Cost)
            {
                pendingCostCards.Add(card);
            }

            RefreshHandHighlights();
            UpdatePrompt();

            if (pendingCostCards.Count == pendingCard.Data.Cost)
            {
                if (RequiresBoardTarget(pendingCard.Data))
                {
                    state = SelectionState.SelectingTarget;
                    RefreshHandHighlights();
                    UpdatePrompt();
                    HighlightValidTargets();
                }
                else
                {
                    SubmitPlay();
                }
            }
        }

        private void HandleCellClicked(HexCell cell)
        {
            if (turnManager.Busy) return;
            if (state != SelectionState.SelectingTarget) return;
            if (pendingCard.Data.TargetType != TargetType.HexCell) return;

            SubmitPlay(targetCell: cell);
        }

        private void HandleCharacterClicked(Character character)
        {
            if (turnManager.Busy) return;
            if (state != SelectionState.SelectingTarget) return;

            bool wantsCharacterTarget =
                pendingCard.Data.TargetType == TargetType.EnemyCharacter ||
                pendingCard.Data.TargetType == TargetType.AllyCharacter ||
                pendingCard.Data.TargetType == TargetType.AnyCharacter;

            if (!wantsCharacterTarget) return;

            SubmitPlay(targetCharacter: character);
        }

        private void SubmitPlay(HexCell targetCell = null, Character targetCharacter = null)
        {
            if (IsClient)
            {
                NetworkManager.Instance.SendPlayCardRequest(new PlayCardRequest
                {
                    cardInstanceId = pendingCard.GetInstanceID(),
                    targetQ = targetCell?.Coordinates.Q,
                    targetR = targetCell?.Coordinates.R,
                    targetCharacterId = targetCharacter != null ? targetCharacter.GetInstanceID() : null
                });
                ResetSelection();
                return;
            }

            bool success = turnManager.PlayCard(pendingCard, new List<CardInstance>(pendingCostCards), targetCell, targetCharacter);

            if (!success)
            {
                if (promptText != null) promptText.text = "That's not a valid play — choose again.";
                if (state == SelectionState.SelectingCost || RequiresBoardTarget(pendingCard.Data))
                {
                    state = SelectionState.SelectingTarget;
                }
                return;
            }

            ResetSelection();
        }

        private static bool RequiresBoardTarget(CardData card)
        {
            return card.TargetType != TargetType.None && card.TargetType != TargetType.Self;
        }

        private void ResetSelection()
        {
            state = SelectionState.Idle;
            pendingCard = null;
            pendingCostCards.Clear();
            handView.ClearHighlights();
            ClearBoardHighlights();
            UpdatePrompt();
        }

        private void HighlightValidTargets()
        {
            ClearBoardHighlights();
            if (pendingCard == null || turnManager.ActiveCharacter == null) return;
            if (!RequiresBoardTarget(pendingCard.Data)) return;

            Character source = turnManager.ActiveCharacter;
            HexCoordinates origin = source.CurrentCell.Coordinates;

            foreach (HexCell cell in grid.GetCellsInRange(origin, pendingCard.Data.Range))
            {
                if (cell == source.CurrentCell) continue;
                if (!IsValidTarget(pendingCard.Data, source, cell)) continue;

                HexMeshGenerator meshGen = cell.GetComponent<HexMeshGenerator>();
                if (meshGen != null)
                {
                    highlightedCells.Add((cell, meshGen.InteriorColor));
                    meshGen.SetInteriorColor(validTargetColor);
                }

                if (cell.Occupant != null && !highlightedCharacters.Contains(cell.Occupant))
                {
                    cell.Occupant.SetHighlighted(validTargetColor);
                    highlightedCharacters.Add(cell.Occupant);
                }
            }
        }

        private static bool IsValidTarget(CardData card, Character source, HexCell cell)
        {
            switch (card.TargetType)
            {
                case TargetType.HexCell:
                    return cell.IsWalkable;
                case TargetType.EnemyCharacter:
                    return cell.Occupant != null
                        && cell.Occupant.OwnerPlayerId != source.OwnerPlayerId
                        && cell.Occupant.Stats.IsAlive;
                case TargetType.AllyCharacter:
                    return cell.Occupant != null
                        && cell.Occupant.OwnerPlayerId == source.OwnerPlayerId
                        && cell.Occupant.Stats.IsAlive;
                case TargetType.AnyCharacter:
                    return cell.Occupant != null && cell.Occupant.Stats.IsAlive;
                default:
                    return false;
            }
        }

        private void ClearBoardHighlights()
        {
            foreach (var entry in highlightedCells)
            {
                if (entry.cell == null) continue;
                HexMeshGenerator meshGen = entry.cell.GetComponent<HexMeshGenerator>();
                if (meshGen != null) meshGen.SetInteriorColor(entry.originalColor);
            }
            highlightedCells.Clear();

            foreach (Character character in highlightedCharacters)
            {
                if (character != null) character.ClearHighlight();
            }
            highlightedCharacters.Clear();
        }

        private void RefreshHandHighlights()
        {
            var highlighted = new List<CardInstance>(pendingCostCards);
            if (pendingCard != null) highlighted.Add(pendingCard);
            CardInstance nonInteractable = state == SelectionState.SelectingCost ? pendingCard : null;
            handView.SetHighlighted(highlighted, nonInteractable);
        }

        private void UpdatePrompt()
        {
            if (promptText == null) return;

            switch (state)
            {
                case SelectionState.Idle:
                    promptText.text = turnManager.ActiveCharacter != null
                        ? $"{turnManager.ActiveCharacter.Stats.CharacterName}'s turn — play cards or End Turn."
                        : string.Empty;
                    break;

                case SelectionState.SelectingCost:
                    int remaining = pendingCard.Data.Cost - pendingCostCards.Count;
                    promptText.text = $"Choose {remaining} more card(s) to discard as cost for {pendingCard.Data.CardName}.";
                    break;

                case SelectionState.SelectingTarget:
                    promptText.text = GetTargetPrompt(pendingCard.Data);
                    break;
            }
        }

        private static string GetTargetPrompt(CardData card)
        {
            switch (card.TargetType)
            {
                case TargetType.HexCell:
                    return $"Select a cell to move to (range {card.Range}).";
                case TargetType.EnemyCharacter:
                    return $"Select an enemy to target with {card.CardName} (range {card.Range}).";
                case TargetType.AllyCharacter:
                    return $"Select an ally to target with {card.CardName} (range {card.Range}).";
                case TargetType.AnyCharacter:
                    return $"Select a character to target with {card.CardName} (range {card.Range}).";
                default:
                    return "Select a target.";
            }
        }
    }
}
