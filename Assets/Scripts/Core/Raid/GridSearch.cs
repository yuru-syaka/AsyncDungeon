using System;
using System.Collections.Generic;
using DungeonGame.Core.Dungeon;

namespace DungeonGame.Core.Raid
{
    /// <summary>
    /// 条件付きの幅優先探索。「通れるマス」と「目標マス」を関数で受け取り、
    /// 一番近い目標へ向かう最初の一歩を返す。隣接マスを見る順番は固定なので決定論的。
    /// 目標マスは通れないマスでもよい(そこに入る一歩、またはその手前までの経路になる)。
    /// </summary>
    public sealed class GridSearch
    {
        private readonly int _w, _h;
        private readonly int[] _parent;
        private readonly int[] _depth;
        private readonly Queue<int> _queue = new Queue<int>();

        public GridSearch(int width, int height)
        {
            _w = width;
            _h = height;
            _parent = new int[width * height];
            _depth = new int[width * height];
        }

        /// <summary>
        /// start から最も近い目標マスへの最初の一歩を探す(start 自身は目標として扱わない)。
        /// 見つからなければ false。distance は start から目標までの歩数。
        /// </summary>
        public bool FirstStep(GridPos start, Func<GridPos, bool> passable, Func<GridPos, bool> isTarget,
            out GridPos step, out int distance)
        {
            step = start;
            distance = -1;
            const int Unvisited = -2;
            for (int i = 0; i < _parent.Length; i++) _parent[i] = Unvisited;
            _queue.Clear();

            int s = start.Y * _w + start.X;
            _parent[s] = -1;
            _depth[s] = 0;
            _queue.Enqueue(s);

            while (_queue.Count > 0)
            {
                int cur = _queue.Dequeue();
                var p = new GridPos(cur % _w, cur / _w);
                if (cur != s && isTarget(p))
                {
                    distance = _depth[cur];
                    int c = cur;
                    while (_parent[c] != s) c = _parent[c];
                    step = new GridPos(c % _w, c / _w);
                    return true;
                }
                foreach (var d in GridPos.Dir4)
                {
                    var n = p + d;
                    if (n.X < 0 || n.Y < 0 || n.X >= _w || n.Y >= _h) continue;
                    int ni = n.Y * _w + n.X;
                    if (_parent[ni] != Unvisited) continue;
                    // 通れないマスは、目標のときだけ入れる(取り出した時点で目標として終了するので先へは広がらない)
                    if (!passable(n) && !isTarget(n)) continue;
                    _parent[ni] = cur;
                    _depth[ni] = _depth[cur] + 1;
                    _queue.Enqueue(ni);
                }
            }
            return false;
        }
    }
}
