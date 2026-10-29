using DungeonGame.Core.Determinism;

namespace DungeonGame.Core.Dungeon
{
    /// <summary>
    /// 層ルールとシードから、掘る前の初期ダンジョン(全部が土)を作る。
    /// 外周は岩盤、入口は上辺の中央、ゴールは下辺の中央。養分・魔分はシードで偏りを持たせて配置する。
    /// </summary>
    public static class DungeonFactory
    {
        public static DungeonMap CreateEmpty(LayerRule rule, ulong seed)
        {
            var map = new DungeonMap(rule.Layer, rule.Width, rule.Height)
            {
                GoalType = rule.Goal,
                DigBudget = rule.DigBudget,
                TileResourceCap = rule.TileResourceCap,
            };

            for (int y = 0; y < rule.Height; y++)
            for (int x = 0; x < rule.Width; x++)
            {
                bool edge = x == 0 || y == 0 || x == rule.Width - 1 || y == rule.Height - 1;
                map.SetTile(x, y, edge ? TileType.Bedrock : TileType.Soil);
            }

            map.SetTile(rule.Width / 2, 1, TileType.Entrance);
            map.SetTile(rule.Width / 2, rule.Height - 2, TileType.Goal);

            var rng = new DeterministicRandom(seed);
            FillResource(map, rng.Fork(1), rule.SoilNutrientMax, true);
            FillResource(map, rng.Fork(2), rule.SoilManaMax, false);
            return map;
        }

        /// <summary>
        /// 全体に少なめの値を撒き、ところどころに「鉱脈」(中心ほど濃い塊)を置く。
        /// 濃い場所は少ないので、強いモンスターが生まれる場所は限られる。整数演算のみ。
        /// </summary>
        private static void FillResource(DungeonMap map, DeterministicRandom rng, int max, bool nutrient)
        {
            int w = map.Width, h = map.Height;
            var v = new int[w * h];
            for (int i = 0; i < v.Length; i++)
                v[i] = rng.Next(max / 2 + 1); // 基本値 0〜max/2

            int veins = IntMath.Max(1, (w * h) / 24);
            for (int k = 0; k < veins; k++)
            {
                int cx = rng.Range(1, w - 1), cy = rng.Range(1, h - 1);
                int radius = rng.Range(1, 3);         // 1〜2
                int peak = rng.Range(max / 2, max + 1); // 中心の上乗せ量
                for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    int d = IntMath.Chebyshev(x, y, cx, cy);
                    v[map.Index(x, y)] += peak * (radius + 1 - d) / (radius + 1);
                }
            }

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (map.GetTile(x, y) != TileType.Soil) continue;
                int value = IntMath.Clamp(v[map.Index(x, y)], 0, max);
                if (nutrient) map.SetNutrient(x, y, value); else map.SetMana(x, y, value);
            }
        }
    }
}
