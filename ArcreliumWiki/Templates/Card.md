---
type: card
name: <% tp.file.title %>
cost: <% tp.system.prompt("Cost (cards to discard to play this card)") %>
target: <% tp.system.prompt("TargetType: None / Self / EnemyCharacter / AllyCharacter / AnyCharacter / HexCell") %>
range: <% tp.system.prompt("Range (hexes)") %>
effect: <% tp.system.prompt("Effect: Attack / Heal / GainBlock / DrawCards / Move / SelfDamage / Composite") %>
status: designed
tags:
  - wiki/card
---

# <% tp.file.title %>

## Description

_What the card does, in plain words._

## Rules / Notes

## Data consistency

- [ ] Asset exists in `Assets/CardData/` #card
- [ ] Registered in `Assets/Resources/GameDataLibrary.asset` #card
- [ ] `SpecFor` golden row added in `Assets/Tests/Editor/CardExecutionTests.cs` #card
- [ ] Test Runner: `Card_PlaysSuccessfully` + `Card_Outcome_MatchesDesign` pass #card

## Related

- Effect: [[]]
- [[Wiki/Systems/Combat Flow]]
- [[Wiki/Systems/Cost Model]]
