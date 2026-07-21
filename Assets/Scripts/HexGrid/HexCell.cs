using UnityEngine;
using Characters;

namespace HexGrid
{
    /// <summary>
    /// A single tile on the board. Tracks its coordinates and whatever occupies it.
    /// Attach to your hex tile prefab.
    /// </summary>
    public class HexCell : MonoBehaviour
    {
        [SerializeField] private bool startBlocked;

        public HexCoordinates Coordinates { get; private set; }
        public Character Occupant { get; private set; }
        public bool IsBlocked { get; private set; }

        public bool IsOccupied => Occupant != null;
        public bool IsWalkable => !IsBlocked && !IsOccupied;

        public void Initialize(HexCoordinates coordinates)
        {
            Coordinates = coordinates;
            IsBlocked = startBlocked;
        }

        public void SetBlocked(bool blocked)
        {
            IsBlocked = blocked;
        }

        public void SetOccupant(Character character)
        {
            Occupant = character;
        }

        public void ClearOccupant()
        {
            Occupant = null;
        }
    }
}
