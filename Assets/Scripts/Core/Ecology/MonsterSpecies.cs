using System;
using System.Collections.Generic;

namespace DungeonGame.Core.Ecology
{
    /// <summary>モンスターの食性。</summary>
    public enum Diet
    {
        /// <summary>隣の土から養分を吸う(下位)。</summary>
        Nutrient = 0,
        /// <summary>隣の土から魔分を吸う(下位)。</summary>
        Mana = 1,
        /// <summary>下の階級のモンスターを捕食する(中位・上位)。</summary>
        Predator = 2,
    }

    /// <summary>特殊な誕生のしかた(土を掘る以外の生まれ方)。</summary>
    public enum SpecialSpawn
    {
        None = 0,
        /// <summary>巣:3×3以上の広い部屋の中に、ときどき巣を作って生まれる。</summary>
        RoomNest = 1,
        /// <summary>墓場:モンスターが何度も死んで骨が積もったマスから起き上がる。</summary>
        Graveyard = 2,
    }

    /// <summary>
    /// モンスターの種族データ。JSON(Resources/GameData/monsters.json)で定義する。
    /// 「体の養分・魔分(バイオマス)」を持ち、それが尽きると死ぬ。死ぬと体の分が土に還る。
    /// 時間の単位はすべてティック(1ティック=0.1秒)。
    /// </summary>
    [Serializable]
    public sealed class MonsterSpecies
    {
        public string Id;
        public string Name;
        /// <summary>図鑑に載せる解説文。</summary>
        public string Description;
        /// <summary>階級(1=下位, 2=中位, 3=上位, 4=最上位)。食物連鎖の上下を表す。</summary>
        public int Tier;
        /// <summary>ボス(魔王)。掘削や自然発生では生まれず、ダンジョンに置くこともできない。</summary>
        public bool IsBoss;
        public Diet Diet;

        // ---- 誕生 ----
        /// <summary>
        /// 誕生に必要な養分・魔分。体の分としてこの量を消費する。
        /// DigSpawn なら「掘った土にこれ以上あると生まれる」、特殊な誕生なら「周りの土からこの量を取って生まれる」。
        /// </summary>
        public int SpawnNutrient;
        public int SpawnMana;
        /// <summary>土を掘ったときに生まれるか(誕生パターンA)。false なら特殊な方法でしか生まれない。</summary>
        public bool DigSpawn = true;
        /// <summary>特殊な誕生のしかた(誕生パターンB)。</summary>
        public SpecialSpawn SpecialSpawn;
        /// <summary>特殊な誕生を試す確率(毎ティック、%)。</summary>
        public int SpecialPercent;
        /// <summary>墓場から生まれるのに必要な骨の数。</summary>
        public int SpecialBones;

        // ---- 進化 ----
        /// <summary>分裂できるほど体が大きく(ReproduceThreshold 以上)、年齢が EvolveAge 以上になると、分裂する代わりにこの種族に進化する。</summary>
        public string EvolveTo;
        public int EvolveAge;

        // ---- 生態 ----
        /// <summary>何ティックごとに行動(移動・捕食・吸収)するか。</summary>
        public int MoveInterval;
        /// <summary>寿命(ティック)。</summary>
        public int Lifespan;
        /// <summary>何ティックごとに体の養分・魔分を1つ土に排出するか(代謝)。</summary>
        public int UpkeepInterval;
        /// <summary>体の養分+魔分がこの値以上になると分裂して子を作る。</summary>
        public int ReproduceThreshold;
        /// <summary>体に溜められる養分+魔分の上限。</summary>
        public int MaxBiomass;
        /// <summary>捕食できる階級。</summary>
        public List<int> PreyTiers = new List<int>();
        /// <summary>捕食した後、次に食べられるまでの消化時間(ティック)。</summary>
        public int DigestTicks;

        // ---- 戦闘(勇者との戦いで使う) ----
        public int MaxHp;
        public int Attack;
        public int Defense;
        /// <summary>攻撃の間隔(ティック)。</summary>
        public int AttackInterval;

        /// <summary>表示色(仮素材用)。"#RRGGBB"。</summary>
        public string Color;

        public bool CanEat(MonsterSpecies prey) => Diet == Diet.Predator && PreyTiers.Contains(prey.Tier);
    }

    /// <summary>全種族の一覧。</summary>
    [Serializable]
    public sealed class MonsterCatalog
    {
        public List<MonsterSpecies> Species = new List<MonsterSpecies>();

        public MonsterSpecies Get(string id)
        {
            foreach (var s in Species)
                if (s.Id == id) return s;
            return null;
        }
    }
}
