using System.Collections.Generic;
using DungeonGame.Core.Data;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Skills;

namespace DungeonGame.Core.Run
{
    /// <summary>
    /// 決まったルールで勇者側を操作する自動プレイヤー。テスト・バランス確認・デモ用。
    ///  ・攻略中:誰かのHPが一定割合を切ったら回復を使う(たいまつは使わない)
    ///  ・報酬:スキル候補の先頭を選び、ポイントは職業に合わせて HP と攻撃(魔法使いは魔力)に交互に振る
    /// </summary>
    public static class AutoPlayer
    {
        public const int DefaultHealThresholdPercent = 35;

        public static void PlayRaid(RaidSimulation raid, int healThresholdPercent = DefaultHealThresholdPercent)
        {
            while (!raid.IsFinished)
            {
                if (raid.Charges.Heal > 0)
                {
                    foreach (var h in raid.Heroes)
                    {
                        if (h.Dead || !h.Entered) continue;
                        if (h.Hp * 100 < h.Stats.MaxHp * healThresholdPercent)
                        {
                            raid.Submit(InterventionType.Heal);
                            break;
                        }
                    }
                }
                raid.Step();
            }
        }

        public static void PlayReward(RunState run)
        {
            if (run.SkillCandidates.Count > 0) run.ChooseSkill(run.SkillCandidates[0]);
            int turn = 0;
            while (run.UnspentPoints > 0)
            {
                int i = turn % run.Party.Count;
                var cls = run.Data.Heroes.Get(run.Party[i].ClassId);
                StatKind stat;
                if ((turn / run.Party.Count) % 2 == 0) stat = StatKind.MaxHp;
                else stat = cls.AttackStat == AttackStat.Magic ? StatKind.Magic : StatKind.Attack;
                run.Allocate(i, stat, 1);
                turn++;
            }
            run.ConfirmReward();
        }

        /// <summary>ランを最後まで自動で遊ぶ。</summary>
        public static RunState PlayRun(GameData data, ulong seed, IList<string> party,
            int healThresholdPercent = DefaultHealThresholdPercent)
        {
            var run = RunState.Start(data, seed, party);
            while (!run.IsOver)
            {
                if (run.Phase == RunPhase.Raid)
                {
                    PlayRaid(run.CurrentRaid, healThresholdPercent);
                    run.CompleteRaid();
                }
                else if (run.Phase == RunPhase.Reward)
                {
                    PlayReward(run);
                }
            }
            return run;
        }
    }
}
