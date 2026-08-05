---
type: system
name: Combat Flow
status: drafted
tags:
  - wiki/system
---

# Combat Flow

## Overview

Every action on the board goes through the card system - there is deliberately no
standalone "move" or "attack" API on `Character`, so it is impossible to bypass the card system.

## How it works

1. A player plays a card from their hand with an optional target.
2. `ActionResolver.PlayCard` validates: card in hand, cost payment valid, target valid.
3. The effect's `CanExecute` is consulted; then `OnBeforeCardPlay` fires.
4. `Execute` applies the effect; the played card and cost cards are discarded.
5. `OnCardPlayed` fires with the resolved play.

Costs are only paid after the play is confirmed valid __ a failed attempt never discards anything.

## Files

- `Assets/Scripts/Game/ActionResolver.cs`
- `Assets/Scripts/Cards/ICardEffect.cs` (CardContext, CardEffectSO, CardValue)
- `Assets/Scripts/Game/GameManager.cs`
- `Assets/Scripts/Game/GameState.cs`

## Related

- [[Wiki/Systems/Cost Model]]
- [[Wiki/Systems/Turn Order]]
