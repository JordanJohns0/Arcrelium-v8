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
        LoadNextLevel
    }

    [System.Serializable]
    public class GameStateSync
    {
        public List<CharacterState> characterStates = new List<CharacterState>();
        public int currentTurnIndex;
        public int activeCharacterId;
        public bool victory;
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
        public int armor;
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
}
