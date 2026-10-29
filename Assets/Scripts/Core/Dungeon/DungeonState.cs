using System.Collections.Generic;
using DungeonGame.Core.Determinism;
using DungeonGame.Core.Ecology;

namespace DungeonGame.Core.Dungeon
{
    /// <summary>
    /// ダンジョン1層ぶんの完全な状態:マップ + モンスター + 生態系の乱数・経過ティック。
    /// スナップショット(JSON)はこのクラスの中身をそのまま書き出したもの。
    /// モンスターは1マスに1体まで。どのマスに誰がいるかを占有表で管理する。
    /// </summary>
    public sealed class DungeonState
    {
        public DungeonMap Map { get; }
        /// <summary>モンスター一覧(ID昇順)。死んだモンスターはティックの終わりに取り除く。</summary>
        public List<Monster> Monsters { get; } = new List<Monster>();
        public int NextMonsterId = 1;
        public int EcologyTick;
        public DeterministicRandom Rng;
        /// <summary>土が満杯で還せなかった養分・魔分(保存則の帳尻合わせ用)。</summary>
        public int LostNutrient;
        public int LostMana;

        private readonly int[] _occupant; // マスにいるモンスターのID(0=いない)

        public DungeonState(DungeonMap map, DeterministicRandom rng)
        {
            Map = map;
            Rng = rng;
            _occupant = new int[map.Width * map.Height];
        }

        /// <summary>モンスターが立てるマスか(通路で、誰もいない)。入口・ゴールには住めない。</summary>
        public bool IsFreeFloor(GridPos p)
            => Map.InBounds(p) && Map.GetTile(p) == TileType.Floor && _occupant[Map.Index(p.X, p.Y)] == 0;

        public Monster MonsterAt(GridPos p)
        {
            if (!Map.InBounds(p)) return null;
            int id = _occupant[Map.Index(p.X, p.Y)];
            return id == 0 ? null : FindById(id);
        }

        public Monster FindById(int id)
        {
            // IDは昇順なので二分探索できる
            int lo = 0, hi = Monsters.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                int mid_id = Monsters[mid].Id;
                if (mid_id == id) return Monsters[mid];
                if (mid_id < id) lo = mid + 1; else hi = mid - 1;
            }
            return null;
        }

        /// <summary>モンスターを配置する(IDは自動で振る)。</summary>
        public Monster AddMonster(MonsterSpecies species, GridPos pos, int nutrient, int mana)
        {
            var m = new Monster
            {
                Id = NextMonsterId++, Species = species, Pos = pos,
                Nutrient = nutrient, Mana = mana, Hp = species.MaxHp, Cooldown = species.MoveInterval,
            };
            Monsters.Add(m);
            _occupant[Map.Index(pos.X, pos.Y)] = m.Id;
            return m;
        }

        /// <summary>読み込み用:IDなども含めてそのまま配置する。</summary>
        public void RestoreMonster(Monster m)
        {
            Monsters.Add(m);
            _occupant[Map.Index(m.Pos.X, m.Pos.Y)] = m.Id;
        }

        public void MoveMonster(Monster m, GridPos to)
        {
            _occupant[Map.Index(m.Pos.X, m.Pos.Y)] = 0;
            m.Pos = to;
            _occupant[Map.Index(to.X, to.Y)] = m.Id;
        }

        /// <summary>死亡扱いにしてマスを空ける(リストからの削除は RemoveDead で行う)。</summary>
        public void MarkDead(Monster m)
        {
            if (m.Dead) return;
            m.Dead = true;
            int i = Map.Index(m.Pos.X, m.Pos.Y);
            if (_occupant[i] == m.Id) _occupant[i] = 0;
        }

        public void RemoveDead() => Monsters.RemoveAll(m => m.Dead);

        /// <summary>全モンスターを取り除く(体の分は土に還さない。テストやデバッグ用)。</summary>
        public void ClearMonsters()
        {
            Monsters.Clear();
            for (int i = 0; i < _occupant.Length; i++) _occupant[i] = 0;
        }

        /// <summary>土・モンスター・失われた分をすべて足した養分の総量(保存則チェック用)。</summary>
        public int GrandTotalNutrient
        {
            get { int s = Map.TotalNutrient + LostNutrient; foreach (var m in Monsters) s += m.Nutrient; return s; }
        }

        public int GrandTotalMana
        {
            get { int s = Map.TotalMana + LostMana; foreach (var m in Monsters) s += m.Mana; return s; }
        }

        public ulong ComputeHash()
        {
            var h = new StateHasher();
            Map.AddToHash(ref h);
            h.Add(NextMonsterId); h.Add(EcologyTick); h.Add(LostNutrient); h.Add(LostMana);
            var r = Rng.GetState();
            h.Add(r.X); h.Add(r.Y); h.Add(r.Z); h.Add(r.W);
            h.Add(Monsters.Count);
            foreach (var m in Monsters)
            {
                h.Add(m.Id); h.Add(m.Species.Id); h.Add(m.Pos.X); h.Add(m.Pos.Y);
                h.Add(m.Nutrient); h.Add(m.Mana); h.Add(m.Age); h.Add(m.Cooldown); h.Add(m.Hp); h.Add(m.Digest); h.Add(m.Dead);
            }
            return h.Value;
        }
    }
}
