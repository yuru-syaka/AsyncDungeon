using DungeonGame.Core.Simulation;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>
    /// Unity側でシミュレーションを固定ティックで動かすドライバー。
    /// Update の経過時間をミリ秒の整数に変換して FixedTickAccumulator に渡すだけで、
    /// ロジックの計算そのものには Unity の float は一切入らない。
    /// </summary>
    public class SimulationDriver : MonoBehaviour
    {
        private readonly FixedTickAccumulator _accumulator = new FixedTickAccumulator();

        /// <summary>動かす対象のシミュレーション。null の間は何もしない。</summary>
        public ITickSimulation Simulation { get; set; }

        /// <summary>一時停止中か。</summary>
        public bool Paused { get; set; }

        /// <summary>再生速度(%)。</summary>
        public int SpeedPercent
        {
            get => _accumulator.SpeedPercent;
            set => _accumulator.SpeedPercent = value;
        }

        /// <summary>描画補間用の 0〜1 の値(見た目専用)。</summary>
        public float InterpolationAlpha => _accumulator.AlphaPermille / 1000f;

        /// <summary>ティックが進んだ直前に呼ばれる(入力の適用などに使う)。</summary>
        public event System.Action BeforeTick;

        /// <summary>ティックが進んだ直後に呼ばれる(描画の更新などに使う)。</summary>
        public event System.Action AfterTick;

        private void Update()
        {
            if (Simulation == null || Paused || Simulation.IsFinished) return;

            int elapsedMs = Mathf.RoundToInt(Time.deltaTime * 1000f);
            int ticks = _accumulator.Advance(elapsedMs);
            for (int i = 0; i < ticks && !Simulation.IsFinished; i++)
            {
                BeforeTick?.Invoke();
                Simulation.Step();
                AfterTick?.Invoke();
            }
        }
    }
}
