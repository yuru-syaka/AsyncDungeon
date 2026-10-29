using DungeonGame.Core.Determinism;
using DungeonGame.Core.Ecology;

namespace DungeonGame.Core.Raid
{
    /// <summary>
    /// 戦闘の計算式(すべて整数)。式を変えるときはここだけ直せばよい。
    /// </summary>
    public static class CombatMath
    {
        /// <summary>
        /// ダメージ = max(1, 攻撃 - 防御) × 乱数(90〜110%)。会心なら2倍。最低1。
        /// </summary>
        public static int Damage(DeterministicRandom rng, int attack, int defense, int critPercent, out bool crit)
        {
            int baseDmg = IntMath.Max(1, attack - defense);
            int dmg = IntMath.Max(1, baseDmg * rng.Range(90, 111) / 100);
            crit = critPercent > 0 && rng.Chance(critPercent);
            if (crit) dmg *= 2;
            return dmg;
        }

        /// <summary>素早さ → 移動間隔(ティック)。素早さ5で10ティック(1秒に1マス)。</summary>
        public static int MoveInterval(int speed) => IntMath.Max(2, 150 / (10 + IntMath.Max(0, speed)));

        /// <summary>素早さ → 攻撃間隔(ティック)。素早さ5で20ティック。</summary>
        public static int AttackInterval(int speed) => IntMath.Max(4, 300 / (10 + IntMath.Max(0, speed)));

        /// <summary>
        /// モンスターは体の養分・魔分が多いほど強い(最大+50%)。生態系で育てた成果が戦闘に効く。
        /// </summary>
        public static int GrowthPercent(Monster m)
        {
            int max = m.Species.MaxBiomass;
            if (max <= 0) return 100;
            return 100 + 50 * IntMath.Min(m.Biomass, max) / max;
        }
    }
}
