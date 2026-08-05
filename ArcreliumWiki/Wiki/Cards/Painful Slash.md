---
type: card
name: Painful Slash
cost: 1
target: Enemy
range: 1
effect: Attack + Self-Damage
status: designed
tags:
  - wiki/card
---
## Description
Deal 4 damage to an enemy and 1 to yourself.

## Rules / Notes
- Two steps: (1) deal 4 damage to an enemy in range 1, (2) deal 1 damage to yourself (block absorbs the self-damage first).

## Data consistency
- [x] Asset exists in `Assets/CardData/` #card ✅ 2026-08-05
- [x] Registered in `Assets/Resources/GameDataLibrary.asset` #card ✅ 2026-08-05
- [x] `SpecFor` golden row added in `Assets/Tests/Editor/CardExecutionTests.cs` #card ✅ 2026-08-05
- [x] Test Runner: `Card_PlaysSuccessfully` + `Card_Outcome_MatchesDesign` pass #card ✅ 2026-08-05

## Related
- [[Wiki/Systems/Combat Flow]]
- [[Wiki/Systems/Cost Model]]
