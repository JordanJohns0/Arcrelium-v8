using UnityEngine;

namespace FantasyBrawl.HexGrid
{
    /// <summary>
    /// Axial coordinates for a flat-top hex grid.
    /// Q = column axis, R = row axis. S is derived (Q + R + S = 0), used for distance/neighbor math.
    /// </summary>
    [System.Serializable]
    public struct HexCoordinates
    {
        public int Q;
        public int R;
        public int S => -Q - R;

        public HexCoordinates(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static readonly HexCoordinates[] Directions =
        {
            new HexCoordinates(1, 0),
            new HexCoordinates(1, -1),
            new HexCoordinates(0, -1),
            new HexCoordinates(-1, 0),
            new HexCoordinates(-1, 1),
            new HexCoordinates(0, 1)
        };

        public HexCoordinates Neighbor(int direction)
        {
            HexCoordinates dir = Directions[((direction % 6) + 6) % 6];
            return new HexCoordinates(Q + dir.Q, R + dir.R);
        }

        public int DistanceTo(HexCoordinates other)
        {
            int dq = Mathf.Abs(Q - other.Q);
            int dr = Mathf.Abs(R - other.R);
            int ds = Mathf.Abs(S - other.S);
            return Mathf.Max(dq, dr, ds);
        }

        /// <summary>World-space position for a flat-top hex grid of the given radius (edge-to-center).</summary>
        public Vector3 ToWorldPosition(float hexSize)
        {
            float x = hexSize * (3f / 2f * Q);
            float z = hexSize * (Mathf.Sqrt(3f) / 2f * Q + Mathf.Sqrt(3f) * R);
            return new Vector3(x, 0f, z);
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoordinates other && Q == other.Q && R == other.R;
        }

        public override int GetHashCode()
        {
            return Q * 31 + R;
        }

        public static bool operator ==(HexCoordinates a, HexCoordinates b) => a.Equals(b);
        public static bool operator !=(HexCoordinates a, HexCoordinates b) => !a.Equals(b);

        public override string ToString() => $"({Q}, {R})";
    }
}
