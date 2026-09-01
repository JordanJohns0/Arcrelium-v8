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

        private static bool IsClient => Networking.NetworkManager.Instance != null
            && !Networking.NetworkManager.Instance.IsHost
            && LobbyManager.Instance.CurrentLobby.HasValue;

        private bool IsLocalTurn => turnManager != null
            && turnManager.ActiveCharacter != null
            && turnManager.ActiveCharacter.OwnerPlayerId == GameState.LocalPlayerId;

        [Header("UI")]
        [SerializeField] private Button passButton;
        [SerializeField] private Button cancelButton;
        [Tooltip("Shows whose turn it is and what to do next (e.g. 'Select 2 cards to discard').")]
        [SerializeField] private TMP_Text promptText;

        private SelectionState state = SelectionState.Idle;
        private CardInstance pendingCard;
        private readonly List<CardInstance> pendingCostCards = new List<CardInstance>();
        private bool requestPending;

        [Header("Board Highlighting")]
        [SerializeField] private HexGridManager grid;
        [SerializeField] private Color validTargetColor = new Color(0.4f, 0.85f, 0.4f, 1f);
        private readonly List<HexMeshGenerator> highlightedCells = new List<HexMeshGenerator>();
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
            requestPending = false;

            if (character.OwnerPlayerId != GameState.LocalPlayerId)
            {
                if (promptText != null) promptText.text = $"{character.Stats.CharacterName}'s turn...";
                if (passButton != null) passButton.interactable = false;
                return;
            }

            if (passButton != null) passButton.interactable = true;
            handView.SetCharacter(character);
            ResetSelection();
        }

        private void HandlePassClicked()
        {
            if (!IsLocalTurn) return;
            if (turnManager.Busy) return;
            if (requestPending) return;
            if (state != SelectionState.Idle) ResetSelection();
            if (IsClient)
            {
                requestPending = true;
                NetworkManager.Instance.SendPassRequest();
            }
            else
                turnManager.Pass();
        }

        private void HandleCardClicked(CardInstance card)
        {
            if (!IsLocalTurn) return;
            if (turnManager.Busy) return;
            if (requestPending) return;

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
            Debug.Log($"[TurnFlow] HandleCellClicked: cell={cell.Coordinates}, state={state}, busy={turnManager.Busy}");
            if (!IsLocalTurn) return;
            if (turnManager.Busy) return;
            if (state != SelectionState.SelectingTarget) return;
            if (pendingCard.Data.TargetType != TargetType.HexCell)
            {
                if (promptText != null) promptText.text = "This card targets a character, not a cell.";
                return;
            }

            SubmitPlay(targetCell: cell);
        }

        private void HandleCharacterClicked(Character character)
        {
            if (!IsLocalTurn) return;
            if (turnManager.Busy) return;
            if (state != SelectionState.SelectingTarget) return;

            bool wantsCharacterTarget =
                pendingCard.Data.TargetType == TargetType.EnemyCharacter ||
                pendingCard.Data.TargetType == TargetType.AllyCharacter ||
                pendingCard.Data.TargetType == TargetType.AnyCharacter;

            if (!wantsCharacterTarget)
            {
                if (promptText != null) promptText.text = "This card targets a cell, not a character.";
                return;
            }

            if (!IsValidCharacterTarget(pendingCard.Data, turnManager.ActiveCharacter, character))
            {
                if (promptText != null) promptText.text = "That character isn't a valid target for this card.";
                return;
            }

            SubmitPlay(targetCharacter: character);
        }

        private static bool IsValidCharacterTarget(CardData card, Character source, Character target)
        {
            if (target == null || !target.Stats.IsAlive) return false;
            switch (card.TargetType)
            {
                case TargetType.EnemyCharacter:
                    return target.IsEnemyOf(source);
                case TargetType.AllyCharacter:
                    return target.IsAllyOf(source);
                case TargetType.AnyCharacter:
                    return true;
                default:
                    return false;
            }
        }

        private void SubmitPlay(HexCell targetCell = null, Character targetCharacter = null)
        {
            if (IsClient)
            {
                Character source = turnManager.ActiveCharacter;
                IReadOnlyList<CardInstance> handCards = source != null ? source.Hand.Cards : null;
                NetworkManager.Instance.SendPlayCardRequest(new PlayCardRequest
                {
                    cardHandIndex = FindCardIndex(handCards, pendingCard),
                    costCardHandIndices = FindCardIndices(handCards, pendingCostCards),
                    hasBoardTarget = targetCell != null,
                    targetQ = targetCell != null ? targetCell.Coordinates.Q : 0,
                    targetR = targetCell != null ? targetCell.Coordinates.R : 0,
                    targetOwnerPlayerId = targetCharacter != null ? targetCharacter.OwnerPlayerId : -1
                });
                requestPending = true;
                ResetSelection();
                return;
            }

            bool success = turnManager.PlayCard(pendingCard, new List<CardInstance>(pendingCostCards), targetCell, targetCharacter);

            Debug.Log($"[TurnFlow] SubmitPlay: card={pendingCard.Data.CardName}, success={success}, phase={turnManager.CurrentPhase}, busy={turnManager.Busy}");

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

        private static int FindCardIndex(IReadOnlyList<CardInstance> cards, CardInstance card)
        {
            if (cards == null || card == null) return -1;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == card) return i;
            }
            return -1;
        }

        private static List<int> FindCardIndices(IReadOnlyList<CardInstance> cards, IReadOnlyCollection<CardInstance> targets)
        {
            var indices = new List<int>();
            if (cards == null || targets == null) return indices;
            foreach (CardInstance target in targets)
                indices.Add(FindCardIndex(cards, target));
            return indices;
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

            int count = 0;
            foreach (HexCell cell in grid.GetCellsInRange(origin, pendingCard.Data.Range))
            {
                if (cell == source.CurrentCell) continue;
                if (!IsValidTarget(pendingCard.Data, source, cell)) continue;
                count++;

                HexMeshGenerator meshGen = cell.GetComponent<HexMeshGenerator>();
                if (meshGen != null)
                {
                    meshGen.SetHighlight(true, validTargetColor);
                    highlightedCells.Add(meshGen);
                }

                if (cell.Occupant != null && !highlightedCharacters.Contains(cell.Occupant))
                {
                    cell.Occupant.SetHighlighted(validTargetColor);
                    highlightedCharacters.Add(cell.Occupant);
                }
            }
            Debug.Log($"[TurnFlow] Highlighted {count} valid targets for {pendingCard.Data.CardName} (type={pendingCard.Data.TargetType}, range={pendingCard.Data.Range})");

            TryAutoSelectSoleAllyTarget(count);
        }

        private void TryAutoSelectSoleAllyTarget(int validTargetCount)
        {
            CardData card = pendingCard?.Data;
            Character source = turnManager.ActiveCharacter;
            if (card == null || source == null) return;

            if (card.TargetType != TargetType.AllyCharacter) return;
            if (validTargetCount > 0) return;

            if (IsValidCharacterTarget(card, source, source))
            {
                Debug.Log($"[TurnFlow] Auto-selecting self as the only valid ally target for {card.CardName}");
                SubmitPlay(targetCharacter: source);
            }
        }

        private static bool IsValidTarget(CardData card, Character source, HexCell cell)
        {
            switch (card.TargetType)
            {
                case TargetType.HexCell:
                    // Blocked terrain is never targetable; occupancy only matters for
                    // Empty-constrained cards. Using IsWalkable here used to un-highlight
                    // occupied cells that clicks still allowed (e.g. AoE like Fireball).
                    if (cell.IsBlocked) return false;
                    if (card.CellConstraint == CellConstraint.Empty && cell.Occupant != null) return false;
                    return true;
                case TargetType.EnemyCharacter:
                    return cell.Occupant != null
                        && cell.Occupant.IsEnemyOf(source)
                        && cell.Occupant.Stats.IsAlive;
                case TargetType.AllyCharacter:
                    return cell.Occupant != null
                        && cell.Occupant.IsAllyOf(source)
                        && cell.Occupant.Stats.IsAlive;
                case TargetType.AnyCharacter:
                    return cell.Occupant != null && cell.Occupant.Stats.IsAlive;
                default:
                    return false;
            }
        }

        private void ClearBoardHighlights()
        {
            foreach (HexMeshGenerator meshGen in highlightedCells)
            {
                if (meshGen != null) meshGen.SetHighlight(false, Color.clear);
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
            CardInstance nonInteractable = state == SelectionState.SelectingCost ? pendingCard : null;
            handView.SetHighlighted(
                pendingCard != null ? new[] { pendingCard } : null,
                nonInteractable,
                pendingCostCards);
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
