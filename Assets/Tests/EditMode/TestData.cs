using System.IO;
using System.Linq;
using DungeonGame.Core.Data;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Serialization;
using UnityEngine;

namespace DungeonGame.Core.Tests
{
    /// <summary>テスト用のデータ読み込みと、よく使うダンジョンの組み立て。</summary>
    public static class TestData
    {
        public static string GameDataDir => Path.Combine(Application.dataPath, "Resources", "GameData");
        public static string NpcDungeonDir => Path.Combine(GameDataDir, "NpcDungeons");

        public static string ReadGameData(string fileName) => File.ReadAllText(Path.Combine(GameDataDir, fileName));

        public static LayerRuleSet LoadRules() => DungeonStorage.LoadRules(ReadGameData("layer_rules.json"));

        /// <summary>NPCダンジョン以外の定義データ。</summary>
        public static GameData LoadGameData()
            => GameData.FromJson(ReadGameData("layer_rules.json"), ReadGameData("monsters.json"),
                ReadGameData("heroes.json"), ReadGameData("skills.json"));

        /// <summary>NPCダンジョンまで含めた定義データ(ラン用)。</summary>
        public static GameData LoadGameDataWithNpc()
        {
            var files = Directory.Exists(NpcDungeonDir)
                ? Directory.GetFiles(NpcDungeonDir, "*.json").OrderBy(f => f).Select(File.ReadAllText).ToList()
                : new System.Collections.Generic.List<string>();
            return LoadGameData().WithNpcDungeons(files);
        }

        /// <summary>入口からまっすぐ下へゴールまで掘った、最小限の有効なダンジョン。</summary>
        public static DungeonMap StraightCorridor(LayerRuleSet rules, int layer, ulong seed = 1)
        {
            var map = DungeonFactory.CreateEmpty(rules.Get(layer), seed);
            int x = map.Entrance.X;
            for (int y = map.Entrance.Y + 1; y < map.Goal.Y; y++)
            {
                var r = map.Dig(x, y);
                if (!r.Success) throw new System.Exception($"掘れません ({x},{y}) {r.Error}");
            }
            return map;
        }

        /// <summary>
        /// 魔王が実際に作りそうなダンジョン:中央の縦通路 + 左右に横穴を何本か掘る。
        /// 掘る順番は固定なので、同じシードなら同じ結果になる。
        /// </summary>
        public static DungeonBuilder BuildTypical(GameData data, int layer, ulong seed)
        {
            var b = DungeonBuilder.CreateNew(data, layer, seed);
            var map = b.Map;
            int cx = map.Entrance.X;
            for (int y = map.Entrance.Y + 1; y < map.Goal.Y; y++) b.Dig(cx, y);
            for (int y = 3; y < map.Goal.Y - 1 && map.RemainingBudget > 0; y += 3)
            {
                for (int x = cx - 1; x >= 1 && map.RemainingBudget > 0; x--) b.Dig(x, y);
                for (int x = cx + 1; x < map.Width - 1 && map.RemainingBudget > 0; x++) b.Dig(x, y);
            }
            return b;
        }
    }
}
