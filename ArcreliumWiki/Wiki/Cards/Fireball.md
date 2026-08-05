---
type: card
name: Fireball
cost: 2
target: HexCell
range: 3
effect: Attack
status: designed
tags:
  - wiki/card
---
## Description
Target a hex. Deal 3 damage to the character there and 1 damage to each adjacent character.

## Rules / Notes
- 

## Data consistency
- [ ] Asset exists in `Assets/CardData/` #card
- [ ] Registered in `Assets/Resources/GameDataLibrary.asset` #card
- [ ] `SpecFor` golden row added in `Assets/Tests/Editor/CardExecutionTests.cs` #card
- [ ] Test Runner: `Card_PlaysSuccessfully` + `Card_Outcome_MatchesDesign` pass #card

## Related
- [[Wiki/Systems/Combat Flow]]
- [[Wiki/Systems/Cost Model]]
