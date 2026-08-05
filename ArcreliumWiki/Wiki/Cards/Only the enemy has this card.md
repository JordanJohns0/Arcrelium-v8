---
type: card
name: Only the enemy has this card
cost: 0
target: HexCell
range: 1
effect: Move
status: designed
tags:
  - wiki/card
---
## Description
Move 1 hex.

## Rules / Notes
- Source asset file is `Assets/CardData/ENEMY ONLY.asset`; its `CardName` field is `Only the enemy has this card`.
- The enemy faction's movement card — same effect as Walk (MoveEffect, move 1).

## Data consistency
- [x] Asset exists in `Assets/CardData/ENEMY ONLY.asset` #card
- [x] Registered in `Assets/Resources/GameDataLibrary.asset` #card
- [x] `SpecFor` golden row added in `Assets/Tests/Editor/CardExecutionTests.cs` #card
- [x] Test Runner: `Card_PlaysSuccessfully` + `Card_Outcome_MatchesDesign` pass #card

## Related
- [[Wiki/Systems/Combat Flow]]
- [[Wiki/Systems/Cost Model]]
