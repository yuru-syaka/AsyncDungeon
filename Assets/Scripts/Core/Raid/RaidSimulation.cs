using System;
using System.Collections.Generic;
using DungeonGame.Core.Data;
using DungeonGame.Core.Determinism;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Ecology;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Serialization;
using DungeonGame.Core.Simulation;
using DungeonGame.Core.Skills;

namespace DungeonGame.Core.Raid
{
    /// <summary>
    /// 勇者パーティによる1層ぶんの攻略シミュレーション(1ティック=0.1秒、リアルタイム制)。
    ///
    /// 勇者AI:
    ///  ・隊列を組んで進む(先頭=リーダー、後ろは前の仲間についていく)。入口が空くと1人ずつ入る。
    ///  ・射程内に敵がいれば攻撃し、その間は動かない。僧侶はHPが減った仲間を自動で回復する。
    ///  ・リーダーは、ゴールが見えていればゴールへ、見えていなければ一番近い未探索の境界へ向かう。
    ///  ・誰かが戦闘中、または隊列が離れすぎたら、リーダーは待つ。
    /// モンスターAI:近く(5マス以内)の勇者に寄っていき、隣接したら攻撃する。魔王はゴールから動かない。
    /// 攻略中は生態系は止まる(倒されたモンスターの養分・魔分は土に還る)。
    ///
    /// プレイヤーの介入(回復・たいまつ)は Submit で受け付け、次のティックの最初に適用して記録する。
    /// 同じスナップショット + 同じシード + 同じ介入記録 なら、必ず同じ結果になる。
    /// </summary>
    public sealed class RaidSimulation : ITickSimulation
    {
        public const int MonsterAggroRange = 5;
        /// <summary>僧侶が回復を始める、仲間のHPの割合(%)。</summary>
        public const int HealAbilityThresholdPercent = 60;
        public const int HealAbilityRange = 4;
        /// <summary>隊列の前後の間隔がこれより開いたら、リーダーは待つ。</summary>
        public const int MaxFormationGap = 2;

        public GameData Data { get; }
        public LayerRule Rule { get; }
        /// <summary>攻略中のダンジョン(元のスナップショットとは別のコピー)。</summary>
        public DungeonState Dungeon { get; }
        public FogOfWar Fog { get; }
        public IReadOnlyList<Hero> Heroes => _heroes;
        /// <summary>魔王(ゴールが魔王の層のみ)。</summary>
        public Monster Boss { get; }
        public InterventionCharges Charges { get; }
        /// <summary>スキルによるパーティ全体の補正。</summary>
        public PartyModifiers Modifiers { get; }
        public int CurrentTick { get; private set; }
        public RaidOutcome Outcome { get; private set; }
        public bool IsFinished => Outcome != RaidOutcome.InProgress;

        /// <summary>実際に適用された介入の記録(入力ログ)。</summary>
        public List<InterventionCommand> AppliedCommands { get; } = new List<InterventionCommand>();
        /// <summary>直前のティックで起きたできごと(画面表示用)。</summary>
        public List<RaidEvent> Events { get; } = new List<RaidEvent>();

        private readonly List<Hero> _heroes = new List<Hero>();
        private readonly List<HeroSetup> _party; // 渡されたままのパーティ(戦闘不能の仲間も含む)
        private readonly List<InterventionCommand> _pending = new List<InterventionCommand>();
        private readonly DeterministicRandom _rng;
        private readonly int[] _heroAt; // マスにいる勇者のID(0=いない)
        private readonly GridSearch _search;

        private DungeonMap Map => Dungeon.Map;

        public RaidSimulation(DungeonSnapshot snapshot, GameData data, IList<HeroSetup> party, ulong seed,
            InterventionCharges charges = null, PartyModifiers modifiers = null)
        {
            Data = data;
            Dungeon = DungeonSnapshotConverter.ToState(snapshot, data);
            Rule = data.Rules.Get(Dungeon.Map.Layer) ?? throw new ArgumentException($"層{Dungeon.Map.Layer}のルールがありません");
            Fog = new FogOfWar(Map);
            _heroAt = new int[Map.Width * Map.Height];
            _search = new GridSearch(Map.Width, Map.Height);
            _rng = new DeterministicRandom(seed);
            Charges = charges ?? new InterventionCharges();
            Modifiers = modifiers ?? PartyModifiers.None;

            foreach (var m in Dungeon.Monsters) InitCombat(m);
            if (Rule.Goal == GoalType.DemonLord)
            {
                var bossSpecies = data.Monsters.Get(Rule.BossSpecies)
                                  ?? throw new ArgumentException($"ボス '{Rule.BossSpecies}' がありません");
                Boss = Dungeon.AddMonster(bossSpecies, Map.Goal, 0, 0);
                InitCombat(Boss);
            }

            int id = 1;
            _party = new List<HeroSetup>(party);
            foreach (var setup in party)
            {
                if (setup.Hp <= 0) continue; // 戦闘不能の仲間は参加しない
                var cls = data.Heroes.Get(setup.ClassId) ?? throw new ArgumentException($"職業 '{setup.ClassId}' がありません");
                _heroes.Add(new Hero
                {
                    Id = id++, Class = cls, Stats = setup.Stats, Setup = setup,
                    Hp = IntMath.Min(setup.Hp, setup.Stats.MaxHp), Pos = Map.Entrance,
                });
            }
            Fog.Reveal(Map.Entrance, 1);
            if (_heroes.Count == 0) Outcome = RaidOutcome.Wiped;
        }

        private static void InitCombat(Monster m)
        {
            int pct = CombatMath.GrowthPercent(m);
            m.CombatMaxHp = IntMath.Max(1, m.Species.MaxHp * pct / 100);
            m.CombatAttack = m.Species.Attack * pct / 100;
            m.Hp = m.CombatMaxHp;
            m.AttackCd = 0;
            m.Cooldown = m.Species.MoveInterval;
        }

        // ================= 介入スキル =================

        /// <summary>
        /// 介入スキルの使用を受け付ける。次のティックの最初に適用される。
        /// 残り回数がない・座標が不正などで受け付けられなければ false。
        /// </summary>
        public bool Submit(InterventionType type, int x = 0, int y = 0)
        {
            if (IsFinished) return false;
            int pending = 0;
            foreach (var c in _pending) if (c.Type == type) pending++;
            if (Charges.Get(type) - pending <= 0) return false;
            if (type == InterventionType.Torch && !Map.InBounds(x, y)) return false;
            _pending.Add(new InterventionCommand { Tick = CurrentTick, Type = type, X = x, Y = y });
            return true;
        }

        private void ApplyPending()
        {
            foreach (var cmd in _pending)
            {
                var c = cmd;
                c.Tick = CurrentTick;
                if (c.Type == InterventionType.Heal)
                {
                    int total = 0;
                    foreach (var h in _heroes)
                    {
                        if (h.Dead) continue;
                        int before = h.Hp;
                        h.Hp = IntMath.Min(h.Stats.MaxHp, h.Hp + IntMath.Max(1, h.Stats.MaxHp * Data.Heroes.Interventions.HealPercent / 100));
                        total += h.Hp - before;
                    }
                    AddEvent(RaidEventKind.InterventionHeal, 0, 0, total, Map.Entrance);
                }
                else
                {
                    var p = new GridPos(c.X, c.Y);
                    int revealed = Fog.Reveal(p, Data.Heroes.Interventions.TorchRadius);
                    AddEvent(RaidEventKind.InterventionTorch, 0, 0, revealed, p);
                }
                Charges.Consume(c.Type);
                AppliedCommands.Add(c);
            }
            _pending.Clear();
        }

        // ================= 1ティックの進行 =================

        public void Step()
        {
            if (IsFinished) return;
            Events.Clear();
            ApplyPending();

            foreach (var h in _heroes)
            {
                if (IsFinished) break;
                if (!h.Dead) HeroTurn(h);
            }

            if (!IsFinished)
            {
                var list = Dungeon.Monsters;
                int count = list.Count;
                for (int i = 0; i < count; i++)
                    if (!list[i].Dead) MonsterTurn(list[i]);
            }

            Dungeon.RemoveDead();
            CurrentTick++;
            CheckEnd();
        }

        private void CheckEnd()
        {
            if (IsFinished) return;
            bool anyAlive = false;
            foreach (var h in _heroes) if (!h.Dead) anyAlive = true;

            if (!anyAlive) Finish(RaidOutcome.Wiped, RaidEventKind.Wiped);
            else if (Boss != null && Boss.Dead) Finish(RaidOutcome.Cleared, RaidEventKind.Cleared);
            else if (CurrentTick >= Rule.RaidTimeLimitTicks) Finish(RaidOutcome.TimeUp, RaidEventKind.TimeUp);
        }

        private void Finish(RaidOutcome outcome, RaidEventKind kind)
        {
            Outcome = outcome;
            AddEvent(kind, 0, 0, 0, Map.Goal);
        }

        // ================= 勇者 =================

        private void HeroTurn(Hero h)
        {
            if (h.MoveCd > 0) h.MoveCd--;
            if (h.AttackCd > 0) h.AttackCd--;
            if (h.AbilityCd > 0) h.AbilityCd--;

            if (!h.Entered) { TryEnter(h); return; }

            // スキル「再生」
            if (Modifiers.RegenAmount > 0 && Modifiers.RegenInterval > 0 && CurrentTick % Modifiers.RegenInterval == 0)
                h.Hp = IntMath.Min(h.Stats.MaxHp, h.Hp + Modifiers.RegenAmount);

            if (h.Class.Ability == ClassAbility.Heal && h.AbilityCd == 0 && TryAbilityHeal(h)) return;

            var target = FindTargetInRange(h);
            if (target != null)
            {
                if (h.AttackCd == 0) HeroAttack(h, target);
                return; // 戦闘中は動かない
            }

            if (h.MoveCd > 0) return;
            if (h == Leader()) LeaderMove(h); else FollowerMove(h);
        }

        /// <summary>生きている先頭の勇者。</summary>
        private Hero Leader()
        {
            foreach (var h in _heroes) if (!h.Dead) return h;
            return null;
        }

        /// <summary>隊列で自分のすぐ前にいる(生きている)勇者。</summary>
        private Hero Ahead(Hero h)
        {
            Hero ahead = null;
            foreach (var o in _heroes)
            {
                if (o == h) break;
                if (!o.Dead) ahead = o;
            }
            return ahead;
        }

        private void TryEnter(Hero h)
        {
            var ent = Map.Entrance;
            if (HeroAt(ent) != null || Dungeon.MonsterAt(ent) != null) return;
            var ahead = Ahead(h);
            if (ahead != null && !ahead.Entered) return;

            h.Entered = true;
            h.Pos = ent;
            _heroAt[Map.Index(ent.X, ent.Y)] = h.Id;
            h.MoveCd = CombatMath.MoveInterval(h.Stats.Speed);
            Fog.Reveal(ent, Vision(h));
            AddEvent(RaidEventKind.HeroEntered, h.Id, 0, 0, ent);
        }

        private bool TryAbilityHeal(Hero h)
        {
            Hero best = null;
            int bestPct = HealAbilityThresholdPercent;
            foreach (var o in _heroes)
            {
                if (!o.Active) continue;
                if (IntMath.Manhattan(h.Pos.X, h.Pos.Y, o.Pos.X, o.Pos.Y) > HealAbilityRange) continue;
                int pct = o.Hp * 100 / o.Stats.MaxHp;
                if (pct < bestPct) { best = o; bestPct = pct; }
            }
            if (best == null) return false;

            int before = best.Hp;
            best.Hp = IntMath.Min(best.Stats.MaxHp, best.Hp + IntMath.Max(1, h.Stats.Magic * 2));
            h.AbilityCd = h.Class.AbilityInterval;
            AddEvent(RaidEventKind.HeroHeal, best.Id, 0, best.Hp - before, best.Pos);
            return true;
        }

        /// <summary>射程内で見えている敵のうち、HPが一番低いもの(同じならID順)。</summary>
        private Monster FindTargetInRange(Hero h)
        {
            Monster best = null;
            int range = IntMath.Max(1, h.Class.AttackRange);
            foreach (var m in Dungeon.Monsters)
            {
                if (m.Dead || !Fog.IsKnown(m.Pos)) continue;
                int d = IntMath.Manhattan(h.Pos.X, h.Pos.Y, m.Pos.X, m.Pos.Y);
                if (d < 1 || d > range) continue;
                if (best == null || m.Hp < best.Hp) best = m;
            }
            return best;
        }

        private void HeroAttack(Hero h, Monster m)
        {
            bool magic = h.Class.AttackStat == AttackStat.Magic;
            int atk = magic ? h.Stats.Magic : h.Stats.Attack;
            int def = magic ? m.Species.Defense / 2 : m.Species.Defense;
            int dmg = CombatMath.Damage(_rng, atk, def, h.Class.CritPercent + Modifiers.CritBonus, out bool crit);
            m.Hp -= dmg;
            h.AttackCd = CombatMath.AttackInterval(h.Stats.Speed);
            AddEvent(RaidEventKind.HeroAttack, h.Id, m.Id, dmg, m.Pos, crit);

            // スキル「吸血」
            if (Modifiers.LifestealPercent > 0)
                h.Hp = IntMath.Min(h.Stats.MaxHp, h.Hp + dmg * Modifiers.LifestealPercent / 100);

            if (m.Hp <= 0) KillMonster(m, h);
        }

        private void KillMonster(Monster m, Hero killer)
        {
            m.Hp = 0;
            killer.Kills++;
            Dungeon.MarkDead(m);
            ResourceSink.Deposit(Dungeon, m.Pos, m.Nutrient, m.Mana);
            m.Nutrient = 0;
            m.Mana = 0;
            AddEvent(RaidEventKind.MonsterKilled, killer.Id, m.Id, 0, m.Pos);
        }

        private int Vision(Hero h) => h.Class.Vision + Modifiers.VisionBonus;

        private bool ShouldLeaderWait(Hero leader)
        {
            GridPos? prev = null;
            foreach (var h in _heroes)
            {
                if (h.Dead) continue;
                if (h.Entered && FindTargetInRange(h) != null) return true; // 誰かが戦闘中
                var pos = h.Entered ? h.Pos : Map.Entrance;           // まだ入っていない仲間は入口にいるとみなす
                if (prev != null && IntMath.Manhattan(prev.Value.X, prev.Value.Y, pos.X, pos.Y) > MaxFormationGap)
                    return true; // 隊列が離れすぎ
                prev = pos;
            }
            return false;
        }

        private void LeaderMove(Hero h)
        {
            if (ShouldLeaderWait(h)) return;

            Func<GridPos, bool> passable = p => Fog.IsKnown(p) && Map.IsWalkable(p) && (Boss == null || p != Boss.Pos);

            // ゴールが見えていて、既知のマスだけで行けるならゴールへ。そうでなければ一番近い未探索の境界へ。
            // (たいまつでゴールだけ照らした場合は、道がわかるまで探索を続ける)
            GridPos step = default;
            bool found = Fog.IsKnown(Map.Goal) && _search.FirstStep(h.Pos, passable, p => p == Map.Goal, out step, out _);
            if (!found) found = _search.FirstStep(h.Pos, passable, p => Fog.IsFrontier(p), out step, out _);
            if (!found) return;
            TryStep(h, step, allowSwap: true);
        }

        private void FollowerMove(Hero h)
        {
            var ahead = Ahead(h);
            if (ahead == null || !ahead.Entered) return;
            if (IntMath.Manhattan(h.Pos.X, h.Pos.Y, ahead.Pos.X, ahead.Pos.Y) <= 1) return;

            // 自分より後ろの仲間とは入れ替われるので、通れるものとして道を探す(行き止まりで引き返すとき用)
            Func<GridPos, bool> passable = p =>
            {
                if (!Fog.IsKnown(p) || !Map.IsWalkable(p) || Dungeon.MonsterAt(p) != null) return false;
                var other = HeroAt(p);
                return other == null || other.Id > h.Id;
            };
            var target = ahead.Pos;
            if (!_search.FirstStep(h.Pos, passable, p => p == target, out var step, out _)) return;
            if (step == target) return;
            TryStep(h, step, allowSwap: true);
        }

        /// <summary>1マス進む。モンスターがいれば進めない。後ろの仲間とは入れ替われる。</summary>
        private void TryStep(Hero h, GridPos step, bool allowSwap)
        {
            if (Dungeon.MonsterAt(step) != null) return;
            var other = HeroAt(step);
            if (other != null)
            {
                if (!allowSwap || other.Id < h.Id) return;
                other.Pos = h.Pos;
                _heroAt[Map.Index(h.Pos.X, h.Pos.Y)] = other.Id;
            }
            else
            {
                _heroAt[Map.Index(h.Pos.X, h.Pos.Y)] = 0;
            }
            h.Pos = step;
            _heroAt[Map.Index(step.X, step.Y)] = h.Id;
            h.MoveCd = CombatMath.MoveInterval(h.Stats.Speed);
            Fog.Reveal(step, Vision(h));

            if (Rule.Goal == GoalType.Stairs && step == Map.Goal)
                Finish(RaidOutcome.Cleared, RaidEventKind.Cleared);
        }

        private Hero HeroAt(GridPos p)
        {
            if (!Map.InBounds(p)) return null;
            int id = _heroAt[Map.Index(p.X, p.Y)];
            return id == 0 ? null : _heroes[id - 1];
        }

        // ================= モンスター =================

        private void MonsterTurn(Monster m)
        {
            if (m.AttackCd > 0) m.AttackCd--;
            if (m.Cooldown > 0) m.Cooldown--;

            Hero target = null;
            foreach (var d in GridPos.Dir4)
            {
                var h = HeroAt(m.Pos + d);
                if (h != null && h.Active) { target = h; break; }
            }
            if (target != null)
            {
                if (m.AttackCd == 0) MonsterAttack(m, target);
                return;
            }
            if (m == Boss || m.Cooldown > 0) return;

            // 近くの勇者に寄っていく
            Hero nearest = null;
            int nearestDist = MonsterAggroRange + 1;
            foreach (var h in _heroes)
            {
                if (!h.Active) continue;
                int d = IntMath.Manhattan(m.Pos.X, m.Pos.Y, h.Pos.X, h.Pos.Y);
                if (d < nearestDist) { nearest = h; nearestDist = d; }
            }
            if (nearest == null) return;

            GridPos best = m.Pos;
            int bestDist = nearestDist;
            foreach (var d in GridPos.Dir4)
            {
                var p = m.Pos + d;
                if (!Dungeon.IsFreeFloor(p) || HeroAt(p) != null) continue;
                int dist = IntMath.Manhattan(p.X, p.Y, nearest.Pos.X, nearest.Pos.Y);
                if (dist < bestDist) { best = p; bestDist = dist; }
            }
            if (best != m.Pos)
            {
                Dungeon.MoveMonster(m, best);
                m.Cooldown = m.Species.MoveInterval;
            }
        }

        private void MonsterAttack(Monster m, Hero h)
        {
            int dmg = CombatMath.Damage(_rng, m.CombatAttack, h.Stats.Defense, 0, out _);
            h.Hp -= dmg;
            m.AttackCd = m.Species.AttackInterval;
            AddEvent(RaidEventKind.MonsterAttack, h.Id, m.Id, dmg, h.Pos);

            // スキル「棘の鎧」
            if (Modifiers.ThornsPercent > 0)
            {
                int back = dmg * Modifiers.ThornsPercent / 100;
                if (back > 0)
                {
                    m.Hp -= back;
                    if (m.Hp <= 0) KillMonster(m, h);
                }
            }

            if (h.Hp <= 0)
            {
                h.Hp = 0;
                h.Dead = true;
                _heroAt[Map.Index(h.Pos.X, h.Pos.Y)] = 0;
                AddEvent(RaidEventKind.HeroDown, h.Id, m.Id, 0, h.Pos);
            }
        }

        // ================= 補助 =================

        private void AddEvent(RaidEventKind kind, int heroId, int monsterId, int amount, GridPos pos, bool crit = false)
            => Events.Add(new RaidEvent
            {
                Tick = CurrentTick, Kind = kind, HeroId = heroId, MonsterId = monsterId,
                Amount = amount, Pos = pos, Critical = crit,
            });

        /// <summary>攻略後の勇者の状態(HPを引き継ぐため)。戦闘不能はHP0。</summary>
        /// <remarks>渡されたパーティと同じ順番・人数で返す(参加しなかった仲間はHP0のまま)。</remarks>
        public List<HeroSetup> ExportParty()
        {
            var list = new List<HeroSetup>();
            foreach (var setup in _party)
            {
                Hero hero = null;
                foreach (var h in _heroes) if (h.Setup == setup) hero = h;
                list.Add(new HeroSetup
                {
                    ClassId = setup.ClassId, Stats = setup.Stats,
                    Hp = hero == null ? IntMath.Max(0, setup.Hp) : (hero.Dead ? 0 : hero.Hp),
                    Skills = new List<string>(setup.Skills),
                });
            }
            return list;
        }

        /// <summary>攻略の状態全体のハッシュ(決定論テスト・サーバー検証用)。</summary>
        public ulong ComputeHash()
        {
            var h = new StateHasher();
            h.Add(CurrentTick);
            h.Add((int)Outcome);
            h.Add(Dungeon.ComputeHash());
            foreach (var m in Dungeon.Monsters) { h.Add(m.AttackCd); h.Add(m.CombatMaxHp); h.Add(m.CombatAttack); }
            foreach (var hero in _heroes)
            {
                h.Add(hero.Id); h.Add(hero.Hp); h.Add(hero.Pos.X); h.Add(hero.Pos.Y); h.Add(hero.Entered); h.Add(hero.Dead);
                h.Add(hero.MoveCd); h.Add(hero.AttackCd); h.Add(hero.AbilityCd); h.Add(hero.Kills);
            }
            Fog.AddToHash(ref h);
            h.Add(Charges.Heal); h.Add(Charges.Torch);
            var r = _rng.GetState();
            h.Add(r.X); h.Add(r.Y); h.Add(r.Z); h.Add(r.W);
            return h.Value;
        }
    }
}
