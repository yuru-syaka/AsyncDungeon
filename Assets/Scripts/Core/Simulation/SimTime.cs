namespace DungeonGame.Core.Simulation
{
    /// <summary>
    /// 固定ティックの定義。ロジックはすべてティック単位(整数)で進む。
    /// 1ティック = 100ミリ秒(0.1秒)。
    /// </summary>
    public static class SimTime
    {
        public const int TickMilliseconds = 100;
        public const int TicksPerSecond = 1000 / TickMilliseconds;

        public static int SecondsToTicks(int seconds) => seconds * TicksPerSecond;
    }

    /// <summary>
    /// 1ティックずつ進められるシミュレーションの共通インターフェース。
    /// 生態系・ダンジョン攻略などがこれを実装する。
    /// </summary>
    public interface ITickSimulation
    {
        /// <summary>現在のティック数(0始まり)。</summary>
        int CurrentTick { get; }

        /// <summary>シミュレーションが終了したか(勝敗がついた等)。</summary>
        bool IsFinished { get; }

        /// <summary>1ティック進める。</summary>
        void Step();
    }

    /// <summary>
    /// 描画側の経過時間(ミリ秒)を受け取り、何ティック進めるべきかを決める。
    /// 描画フレームレートに関係なくロジックは一定間隔で進む(描画とロジックの分離)。
    /// ここ自体はUnityに依存しないので、サーバーやテストでも使える。
    /// </summary>
    public sealed class FixedTickAccumulator
    {
        private int _accumulatedMs;

        /// <summary>1回のUpdateで処理する最大ティック数(処理落ち時の暴走防止)。</summary>
        public int MaxTicksPerUpdate { get; set; } = 10;

        /// <summary>再生速度(%)。100で等速、200で2倍速。</summary>
        public int SpeedPercent { get; set; } = 100;

        /// <summary>前回ティックからの経過割合(0〜999の千分率)。描画の補間に使う。</summary>
        public int AlphaPermille => _accumulatedMs * 1000 / SimTime.TickMilliseconds;

        /// <summary>経過ミリ秒を加算し、今回進めるべきティック数を返す。</summary>
        public int Advance(int elapsedMs)
        {
            if (elapsedMs < 0) elapsedMs = 0;
            _accumulatedMs += elapsedMs * SpeedPercent / 100;
            int ticks = _accumulatedMs / SimTime.TickMilliseconds;
            _accumulatedMs -= ticks * SimTime.TickMilliseconds;
            if (ticks > MaxTicksPerUpdate)
            {
                ticks = MaxTicksPerUpdate;
                _accumulatedMs = 0; // 追いつけない分は捨てる(結果には影響しない。進行が遅れるだけ)
            }
            return ticks;
        }

        public void Reset() => _accumulatedMs = 0;
    }

    /// <summary>
    /// シミュレーションを描画なしで最後まで(または上限ティックまで)一気に回す。
    /// テストやサーバーでの再計算に使う。
    /// </summary>
    public static class SimulationRunner
    {
        public static int RunToEnd(ITickSimulation sim, int maxTicks)
        {
            int steps = 0;
            while (!sim.IsFinished && steps < maxTicks)
            {
                sim.Step();
                steps++;
            }
            return steps;
        }
    }
}
