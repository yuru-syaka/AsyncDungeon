namespace DungeonGame.Core.Determinism
{
    /// <summary>
    /// 整数だけで計算するための補助関数群。
    /// ゲームロジックでは float/double を使わず、割合は百分率(%)や千分率(‰)の整数で表す。
    /// </summary>
    public static class IntMath
    {
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        public static int Min(int a, int b) => a < b ? a : b;

        public static int Max(int a, int b) => a > b ? a : b;

        public static int Abs(int v) => v < 0 ? -v : v;

        /// <summary>value * percent / 100(切り捨て、0方向)。</summary>
        public static int Percent(int value, int percent) => (int)((long)value * percent / 100);

        /// <summary>value * num / den を64ビットで安全に計算する。</summary>
        public static int MulDiv(int value, int num, int den) => (int)((long)value * num / den);

        /// <summary>切り上げ除算(正の数のみ想定)。</summary>
        public static int CeilDiv(int a, int b) => (a + b - 1) / b;

        /// <summary>マンハッタン距離。</summary>
        public static int Manhattan(int x1, int y1, int x2, int y2) => Abs(x1 - x2) + Abs(y1 - y2);

        /// <summary>チェビシェフ距離(8方向で隣接判定に使う)。</summary>
        public static int Chebyshev(int x1, int y1, int x2, int y2) => Max(Abs(x1 - x2), Abs(y1 - y2));
    }
}
