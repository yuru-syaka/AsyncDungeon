using System.Collections.Generic;
using System.Linq;
using DungeonGame.Core.Data;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Run;
using DungeonGame.Core.Serialization;
using DungeonGame.Core.Skills;
using NUnit.Framework;

namespace DungeonGame.Core.Tests
{
    /// <summary>マイルストーン5:3層ラン・スキル・ポイント配分・★評価のテスト。</summary>
    public class RunTests
    {
        private static readonly string[] DefaultParty = { "warrior", "mage", "priest" };
        private GameData _data;

        [SetUp]
        public void SetUp() => _data = TestData.LoadGameDataWithNpc();

        /// <summary>1層を自動で攻略し、クリアできたら報酬フェーズのランを返す(できなければ null)。</summary>
        private RunState ClearLayer1(ulong seed)
        {
            var run = RunState.Start(_data, seed, DefaultParty);
            AutoPlayer.PlayRaid(run.CurrentRaid);
            run.CompleteRaid();
            return run.Phase == RunPhase.Reward ? run : null;
        }

        // ---------- データ ----------

        [Test]
        public void スキルが10種以上データで定義されている()
        {
            Assert.GreaterOrEqual(_data.Skills.Skills.Count, 10);
            Assert.AreEqual(_data.Skills.Skills.Count, _data.Skills.Skills.Select(s => s.Id).Distinct().Count(), "IDが重複していない");
            Assert.IsTrue(_data.Skills.Skills.All(s => s.Effects.Count > 0 && !string.IsNullOrEmpty(s.Name)));
        }

        [Test]
        public void NPCダンジョンが各層2個以上あり全て有効()
        {
            for (int layer = 1; layer <= _data.Rules.LayerCount; layer++)
            {
                var list = _data.NpcDungeons.ForLayer(layer);
                Assert.GreaterOrEqual(list.Count, 2, $"層{layer}");
                foreach (var s in list)
                    Assert.IsTrue(DungeonStorage.TrySave(s, _data, out _).IsValid, s.Id);
            }
        }

        // ---------- 抽選の再現性(必須テスト) ----------

        [Test]
        public void スキル候補の抽選がシードで再現できる()
        {
            var owned = new List<string>();
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var a = RunState.DrawSkillCandidates(_data.Skills, owned, seed);
                var b = RunState.DrawSkillCandidates(_data.Skills, owned, seed);
                CollectionAssert.AreEqual(a, b);
                Assert.AreEqual(3, a.Count);
                Assert.AreEqual(3, a.Distinct().Count(), "候補は重複しない");
            }
            // シードが違えば、少なくともいくつかは違う候補になる
            var sets = Enumerable.Range(1, 20).Select(s => string.Join(",", RunState.DrawSkillCandidates(_data.Skills, owned, (ulong)s))).Distinct();
            Assert.Greater(sets.Count(), 5);
        }

        [Test]
        public void 持っているスキルは候補に出ず_残りが少なければその数だけ出る()
        {
            var owned = _data.Skills.Skills.Select(s => s.Id).Take(_data.Skills.Skills.Count - 2).ToList();
            var c = RunState.DrawSkillCandidates(_data.Skills, owned, 3);
            Assert.AreEqual(2, c.Count);
            Assert.IsFalse(c.Any(owned.Contains));
        }

        [Test]
        public void ランの報酬画面のスキル候補もシードで再現できる()
        {
            RunState a = null, b = null;
            for (ulong seed = 1; seed < 30 && a == null; seed++) { a = ClearLayer1(seed); b = a != null ? ClearLayer1(seed) : null; }
            Assert.IsNotNull(a, "1層をクリアできるシードがあること");
            CollectionAssert.AreEqual(a.SkillCandidates, b.SkillCandidates);
            Assert.AreEqual(a.Log.Layers[0].SkillCandidates, a.SkillCandidates);
        }

        [Test]
        public void NPCダンジョンの抽選がシードで再現できる()
        {
            var ids = new HashSet<string>();
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var a = RunState.Start(_data, seed, DefaultParty);
                var b = RunState.Start(_data, seed, DefaultParty);
                Assert.AreEqual(a.CurrentDungeonId, b.CurrentDungeonId);
                ids.Add(a.CurrentDungeonId);
            }
            Assert.Greater(ids.Count, 1, "シードによって違うダンジョンが選ばれる");
        }

        // ---------- 編成 ----------

        [Test]
        public void 編成は異なる職業3人でなければならない()
        {
            Assert.Throws<System.ArgumentException>(() => RunState.Start(_data, 1, new[] { "warrior", "warrior", "mage" }));
            Assert.Throws<System.ArgumentException>(() => RunState.Start(_data, 1, new[] { "warrior", "mage" }));
            Assert.Throws<System.ArgumentException>(() => RunState.Start(_data, 1, new[] { "warrior", "mage", "ninja" }));
            Assert.DoesNotThrow(() => RunState.Start(_data, 1, new[] { "thief", "mage", "priest" }));
        }

        // ---------- 報酬 ----------

        [Test]
        public void 報酬_スキルを選ばないと次の層へ進めない()
        {
            var run = Enumerable.Range(1, 30).Select(s => ClearLayer1((ulong)s)).First(r => r != null);
            Assert.IsFalse(run.ConfirmReward());
            Assert.IsFalse(run.ChooseSkill("not_a_candidate"));
            Assert.IsTrue(run.ChooseSkill(run.SkillCandidates[1]));
            Assert.IsFalse(run.ChooseSkill(run.SkillCandidates[0]), "1回の報酬で選べるのは1つ");
            Assert.IsTrue(run.ConfirmReward());
            Assert.AreEqual(2, run.Layer);
            Assert.AreEqual(RunPhase.Raid, run.Phase);
        }

        [Test]
        public void 報酬_ポイント配分で能力値が上がり_持っている以上は振れない()
        {
            var run = Enumerable.Range(1, 30).Select(s => ClearLayer1((ulong)s)).First(r => r != null);
            int pts = _data.Skills.PointsPerLayer;
            Assert.AreEqual(pts, run.UnspentPoints);

            var before = run.EffectiveStats(0);
            int hpBefore = run.Party[0].Hp;
            Assert.IsFalse(run.Allocate(0, StatKind.Attack, pts + 1));
            Assert.IsTrue(run.Allocate(0, StatKind.Attack, 2));
            Assert.IsTrue(run.Allocate(0, StatKind.MaxHp, 3));
            Assert.AreEqual(pts - 5, run.UnspentPoints);

            var after = run.EffectiveStats(0);
            Assert.AreEqual(before.Attack + 2 * _data.Skills.PointValues.Attack, after.Attack);
            Assert.AreEqual(before.MaxHp + 3 * _data.Skills.PointValues.MaxHp, after.MaxHp);

            run.ChooseSkill(run.SkillCandidates.First(id => id != "vitality" && id != "rest"));
            run.ConfirmReward();
            if (hpBefore > 0) Assert.AreEqual(hpBefore + 3 * _data.Skills.PointValues.MaxHp, run.Party[0].Hp, "増えた最大HPの分だけ現在HPも増える");
            Assert.AreEqual(pts - 5, run.UnspentPoints, "余ったポイントは持ち越せる");
            Assert.AreEqual(2, run.Log.Layers[0].Allocations.Count);
        }

        [Test]
        public void 勇者のHPは層をまたいで引き継がれる()
        {
            var run = Enumerable.Range(1, 30).Select(s => ClearLayer1((ulong)s)).First(r => r != null);
            var hpAfterLayer1 = run.Party.Select(h => h.Hp).ToList();
            Assert.IsTrue(hpAfterLayer1.Any(hp => hp < 100), "1層で多少はダメージを受けている");

            run.ChooseSkill(run.SkillCandidates.First(id => id != "vitality" && id != "rest" && id != "phoenix"));
            run.ConfirmReward();
            var heroes = run.CurrentRaid.Heroes;
            for (int i = 0, h = 0; i < hpAfterLayer1.Count; i++)
            {
                if (hpAfterLayer1[i] <= 0) continue; // 倒れた仲間は参加しない
                Assert.AreEqual(hpAfterLayer1[i], heroes[h++].Hp);
            }
        }

        // ---------- スキル効果 ----------

        [Test]
        public void スキル効果_固定値を足してから割合で上がる()
        {
            var baseStats = new HeroStats { MaxHp = 100, Attack = 10, Defense = 2, Speed = 5, Magic = 10 };
            var skills = new[] { _data.Skills.Get("power"), _data.Skills.Get("iron_wall"), _data.Skills.Get("vitality") };
            var s = SkillMath.EffectiveStats(baseStats, new HeroStats { Attack = 5 }, _data.Skills, skills);
            Assert.AreEqual((10 + 5 * _data.Skills.PointValues.Attack) * 120 / 100, s.Attack);
            Assert.AreEqual(5, s.Defense);
            Assert.AreEqual(125, s.MaxHp);

            var mods = SkillMath.Modifiers(new[] { _data.Skills.Get("keen_eye"), _data.Skills.Get("vampire"), _data.Skills.Get("regeneration") });
            Assert.AreEqual(15, mods.CritBonus);
            Assert.AreEqual(25, mods.LifestealPercent);
            Assert.AreEqual(30, mods.RegenInterval);
        }

        [Test]
        public void スキル効果_道具袋で介入スキルの回数が増える()
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                var cands = RunState.DrawSkillCandidates(_data.Skills, new List<string>(), RunState.DeriveSeed(seed, RunState.PurposeSkill + 1));
                if (!cands.Contains("supplies")) continue;
                var run = ClearLayer1(seed);
                if (run == null) continue;
                int heal = run.Charges.Heal, torch = run.Charges.Torch;
                Assert.IsTrue(run.ChooseSkill("supplies"));
                Assert.AreEqual(heal + 1, run.Charges.Heal);
                Assert.AreEqual(torch + 1, run.Charges.Torch);
                return;
            }
            Assert.Fail("道具袋が候補に出るシードが見つからない");
        }

        [Test]
        public void スキル効果_不死鳥の羽で倒れた仲間が次の層で復活する()
        {
            for (ulong seed = 1; seed < 300; seed++)
            {
                var cands = RunState.DrawSkillCandidates(_data.Skills, new List<string>(), RunState.DeriveSeed(seed, RunState.PurposeSkill + 1));
                if (!cands.Contains("phoenix")) continue;
                var run = ClearLayer1(seed);
                if (run == null) continue;
                run.Party[2].Hp = 0; // 倒れたことにする
                run.ChooseSkill("phoenix");
                run.ConfirmReward();
                Assert.AreEqual(run.EffectiveStats(2).MaxHp / 2, run.Party[2].Hp);
                Assert.AreEqual(3, run.CurrentRaid.Heroes.Count, "復活した仲間も攻略に参加する");
                return;
            }
            Assert.Fail("不死鳥の羽が候補に出るシードが見つからない");
        }

        [Test]
        public void スキル効果_再生で攻略中にHPが回復する()
        {
            var party = new List<HeroSetup> { RaidFactory.CreateHero(_data, "warrior") };
            party[0].Hp = 10;
            // モンスターのいない通路(ダメージを受けない)で確認する
            var b = Dungeon.DungeonBuilder.CreateNew(_data, 1, 1);
            for (int y = b.Map.Entrance.Y + 1; y < b.Map.Goal.Y; y++) b.Map.Dig(b.Map.Entrance.X, y);
            var mods = SkillMath.Modifiers(new[] { _data.Skills.Get("regeneration") });
            var sim = new RaidSimulation(b.ToSnapshot(), _data, party, 1, null, mods);
            while (sim.CurrentTick <= 30) sim.Step(); // 0ティック目に入場、30ティック目に1回目の再生
            Assert.AreEqual(10 + 3, sim.Heroes[0].Hp);
        }

        // ---------- ★評価・決定論 ----------

        [Test]
        public void 星の数は突破した層の数と一致する()
        {
            var seen = new HashSet<int>();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var run = AutoPlayer.PlayRun(_data, seed, new[] { "warrior", "mage", "thief" });
                int cleared = run.Log.Layers.Count(l => l.Outcome == RaidOutcome.Cleared);
                Assert.AreEqual(cleared, run.Stars);
                Assert.AreEqual(run.Stars, run.Log.Stars);
                Assert.AreEqual(run.Stars == run.LayerCount ? RunPhase.Victory : RunPhase.Defeat, run.Phase);
                seen.Add(run.Stars);
            }
            Assert.Greater(seen.Count, 1, "結果がばらつくこと");
        }

        [Test]
        public void 同じシードと同じ操作ならラン全体が完全に同じ結果になる()
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var a = AutoPlayer.PlayRun(_data, seed, DefaultParty);
                var b = AutoPlayer.PlayRun(_data, seed, DefaultParty);
                Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
                Assert.AreEqual(GameJson.Serialize(a.Log), GameJson.Serialize(b.Log));
            }
        }
    }
}
