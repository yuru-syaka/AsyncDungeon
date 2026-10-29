using System;

namespace DungeonGame.Core.Determinism
{
    /// <summary>
    /// シード付きの決定論的乱数(xorshift128)。
    /// UnityEngine.Random / System.Random は実装がプラットフォームや.NETのバージョンで
    /// 変わる可能性があるため使わず、クライアントとサーバーで必ず同じ数列を出すこの実装を使う。
    /// 状態(4つのuint)を保存・復元できるので、スナップショットにも含められる。
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _x, _y, _z, _w;

        public DeterministicRandom(ulong seed)
        {
            // シードをSplitMix64でかき混ぜて初期状態を作る(全ゼロ状態を避ける)
            ulong s = seed;
            ulong a = SplitMix64(ref s);
            ulong b = SplitMix64(ref s);
            _x = (uint)a;
            _y = (uint)(a >> 32);
            _z = (uint)b;
            _w = (uint)(b >> 32);
            if ((_x | _y | _z | _w) == 0) _w = 0x9E3779B9u;
        }

        private DeterministicRandom(RandomState state)
        {
            _x = state.X; _y = state.Y; _z = state.Z; _w = state.W;
        }

        /// <summary>保存しておいた状態から乱数を復元する。</summary>
        public static DeterministicRandom FromState(RandomState state)
        {
            if ((state.X | state.Y | state.Z | state.W) == 0)
                throw new ArgumentException("乱数の状態がすべて0です");
            return new DeterministicRandom(state);
        }

        /// <summary>現在の内部状態を取り出す(JSON保存用)。</summary>
        public RandomState GetState() => new RandomState { X = _x, Y = _y, Z = _z, W = _w };

        /// <summary>32ビットの乱数を1つ返す。</summary>
        public uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            _w = _w ^ (_w >> 19) ^ (t ^ (t >> 8));
            return _w;
        }

        /// <summary>[0, maxExclusive) の整数を返す。浮動小数点は使わない。</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(((ulong)NextUInt() * (ulong)maxExclusive) >> 32);
        }

        /// <summary>[minInclusive, maxExclusive) の整数を返す。</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        /// <summary>percent% の確率でtrue(0〜100の整数)。</summary>
        public bool Chance(int percent) => Next(100) < percent;

        /// <summary>
        /// この乱数から独立した子乱数を作る(層ごと・用途ごとに乱数列を分けるため)。
        /// 子を作っても親の数列は1回分しか進まないので、用途追加による影響が局所化される。
        /// </summary>
        public DeterministicRandom Fork(uint salt)
        {
            ulong seed = ((ulong)NextUInt() << 32) | (NextUInt() ^ salt);
            return new DeterministicRandom(seed);
        }

        /// <summary>配列をシャッフルする(Fisher-Yates)。</summary>
        public void Shuffle<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        private static ulong SplitMix64(ref ulong s)
        {
            s += 0x9E3779B97F4A7C15UL;
            ulong z = s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>乱数の内部状態。JSONにそのまま書き出せる単純な入れ物。</summary>
    [Serializable]
    public struct RandomState
    {
        public uint X;
        public uint Y;
        public uint Z;
        public uint W;
    }
}
