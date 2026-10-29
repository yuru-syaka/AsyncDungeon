namespace DungeonGame.Core.Determinism
{
    /// <summary>
    /// シミュレーション状態のチェックサムを作るためのFNV-1a(64bit)ハッシュ。
    /// 決定論テストや、将来サーバーでの勝敗検証で「同じ結果になったか」を比較するのに使う。
    /// string.GetHashCode() は実行ごとに変わり得るので使わない。
    /// </summary>
    public struct StateHasher
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _hash;
        private bool _started;

        public ulong Value => _started ? _hash : Offset;

        public void Add(byte b)
        {
            if (!_started) { _hash = Offset; _started = true; }
            _hash ^= b;
            _hash *= Prime;
        }

        public void Add(int v)
        {
            unchecked
            {
                Add((byte)v); Add((byte)(v >> 8)); Add((byte)(v >> 16)); Add((byte)(v >> 24));
            }
        }

        public void Add(uint v) => Add(unchecked((int)v));

        public void Add(long v)
        {
            Add(unchecked((int)v));
            Add(unchecked((int)(v >> 32)));
        }

        public void Add(ulong v) => Add(unchecked((long)v));

        public void Add(bool v) => Add(v ? (byte)1 : (byte)0);

        public void Add(string s)
        {
            if (s == null) { Add(-1); return; }
            Add(s.Length);
            foreach (char c in s) { Add((byte)c); Add((byte)(c >> 8)); }
        }
    }
}
