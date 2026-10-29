using System.Collections.Generic;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Ecology;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Run;
using DungeonGame.Core.Serialization;
using DungeonGame.Core.Skills;

namespace DungeonGame.Core.Data
{
    /// <summary>
    /// ゲームの定義データ一式(層ルール・モンスター・勇者の職業・スキル・NPCダンジョン)。
    /// JSON文字列から作るので、Unity(Resources)でもサーバー(ファイル)でも同じように読み込める。
    /// </summary>
    public sealed class GameData
    {
        public LayerRuleSet Rules;
        public MonsterCatalog Monsters;
        public HeroCatalog Heroes;
        public SkillCatalog Skills;
        public NpcDungeonPool NpcDungeons;

        public static GameData FromJson(string layerRulesJson, string monstersJson, string heroesJson = null,
            string skillsJson = null)
        {
            return new GameData
            {
                Rules = GameJson.Deserialize<LayerRuleSet>(layerRulesJson),
                Monsters = GameJson.Deserialize<MonsterCatalog>(monstersJson),
                Heroes = heroesJson != null ? GameJson.Deserialize<HeroCatalog>(heroesJson) : new HeroCatalog(),
                Skills = skillsJson != null ? GameJson.Deserialize<SkillCatalog>(skillsJson) : new SkillCatalog(),
            };
        }

        /// <summary>NPCダンジョン(JSON文字列の集まり)を読み込んで検証する。</summary>
        public GameData WithNpcDungeons(IEnumerable<string> dungeonJsons)
        {
            NpcDungeons = NpcDungeonPool.FromJsons(dungeonJsons, this);
            return this;
        }
    }
}
