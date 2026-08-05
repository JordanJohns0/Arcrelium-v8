---
type: card
name: Poke
cost: 1
target: Enemy
range: 1
effect: Attack
status: implemented
tags:
  - wiki/card
---
## Description
Deal 1 damage

## Rules / Notes

## Data consistency
- [x] Asset exists in `Assets/CardData/` #card ✅ 2026-08-05
- [x] Registered in `Assets/Resources/GameDataLibrary.asset` #card ✅ 2026-08-05
- [x] `SpecFor` golden row added in `Assets/Tests/Editor/CardExecutionTests.cs` #card ✅ 2026-08-05
- [x] Test Runner: `Card_PlaysSuccessfully` + `Card_Outcome_MatchesDesign` pass #card ✅ 2026-08-05

## Related
- [[Wiki/Systems/Combat Flow]]
- [[Wiki/Systems/Cost Model]]
