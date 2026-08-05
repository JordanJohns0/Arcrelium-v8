---
type: system
name: Networking
status: drafted
tags:
  - wiki/system
---

# Networking

## Overview

PvP is an opt-in 8-player, last-team-standing lobby mode (no AI). PvE is
single-player or up to 4 in a lobby and stays behaviorally identical offline.
Lobby state (members, match mode) is authoritative via Steam; the match-mode flag in
lobby data is how every machine agrees on PvE vs PvP.

## Files

- `Assets/Scripts/Steam/LobbyManager.cs` — lobby create/join/leave, match mode, member tracking
- `Assets/Scripts/Steam/SteamManager.cs`
- `Assets/Scripts/Networking/NetworkManager.cs`
- `Assets/Scripts/Networking/NetworkMessages.cs` — GameStateSync, CharacterState, PlayCardRequest, PassRequest, ReadyRequest/ReadyStateSync, ChatMessage, LoadoutSync, CardPlayedDisplay
- `Assets/Scripts/Networking/ReadyTracker.cs`

## Tests

- `Assets/Tests/Editor/NetworkMessageTests.cs`
- `Assets/Tests/Editor/CharacterSyncTests.cs`
- `Assets/Tests/Editor/TurnManagerSyncTests.cs`

## Related

- [[Wiki/Systems/Turn Order]]
