---
type: card
name: Spiked Handle
cost: 0
target: Self
range: 1
effect: Composite
status: designed
tags:
  - wiki/card
---
## Description
Draw 2 cards, take 1 damage.

## Rules / Notes
- Composite of `DrawCardsEffect_2` (draw 2) + `SelfDamageEffect_1` (take 1).
- Both child effects ignore the declared Range 1 / TargetType Self.
- Block absorbs the self-damage: with block up, taking 1 damage reduces block, not health.

## Data consistency
- [x] Asset exists in `Assets/CardData/Self-Damage/Spiked Handle.asset` #card
- [x] Registered in `Assets/Resources/GameDataLibrary.asset` #card
- [x] `SpecFor` golden row added in `Assets/Tests/Editor/CardExecutionTests.cs` #card
- [ ] Test Runner: `Card_PlaysSuccessfully` + `Card_Outcome_MatchesDesign` pass #card — pending reimport of `Take_1_Draw_2.asset` + `SelfDamage_1.asset` (missing-script cache)

## Related
- [[Wiki/Systems/Combat Flow]]
- [[Wiki/Systems/Cost Model]]
