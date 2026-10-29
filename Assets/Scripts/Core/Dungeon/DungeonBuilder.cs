using DungeonGame.Core.Data;
using DungeonGame.Core.Determinism;
using DungeonGame.Core.Ecology;
using DungeonGame.Core.Serialization;

namespace DungeonGame.Core.Dungeon
{
    /// <summary>掘削1回の結果(誕生したモンスターを含む)。</summary>
    public struct DigOutcome
    {
        public DigResult Dig;
        /// <summary>生まれたモンスター(生まれなければ null)。</summary>
        public Monster Spawned;
        public bool Success => Dig.Success;
    }

    /// <summary>
    /// 魔王側の編集セッション。「掘る」「育てる」「保存する」をまとめた窓口。
    /// UIはこのクラスだけを触ればよい。
    /// </summary>
    public sealed class DungeonBuilder
    {
        public GameData Data { get; }
        public LayerRule Rule { get; }
        public DungeonState State { get; }
        private readonly EcologySimulation _ecology;

        private DungeonBuilder(GameData data, DungeonState state)
        {
            Data = data;
            State = state;
            Rule = data.Rules.Get(state.Map.Layer);
            _ecology = new EcologySimulation(state, Rule, data.Monsters);
        }

        /// <summary>新しいダンジョン(全部が土)から始める。</summary>
        public static DungeonBuilder CreateNew(GameData data, int layer, ulong seed)
        {
            var rule = data.Rules.Get(layer);
            var root = new DeterministicRandom(seed);
            var map = DungeonFactory.CreateEmpty(rule, seed);
            var state = new DungeonState(map, root.Fork(100));
            return new DungeonBuilder(data, state);
        }

        /// <summary>保存済みのスナップショットから続きを編集する。</summary>
        public static DungeonBuilder FromSnapshot(DungeonSnapshot snapshot, GameData data)
            => new DungeonBuilder(data, DungeonSnapshotConverter.ToState(snapshot, data));

        public DungeonMap Map => State.Map;
        public int RemainingEcologyTicks => Rule.MaxEcologyTicks - State.EcologyTick;

        /// <summary>掘る。条件を満たせばモンスターが生まれる。</summary>
        public DigOutcome Dig(int x, int y)
        {
            var dig = State.Map.Dig(x, y);
            var outcome = new DigOutcome { Dig = dig };
            if (dig.Success)
                outcome.Spawned = MonsterSpawner.SpawnFromDig(State, Data.Monsters, dig);
            return outcome;
        }

        /// <summary>1ティックだけ生態系を進める(画面で動きを見せながら育てるとき用)。上限に達していたら false。</summary>
        public bool GrowStep()
        {
            if (RemainingEcologyTicks <= 0) return false;
            _ecology.Step();
            return true;
        }

        /// <summary>生態系を指定ティック数だけ進める(上限まで)。実際に進めたティック数を返す。</summary>
        public int Grow(int ticks)
        {
            int n = 0;
            while (n < ticks && GrowStep()) n++;
            return n;
        }

        public ValidationResult Validate() => DungeonValidator.ValidateState(State, Data);

        /// <summary>
        /// テスト防衛:今のダンジョンにNPC勇者パーティを送り込んで結果を見る。
        /// 元のダンジョンは変わらない(コピーで攻略する)。
        /// </summary>
        public Raid.RaidSimulation RunTestDefense(ulong seed)
            => Raid.RaidFactory.RunTestDefense(ToSnapshot(), Data, seed);

        /// <summary>画面で攻略の様子を見せるため、まだ動かしていないテスト防衛を作る。</summary>
        public Raid.RaidSimulation CreateTestDefense(ulong seed)
            => new Raid.RaidSimulation(ToSnapshot(), Data, Raid.RaidFactory.CreateTestParty(Data), seed);

        public DungeonSnapshot ToSnapshot(string id = null, string name = null, string author = null)
            => DungeonSnapshotConverter.FromState(State, id, name, author);
    }
}
