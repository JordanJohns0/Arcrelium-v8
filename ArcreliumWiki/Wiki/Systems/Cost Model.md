---
type: system
name: Cost Model
status: drafted
tags:
  - wiki/system
---

# Cost Model

## Overview

A card's `Cost` is the number of OTHER cards from the same hand that must be discarded
to play it. There is no mana or action-point resource - hand size is the economy.

## Rules

- Cost cards must number exactly `Cost`.
- All must be in hand, all distinct, and none may be the card being played.
- A card that costs `c` needs `1 + c` cards total (played + discarded) to be legal, so
  `Cost <= 4` keeps every card playable in a 5-card hand - enforced by the Test Runner.

## Files

- `Assets/Scripts/Game/ActionResolver.cs` (`IsCostValid`)
- `Assets/Tests/Editor/CardExecutionTests.cs`

## Related

- [[Wiki/Systems/Combat Flow]]
