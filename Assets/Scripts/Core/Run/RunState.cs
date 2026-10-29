using System;
using System.Collections.Generic;
using DungeonGame.Core.Data;
using DungeonGame.Core.Determinism;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Skills;

namespace DungeonGame.Core.Run
{
    /// <summary>ランの進行段階。</summary>
    public enum RunPhase
    {
        /// <summary>層を攻略中(CurrentRaid を進める)。</summary>
        Raid = 0,
        /// <summary>層をクリアして報酬を選んでいる(スキル選択・ポイント配分)。</summary>
        Reward = 1,
        /// <summary>魔王を倒した。</summary>
        Victory = 2,
        /// <summary>全滅・時間切れ。</summary>
        Defeat = 3,
    }

    /// <summary>ラン中の勇者1人の状態(職業・振ったポイント・現在HP)。</summary>
    [Serializable]
    public sealed class RunHero
    {
        public string ClassId;
        /// <summary>割り振ったポイント数(能力値ごと)。</summary>
        public HeroStats Points;
        public int Hp;
    }

    /// <summary>
    /// 勇者側のラン(ローグライト)の進行を管理する。層の数は層ルール(layer_rules.json)の数で決まる。
    ///  1. 編成(職業3人)してラン開始 → 1層の攻略が始まる
    ///  2. 攻略が終わったら CompleteRaid() → クリアなら報酬、失敗なら終了
    ///  3. 報酬:3つの候補からスキルを1つ選び(ChooseSkill)、ポイントを割り振って(Allocate)、ConfirmReward() で次の層へ
    ///  4. 最後の層の魔王を倒せば勝利。★ = 突破した層の数
    /// 勇者のHPは層をまたいで引き継ぐ。介入スキルの回数もラン全体で共有。
    /// プレイヤーの入力はすべてこのクラスのメソッドを通り、Log(入力ログ)に記録される。
    /// </summary>
    public sealed class RunState
    {
        /// <summary>このランで攻略する層の数(層ルールの数)。</summary>
        public int LayerCount => Data.Rules.LayerCount;

        public GameData Data { get; }
        public ulong Seed { get; }
        public IReadOnlyList<RunHero> Party => _party;
        /// <summary>獲得済みスキル(獲得順)。</summary>
        public IReadOnlyList<string> Skills => _skills;
        public InterventionCharges Charges { get; }
        public int Layer { get; private set; }
        public RunPhase Phase { get; private set; }
        public RaidSimulation CurrentRaid { get; private set; }
        public string CurrentDungeonId { get; private set; }
        /// <summary>今回の報酬のスキル候補。</summary>
        public IReadOnlyList<string> SkillCandidates => _candidates;
        public bool SkillChosenThisReward { get; private set; }
        /// <summary>まだ割り振っていないスキルポイント(次の層に持ち越せる)。</summary>
        public int UnspentPoints { get; private set; }
        /// <summary>★評価(突破した層の数。魔王撃破で3)。</summary>
        public int Stars { get; private set; }
        public RunLog Log { get; } = new RunLog();

        private readonly List<RunHero> _party = new List<RunHero>();
        private readonly List<string> _skills = new List<string>();
        private readonly List<string> _candidates = new List<string>();
        private int[] _maxHpAtRewardStart;

        public bool IsOver => Phase == RunPhase.Victory || Phase == RunPhase.Defeat;
        private RunLayerLog CurrentLayerLog => Log.Layers[Log.Layers.Count - 1];

        private RunState(GameData data, ulong seed)
        {
            Data = data;
            Seed = seed;
            Charges = InterventionCharges.FromSettings(data.Heroes.Interventions);
        }

        /// <summary>
        /// ランを始める。職業は4職業から異なる3人(隊列順)。不正な編成なら例外。
        /// </summary>
        public static RunState Start(GameData data, ulong seed, IList<string> classIds)
        {
            ValidateParty(data, classIds);
            var run = new RunState(data, seed);
            run.Log.Seed = seed;
            foreach (var id in classIds)
            {
                var cls = data.Heroes.Get(id);
                run._party.Add(new RunHero { ClassId = id, Hp = cls.BaseStats.MaxHp });
                run.Log.PartyClasses.Add(id);
            }
            run.BeginLayer(1);
            return run;
        }

        public static void ValidateParty(GameData data, IList<string> classIds)
        {
            if (classIds == null || classIds.Count != data.Heroes.PartySize)
                throw new ArgumentException($"パーティは{data.Heroes.PartySize}人にしてください");
            var seen = new HashSet<string>();
            foreach (var id in classIds)
            {
                if (data.Heroes.Get(id) == null) throw new ArgumentException($"職業 '{id}' がありません");
                if (!seen.Add(id)) throw new ArgumentException($"職業 '{id}' が重複しています");
            }
        }

        // ================= 乱数(用途ごとに独立) =================

        /// <summary>
        /// ランのシードと用途番号から、独立したシードを作る。呼ぶ順番に左右されないので、
        /// 用途を増やしても既存の抽選結果は変わらない。
        /// </summary>
        public static ulong DeriveSeed(ulong seed, uint purpose)
        {
            var r = new DeterministicRandom(seed).Fork(purpose);
            return ((ulong)r.NextUInt() << 32) | r.NextUInt();
        }

        public const uint PurposeDungeon = 100;
        public const uint PurposeRaid = 200;
        public const uint PurposeSkill = 300;

        // ================= 層の開始 =================

        private void BeginLayer(int layer)
        {
            Layer = layer;
            Phase = RunPhase.Raid;

            // 層の開始時に効くスキル(野営の心得・不死鳥の羽)
            var skillDefs = SkillDefs();
            int revive = SkillMath.Sum(skillDefs, SkillEffectType.Revive);
            int heal = SkillMath.Sum(skillDefs, SkillEffectType.LayerStartHeal);
            for (int i = 0; i < _party.Count; i++)
            {
                var h = _party[i];
                int max = EffectiveStats(i).MaxHp;
                if (h.Hp <= 0 && revive > 0) h.Hp = IntMath.Max(1, max * IntMath.Min(revive, 100) / 100);
                else if (h.Hp > 0 && heal > 0) h.Hp = IntMath.Min(max, h.Hp + max * heal / 100);
            }

            // ダンジョンを抽選
            var candidates = Data.NpcDungeons?.ForLayer(layer);
            if (candidates == null || candidates.Count == 0) throw new InvalidOperationException($"層{layer}のNPCダンジョンがありません");
            var pick = candidates[new DeterministicRandom(DeriveSeed(Seed, PurposeDungeon + (uint)layer)).Next(candidates.Count)];
            CurrentDungeonId = pick.Id;

            CurrentRaid = new RaidSimulation(pick, Data, CurrentPartySetups(), DeriveSeed(Seed, PurposeRaid + (uint)layer),
                Charges, SkillMath.Modifiers(skillDefs));

            Log.Layers.Add(new RunLayerLog { Layer = layer, DungeonId = pick.Id, DungeonHash = DungeonHash(pick, Data) });
        }

        /// <summary>ダンジョンの中身(マス・養分・モンスター)のハッシュ。</summary>
        public static ulong DungeonHash(Serialization.DungeonSnapshot snapshot, GameData data)
            => Serialization.DungeonSnapshotConverter.ToState(snapshot, data).ComputeHash();

        // ================= 攻略の終了 =================

        /// <summary>
        /// 攻略(CurrentRaid)が終わったら呼ぶ。HPを引き継ぎ、クリアなら報酬へ、そうでなければランを終える。
        /// </summary>
        public void CompleteRaid()
        {
            if (Phase != RunPhase.Raid) throw new InvalidOperationException("攻略中ではありません");
            if (!CurrentRaid.IsFinished) throw new InvalidOperationException("攻略がまだ終わっていません");

            var log = CurrentLayerLog;
            log.Commands.AddRange(CurrentRaid.AppliedCommands);
            log.Outcome = CurrentRaid.Outcome;
            log.EndTick = CurrentRaid.CurrentTick;

            var after = CurrentRaid.ExportParty();
            for (int i = 0; i < _party.Count; i++) _party[i].Hp = after[i].Hp;

            if (CurrentRaid.Outcome != RaidOutcome.Cleared)
            {
                Phase = RunPhase.Defeat;
                FinishLog();
                return;
            }

            Stars = Layer;
            if (Layer >= LayerCount)
            {
                Phase = RunPhase.Victory;
                FinishLog();
                return;
            }
            BeginReward();
        }

        // ================= 報酬 =================

        private void BeginReward()
        {
            Phase = RunPhase.Reward;
            SkillChosenThisReward = false;
            UnspentPoints += Data.Skills.PointsPerLayer;
            _maxHpAtRewardStart = new int[_party.Count];
            for (int i = 0; i < _party.Count; i++) _maxHpAtRewardStart[i] = EffectiveStats(i).MaxHp;

            _candidates.Clear();
            _candidates.AddRange(DrawSkillCandidates(Data.Skills, _skills, DeriveSeed(Seed, PurposeSkill + (uint)Layer)));
            CurrentLayerLog.SkillCandidates.AddRange(_candidates);
        }

        /// <summary>
        /// まだ持っていないスキルから候補を抽選する(シードで再現できる)。残りが少なければその数だけ。
        /// </summary>
        public static List<string> DrawSkillCandidates(SkillCatalog catalog, IReadOnlyCollection<string> owned, ulong seed)
        {
            var pool = new List<string>();
            foreach (var s in catalog.Skills)
            {
                bool has = false;
                foreach (var o in owned) if (o == s.Id) { has = true; break; }
                if (!has) pool.Add(s.Id);
            }
            var arr = pool.ToArray();
            new DeterministicRandom(seed).Shuffle(arr);
            var result = new List<string>();
            for (int i = 0; i < arr.Length && i < catalog.CandidateCount; i++) result.Add(arr[i]);
            return result;
        }

        /// <summary>候補からスキルを1つ選ぶ(報酬1回につき1つ)。</summary>
        public bool ChooseSkill(string skillId)
        {
            if (Phase != RunPhase.Reward || SkillChosenThisReward || !_candidates.Contains(skillId)) return false;
            var def = Data.Skills.Get(skillId);
            _skills.Add(skillId);
            SkillChosenThisReward = true;
            CurrentLayerLog.SkillChoice = skillId;

            foreach (var e in def.Effects)
            {
                if (e.Type != SkillEffectType.ExtraCharge) continue;
                if (e.Intervention == InterventionType.Heal) Charges.Heal += e.Value; else Charges.Torch += e.Value;
            }
            return true;
        }

        /// <summary>スキルポイントを割り振る。</summary>
        public bool Allocate(int heroIndex, StatKind stat, int points)
        {
            if (Phase != RunPhase.Reward || points <= 0 || points > UnspentPoints) return false;
            if (heroIndex < 0 || heroIndex >= _party.Count) return false;
            var h = _party[heroIndex];
            h.Points = SkillMath.With(h.Points, stat, SkillMath.Get(h.Points, stat) + points);
            UnspentPoints -= points;
            CurrentLayerLog.Allocations.Add(new PointAllocation { HeroIndex = heroIndex, Stat = stat, Points = points });
            return true;
        }

        /// <summary>報酬を確定して次の層へ。スキルを選んでいないと進めない(候補がない場合を除く)。</summary>
        public bool ConfirmReward()
        {
            if (Phase != RunPhase.Reward) return false;
            if (!SkillChosenThisReward && _candidates.Count > 0) return false;

            // 最大HPが増えた分は、生きている仲間の現在HPにも足す
            for (int i = 0; i < _party.Count; i++)
            {
                int delta = EffectiveStats(i).MaxHp - _maxHpAtRewardStart[i];
                if (_party[i].Hp > 0 && delta > 0) _party[i].Hp += delta;
            }
            BeginLayer(Layer + 1);
            return true;
        }

        // ================= 能力値 =================

        public List<SkillDefinition> SkillDefs()
        {
            var list = new List<SkillDefinition>();
            foreach (var id in _skills) list.Add(Data.Skills.Get(id));
            return list;
        }

        /// <summary>職業の初期値・ポイント・スキルを合わせた最終的な能力値。</summary>
        public HeroStats EffectiveStats(int heroIndex)
        {
            var h = _party[heroIndex];
            return SkillMath.EffectiveStats(Data.Heroes.Get(h.ClassId).BaseStats, h.Points, Data.Skills, SkillDefs());
        }

        /// <summary>攻略に渡す勇者の設定(現在の能力値とHP)。</summary>
        public List<HeroSetup> CurrentPartySetups()
        {
            var list = new List<HeroSetup>();
            for (int i = 0; i < _party.Count; i++)
            {
                var stats = EffectiveStats(i);
                list.Add(new HeroSetup
                {
                    ClassId = _party[i].ClassId, Stats = stats,
                    Hp = IntMath.Min(_party[i].Hp, stats.MaxHp), Skills = new List<string>(_skills),
                });
            }
            return list;
        }

        // ================= 結果 =================

        private void FinishLog()
        {
            // 最後の攻略(CurrentRaid)は画面表示のため残しておく
            Log.Stars = Stars;
            Log.FinalHash = ComputeHash();
        }

        /// <summary>ラン全体の状態のハッシュ(リプレイ照合用)。</summary>
        public ulong ComputeHash()
        {
            var h = new StateHasher();
            h.Add(Layer); h.Add((int)Phase); h.Add(Stars); h.Add(UnspentPoints);
            h.Add(Charges.Heal); h.Add(Charges.Torch);
            foreach (var p in _party)
            {
                h.Add(p.ClassId); h.Add(p.Hp);
                h.Add(p.Points.MaxHp); h.Add(p.Points.Attack); h.Add(p.Points.Defense); h.Add(p.Points.Speed); h.Add(p.Points.Magic);
            }
            foreach (var s in _skills) h.Add(s);
            if (CurrentRaid != null) h.Add(CurrentRaid.ComputeHash());
            return h.Value;
        }
    }
}
