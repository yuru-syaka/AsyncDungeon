using System;
using System.Collections.Generic;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Raid;

namespace DungeonGame.Core.Skills
{
    /// <summary>能力値の種類(スキルポイントの割り振り先・スキル効果の対象)。</summary>
    public enum StatKind
    {
        MaxHp = 0,
        Attack = 1,
        Defense = 2,
        Speed = 3,
        Magic = 4,
    }

    /// <summary>
    /// スキル効果の種類。新しい効果を足すときは、ここに追加して SkillMath / RaidSimulation に処理を書く。
    /// </summary>
    public enum SkillEffectType
    {
        /// <summary>能力値を固定値だけ上げる(Stat, Value)。</summary>
        StatFlat = 0,
        /// <summary>能力値を%で上げる(Stat, Value=%)。</summary>
        StatPercent = 1,
        /// <summary>会心率を上げる(Value=%)。</summary>
        CritBonus = 2,
        /// <summary>視界を広げる(Value=マス)。</summary>
        VisionBonus = 3,
        /// <summary>攻略中、Interval ティックごとにHPが Value 回復する。</summary>
        Regen = 4,
        /// <summary>与えたダメージの Value% だけHPを回復する。</summary>
        Lifesteal = 5,
        /// <summary>受けたダメージの Value% を相手に返す。</summary>
        Thorns = 6,
        /// <summary>層の開始時に、生きている仲間のHPを最大の Value% 回復する。</summary>
        LayerStartHeal = 7,
        /// <summary>層の開始時に、倒れた仲間を最大HPの Value% で復活させる。</summary>
        Revive = 8,
        /// <summary>介入スキルの使用回数を増やす(Intervention, Value=回数)。</summary>
        ExtraCharge = 9,
    }

    /// <summary>スキル効果1つ。</summary>
    [Serializable]
    public sealed class SkillEffect
    {
        public SkillEffectType Type;
        public StatKind Stat;
        public int Value;
        public int Interval;
        public InterventionType Intervention;
    }

    /// <summary>スキル1つの定義。効果は複数持てる。パーティ全員に効く。</summary>
    [Serializable]
    public sealed class SkillDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public List<SkillEffect> Effects = new List<SkillEffect>();
    }

    /// <summary>スキル一覧と、スキルポイントの設定。JSON(Resources/GameData/skills.json)で持つ。</summary>
    [Serializable]
    public sealed class SkillCatalog
    {
        public List<SkillDefinition> Skills = new List<SkillDefinition>();
        /// <summary>層クリア時に出すスキル候補の数。</summary>
        public int CandidateCount = 3;
        /// <summary>層クリアごとにもらえるスキルポイント(パーティ全体で共有)。</summary>
        public int PointsPerLayer = 6;
        /// <summary>1ポイントあたりの上昇量。</summary>
        public HeroStats PointValues;

        public SkillDefinition Get(string id)
        {
            foreach (var s in Skills)
                if (s.Id == id) return s;
            return null;
        }
    }

    /// <summary>攻略中に効くパーティ全体の補正(スキルから計算する)。</summary>
    public sealed class PartyModifiers
    {
        public int CritBonus;
        public int VisionBonus;
        public int RegenAmount;
        public int RegenInterval;
        public int LifestealPercent;
        public int ThornsPercent;

        public static readonly PartyModifiers None = new PartyModifiers();
    }
}
