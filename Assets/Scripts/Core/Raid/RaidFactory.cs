using System.Collections.Generic;
using DungeonGame.Core.Data;
using DungeonGame.Core.Serialization;
using DungeonGame.Core.Simulation;

namespace DungeonGame.Core.Raid
{
    /// <summary>
    /// 攻略シミュレーションを作る・最後まで回すための便利関数。
    /// </summary>
    public static class RaidFactory
    {
        /// <summary>職業の初期能力値で勇者を1人作る(HP満タン)。</summary>
        public static HeroSetup CreateHero(GameData data, string classId)
        {
            var cls = data.Heroes.Get(classId);
            return new HeroSetup { ClassId = classId, Stats = cls.BaseStats, Hp = cls.BaseStats.MaxHp };
        }

        /// <summary>魔王側の「テスト防衛」で送り込むNPCパーティ。</summary>
        public static List<HeroSetup> CreateTestParty(GameData data)
        {
            var list = new List<HeroSetup>();
            foreach (var id in data.Heroes.TestParty) list.Add(CreateHero(data, id));
            return list;
        }

        /// <summary>
        /// 記録された介入を、記録どおりのティックで入れながら最後まで回す(リプレイ・サーバー検証用)。
        /// </summary>
        public static RaidSimulation RunWithCommands(RaidSimulation sim, IList<InterventionCommand> commands)
        {
            int next = 0;
            while (!sim.IsFinished)
            {
                while (next < commands.Count && commands[next].Tick == sim.CurrentTick)
                {
                    var c = commands[next++];
                    sim.Submit(c.Type, c.X, c.Y);
                }
                sim.Step();
            }
            return sim;
        }

        /// <summary>魔王側のテスト防衛:NPCパーティを介入なしで送り込み、結果を返す。</summary>
        public static RaidSimulation RunTestDefense(DungeonSnapshot snapshot, GameData data, ulong seed)
        {
            var sim = new RaidSimulation(snapshot, data, CreateTestParty(data), seed);
            SimulationRunner.RunToEnd(sim, int.MaxValue);
            return sim;
        }
    }
}
