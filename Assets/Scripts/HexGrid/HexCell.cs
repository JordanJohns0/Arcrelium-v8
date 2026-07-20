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
        public HexCoordinates Coordinates { get; private set; }
        public Character Occupant { get; private set; }
        public bool IsBlocked { get; set; } // for terrain/obstacles, independent of occupant

        public bool IsOccupied => Occupant != null;
        public bool IsWalkable => !IsBlocked && !IsOccupied;

        public void Initialize(HexCoordinates coordinates)
        {
            Coordinates = coordinates;
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
