# Fantasy Brawl Base — Architecture Notes

A minimal but complete skeleton: hex grid + characters + a card system that is the
*only* way to take actions. Nothing here is Super Fantasy Brawl-specific yet —
it's the plumbing to build that on top of.

## Folder layout

```
Scripts/
  HexGrid/
    HexCoordinates.cs   - axial coordinate math (distance, neighbors, world position)
    HexCell.cs           - a single tile MonoBehaviour (occupant, blocked state)
    HexGridManager.cs    - builds the board, lookups, BFS pathing
  Characters/
    CharacterStats.cs    - plain data: health, movement, attack, armor
    Character.cs          - MonoBehaviour: stats + deck/hand + board position
  Cards/
    ICardEffect.cs        - interface + CardContext (source, grid, target, range)
    CardEffectSO.cs       - abstract ScriptableObject base so effects are editor assets
    CardData.cs           - ScriptableObject: name, target type, range, effect
    Deck.cs                - draw pile / discard pile with auto-reshuffle
    Hand.cs                 - cards currently held
    Effects/
      MoveEffect.cs        - example: move to a targeted cell within range
      AttackEffect.cs      - example: damage a targeted character within range
  Game/
    ActionResolver.cs     - the single entry point for playing a card (PlayCard)
    TurnManager.cs         - turn order, draw/play/end phases
    GameManager.cs          - spawns characters onto the grid, starts the match
```

## The core rule: cards are the only actions

`Character` deliberately has no `Move()` or `Attack()` methods. The only way anything
happens is:

```csharp
actionResolver.PlayCard(sourceCharacter, cardData, targetCell, targetCharacter);
```

`ActionResolver` checks the card is in hand, validates the target against the card's
`TargetType`, asks the effect if it `CanExecute`, then runs it and moves the card to
the discard pile. To add a new kind of action, create a new `CardEffectSO` subclass
and a `CardData` asset that references it — no other code needs to change.

## Setting it up in a scene

1. Create a hex tile prefab, add `HexCell` to it.
2. Empty GameObject "Board" → add `HexGridManager`, assign the tile prefab.
3. Empty GameObject "GameManager" → add `GameManager`, `ActionResolver`, `TurnManager`
   (can be the same object or children), wire up the references in the inspector.
4. Create a character prefab with `Character` on it (assign a `Deck` with some
   starting `CardData` assets once you have some).
5. On `GameManager`, add entries to `Spawns`: prefab + owner player id + spawn hex
   coordinates.
6. Create a couple of `CardData` assets (`Create > Card`), set
   `TargetType` (e.g. `HexCell` for a move card, `EnemyCharacter` for an attack card),
   set `Range`, and assign a `MoveEffect`/`AttackEffect` asset (or your own) as `Effect`.
7. Press Play — `GameManager.Start()` builds the grid, spawns characters, and starts
   the turn loop. Wire your UI to call `actionResolver.PlayCard(...)` when the player
   selects a card and target, and `turnManager.EndTurn()` when they end their turn.

## Deliberately left open for you to design later

- Card costs / action-point economy (Super Fantasy Brawl uses a hand-size-as-resource
  model — you may want cards to have an "actions per turn" limit rather than one card
  per whole turn).
- Actual card content (the interesting part!) — attack patterns, positioning tricks,
  status effects, per-character unique decks.
- AI-facing hex highlighting / range preview in the view layer.
- Simultaneous "programmed" turns if you want that instead of strict turn order.
