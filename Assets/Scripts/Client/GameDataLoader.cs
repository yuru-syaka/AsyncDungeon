using System.Linq;
using DungeonGame.Core.Data;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>
    /// Resources/GameData の JSON を読み込んで GameData を作る(Unity側の窓口)。
    /// ロジック層は文字列を受け取るだけなので、サーバーではファイルから同じ JSON を読めばよい。
    /// </summary>
    public static class GameDataLoader
    {
        public static GameData Load()
        {
            string Read(string name) => Resources.Load<TextAsset>("GameData/" + name).text;

            var npc = Resources.LoadAll<TextAsset>("GameData/NpcDungeons").Select(t => t.text).ToList();
            return GameData.FromJson(Read("layer_rules"), Read("monsters"), Read("heroes"), Read("skills"))
                .WithNpcDungeons(npc);
        }
    }

    /// <summary>
    /// 保存先(PlayerPrefs)。WebGLでもブラウザ内(IndexedDB)に残る。
    /// エディタ・PC版では Application.persistentDataPath にも JSON ファイルとして書き出す。
    /// </summary>
    public static class SaveStore
    {
        public static string DungeonKey(int layer) => $"dungeon_layer{layer}";
        public const string LastRunLogKey = "last_run_log";

        public static void Save(string key, string json)
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
#if !UNITY_WEBGL || UNITY_EDITOR
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, key + ".json"), json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("ファイルに保存できませんでした: " + e.Message);
            }
#endif
        }

        public static string Load(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        /// <summary>保存先フォルダ(表示用)。</summary>
        public static string FileLocation =>
#if UNITY_WEBGL && !UNITY_EDITOR
            "ブラウザ内(PlayerPrefs)";
#else
            Application.persistentDataPath;
#endif
    }
}
