using System.Collections.Generic;
using DungeonGame.Core.Determinism;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Simulation;

namespace DungeonGame.Core.Ecology
{
    /// <summary>
    /// 簡易生態系シミュレーション(1ティック=0.1秒)。
    ///
    /// 食物連鎖:下位(土の養分・魔分を吸う)→ 中位(下位を食べる)→ 上位(中位を食べる)→ 最上位(上位も食べる)
    /// 特殊な誕生:巣(広い部屋に巣を作る)・墓場(骨が積もった場所から起き上がる)・進化(育ち切ると別の種族に変わる)
    ///
    /// 各モンスターは毎ティック次の順で処理される(ID昇順):
    ///  1. 年を取る。寿命を迎えたら死ぬ。
    ///  2. 一定間隔で体の養分・魔分を1つ土に排出する(代謝)。体が空になったら死ぬ。
    ///  3. 行動間隔ごとに、[食べる → 進化する → 分裂する → 移動する] のうち最初にできたことを1つ行う。
    /// 死ぬ(食べられるも含む)と、その通路に骨が1本残る。
    /// 死ぬと体の養分・魔分は周りの土に還る。食べられた場合は捕食者の体に移る。
    /// つまり養分・魔分の総量は(土が満杯で失われる分を除き)常に一定。
    /// </summary>
    public sealed class EcologySimulation : ITickSimulation
    {
        /// <summary>捕食者が獲物を探す距離(マンハッタン距離)。</summary>
        public const int HuntRange = 6;

        private readonly DungeonState _state;
        private readonly LayerRule _rule;
        private readonly MonsterCatalog _catalog;
        private readonly List<GridPos> _candidates = new List<GridPos>(4);

        /// <summary>rule と catalog を渡すと、下位モンスターの自然発生が有効になる。</summary>
        public EcologySimulation(DungeonState state, LayerRule rule = null, MonsterCatalog catalog = null)
        {
            _state = state;
            _rule = rule;
            _catalog = catalog;
        }

        public int CurrentTick => _state.EcologyTick;
        public bool IsFinished => false;

        public void Step()
        {
            if (_rule != null && _catalog != null && _rule.WildSpawnPercent > 0 && Rng.Chance(_rule.WildSpawnPercent))
                TryWildSpawn();
            if (_catalog != null) TrySpecialSpawns();

            var list = _state.Monsters;
            int count = list.Count; // このティックに生まれた子は次のティックから動く
            for (int i = 0; i < count; i++)
            {
                var m = list[i];
                if (!m.Dead) UpdateMonster(m);
            }
            _state.RemoveDead();
            _state.EcologyTick++;
        }

        private DeterministicRandom Rng => _state.Rng;

        private void UpdateMonster(Monster m)
        {
            var sp = m.Species;
            m.Age++;
            if (m.Age >= sp.Lifespan) { Kill(m); return; }

            if (sp.UpkeepInterval > 0 && m.Age % sp.UpkeepInterval == 0)
            {
                Excrete(m);
                if (m.Biomass <= 0) { Kill(m); return; }
            }

            if (m.Digest > 0) m.Digest--;
            if (m.Cooldown > 0) { m.Cooldown--; return; }
            m.Cooldown = sp.MoveInterval;

            if (TryFeed(m)) return;
            if (TryEvolve(m)) return;
            if (TryReproduce(m)) return;
            Wander(m);
        }

        /// <summary>
        /// 進化:分裂できるほど体が大きく、年齢が EvolveAge 以上なら、分裂する代わりに別の種族に変わる。
        /// 体の養分・魔分はそのまま引き継ぐ(進化先の体の上限を超える分は土に還る)。
        /// </summary>
        private bool TryEvolve(Monster m)
        {
            var sp = m.Species;
            if (_catalog == null || string.IsNullOrEmpty(sp.EvolveTo)) return false;
            if (m.Age < sp.EvolveAge || m.Biomass < sp.ReproduceThreshold) return false;
            var to = _catalog.Get(sp.EvolveTo);
            if (to == null) return false;

            m.Species = to;
            m.Hp = to.MaxHp;
            m.Age = 0;
            m.Digest = 0;
            m.Cooldown = to.MoveInterval;
            int over = m.Biomass - to.MaxBiomass;
            if (over > 0)
            {
                int dn = IntMath.Min(over, m.Nutrient);
                m.Nutrient -= dn;
                m.Mana -= over - dn;
                ResourceSink.Deposit(_state, m.Pos, dn, over - dn);
            }
            return true;
        }

        /// <summary>死んだ場所に骨を1本残す(墓場からの誕生に使う)。</summary>
        private void LeaveBones(GridPos p)
        {
            var map = _state.Map;
            if (map.InBounds(p) && map.GetTile(p) == TileType.Floor) map.AddBones(p.X, p.Y, 1);
        }

        // ================= 特殊な誕生(パターンB) =================

        /// <summary>特殊な誕生をする種族ごとに、確率で誕生を試す(種族の定義順に処理する)。</summary>
        private void TrySpecialSpawns()
        {
            foreach (var sp in _catalog.Species)
            {
                if (sp.SpecialSpawn == SpecialSpawn.None || sp.SpecialPercent <= 0) continue;
                if (!Rng.Chance(sp.SpecialPercent)) continue;
                if (sp.SpecialSpawn == SpecialSpawn.RoomNest) TryRoomNest(sp);
                else if (sp.SpecialSpawn == SpecialSpawn.Graveyard) TryGraveyard(sp);
            }
        }

        /// <summary>
        /// 巣:まわり8マスがすべて通路の「広い部屋の中」のマスに巣を作る。
        /// 同じ種族が近く(6マス以内)にいる場所は選ばない(巣どうしは離れる)。体の分は半径2マス以内の土から取る。
        /// </summary>
        private void TryRoomNest(MonsterSpecies sp)
        {
            var map = _state.Map;
            var spots = new List<GridPos>();
            for (int y = 1; y < map.Height - 1; y++)
            for (int x = 1; x < map.Width - 1; x++)
            {
                var p = new GridPos(x, y);
                if (!_state.IsFreeFloor(p)) continue;
                bool open = true;
                for (int dy = -1; dy <= 1 && open; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (!map.IsWalkable(x + dx, y + dy)) { open = false; break; }
                if (!open || SameSpeciesNear(sp, p, 6)) continue;
                spots.Add(p);
            }
            if (spots.Count == 0) return;
            var spot = spots[Rng.Next(spots.Count)];
            if (!MonsterSpawner.TakeFromSoil(map, spot, 2, sp.SpawnNutrient, sp.SpawnMana)) return;
            _state.AddMonster(sp, spot, sp.SpawnNutrient, sp.SpawnMana);
        }

        /// <summary>
        /// 墓場:骨が SpecialBones 本以上積もった空き通路から起き上がる。骨はその分だけ減る。
        /// 同じ種族が近く(4マス以内)にいる場所からは起き上がらない(墓場がひとつの種族で埋まらないように)。
        /// 体の分は半径2マス以内の土から取る。
        /// </summary>
        private void TryGraveyard(MonsterSpecies sp)
        {
            var map = _state.Map;
            var spots = new List<GridPos>();
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var p = new GridPos(x, y);
                if (map.GetBones(x, y) >= sp.SpecialBones && _state.IsFreeFloor(p) && !SameSpeciesNear(sp, p, 4)) spots.Add(p);
            }
            if (spots.Count == 0) return;
            var spot = spots[Rng.Next(spots.Count)];
            if (!MonsterSpawner.TakeFromSoil(map, spot, 2, sp.SpawnNutrient, sp.SpawnMana)) return;
            map.AddBones(spot.X, spot.Y, -sp.SpecialBones);
            _state.AddMonster(sp, spot, sp.SpawnNutrient, sp.SpawnMana);
        }

        private bool SameSpeciesNear(MonsterSpecies sp, GridPos p, int range)
        {
            foreach (var o in _state.Monsters)
                if (!o.Dead && o.Species == sp && IntMath.Manhattan(o.Pos.X, o.Pos.Y, p.X, p.Y) <= range) return true;
            return false;
        }

        /// <summary>死亡。体の養分・魔分を周りの土に還し、骨を残す。</summary>
        public void Kill(Monster m)
        {
            _state.MarkDead(m);
            LeaveBones(m.Pos);
            ResourceSink.Deposit(_state, m.Pos, m.Nutrient, m.Mana);
            m.Nutrient = 0;
            m.Mana = 0;
        }

        /// <summary>代謝:多い方の資源を1つ土に出す。</summary>
        private void Excrete(Monster m)
        {
            if (m.Nutrient >= m.Mana && m.Nutrient > 0) { m.Nutrient--; ResourceSink.Deposit(_state, m.Pos, 1, 0); }
            else if (m.Mana > 0) { m.Mana--; ResourceSink.Deposit(_state, m.Pos, 0, 1); }
        }

        private bool TryFeed(Monster m)
        {
            var sp = m.Species;
            if (m.Biomass >= sp.MaxBiomass) return false;

            if (sp.Diet == Diet.Predator)
            {
                if (m.Digest > 0) return false; // 消化中は食べない
                foreach (var d in GridPos.Dir4)
                {
                    var prey = _state.MonsterAt(m.Pos + d);
                    if (prey == null || prey.Dead || !sp.CanEat(prey.Species)) continue;
                    Eat(m, prey);
                    return true;
                }
                return false;
            }

            // 吸収型:隣の土のうち一番多いところから1つ吸う
            bool nutrient = sp.Diet == Diet.Nutrient;
            var map = _state.Map;
            GridPos best = default;
            int bestValue = 0;
            foreach (var d in GridPos.Dir4)
            {
                var p = m.Pos + d;
                if (!map.InBounds(p) || map.GetTile(p) != TileType.Soil) continue;
                int v = nutrient ? map.GetNutrient(p.X, p.Y) : map.GetMana(p.X, p.Y);
                if (v > bestValue) { best = p; bestValue = v; }
            }
            if (bestValue <= 0) return false;
            if (nutrient) { map.SetNutrient(best.X, best.Y, bestValue - 1); m.Nutrient++; }
            else { map.SetMana(best.X, best.Y, bestValue - 1); m.Mana++; }
            return true;
        }

        /// <summary>捕食。獲物の体は捕食者に移り、入りきらない分は土に還る。</summary>
        private void Eat(Monster predator, Monster prey)
        {
            _state.MarkDead(prey);
            LeaveBones(prey.Pos);
            predator.Digest = predator.Species.DigestTicks;
            predator.Nutrient += prey.Nutrient;
            predator.Mana += prey.Mana;
            prey.Nutrient = 0;
            prey.Mana = 0;

            int over = predator.Biomass - predator.Species.MaxBiomass;
            if (over > 0)
            {
                int dn = IntMath.Min(over, predator.Nutrient);
                int dm = over - dn;
                predator.Nutrient -= dn;
                predator.Mana -= dm;
                ResourceSink.Deposit(_state, predator.Pos, dn, dm);
            }
        }

        /// <summary>分裂:体が十分大きければ、隣の空きマスに半分の体を持つ子を産む。</summary>
        private bool TryReproduce(Monster m)
        {
            if (m.Biomass < m.Species.ReproduceThreshold) return false;
            CollectFreeNeighbors(m.Pos);
            if (_candidates.Count == 0) return false;

            var pos = _candidates[Rng.Next(_candidates.Count)];
            int cn = m.Nutrient / 2, cm = m.Mana / 2;
            m.Nutrient -= cn;
            m.Mana -= cm;
            _state.AddMonster(m.Species, pos, cn, cm);
            return true;
        }

        /// <summary>移動。捕食者は近くの獲物に近づき、それ以外はランダムに歩く(たまに立ち止まる)。</summary>
        private void Wander(Monster m)
        {
            CollectFreeNeighbors(m.Pos);
            if (_candidates.Count == 0) return;

            if (m.Species.Diet == Diet.Predator && m.Digest == 0)
            {
                var target = FindNearestPrey(m);
                if (target != null)
                {
                    int bestDist = IntMath.Manhattan(m.Pos.X, m.Pos.Y, target.Pos.X, target.Pos.Y);
                    int bestIndex = -1;
                    for (int i = 0; i < _candidates.Count; i++)
                    {
                        var c = _candidates[i];
                        int dist = IntMath.Manhattan(c.X, c.Y, target.Pos.X, target.Pos.Y);
                        if (dist < bestDist) { bestDist = dist; bestIndex = i; }
                    }
                    if (bestIndex >= 0) { _state.MoveMonster(m, _candidates[bestIndex]); return; }
                }
            }

            if (Rng.Chance(25)) return; // 立ち止まる
            _state.MoveMonster(m, _candidates[Rng.Next(_candidates.Count)]);
        }

        /// <summary>
        /// 自然発生:ランダムに選んだ空き通路マスに、養分(魔分)の濃い土が隣接していれば下位モンスターが湧く。
        /// 誕生条件 + WildSpawnMargin 以上の濃さが必要で、体の分はその土から取る。
        /// 捕食者が下位を食べ尽くしても、土に還った養分から食物連鎖の底辺が回復する。
        /// </summary>
        private void TryWildSpawn()
        {
            var map = _state.Map;
            int idx = Rng.Next(map.Width * map.Height);
            var p = new GridPos(idx % map.Width, idx / map.Width);
            if (!_state.IsFreeFloor(p)) return;

            int margin = _rule.WildSpawnMargin;
            foreach (var d in GridPos.Dir4)
            {
                var s = p + d;
                if (!map.InBounds(s) || map.GetTile(s) != TileType.Soil) continue;
                int n = map.GetNutrient(s.X, s.Y), mana = map.GetMana(s.X, s.Y);

                MonsterSpecies best = null;
                int bestAffinity = -1;
                foreach (var sp in _catalog.Species)
                {
                    if (sp.Tier != 1 || !sp.DigSpawn || sp.IsBoss) continue;
                    if (sp.SpawnNutrient > 0 && n < sp.SpawnNutrient + margin) continue;
                    if (sp.SpawnMana > 0 && mana < sp.SpawnMana + margin) continue;
                    int affinity = sp.Diet == Diet.Mana ? mana : n;
                    if (affinity > bestAffinity) { best = sp; bestAffinity = affinity; }
                }
                if (best == null) continue;

                map.SetNutrient(s.X, s.Y, n - best.SpawnNutrient);
                map.SetMana(s.X, s.Y, mana - best.SpawnMana);
                _state.AddMonster(best, p, best.SpawnNutrient, best.SpawnMana);
                return;
            }
        }

        private Monster FindNearestPrey(Monster m)
        {
            Monster best = null;
            int bestDist = HuntRange + 1;
            foreach (var other in _state.Monsters)
            {
                if (other.Dead || other == m || !m.Species.CanEat(other.Species)) continue;
                int d = IntMath.Manhattan(m.Pos.X, m.Pos.Y, other.Pos.X, other.Pos.Y);
                if (d < bestDist) { best = other; bestDist = d; }
            }
            return best;
        }

        private void CollectFreeNeighbors(GridPos pos)
        {
            _candidates.Clear();
            foreach (var d in GridPos.Dir4)
            {
                var p = pos + d;
                if (_state.IsFreeFloor(p)) _candidates.Add(p);
            }
        }
    }
}
