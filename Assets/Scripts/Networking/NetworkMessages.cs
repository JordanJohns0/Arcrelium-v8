using System.Collections.Generic;

namespace Networking
{
    public enum MessageType : byte
    {
        GameStateSync,
        PlayCardRequest,
        PassRequest,
        StartGame,
        ChatMessage,
        RequestInitialState,
        ReadyRequest,
        ReadyStateSync,
        LoadNextLevel,
        LoadoutSync,
        CardPlayedDisplay,
        StageVoteRequest,
        StageVoteState
    }

    /// <summary>
    /// Client -> host. The sender's vote for one of the two stage options. The host records it
    /// for the sender's player (never trusting a player id from the message).
    /// </summary>
    [System.Serializable]
    public class StageVoteRequest
    {
        public int option = -1;
    }

    /// <summary>
    /// Host -> all clients. The whole stage vote: the two options (as enemy group and reward
    /// indices into the shared config), who has voted for what, and the result once resolved.
    /// Votes are parallel arrays because JsonUtility cannot serialize dictionaries.
    /// </summary>
    [System.Serializable]
    public class StageVoteState
    {
        public int[] enemyGroups = new int[2];
        public int[] rewards = new int[2];
        public int expectedVoters;
        public int[] votePlayerIds = new int[0];
        public int[] voteOptions = new int[0];
        public bool resolved;
        public int chosen = -1;
    }

    [System.Serializable]
    public class GameStateSync
    {
        public List<CharacterState> characterStates = new List<CharacterState>();
        public List<StructureState> structures = new List<StructureState>();
        public int currentTurnIndex;
        public int activeCharacterId;
        public bool victory;
        public bool gameOver;
        /// <summary>Host-decided: the end-of-level Special pane is the enchantment station (not the shop).</summary>
        public bool enchantArea;
    }

    /// <summary>
    /// A board structure (rock): an immovable, neutral blocking entity clients rebuild from
    /// the same prefab as the host. Never in turn order and never owns cards.
    /// </summary>
    [System.Serializable]
    public class StructureState
    {
        public int q;
        public int r;
        public int currentHp;
        public int maxHp;
        public int block;
    }

    [System.Serializable]
    public class CharacterState
    {
        public int characterId;
        public int ownerPlayerId;
        public int teamId;
        public int q;
        public int r;
        public int currentHp;
        public int maxHp;
        public int block;
        public bool isAlive;
        public string name;
        public float colorR;
        public float colorG;
        public float colorB;
        public string[] handCardNames;
    }

    [System.Serializable]
    public class PlayCardRequest
    {
        public int cardHandIndex = -1;
        public List<int> costCardHandIndices = new List<int>();
        public bool hasBoardTarget;
        public int targetQ;
        public int targetR;
        public int targetOwnerPlayerId = -1;
        /// <summary>Optional exact move route for movement-style plays (client -> host).</summary>
        public List<int> pathQ = new List<int>();
        public List<int> pathR = new List<int>();
    }

    [System.Serializable]
    public class PassRequest { }

    /// <summary>
    /// Client -> host. Toggles whether the sender's player is ready (lobby and victory).
    /// </summary>
    [System.Serializable]
    public class ReadyRequest
    {
        public bool ready;
    }

    /// <summary>
    /// Host -> clients. Full ready-state snapshot for every lobby member.
    /// </summary>
    [System.Serializable]
    public class ReadyStateSync
    {
        public List<PlayerReadyState> players = new List<PlayerReadyState>();
    }

    [System.Serializable]
    public class PlayerReadyState
    {
        public int playerIndex;
        public string name;
        public bool ready;
        public float colorR;
        public float colorG;
        public float colorB;
    }

    [System.Serializable]
    public class ChatMessage
    {
        public string senderName;
        public string message;
    }

    /// <summary>
    /// Client -> host. Sent when the sender confirms their loadout on the loadout screen.
    /// Contains only the equipped items' names, in fixed slot order (see
    /// EquipmentLoadout.ToNames) — the host never sees the sender's inventory or rewards.
    /// </summary>
    [System.Serializable]
    public class LoadoutSync
    {
        public int playerIndex = -1;
        public string[] loadoutNames = new string[Equipment.EquipmentLoadout.SlotCount];
        /// <summary>Enchantments this player applied this run, so the host can simulate them.</summary>
        public List<Equipment.EnchantmentAssignment> enchantments = new List<Equipment.EnchantmentAssignment>();
    }

    /// <summary>
    /// Host -> all clients. Announced after the host resolves a card play, so every machine
    /// can show the played card on screen. cardName is resolved to CardData locally via the
    /// GameDataLibrary; cost/description ride along so even if that lookup fails this
    /// machine still renders an accurate stand-in instead of a wrong or blank popup.
    /// </summary>
    [System.Serializable]
    public class CardPlayedDisplay
    {
        public string cardName;
        public int cost;
        public string description;
        public string actorName;
        public int actorPlayerId = -1;
    }
}
