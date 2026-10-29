using DungeonGame.Core.Dungeon;

namespace DungeonGame.Core.Ecology
{
    /// <summary>
    /// 養分・魔分を土に還す処理。指定位置の近くの土から順に、上限まで詰めていく。
    /// 探す順番は「距離が近い順 → 上の行から → 左から」で固定(決定論)。
    /// どこにも入らなかった分は DungeonState.Lost に記録する(総量の帳尻を合わせるため)。
    /// </summary>
    public static class ResourceSink
    {
        public static void Deposit(DungeonState state, GridPos pos, int nutrient, int mana)
        {
            if (nutrient > 0) state.LostNutrient += DepositOne(state.Map, pos, nutrient, true);
            if (mana > 0) state.LostMana += DepositOne(state.Map, pos, mana, false);
        }

        /// <summary>還せなかった量を返す。</summary>
        private static int DepositOne(DungeonMap map, GridPos pos, int amount, bool nutrient)
        {
            int maxR = map.Width > map.Height ? map.Width : map.Height;
            for (int r = 1; r <= maxR && amount > 0; r++)
            {
                for (int y = pos.Y - r; y <= pos.Y + r && amount > 0; y++)
                for (int x = pos.X - r; x <= pos.X + r && amount > 0; x++)
                {
                    // 距離 r の輪の上だけを見る
                    bool onRing = y == pos.Y - r || y == pos.Y + r || x == pos.X - r || x == pos.X + r;
                    if (!onRing || !map.InBounds(x, y) || map.GetTile(x, y) != TileType.Soil) continue;
                    amount = nutrient ? map.AddNutrient(x, y, amount) : map.AddMana(x, y, amount);
                }
            }
            return amount;
        }
    }
}
