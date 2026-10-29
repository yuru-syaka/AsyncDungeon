using DungeonGame.Core.Dungeon;

namespace DungeonGame.Core.Ecology
{
    /// <summary>
    /// 掘削時のモンスター誕生を決める(誕生パターンA:土の濃さで決まる)。
    /// 掘った土の養分・魔分が種族の誕生条件を満たしていれば、その場所に生まれる。層による制限はない
    /// (浅い層は土がやせているので、自然と弱いモンスターが中心になる)。
    /// 複数の種族が条件を満たすときは「階級が高い → 食性に合う資源が多い → 誕生条件が厳しい → 定義順」で1種族を選ぶ。
    /// 特殊な方法でしか生まれない種族(DigSpawn = false)は対象外。余った養分・魔分は周りの土に還る。
    /// </summary>
    public static class MonsterSpawner
    {
        public static MonsterSpecies Choose(MonsterCatalog catalog, int nutrient, int mana)
        {
            MonsterSpecies best = null;
            int bestScore = -1;
            foreach (var s in catalog.Species)
            {
                if (s.IsBoss || !s.DigSpawn) continue;
                if (nutrient < s.SpawnNutrient || mana < s.SpawnMana) continue;
                if (s.SpawnNutrient + s.SpawnMana <= 0) continue;

                int affinity = s.Diet == Diet.Nutrient ? nutrient : s.Diet == Diet.Mana ? mana : nutrient + mana;
                // 同じ階級なら食性に合う資源が多い方、それも同じなら誕生条件の厳しい(=珍しい)方を選ぶ
                int score = s.Tier * 1000000 + affinity * 1000 + s.SpawnNutrient + s.SpawnMana;
                if (score > bestScore) { best = s; bestScore = score; }
            }
            return best;
        }

        /// <summary>掘削結果からモンスターを生む。生まれなければ null。</summary>
        public static Monster SpawnFromDig(DungeonState state, MonsterCatalog catalog, DigResult dig)
        {
            if (!dig.Success) return null;
            var species = Choose(catalog, dig.Nutrient, dig.Mana);
            Monster m = null;
            int n = dig.Nutrient, mana = dig.Mana;
            if (species != null && state.IsFreeFloor(dig.Pos))
            {
                m = state.AddMonster(species, dig.Pos, species.SpawnNutrient, species.SpawnMana);
                n -= species.SpawnNutrient;
                mana -= species.SpawnMana;
            }
            ResourceSink.Deposit(state, dig.Pos, n, mana);
            return m;
        }

        /// <summary>
        /// center のまわり(チェビシェフ距離 radius 以内)の土から、養分 nutrient・魔分 mana を集める。
        /// 足りなければ何も取らずに false。足りれば、上の行・左の列から順に取って true(決定論)。
        /// 特殊な誕生(巣・墓場)で、生まれるモンスターの体の分を土から取るのに使う。
        /// </summary>
        public static bool TakeFromSoil(DungeonMap map, GridPos center, int radius, int nutrient, int mana)
        {
            int haveN = 0, haveM = 0;
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                if (!map.InBounds(x, y) || map.GetTile(x, y) != TileType.Soil) continue;
                haveN += map.GetNutrient(x, y);
                haveM += map.GetMana(x, y);
            }
            if (haveN < nutrient || haveM < mana) return false;

            for (int y = center.Y - radius; y <= center.Y + radius; y++)
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                if (!map.InBounds(x, y) || map.GetTile(x, y) != TileType.Soil) continue;
                int tn = System.Math.Min(nutrient, map.GetNutrient(x, y));
                int tm = System.Math.Min(mana, map.GetMana(x, y));
                map.SetNutrient(x, y, map.GetNutrient(x, y) - tn);
                map.SetMana(x, y, map.GetMana(x, y) - tm);
                nutrient -= tn;
                mana -= tm;
            }
            return true;
        }
    }
}
