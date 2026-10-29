using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonGame.Core.Data;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Serialization;
using DungeonGame.Core.Simulation;
using NUnit.Framework;

namespace DungeonGame.Core.Tests
{
    /// <summary>マイルストーン4:勇者AI・戦闘・介入スキルのテスト。</summary>
    public class RaidTests
    {
        private GameData _data;

        [SetUp]
        public void SetUp() => _data = TestData.LoadGameData();

        /// <summary>モンスターのいない、まっすぐな通路のスナップショット。</summary>
        private DungeonSnapshot EmptyCorridor(int layer)
        {
            var b = DungeonBuilder.CreateNew(_data, layer, 1);
            int x = b.Map.Entrance.X;
            for (int y = b.Map.Entrance.Y + 1; y < b.Map.Goal.Y; y++) b.Map.Dig(x, y); // Map.Digなのでモンスターは生まれない
            return b.ToSnapshot();
        }

        /// <summary>育成まで済ませた「普通の」ダンジョン。</summary>
        private DungeonSnapshot Grown(int layer, ulong seed)
        {
            var b = TestData.BuildTypical(_data, layer, seed);
            b.Grow(b.Rule.MaxEcologyTicks);
            return b.ToSnapshot();
        }

        private List<HeroSetup> Party(params string[] classes) => classes.Select(c => RaidFactory.CreateHero(_data, c)).ToList();

        private static RaidSimulation Run(RaidSimulation sim)
        {
            SimulationRunner.RunToEnd(sim, int.MaxValue);
            return sim;
        }

        // ---------- 基本動作 ----------

        [Test]
        public void 職業データが7種類読めて全員に解説がある()
        {
            Assert.AreEqual(7, _data.Heroes.Classes.Count);
            Assert.IsTrue(_data.Heroes.Classes.All(c => !string.IsNullOrEmpty(c.Description)));
            Assert.AreEqual(3, _data.Heroes.Get("hunter").AttackRange);
            Assert.AreEqual(ClassAbility.Heal, _data.Heroes.Get("paladin").Ability);
            Assert.IsNotNull(_data.Heroes.Get("warrior"));
            Assert.AreEqual(2, _data.Heroes.Get("mage").AttackRange);
            Assert.AreEqual(3, _data.Heroes.PartySize);
        }

        [Test]
        public void 敵のいない通路なら階段に着いてクリアする()
        {
            var sim = Run(new RaidSimulation(EmptyCorridor(1), _data, Party("warrior", "mage", "priest"), 1));
            Assert.AreEqual(RaidOutcome.Cleared, sim.Outcome);
            Assert.IsTrue(sim.Heroes.All(h => h.Entered && !h.Dead), "全員入って生きていること");
        }

        [Test]
        public void 霧の中を探索してゴールを見つけられる()
        {
            // 横穴だらけのダンジョンで、モンスターを消して探索だけさせる
            var b = TestData.BuildTypical(_data, 2, 5);
            b.State.ClearMonsters();
            var sim = new RaidSimulation(b.ToSnapshot(), _data, Party("thief", "warrior", "mage"), 2);
            Assert.IsFalse(sim.Fog.IsKnown(b.Map.Goal), "最初はゴールが見えていない");
            Run(sim);
            Assert.AreEqual(RaidOutcome.Cleared, sim.Outcome, $"tick={sim.CurrentTick}");
        }

        [Test]
        public void 魔王の層では魔王を倒すとクリアになる()
        {
            var strong = Party("warrior", "mage", "priest");
            foreach (var h in strong) { h.Stats.Attack += 40; h.Stats.Magic += 40; h.Stats.MaxHp += 300; h.Hp = h.Stats.MaxHp; }
            var sim = new RaidSimulation(EmptyCorridor(_data.Rules.LayerCount), _data, strong, 3);
            Assert.IsNotNull(sim.Boss);
            Assert.AreEqual(sim.Dungeon.Map.Goal, sim.Boss.Pos);
            Run(sim);
            Assert.AreEqual(RaidOutcome.Cleared, sim.Outcome);
            Assert.IsTrue(sim.Boss.Dead);
        }

        [Test]
        public void 弱すぎるパーティは全滅する()
        {
            var weak = Party("mage", "mage", "mage");
            foreach (var h in weak) { h.Hp = 1; }
            var sim = Run(new RaidSimulation(Grown(3, 7), _data, weak, 4));
            Assert.AreEqual(RaidOutcome.Wiped, sim.Outcome);
            Assert.IsTrue(sim.ExportParty().All(h => h.Hp == 0));
        }

        [Test]
        public void HPが0の勇者は参加せず_全員0なら即全滅()
        {
            var party = Party("warrior", "mage", "priest");
            party[1].Hp = 0;
            var sim = new RaidSimulation(EmptyCorridor(1), _data, party, 1);
            Assert.AreEqual(2, sim.Heroes.Count);

            foreach (var h in party) h.Hp = 0;
            Assert.AreEqual(RaidOutcome.Wiped, new RaidSimulation(EmptyCorridor(1), _data, party, 1).Outcome);
        }

        [Test]
        public void 戦闘でモンスターを倒すと養分が土に還り総量は変わらない()
        {
            var snap = Grown(2, 3);
            var sim = new RaidSimulation(snap, _data, Party("warrior", "mage", "priest"), 9);
            int n0 = sim.Dungeon.GrandTotalNutrient, m0 = sim.Dungeon.GrandTotalMana;
            Run(sim);
            Assert.Greater(sim.Heroes.Sum(h => h.Kills), 0, "何体かは倒していること");
            Assert.AreEqual(n0, sim.Dungeon.GrandTotalNutrient);
            Assert.AreEqual(m0, sim.Dungeon.GrandTotalMana);
        }

        [Test]
        public void 攻略しても元のスナップショットは変わらない()
        {
            var snap = Grown(1, 3);
            string before = GameJson.Serialize(snap);
            Run(new RaidSimulation(snap, _data, Party("warrior", "mage", "priest"), 9));
            Assert.AreEqual(before, GameJson.Serialize(snap));
        }

        [Test]
        public void ダメージは最低1で会心は2倍()
        {
            var rng = new Determinism.DeterministicRandom(1);
            for (int i = 0; i < 100; i++)
            {
                Assert.GreaterOrEqual(CombatMath.Damage(rng, 1, 100, 0, out _), 1);
                int d = CombatMath.Damage(rng, 20, 0, 100, out bool crit);
                Assert.IsTrue(crit);
                Assert.That(d, Is.InRange(36, 44));
            }
        }

        // ---------- 介入スキル ----------

        [Test]
        public void 回復はパーティ全員を回復し回数を消費する()
        {
            var party = Party("warrior", "mage", "priest");
            foreach (var h in party) h.Hp = 5;
            var charges = new InterventionCharges { Heal = 1, Torch = 0 };
            var sim = new RaidSimulation(EmptyCorridor(1), _data, party, 1, charges);

            Assert.IsTrue(sim.Submit(InterventionType.Heal));
            Assert.IsFalse(sim.Submit(InterventionType.Heal), "残り回数を超えては使えない");
            sim.Step();

            int pct = _data.Heroes.Interventions.HealPercent;
            foreach (var h in sim.Heroes) Assert.AreEqual(5 + h.Stats.MaxHp * pct / 100, h.Hp);
            Assert.AreEqual(0, charges.Heal);
            Assert.AreEqual(1, sim.AppliedCommands.Count);
            Assert.AreEqual(0, sim.AppliedCommands[0].Tick);
        }

        [Test]
        public void たいまつは指定した範囲の霧を晴らす()
        {
            var snap = EmptyCorridor(2);
            var charges = new InterventionCharges { Heal = 0, Torch = 1 };
            var sim = new RaidSimulation(snap, _data, Party("warrior"), 1, charges);
            var goal = sim.Dungeon.Map.Goal;
            Assert.IsFalse(sim.Fog.IsKnown(goal));

            Assert.IsFalse(sim.Submit(InterventionType.Torch, -1, 0), "マップ外は指定できない");
            Assert.IsTrue(sim.Submit(InterventionType.Torch, goal.X, goal.Y));
            sim.Step();
            Assert.IsTrue(sim.Fog.IsKnown(goal));
            Assert.IsFalse(sim.Submit(InterventionType.Torch, goal.X, goal.Y), "使い切ったら使えない");
        }

        [Test]
        public void たいまつでゴールを照らすと早く着く()
        {
            var b = TestData.BuildTypical(_data, 3, 5);
            b.State.ClearMonsters();
            var snap = b.ToSnapshot();
            var heroes = Party("warrior", "mage", "priest").Select(h => { h.Stats.MaxHp += 2000; h.Hp = h.Stats.MaxHp; h.Stats.Magic += 100; h.Stats.Attack += 100; return h; }).ToList();

            var plain = Run(new RaidSimulation(snap, _data, heroes, 1));
            var lit = new RaidSimulation(snap, _data, heroes, 1, new InterventionCharges { Torch = 1 });
            lit.Submit(InterventionType.Torch, b.Map.Goal.X, b.Map.Goal.Y);
            Run(lit);

            Assert.AreEqual(RaidOutcome.Cleared, plain.Outcome);
            Assert.AreEqual(RaidOutcome.Cleared, lit.Outcome);
            Assert.LessOrEqual(lit.CurrentTick, plain.CurrentTick);
        }

        // ---------- 決定論(必須テスト) ----------

        [Test]
        public void 同じスナップショットとシードと介入記録なら攻略結果が完全に同じになる()
        {
            var snap = Grown(3, 11);
            var party = Party("warrior", "priest", "thief");

            // 1回目:プレイ中に介入した想定
            var live = new RaidSimulation(snap, _data, party, 42, new InterventionCharges { Heal = 2, Torch = 3 });
            while (!live.IsFinished)
            {
                if (live.CurrentTick == 30) live.Submit(InterventionType.Torch, 11, 10);
                if (live.CurrentTick == 200) live.Submit(InterventionType.Heal);
                if (live.CurrentTick == 450) live.Submit(InterventionType.Torch, 5, 12);
                live.Step();
            }

            // 2回目:記録から再計算(JSON経由)
            string logJson = GameJson.Serialize(live.AppliedCommands);
            var commands = GameJson.Deserialize<List<InterventionCommand>>(logJson);
            var replay = RaidFactory.RunWithCommands(
                new RaidSimulation(GameJson.Deserialize<DungeonSnapshot>(GameJson.Serialize(snap)), _data, party, 42,
                    new InterventionCharges { Heal = 2, Torch = 3 }), commands);

            Assert.AreEqual(live.Outcome, replay.Outcome);
            Assert.AreEqual(live.CurrentTick, replay.CurrentTick);
            Assert.AreEqual(live.ComputeHash(), replay.ComputeHash());
            Assert.GreaterOrEqual(live.AppliedCommands.Count, 1);
        }

        [Test]
        public void シードが違えば戦闘の乱数が変わる()
        {
            var snap = Grown(2, 3);
            var a = Run(new RaidSimulation(snap, _data, Party("warrior", "mage", "priest"), 1));
            var b = Run(new RaidSimulation(snap, _data, Party("warrior", "mage", "priest"), 2));
            Assert.AreNotEqual(a.ComputeHash(), b.ComputeHash());
        }

        // ---------- テスト防衛 ----------

        [Test]
        public void 魔王側のテスト防衛で結果が見られ元のダンジョンは変わらない()
        {
            var b = TestData.BuildTypical(_data, 1, 3);
            b.Grow(300);
            ulong before = b.State.ComputeHash();
            var result = b.RunTestDefense(5);
            Assert.AreNotEqual(RaidOutcome.InProgress, result.Outcome);
            Assert.AreEqual(before, b.State.ComputeHash());
        }

        // ---------- バランス確認(診断) ----------

        [Test, Explicit("バランス調整用")]
        public void テスト防衛の結果一覧を出力する()
        {
            var sb = new StringBuilder();
            for (int layer = 1; layer <= 3; layer++)
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var snap = Grown(layer, seed * 1000 + 24);
                var sim = Run(new RaidSimulation(snap, _data, RaidFactory.CreateTestParty(_data), seed));
                sb.AppendLine($"L{layer} s{seed}: {sim.Outcome} tick={sim.CurrentTick} mon={snap.Monsters.Count} " +
                              $"hp=[{string.Join(",", sim.Heroes.Select(h => $"{h.Class.Id}:{h.Hp}/{h.Stats.MaxHp}"))}] " +
                              $"kills={sim.Heroes.Sum(h => h.Kills)} known={sim.Fog.KnownCount}");
            }
            TestContext.WriteLine(sb.ToString());
        }
    }
}
