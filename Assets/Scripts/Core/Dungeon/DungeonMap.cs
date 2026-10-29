using System;
using DungeonGame.Core.Determinism;

namespace DungeonGame.Core.Dungeon
{
    /// <summary>掘削の結果。</summary>
    public enum DigError
    {
        None = 0,
        /// <summary>マップの外。</summary>
        OutOfBounds,
        /// <summary>土ではない(岩盤・既に掘った場所など)。</summary>
        NotSoil,
        /// <summary>掘った通路(または入口)に隣接していない。</summary>
        NotConnected,
        /// <summary>掘削予算を使い切っている。</summary>
        BudgetExceeded,
    }

    /// <summary>掘削1回の結果。掘った土に含まれていた養分・魔分を返す(モンスター誕生に使う)。</summary>
    public struct DigResult
    {
        public DigError Error;
        public GridPos Pos;
        public int Nutrient;
        public int Mana;
        public bool Success => Error == DigError.None;
    }

    /// <summary>
    /// 1層ぶんのダンジョンのグリッド。マス種別と、各マスの養分・魔分を持つ。
    /// 掘削ルール:入口か掘った通路に4方向で隣接する土だけを掘れる。1マス掘るごとに予算を1消費する。
    /// </summary>
    public sealed class DungeonMap
    {
        private readonly TileType[] _tiles;
        private readonly int[] _nutrient;
        private readonly int[] _mana;
        private readonly int[] _bones; // 通路に積もった骨の数(モンスターが死んだ回数)。墓場からの誕生に使う

        public int Layer { get; }
        public int Width { get; }
        public int Height { get; }
        public GoalType GoalType { get; set; }
        /// <summary>掘削予算(層ルールからコピーした値。検証時は層ルール側の値を正とする)。</summary>
        public int DigBudget { get; set; }
        /// <summary>1マスの養分・魔分の上限。</summary>
        public int TileResourceCap { get; set; } = 20;

        public GridPos Entrance { get; private set; }
        public GridPos Goal { get; private set; }

        public DungeonMap(int layer, int width, int height)
        {
            if (width < 3 || height < 3) throw new ArgumentException("マップが小さすぎます");
            Layer = layer;
            Width = width;
            Height = height;
            _tiles = new TileType[width * height];
            _nutrient = new int[width * height];
            _mana = new int[width * height];
            _bones = new int[width * height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(GridPos p) => InBounds(p.X, p.Y);
        public int Index(int x, int y) => y * Width + x;

        public TileType GetTile(int x, int y) => _tiles[Index(x, y)];
        public TileType GetTile(GridPos p) => _tiles[Index(p.X, p.Y)];

        /// <summary>マス種別を直接書き換える(マップ生成・読み込み用)。入口・ゴールの位置も追跡する。</summary>
        public void SetTile(int x, int y, TileType t)
        {
            _tiles[Index(x, y)] = t;
            if (t == TileType.Entrance) Entrance = new GridPos(x, y);
            if (t == TileType.Goal) Goal = new GridPos(x, y);
        }

        public int GetNutrient(int x, int y) => _nutrient[Index(x, y)];
        public int GetMana(int x, int y) => _mana[Index(x, y)];
        public void SetNutrient(int x, int y, int v) => _nutrient[Index(x, y)] = v;
        public void SetMana(int x, int y, int v) => _mana[Index(x, y)] = v;

        /// <summary>通路に積もった骨の数。モンスターが死ぬと増え、墓場の誕生で減る。</summary>
        public int GetBones(int x, int y) => _bones[Index(x, y)];
        public void SetBones(int x, int y, int v) => _bones[Index(x, y)] = v;
        public void AddBones(int x, int y, int v) => _bones[Index(x, y)] += v;

        /// <summary>養分・魔分をマスに足す(上限で切り捨て)。足せなかった量を返す。</summary>
        public int AddNutrient(int x, int y, int amount) => AddCapped(_nutrient, x, y, amount);
        public int AddMana(int x, int y, int amount) => AddCapped(_mana, x, y, amount);

        private int AddCapped(int[] arr, int x, int y, int amount)
        {
            int i = Index(x, y);
            int room = TileResourceCap - arr[i];
            if (room <= 0) return amount;
            int add = IntMath.Min(room, amount);
            arr[i] += add;
            return amount - add;
        }

        public bool IsWalkable(int x, int y) => InBounds(x, y) && TileChars.IsWalkable(GetTile(x, y));
        public bool IsWalkable(GridPos p) => IsWalkable(p.X, p.Y);

        /// <summary>掘ったマス数(Floorの数)。予算の消費量そのもの。</summary>
        public int DugCount
        {
            get
            {
                int n = 0;
                foreach (var t in _tiles) if (t == TileType.Floor) n++;
                return n;
            }
        }

        public int RemainingBudget => DigBudget - DugCount;

        /// <summary>このマスを掘れるかどうかを調べる(実際には掘らない)。</summary>
        public DigError CanDig(int x, int y)
        {
            if (!InBounds(x, y)) return DigError.OutOfBounds;
            if (GetTile(x, y) != TileType.Soil) return DigError.NotSoil;
            if (RemainingBudget <= 0) return DigError.BudgetExceeded;
            foreach (var d in GridPos.Dir4)
            {
                int nx = x + d.X, ny = y + d.Y;
                if (!InBounds(nx, ny)) continue;
                var t = GetTile(nx, ny);
                if (t == TileType.Floor || t == TileType.Entrance) return DigError.None;
            }
            return DigError.NotConnected;
        }

        /// <summary>土を掘って通路にする。含まれていた養分・魔分はマスから取り出して結果に入れる。</summary>
        public DigResult Dig(int x, int y)
        {
            var result = new DigResult { Pos = new GridPos(x, y), Error = CanDig(x, y) };
            if (!result.Success) return result;

            int i = Index(x, y);
            result.Nutrient = _nutrient[i];
            result.Mana = _mana[i];
            _nutrient[i] = 0;
            _mana[i] = 0;
            _tiles[i] = TileType.Floor;
            return result;
        }

        /// <summary>マップ全体の養分・魔分の合計(生態系の保存則テストに使う)。</summary>
        public int TotalNutrient { get { int s = 0; foreach (var v in _nutrient) s += v; return s; } }
        public int TotalMana { get { int s = 0; foreach (var v in _mana) s += v; return s; } }

        /// <summary>状態のハッシュ。</summary>
        public void AddToHash(ref StateHasher h)
        {
            h.Add(Layer); h.Add(Width); h.Add(Height); h.Add((int)GoalType);
            for (int i = 0; i < _tiles.Length; i++)
            {
                h.Add((byte)_tiles[i]);
                h.Add(_nutrient[i]);
                h.Add(_mana[i]);
                h.Add(_bones[i]);
            }
        }

        /// <summary>完全なコピーを作る(テスト防衛で元のダンジョンを壊さないため)。</summary>
        public DungeonMap Clone()
        {
            var m = new DungeonMap(Layer, Width, Height)
            {
                GoalType = GoalType, DigBudget = DigBudget, TileResourceCap = TileResourceCap,
                Entrance = Entrance, Goal = Goal,
            };
            Array.Copy(_tiles, m._tiles, _tiles.Length);
            Array.Copy(_nutrient, m._nutrient, _nutrient.Length);
            Array.Copy(_mana, m._mana, _mana.Length);
            Array.Copy(_bones, m._bones, _bones.Length);
            return m;
        }
    }
}
