using System.Collections.Generic;

namespace Networking
{
    public enum MessageType : byte
    {
        GameStateSync,
        PlayCardRequest,
        PassRequest,
        StartGame,
        ChatMessage
    }

    [System.Serializable]
    public class GameStateSync
    {
        public List<CharacterState> characterStates = new List<CharacterState>();
        public int currentTurnIndex;
        public int activeCharacterId;
    }

    [System.Serializable]
    public class CharacterState
    {
        public int characterId;
        public int ownerPlayerId;
        public int q;
        public int r;
        public int currentHp;
        public int maxHp;
        public int armor;
        public bool isAlive;
    }

    [System.Serializable]
    public class PlayCardRequest
    {
        public int cardInstanceId;
        public List<int> costCardIds = new List<int>();
        public int? targetQ;
        public int? targetR;
        public int? targetCharacterId;
    }

    [System.Serializable]
    public class PassRequest { }

    [System.Serializable]
    public class ChatMessage
    {
        public string senderName;
        public string message;
    }
}
