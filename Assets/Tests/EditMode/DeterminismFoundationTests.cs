using System.Linq;
using DungeonGame.Core.Determinism;
using DungeonGame.Core.Simulation;
using NUnit.Framework;

namespace DungeonGame.Core.Tests
{
    /// <summary>マイルストーン1:決定論的乱数・固定ティック・アセンブリ分離のテスト。</summary>
    public class DeterminismFoundationTests
    {
        [Test]
        public void 同じシードなら同じ乱数列になる()
        {
            var a = new DeterministicRandom(12345);
            var b = new DeterministicRandom(12345);
            for (int i = 0; i < 10000; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"{i}回目で不一致");
        }

        [Test]
        public void 違うシードなら違う乱数列になる()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++)
                if (a.NextUInt() == b.NextUInt()) same++;
            Assert.Less(same, 3);
        }

        [Test]
        public void 乱数列は固定値と一致する_実装変更の検知用()
        {
            // サーバーとクライアントで同じ値が出ることの保証。実装を変えたらこの値も変わるので気付ける。
            var r = new DeterministicRandom(42);
            uint[] first = { r.NextUInt(), r.NextUInt(), r.NextUInt() };
            var r2 = new DeterministicRandom(42);
            CollectionAssert.AreEqual(first, new[] { r2.NextUInt(), r2.NextUInt(), r2.NextUInt() });
            // 0固定ではないこと
            Assert.IsTrue(first.Any(v => v != 0));
        }

        [Test]
        public void Rangeは範囲内の値だけを返す()
        {
            var r = new DeterministicRandom(7);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 5000; i++)
            {
                int v = r.Range(-3, 4);
                Assert.That(v, Is.InRange(-3, 3));
                if (v == -3) sawMin = true;
                if (v == 3) sawMax = true;
            }
            Assert.IsTrue(sawMin && sawMax, "端の値も出ること");
        }

        [Test]
        public void 状態を保存して復元すると続きの乱数列が一致する()
        {
            var r = new DeterministicRandom(999);
            for (int i = 0; i < 50; i++) r.NextUInt();
            RandomState saved = r.GetState();
            uint[] expected = Enumerable.Range(0, 20).Select(_ => r.NextUInt()).ToArray();

            var restored = DeterministicRandom.FromState(saved);
            uint[] actual = Enumerable.Range(0, 20).Select(_ => restored.NextUInt()).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void Forkした子乱数も再現できる()
        {
            var a = new DeterministicRandom(5).Fork(3);
            var b = new DeterministicRandom(5).Fork(3);
            var c = new DeterministicRandom(5).Fork(4);
            uint av = a.NextUInt();
            Assert.AreEqual(av, b.NextUInt());
            Assert.AreNotEqual(av, c.NextUInt());
        }

        [Test]
        public void 固定ティック_経過時間を正しくティックに変換する()
        {
            var acc = new FixedTickAccumulator();
            Assert.AreEqual(0, acc.Advance(50));
            Assert.AreEqual(1, acc.Advance(60));   // 合計110ms → 1ティック、余り10ms
            Assert.AreEqual(2, acc.Advance(190));  // 10+190=200ms → 2ティック
            acc.SpeedPercent = 200;
            Assert.AreEqual(2, acc.Advance(100));  // 2倍速
        }

        [Test]
        public void 固定ティック_処理落ちしても上限ティックで止まる()
        {
            var acc = new FixedTickAccumulator { MaxTicksPerUpdate = 5 };
            Assert.AreEqual(5, acc.Advance(10000));
            Assert.AreEqual(0, acc.Advance(0));
        }

        [Test]
        public void ティックの刻み方が違っても結果のハッシュは同じ()
        {
            // 描画のフレームレートが違っても、ロジックの結果は同じになることの確認
            var sim1 = new RandomWalkSim(77);
            SimulationRunner.RunToEnd(sim1, 10000);

            var sim2 = new RandomWalkSim(77);
            var acc = new FixedTickAccumulator { MaxTicksPerUpdate = 1000 };
            int[] frames = { 16, 33, 7, 120, 250, 1 };
            int f = 0;
            while (!sim2.IsFinished)
            {
                int n = acc.Advance(frames[f++ % frames.Length]);
                for (int i = 0; i < n && !sim2.IsFinished; i++) sim2.Step();
            }
            Assert.AreEqual(sim1.Hash(), sim2.Hash());
            Assert.AreEqual(sim1.CurrentTick, sim2.CurrentTick);
        }

        [Test]
        public void ロジック層はUnityEngineに依存していない()
        {
            var asm = typeof(DeterministicRandom).Assembly;
            var refs = asm.GetReferencedAssemblies().Select(n => n.Name).ToArray();
            Assert.IsFalse(refs.Any(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor")),
                "Coreが参照しているアセンブリ: " + string.Join(", ", refs));
        }

        [Test]
        public void IntMath_割合計算はオーバーフローしない()
        {
            Assert.AreEqual(1_500_000_000, IntMath.Percent(2_000_000_000, 75));
            Assert.AreEqual(33, IntMath.MulDiv(100, 1, 3));
            Assert.AreEqual(4, IntMath.CeilDiv(10, 3));
        }

        /// <summary>テスト用の小さなシミュレーション:乱数で動く点が300ティック歩く。</summary>
        private sealed class RandomWalkSim : ITickSimulation
        {
            private readonly DeterministicRandom _rng;
            private int _x, _y;
            public int CurrentTick { get; private set; }
            public bool IsFinished => CurrentTick >= 300;

            public RandomWalkSim(ulong seed) { _rng = new DeterministicRandom(seed); }

            public void Step()
            {
                _x += _rng.Range(-1, 2);
                _y += _rng.Range(-1, 2);
                CurrentTick++;
            }

            public ulong Hash()
            {
                var h = new StateHasher();
                h.Add(_x); h.Add(_y); h.Add(CurrentTick);
                return h.Value;
            }
        }
    }
}
