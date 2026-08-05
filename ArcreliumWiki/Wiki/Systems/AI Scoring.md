---
type: system
name: AI Scoring
status: drafted
tags:
  - wiki/system
---

# AI Scoring

## Overview

Pure, unit-testable heuristics that decide how valuable a candidate card play is:
finishing kills, focusing damaged enemies, keeping the source alive, moving toward a
fight, and never wasting a draw at a full hand.

## Current heuristics

| Factor | Constant | Value |
| --- | --- | --- |
| Finishing a kill | `LethalBonus` | 10 |
| Damage per HP dealt | `DamagePerHp` | 1 |
| Self-damage per HP taken | `SelfDamagePerHp` | 1 |
| Damaging a near-death target | `WeakTargetBonus` | 0.5 |
| Heal per point | `HealPerPoint` | 1.5 |
| Heal when near death | `NearDeathHealBonus` | 5 |
| Block while threatened / idle | `BlockThreatened` / `BlockIdle` | 1.0 / 0.3 |
| Draw per card | `DrawPerCard` | 0.8 |
| Drawing at a full hand | `NoHandSpacePenalty` | -3 |
| Cost penalty per card | `CostPenalty` | 0.5 |
| Hexes closed toward a fight | `MovePerHex` | 3 |

## Files

- `Assets/Scripts/Game/AICardScoring.cs`
- `Assets/Scripts/Game/AIController.cs`
- `Assets/Tests/Editor/AICardScoringTests.cs`

## Related

- [[Wiki/Systems/Combat Flow]]
