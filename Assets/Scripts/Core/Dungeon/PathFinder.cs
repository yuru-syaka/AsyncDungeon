using System.Collections.Generic;

namespace DungeonGame.Core.Dungeon
{
    /// <summary>
    /// 幅優先探索による経路探索(4方向)。隣接マスを調べる順番を固定しているので結果は決定論的。
    /// 保存時の「経路があるか」チェックや、勇者・モンスターの移動に使う。
    /// </summary>
    public static class PathFinder
    {
        /// <summary>from から to まで歩いて行けるか。</summary>
        public static bool HasPath(DungeonMap map, GridPos from, GridPos to)
            => FindPath(map, from, to) != null;

        /// <summary>
        /// from から to までの最短経路(from を含まず to を含む)を返す。行けなければ null。
        /// </summary>
        public static List<GridPos> FindPath(DungeonMap map, GridPos from, GridPos to)
        {
            if (!map.InBounds(from) || !map.InBounds(to)) return null;
            if (from == to) return new List<GridPos>();
            if (!map.IsWalkable(to)) return null;

            var dist = Distances(map, from, to);
            if (dist[map.Index(to.X, to.Y)] < 0) return null;

            // ゴールから逆にたどる
            var path = new List<GridPos>();
            var cur = to;
            while (cur != from)
            {
                path.Add(cur);
                int d = dist[map.Index(cur.X, cur.Y)];
                foreach (var dir in GridPos.Dir4)
                {
                    var n = cur + dir;
                    if (map.InBounds(n) && dist[map.Index(n.X, n.Y)] == d - 1) { cur = n; break; }
                }
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// from から各マスまでの歩数(行けないマスは -1)。stopAt を指定するとそこに着いた時点で打ち切る。
        /// </summary>
        public static int[] Distances(DungeonMap map, GridPos from, GridPos? stopAt = null)
        {
            var dist = new int[map.Width * map.Height];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            if (!map.IsWalkable(from)) return dist;

            var queue = new Queue<GridPos>();
            dist[map.Index(from.X, from.Y)] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (stopAt.HasValue && p == stopAt.Value) break;
                int d = dist[map.Index(p.X, p.Y)];
                foreach (var dir in GridPos.Dir4)
                {
                    var n = p + dir;
                    if (!map.IsWalkable(n)) continue;
                    int ni = map.Index(n.X, n.Y);
                    if (dist[ni] >= 0) continue;
                    dist[ni] = d + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }
    }
}
