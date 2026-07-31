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
        RequestInitialState
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
        public int teamId;
        public int q;
        public int r;
        public int currentHp;
        public int maxHp;
        public int armor;
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

    [System.Serializable]
    public class ChatMessage
    {
        public string senderName;
        public string message;
    }
}
