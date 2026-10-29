using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonGame.Core.Data;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Run;
using DungeonGame.Core.Serialization;
using DungeonGame.Core.Skills;
using NUnit.Framework;

namespace DungeonGame.Core.Tests
{
    /// <summary>マイルストーン6:入力ログの記録・JSON・リプレイと検証のテスト。</summary>
    public class ReplayTests
    {
        private GameData _data;

        [SetUp]
        public void SetUp() => _data = TestData.LoadGameDataWithNpc();

        /// <summary>
        /// 人が遊んだ想定のラン:決まったタイミングでたいまつ、HPが減ったら回復、
        /// 報酬は候補の最後を選び、ポイントは色々な能力値にばらばらに振る。
        /// </summary>
        private RunState PlayLikeHuman(ulong seed, string[] party)
        {
            var run = RunState.Start(_data, seed, party);
            var stats = new[] { StatKind.MaxHp, StatKind.Attack, StatKind.Defense, StatKind.Speed, StatKind.Magic };
            int turn = 0;
            while (!run.IsOver)
            {
                if (run.Phase == RunPhase.Raid)
                {
                    var raid = run.CurrentRaid;
                    var goal = raid.Dungeon.Map.Goal;
                    while (!raid.IsFinished)
                    {
                        if (raid.CurrentTick == 15) raid.Submit(InterventionType.Torch, goal.X, goal.Y - 3);
                        if (raid.Heroes.Any(h => h.Active && h.Hp * 100 < h.Stats.MaxHp * 40)) raid.Submit(InterventionType.Heal);
                        raid.Step();
                    }
                    run.CompleteRaid();
                }
                else
                {
                    run.ChooseSkill(run.SkillCandidates[run.SkillCandidates.Count - 1]);
                    while (run.UnspentPoints > 1)
                    {
                        run.Allocate(turn % 3, stats[turn % stats.Length], 1 + turn % 2);
                        turn++;
                    }
                    run.ConfirmReward(); // 1ポイントは持ち越し
                }
            }
            return run;
        }

        private static RunLog Clone(RunLog log) => RunLogIO.FromJson(RunLogIO.ToJson(log));

        /// <summary>2層以上進んだ(=報酬の入力がある)ランを探す。</summary>
        private RunState PlayedRunWithReward()
        {
            for (ulong seed = 1; seed < 40; seed++)
            {
                var run = PlayLikeHuman(seed, new[] { "warrior", "mage", "priest" });
                if (run.Stars >= 1 && run.Log.Layers.Count >= 2) return run;
            }
            Assert.Fail("報酬まで進むランが見つからない");
            return null;
        }

        // ---------- 記録・JSON ----------

        [Test]
        public void 入力ログに編成と介入と報酬の選択が記録される()
        {
            var run = PlayedRunWithReward();
            var log = run.Log;
            CollectionAssert.AreEqual(new[] { "warrior", "mage", "priest" }, log.PartyClasses);
            Assert.IsTrue(log.Layers[0].Commands.Any(c => c.Type == InterventionType.Torch && c.Tick == 15));
            Assert.IsNotNull(log.Layers[0].SkillChoice);
            Assert.Greater(log.Layers[0].Allocations.Count, 0);
            Assert.AreEqual(run.Stars, log.Stars);
            Assert.AreNotEqual(0UL, log.Layers[0].DungeonHash);
        }

        [Test]
        public void 入力ログをJSONにして戻しても内容が変わらない()
        {
            var log = PlayedRunWithReward().Log;
            string json1 = RunLogIO.ToJson(log);
            string json2 = RunLogIO.ToJson(RunLogIO.FromJson(json1));
            Assert.AreEqual(json1, json2);
            StringAssert.Contains("\"Torch\"", json1);
        }

        // ---------- 再計算(必須の決定論テスト) ----------

        [Test]
        public void ログから再計算すると同じ結果になる()
        {
            var parties = new[]
            {
                new[] { "warrior", "mage", "priest" }, new[] { "thief", "warrior", "priest" }, new[] { "mage", "thief", "warrior" },
            };
            int checkedRuns = 0;
            foreach (var party in parties)
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var run = PlayLikeHuman(seed, party);
                var result = RunReplayer.Verify(RunLogIO.ToJson(run.Log), _data);
                Assert.IsTrue(result.IsValid, $"{string.Join("/", party)} seed={seed}: {result}");
                Assert.AreEqual(run.Stars, result.Stars);
                Assert.AreEqual(run.ComputeHash(), result.Run.ComputeHash());
                checkedRuns++;
            }
            Assert.AreEqual(12, checkedRuns);
        }

        [Test]
        public void 一ティックずつ再生しても同じ結果になる()
        {
            var run = PlayedRunWithReward();
            var replayer = new RunReplayer(Clone(run.Log), _data);
            int steps = 0;
            var layersSeen = new HashSet<int>();
            while (!replayer.IsFinished)
            {
                replayer.Step();
                layersSeen.Add(replayer.Run.Layer);
                steps++;
            }
            Assert.IsNull(replayer.Error);
            Assert.AreEqual(run.ComputeHash(), replayer.Run.ComputeHash());
            Assert.AreEqual(run.Log.Layers.Count, layersSeen.Count, "全部の層を再生した");
            Assert.AreEqual(run.Log.Layers.Sum(l => l.EndTick), steps, "ティック数の合計が一致");
        }

        // ---------- 改ざん・不正の検出 ----------

        [Test]
        public void 星を書き換えたログは弾かれる()
        {
            var log = Clone(PlayedRunWithReward().Log);
            log.Stars = 3 - log.Stars == 0 ? 2 : 3;
            var r = RunReplayer.Verify(log, _data);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("★", r.Error);
        }

        [Test]
        public void 候補にないスキルを選んだログは弾かれる()
        {
            var log = Clone(PlayedRunWithReward().Log);
            var notCandidate = _data.Skills.Skills.Select(s => s.Id).First(id => !log.Layers[0].SkillCandidates.Contains(id));
            log.Layers[0].SkillChoice = notCandidate;
            var r = RunReplayer.Verify(log, _data);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("候補", r.Error);
        }

        [Test]
        public void 持っている以上のポイントを振ったログは弾かれる()
        {
            var log = Clone(PlayedRunWithReward().Log);
            log.Layers[0].Allocations.Add(new PointAllocation { HeroIndex = 0, Stat = StatKind.Attack, Points = 99 });
            var r = RunReplayer.Verify(log, _data);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("ポイント", r.Error);
        }

        [Test]
        public void 回数を超えて介入したログは弾かれる()
        {
            var log = Clone(PlayedRunWithReward().Log);
            var cmds = log.Layers[0].Commands;
            for (int i = 0; i < 10; i++) cmds.Add(new InterventionCommand { Tick = 1, Type = InterventionType.Heal });
            log.Layers[0].Commands = cmds.OrderBy(c => c.Tick).ToList();
            var r = RunReplayer.Verify(log, _data);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("介入", r.Error);
        }

        [Test]
        public void 記録された攻略結果や最終ハッシュを書き換えたログは弾かれる()
        {
            var original = PlayedRunWithReward().Log;

            var endTick = Clone(original);
            endTick.Layers[0].EndTick -= 1;
            var r1 = RunReplayer.Verify(endTick, _data);
            Assert.IsFalse(r1.IsValid);
            StringAssert.Contains("攻略結果", r1.Error);

            var hash = Clone(original);
            hash.FinalHash ^= 1;
            Assert.IsFalse(RunReplayer.Verify(hash, _data).IsValid);
        }

        [Test]
        public void 介入を書き換えると再計算の内容に反映される()
        {
            // 介入の時刻を変えたログは「そう遊んだ場合」として再計算される。
            // 結果が変わらない場合でも、ログの内容(介入の時刻)は再計算結果と一致していなければならない。
            var log = Clone(PlayedRunWithReward().Log);
            var c = log.Layers[0].Commands[0];
            c.Tick += 7;
            log.Layers[0].Commands[0] = c;
            log.Layers[0].Commands = log.Layers[0].Commands.OrderBy(x => x.Tick).ToList();

            var r = RunReplayer.Verify(log, _data);
            if (r.IsValid)
                Assert.AreEqual(c.Tick, r.Run.Log.Layers[0].Commands.First(x => x.Type == c.Type && x.X == c.X && x.Y == c.Y).Tick);
            else
                Assert.IsNotNull(r.Error); // 結果が変わったので、記録された結果と合わず弾かれた
        }

        [Test]
        public void シードや編成を書き換えたログは弾かれる()
        {
            var original = PlayedRunWithReward().Log;

            var seedChanged = Clone(original);
            seedChanged.Seed += 12345;
            Assert.IsFalse(RunReplayer.Verify(seedChanged, _data).IsValid);

            var partyChanged = Clone(original);
            partyChanged.PartyClasses[0] = partyChanged.PartyClasses[1];
            var r = RunReplayer.Verify(partyChanged, _data);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("編成", r.Error);
        }

        [Test]
        public void ダンジョンの中身が記録時と違うと弾かれる()
        {
            var log = PlayedRunWithReward().Log;
            string targetId = log.Layers[0].DungeonId;

            // 同じIDのまま中身(土の養分1マス)だけ変えたNPCダンジョンでデータを作り直す
            var jsons = Directory.GetFiles(TestData.NpcDungeonDir, "*.json").OrderBy(f => f).Select(File.ReadAllText).Select(json =>
            {
                var s = DungeonStorage.LoadSnapshot(json);
                if (s.Id != targetId) return json;
                int y = 2;
                int x = s.Tiles[y].IndexOf('s');
                var cells = s.Nutrient[y].Split(' ');
                cells[x] = (int.Parse(cells[x]) == 0 ? 1 : 0).ToString();
                s.Nutrient[y] = string.Join(" ", cells);
                return GameJson.Serialize(s);
            }).ToList();
            var altered = TestData.LoadGameData().WithNpcDungeons(jsons);

            var r = RunReplayer.Verify(log, altered);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("中身", r.Error);
        }

        [Test]
        public void 壊れたJSONや新しすぎる形式のログは弾かれる()
        {
            Assert.IsFalse(RunReplayer.Verify("{ this is not json", _data).IsValid);

            var log = Clone(PlayedRunWithReward().Log);
            log.FormatVersion = RunLog.CurrentFormatVersion + 1;
            Assert.IsFalse(RunReplayer.Verify(RunLogIO.ToJson(log), _data).IsValid);
        }
    }
}
