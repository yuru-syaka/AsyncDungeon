using System;

namespace DungeonGame.Core.Dungeon
{
    /// <summary>グリッド上の座標(整数)。左上が(0,0)、Yは下向きに増える。</summary>
    [Serializable]
    public struct GridPos : IEquatable<GridPos>
    {
        public int X;
        public int Y;

        public GridPos(int x, int y) { X = x; Y = y; }

        /// <summary>4方向の隣接オフセット。順番は決定論のため固定(上・右・下・左)。</summary>
        public static readonly GridPos[] Dir4 =
        {
            new GridPos(0, -1), new GridPos(1, 0), new GridPos(0, 1), new GridPos(-1, 0)
        };

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y);
        public static bool operator ==(GridPos a, GridPos b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(GridPos a, GridPos b) => !(a == b);

        public bool Equals(GridPos other) => this == other;
        public override bool Equals(object obj) => obj is GridPos p && this == p;
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }
}
