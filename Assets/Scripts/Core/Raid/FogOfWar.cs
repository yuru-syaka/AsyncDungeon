using DungeonGame.Core.Determinism;
using DungeonGame.Core.Dungeon;

namespace DungeonGame.Core.Raid
{
    /// <summary>
    /// 勇者側から見た霧。一度見えたマスは「既知」になり、勇者AIは既知のマスだけを使って道を考える。
    /// 勇者の周囲(視界)と、たいまつで照らした範囲が既知になる。壁越しにも見える簡易版。
    /// </summary>
    public sealed class FogOfWar
    {
        private readonly bool[] _known;
        private readonly DungeonMap _map;

        public FogOfWar(DungeonMap map)
        {
            _map = map;
            _known = new bool[map.Width * map.Height];
        }

        public bool IsKnown(int x, int y) => _map.InBounds(x, y) && _known[_map.Index(x, y)];
        public bool IsKnown(GridPos p) => IsKnown(p.X, p.Y);

        /// <summary>中心から半径 radius(チェビシェフ距離)のマスを既知にする。新しく見えたマス数を返す。</summary>
        public int Reveal(GridPos center, int radius)
        {
            int count = 0;
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                if (!_map.InBounds(x, y)) continue;
                int i = _map.Index(x, y);
                if (_known[i]) continue;
                _known[i] = true;
                count++;
            }
            return count;
        }

        /// <summary>未踏の境界か(既知の歩けるマスで、隣に未知のマスがある)。探索の目標になる。</summary>
        public bool IsFrontier(GridPos p)
        {
            if (!IsKnown(p) || !_map.IsWalkable(p)) return false;
            foreach (var d in GridPos.Dir4)
            {
                var n = p + d;
                if (_map.InBounds(n) && !IsKnown(n)) return true;
            }
            return false;
        }

        public int KnownCount
        {
            get { int c = 0; foreach (var k in _known) if (k) c++; return c; }
        }

        public void AddToHash(ref StateHasher h)
        {
            foreach (var k in _known) h.Add(k);
        }
    }
}
