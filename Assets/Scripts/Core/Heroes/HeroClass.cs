using System;
using System.Collections.Generic;

namespace DungeonGame.Core.Heroes
{
    /// <summary>勇者の能力値(HP・攻撃・防御・素早さ・魔力)。スキルポイントの割り振り先でもある。</summary>
    [Serializable]
    public struct HeroStats
    {
        public int MaxHp;
        public int Attack;
        public int Defense;
        public int Speed;
        public int Magic;

        public static HeroStats operator +(HeroStats a, HeroStats b) => new HeroStats
        {
            MaxHp = a.MaxHp + b.MaxHp, Attack = a.Attack + b.Attack, Defense = a.Defense + b.Defense,
            Speed = a.Speed + b.Speed, Magic = a.Magic + b.Magic,
        };
    }

    /// <summary>通常攻撃に使う能力値。</summary>
    public enum AttackStat
    {
        /// <summary>攻撃力で殴る(相手の防御をそのまま受ける)。</summary>
        Attack = 0,
        /// <summary>魔力で撃つ(相手の防御を半分しか受けない)。</summary>
        Magic = 1,
    }

    /// <summary>職業固有の自動行動。</summary>
    public enum ClassAbility
    {
        None = 0,
        /// <summary>HPが減った仲間を魔力に応じて回復する。</summary>
        Heal = 1,
    }

    /// <summary>
    /// 職業(戦士・魔法使い・僧侶・盗賊)の定義。JSON(Resources/GameData/heroes.json)で持つ。
    /// </summary>
    [Serializable]
    public sealed class HeroClass
    {
        public string Id;
        public string Name;
        /// <summary>図鑑に載せる解説文。</summary>
        public string Description;
        public HeroStats BaseStats;
        public AttackStat AttackStat;
        /// <summary>攻撃が届く距離(マンハッタン距離)。1=隣接のみ。</summary>
        public int AttackRange = 1;
        /// <summary>周りを見通せる距離(霧が晴れる範囲)。</summary>
        public int Vision = 2;
        /// <summary>会心(ダメージ2倍)の確率(%)。</summary>
        public int CritPercent;
        public ClassAbility Ability;
        /// <summary>職業固有行動の間隔(ティック)。</summary>
        public int AbilityInterval;
        public string Color;
    }

    /// <summary>介入スキル(プレイヤーが使う回復・たいまつ)の設定。</summary>
    [Serializable]
    public sealed class InterventionSettings
    {
        /// <summary>1回のランで使える回数。</summary>
        public int HealCharges = 2;
        public int TorchCharges = 3;
        /// <summary>回復量(最大HPに対する%)。</summary>
        public int HealPercent = 40;
        /// <summary>たいまつで霧が晴れる半径。</summary>
        public int TorchRadius = 4;
    }

    /// <summary>職業一覧と、テスト防衛用のNPCパーティ。</summary>
    [Serializable]
    public sealed class HeroCatalog
    {
        public List<HeroClass> Classes = new List<HeroClass>();
        /// <summary>魔王側の「テスト防衛」で送り込むNPCパーティの職業。</summary>
        public List<string> TestParty = new List<string>();
        public InterventionSettings Interventions = new InterventionSettings();
        /// <summary>パーティの人数。</summary>
        public int PartySize = 3;

        public HeroClass Get(string id)
        {
            foreach (var c in Classes)
                if (c.Id == id) return c;
            return null;
        }
    }
}
