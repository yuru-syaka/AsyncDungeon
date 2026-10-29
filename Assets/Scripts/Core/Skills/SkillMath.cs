using System.Collections.Generic;
using DungeonGame.Core.Heroes;

namespace DungeonGame.Core.Skills
{
    /// <summary>
    /// スキルとスキルポイントから、勇者の最終的な能力値と攻略中の補正を計算する。
    /// 計算順:職業の初期値 + ポイント × 1ポイントの上昇量 + 固定値スキル → %スキル(切り捨て)。
    /// </summary>
    public static class SkillMath
    {
        public static int Get(HeroStats s, StatKind k)
        {
            switch (k)
            {
                case StatKind.MaxHp: return s.MaxHp;
                case StatKind.Attack: return s.Attack;
                case StatKind.Defense: return s.Defense;
                case StatKind.Speed: return s.Speed;
                default: return s.Magic;
            }
        }

        public static HeroStats With(HeroStats s, StatKind k, int value)
        {
            switch (k)
            {
                case StatKind.MaxHp: s.MaxHp = value; break;
                case StatKind.Attack: s.Attack = value; break;
                case StatKind.Defense: s.Defense = value; break;
                case StatKind.Speed: s.Speed = value; break;
                default: s.Magic = value; break;
            }
            return s;
        }

        public static HeroStats EffectiveStats(HeroStats baseStats, HeroStats points, SkillCatalog catalog,
            IEnumerable<SkillDefinition> skills)
        {
            var pv = catalog.PointValues;
            var s = new HeroStats
            {
                MaxHp = baseStats.MaxHp + points.MaxHp * pv.MaxHp,
                Attack = baseStats.Attack + points.Attack * pv.Attack,
                Defense = baseStats.Defense + points.Defense * pv.Defense,
                Speed = baseStats.Speed + points.Speed * pv.Speed,
                Magic = baseStats.Magic + points.Magic * pv.Magic,
            };

            var percent = new int[5];
            foreach (var skill in skills)
            foreach (var e in skill.Effects)
            {
                if (e.Type == SkillEffectType.StatFlat) s = With(s, e.Stat, Get(s, e.Stat) + e.Value);
                else if (e.Type == SkillEffectType.StatPercent) percent[(int)e.Stat] += e.Value;
            }
            for (int k = 0; k < 5; k++)
            {
                if (percent[k] == 0) continue;
                var kind = (StatKind)k;
                s = With(s, kind, Get(s, kind) * (100 + percent[k]) / 100);
            }
            return s;
        }

        public static PartyModifiers Modifiers(IEnumerable<SkillDefinition> skills)
        {
            var m = new PartyModifiers();
            foreach (var skill in skills)
            foreach (var e in skill.Effects)
            {
                switch (e.Type)
                {
                    case SkillEffectType.CritBonus: m.CritBonus += e.Value; break;
                    case SkillEffectType.VisionBonus: m.VisionBonus += e.Value; break;
                    case SkillEffectType.Regen:
                        m.RegenAmount += e.Value;
                        // 複数あるときは短い間隔を採用
                        if (m.RegenInterval == 0 || e.Interval < m.RegenInterval) m.RegenInterval = e.Interval;
                        break;
                    case SkillEffectType.Lifesteal: m.LifestealPercent += e.Value; break;
                    case SkillEffectType.Thorns: m.ThornsPercent += e.Value; break;
                }
            }
            return m;
        }

        /// <summary>指定した種類の効果の値の合計。</summary>
        public static int Sum(IEnumerable<SkillDefinition> skills, SkillEffectType type)
        {
            int total = 0;
            foreach (var skill in skills)
            foreach (var e in skill.Effects)
                if (e.Type == type) total += e.Value;
            return total;
        }
    }
}
