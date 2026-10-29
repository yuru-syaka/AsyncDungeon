using DungeonGame.Core.Data;
using DungeonGame.Core.Determinism;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Serialization;

namespace DungeonGame.Core.Run
{
    /// <summary>NPC魔王の掘り方。</summary>
    public enum NpcDigPattern
    {
        /// <summary>まっすぐな本道から左右に横穴を何本も掘る。</summary>
        Comb = 0,
        /// <summary>大きく回り道をする本道 + 小部屋。勇者を長く歩かせる。</summary>
        Winding = 1,
        /// <summary>本道の左右に3×3の部屋を並べる。モンスターが溜まりやすい。</summary>
        Rooms = 2,
    }

    /// <summary>
    /// NPCダンジョンを自動で作る(開発用ツール)。魔王側と同じ DungeonBuilder で「掘る→育てる」を行うので、
    /// できたダンジョンは必ずルールを満たす。生成結果は JSON にして Resources/GameData/NpcDungeons に置く。
    /// </summary>
    public static class NpcDungeonGenerator
    {
        public static DungeonSnapshot Generate(GameData data, int layer, NpcDigPattern pattern, ulong seed,
            int budgetPercent, int growPercent, string id, string name)
        {
            var b = DungeonBuilder.CreateNew(data, layer, seed);
            var map = b.Map;
            var rng = new DeterministicRandom(seed).Fork(7);
            int limit = map.DigBudget * budgetPercent / 100;
            var ent = map.Entrance;
            var goal = map.Goal;

            // ---- 本道(必ずゴールにつなぐ) ----
            if (pattern == NpcDigPattern.Winding)
            {
                bool left = rng.Chance(50);
                int sideX = left ? 2 : map.Width - 3;
                int turnY = ent.Y + 2;
                DigLine(b, ent.X, ent.Y + 1, ent.X, turnY, int.MaxValue);
                DigLine(b, ent.X, turnY, sideX, turnY, int.MaxValue);
                DigLine(b, sideX, turnY, sideX, goal.Y - 1, int.MaxValue);
                DigLine(b, sideX, goal.Y - 1, goal.X, goal.Y - 1, int.MaxValue);
            }
            else
            {
                DigLine(b, ent.X, ent.Y + 1, goal.X, goal.Y - 1, int.MaxValue);
            }

            // ---- 飾り(予算の範囲で) ----
            if (pattern == NpcDigPattern.Comb)
            {
                bool leftSide = rng.Chance(50);
                for (int y = ent.Y + 2; y < goal.Y - 1 && map.DugCount < limit; y += 2)
                {
                    int len = rng.Range(2, map.Width / 2);
                    int dir = leftSide ? -1 : 1;
                    DigLine(b, ent.X + dir, y, ent.X + dir * len, y, limit);
                    leftSide = !leftSide;
                }
            }
            else if (pattern == NpcDigPattern.Rooms)
            {
                bool leftSide = rng.Chance(50);
                for (int y = ent.Y + 3; y < goal.Y - 1 && map.DugCount < limit; y += 4)
                {
                    int dir = leftSide ? -1 : 1;
                    DigLine(b, ent.X + dir, y, ent.X + dir * 2, y, limit);       // 扉
                    int cx = ent.X + dir * 4;
                    foreach (int dy in new[] { 0, -1, 1 }) // 扉の行から先に掘る(つながった状態で掘り広げる)
                        DigLine(b, ent.X + dir * 3, y + dy, cx + dir, y + dy, limit); // 部屋
                    leftSide = !leftSide;
                }
            }

            // ---- 残りは小さな枝をランダムに ----
            int guard = 0;
            while (map.DugCount < limit && guard++ < 500)
            {
                int x = rng.Range(1, map.Width - 1), y = rng.Range(1, map.Height - 1);
                if (map.CanDig(x, y) != DigError.None) continue;
                var d = GridPos.Dir4[rng.Next(4)];
                int len = rng.Range(1, 4);
                for (int i = 0; i < len && map.DugCount < limit; i++)
                    if (!b.Dig(x + d.X * i, y + d.Y * i).Success) break;
            }

            b.Grow(b.Rule.MaxEcologyTicks * growPercent / 100);
            return b.ToSnapshot(id, name, "NPC");
        }

        /// <summary>縦か横の直線を掘る(すでに通路のマスは飛ばす)。掘ったマス数が limit に達したら止める。</summary>
        private static void DigLine(DungeonBuilder b, int x0, int y0, int x1, int y1, int limit)
        {
            int dx = x1 > x0 ? 1 : x1 < x0 ? -1 : 0;
            int dy = y1 > y0 ? 1 : y1 < y0 ? -1 : 0;
            int x = x0, y = y0;
            while (true)
            {
                if (b.Map.DugCount >= limit) return;
                if (b.Map.InBounds(x, y) && b.Map.GetTile(x, y) == TileType.Soil)
                {
                    if (!b.Dig(x, y).Success) return;
                }
                if (x == x1 && y == y1) return;
                x += dx;
                y += dy;
            }
        }
    }
}
