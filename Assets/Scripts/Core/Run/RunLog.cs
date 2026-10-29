using System;
using System.Collections.Generic;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Skills;

namespace DungeonGame.Core.Run
{
    /// <summary>
    /// 勇者側の入力ログ。これとゲームデータ(+NPCダンジョン)があれば、ランを完全に再計算できる。
    /// 記録するもの:シード・編成・各層の介入スキル(ティックと対象)・スキル選択・ポイント配分。
    /// 抽選されたダンジョンIDや結果も入れておく(再計算時の照合用。入力そのものではない)。
    /// </summary>
    [Serializable]
    public sealed class RunLog
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion = CurrentFormatVersion;
        public ulong Seed;
        /// <summary>選んだ職業(隊列順)。</summary>
        public List<string> PartyClasses = new List<string>();
        public List<RunLayerLog> Layers = new List<RunLayerLog>();

        // ---- 照合用の結果 ----
        public int Stars;
        public ulong FinalHash;
    }

    /// <summary>1層ぶんの入力。</summary>
    [Serializable]
    public sealed class RunLayerLog
    {
        public int Layer;
        /// <summary>抽選されたダンジョン(照合用)。</summary>
        public string DungeonId;
        /// <summary>
        /// ダンジョンの中身のハッシュ(照合用)。同じIDでも中身が違えば再計算結果が変わるので、
        /// オンライン化したときに「どの版のダンジョンを攻略したか」を確かめるのに使う。
        /// </summary>
        public ulong DungeonHash;
        /// <summary>介入スキルの使用(ティック・種類・対象座標)。</summary>
        public List<InterventionCommand> Commands = new List<InterventionCommand>();
        public RaidOutcome Outcome;
        public int EndTick;
        /// <summary>提示されたスキル候補(照合用)。</summary>
        public List<string> SkillCandidates = new List<string>();
        /// <summary>選んだスキル(クリアしなかった層では null)。</summary>
        public string SkillChoice;
        /// <summary>スキルポイントの配分(行った順)。</summary>
        public List<PointAllocation> Allocations = new List<PointAllocation>();
    }

    /// <summary>スキルポイントの配分1回ぶん。</summary>
    [Serializable]
    public struct PointAllocation
    {
        /// <summary>パーティ内の番号(0始まり、隊列順)。</summary>
        public int HeroIndex;
        public StatKind Stat;
        public int Points;
    }
}
