using System;
using System.Collections.Generic;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Heroes;

namespace DungeonGame.Core.Raid
{
    /// <summary>プレイヤーが使う介入スキルの種類。</summary>
    public enum InterventionType
    {
        /// <summary>パーティ全体を回復する。</summary>
        Heal = 0,
        /// <summary>指定した場所の霧を晴らす。</summary>
        Torch = 1,
    }

    /// <summary>
    /// 介入スキルの使用命令。「何ティック目に・何を・どこに」を持つ。入力ログにそのまま記録する。
    /// </summary>
    [Serializable]
    public struct InterventionCommand
    {
        public int Tick;
        public InterventionType Type;
        /// <summary>対象の座標(たいまつのみ使用)。</summary>
        public int X;
        public int Y;
    }

    /// <summary>介入スキルの残り回数。ラン全体(全部の層)で共有する。</summary>
    [Serializable]
    public sealed class InterventionCharges
    {
        public int Heal;
        public int Torch;

        public int Get(InterventionType t) => t == InterventionType.Heal ? Heal : Torch;

        public void Consume(InterventionType t)
        {
            if (t == InterventionType.Heal) Heal--; else Torch--;
        }

        public static InterventionCharges FromSettings(InterventionSettings s)
            => new InterventionCharges { Heal = s.HealCharges, Torch = s.TorchCharges };
    }

    /// <summary>攻略に送り込む勇者1人の設定(職業・能力値・現在HP)。</summary>
    [Serializable]
    public sealed class HeroSetup
    {
        public string ClassId;
        public HeroStats Stats;
        /// <summary>現在HP(層をまたいで引き継ぐ)。0以下なら戦闘不能で参加しない。</summary>
        public int Hp;
        /// <summary>獲得済みのスキルID(M5で使用)。</summary>
        public List<string> Skills = new List<string>();
    }

    /// <summary>攻略中の勇者1人。</summary>
    public sealed class Hero
    {
        /// <summary>1始まりの番号。隊列の順番でもある(1が先頭)。</summary>
        public int Id;
        public HeroClass Class;
        public HeroStats Stats;
        public int Hp;
        public GridPos Pos;
        /// <summary>ダンジョンに入ったか(入口が空くまで外で待つ)。</summary>
        public bool Entered;
        public bool Dead;
        public int MoveCd;
        public int AttackCd;
        public int AbilityCd;
        /// <summary>この攻略で倒したモンスター数。</summary>
        public int Kills;
        public HeroSetup Setup;

        public bool Active => Entered && !Dead;
    }

    /// <summary>攻略の結果。</summary>
    public enum RaidOutcome
    {
        InProgress = 0,
        /// <summary>層を突破した(階段に着いた / 魔王を倒した)。</summary>
        Cleared = 1,
        /// <summary>全滅した。</summary>
        Wiped = 2,
        /// <summary>制限時間切れ。</summary>
        TimeUp = 3,
    }

    /// <summary>画面表示用のできごと(攻撃・回復・撃破など)。ロジックの結果には影響しない。</summary>
    public enum RaidEventKind
    {
        HeroAttack, MonsterAttack, HeroHeal, MonsterKilled, HeroDown, HeroEntered,
        InterventionHeal, InterventionTorch, Cleared, Wiped, TimeUp,
    }

    public struct RaidEvent
    {
        public int Tick;
        public RaidEventKind Kind;
        public int HeroId;
        public int MonsterId;
        public int Amount;
        public bool Critical;
        public GridPos Pos;

        public override string ToString() => $"[{Tick}] {Kind} hero={HeroId} mon={MonsterId} amt={Amount}{(Critical ? "!" : "")} {Pos}";
    }
}
