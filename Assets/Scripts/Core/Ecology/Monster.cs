using DungeonGame.Core.Dungeon;

namespace DungeonGame.Core.Ecology
{
    /// <summary>
    /// ダンジョンにいるモンスター1体の状態。
    /// Nutrient / Mana は「体の養分・魔分」。死ぬと土に還る。
    /// </summary>
    public sealed class Monster
    {
        /// <summary>ダンジョン内で一意のID(生まれた順に増える)。処理順もこの順。</summary>
        public int Id;
        public MonsterSpecies Species;
        public GridPos Pos;
        public int Nutrient;
        public int Mana;
        /// <summary>生まれてからのティック数。</summary>
        public int Age;
        /// <summary>次の行動までの待ちティック。</summary>
        public int Cooldown;
        public int Hp;
        /// <summary>消化中の残りティック(0なら食べられる)。</summary>
        public int Digest;
        public bool Dead;

        // ---- 勇者の攻略中だけ使う値(スナップショットには保存しない。攻略は入力ログから再計算する) ----
        /// <summary>体の大きさで強化された最大HP・攻撃力。</summary>
        public int CombatMaxHp;
        public int CombatAttack;
        /// <summary>次の攻撃までの待ちティック。</summary>
        public int AttackCd;

        public int Biomass => Nutrient + Mana;
    }
}
