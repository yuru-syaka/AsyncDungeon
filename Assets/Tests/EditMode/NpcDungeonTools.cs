using System.IO;
using System.Linq;
using System.Text;
using DungeonGame.Core.Run;
using DungeonGame.Core.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace DungeonGame.Core.Tests
{
    /// <summary>
    /// NPCダンジョンの生成とバランス確認(開発用。普段は実行されない)。
    /// 結果はテストの出力と Logs/diag.txt の両方に書き出す。
    /// 実行方法は README の「NPCダンジョンの再生成」を参照。
    /// </summary>
    public class NpcDungeonTools
    {
        /// <summary>層ごとの生成設定:掘る割合(%)と育成する割合(%)。難易度の調整はここで行う。</summary>
        private static readonly (int layer, int budget, int grow)[] LayerSettings =
        {
            (1, 70, 60),
            (2, 75, 70),
            (3, 65, 60),
            (4, 55, 55),
            (5, 60, 60),
        };

        private static readonly (NpcDigPattern pattern, string suffix, string name)[] Patterns =
        {
            (NpcDigPattern.Comb, "a", "櫛の巣"),
            (NpcDigPattern.Winding, "b", "回り道の洞"),
            (NpcDigPattern.Rooms, "c", "小部屋の館"),
        };

        private static void Report(string text)
        {
            TestContext.WriteLine(text);
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Logs", "diag.txt"), text);
        }

        [Test, Explicit("NPCダンジョンのJSONを作り直す")]
        public void NPCダンジョンを生成する()
        {
            var data = TestData.LoadGameData();
            Directory.CreateDirectory(TestData.NpcDungeonDir);
            foreach (var old in Directory.GetFiles(TestData.NpcDungeonDir, "npc_l*.json")) File.Delete(old);
            var sb = new StringBuilder();
            foreach (var (layer, budget, grow) in LayerSettings)
            foreach (var (pattern, suffix, name) in Patterns)
            {
                string id = $"npc_l{layer}_{suffix}";
                ulong seed = (ulong)(layer * 100 + suffix[0]);
                var snap = NpcDungeonGenerator.Generate(data, layer, pattern, seed, budget, grow, id, $"{layer}層 {name}");
                Assert.IsTrue(DungeonStorage.TrySave(snap, data, out var json).IsValid, id);
                File.WriteAllText(Path.Combine(TestData.NpcDungeonDir, id + ".json"), json);
                var species = snap.Monsters.GroupBy(m => m.Species).Select(g => $"{g.Key}x{g.Count()}");
                sb.AppendLine($"{id}: dug={snap.Tiles.Sum(r => r.Count(c => c == '.'))} monsters={snap.Monsters.Count} [{string.Join(", ", species)}]");
            }
            Report(sb.ToString());
        }

        [Test, Explicit("バランス確認")]
        public void 自動プレイで星の分布を出力する()
        {
            var data = TestData.LoadGameDataWithNpc();
            int layers = data.Rules.LayerCount;
            string[][] parties =
            {
                new[] { "warrior", "mage", "priest" },
                new[] { "warrior", "thief", "priest" },
                new[] { "warrior", "mage", "thief" },
                new[] { "thief", "mage", "priest" },
            };
            var sb = new StringBuilder();
            var total = new int[layers + 1];
            foreach (var party in parties)
            {
                var stars = new int[layers + 1];
                for (ulong seed = 1; seed <= 30; seed++)
                {
                    var run = AutoPlayer.PlayRun(data, seed, party);
                    stars[run.Stars]++;
                    total[run.Stars]++;
                }
                sb.AppendLine($"{string.Join("/", party)}: " + string.Join(" ", stars.Select((n, i) => $"★{i}={n}")));
            }
            sb.AppendLine("合計: " + string.Join(" ", total.Select((n, i) => $"★{i}={n}")));

            // ダンジョンごとの突破率(テストパーティ・強化なし)
            foreach (var layer in data.Rules.Layers.Select(l => l.Layer))
            foreach (var snap in data.NpcDungeons.ForLayer(layer))
            {
                int cleared = 0;
                for (ulong s = 1; s <= 10; s++)
                    if (Raid.RaidFactory.RunTestDefense(snap, data, s).Outcome == Raid.RaidOutcome.Cleared) cleared++;
                sb.AppendLine($"{snap.Id}: 強化なしNPCパーティの突破 {cleared}/10 monsters={snap.Monsters.Count}");
            }
            Report(sb.ToString());
        }
    }
}
