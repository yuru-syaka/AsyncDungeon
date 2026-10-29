using System;
using System.Collections.Generic;
using DungeonGame.Core.Data;
using DungeonGame.Core.Serialization;

namespace DungeonGame.Core.Run
{
    /// <summary>
    /// 層ごとのNPCダンジョン(JSON)の集まり。ラン開始時に各層から1つずつシードで抽選する。
    /// 読み込み順に左右されないよう、層ごとにID順で並べておく(抽選結果がクライアントとサーバーで一致するように)。
    /// 将来オンライン化したら、ここが「他の魔王が作ったダンジョン」の一覧に置き換わる。
    /// </summary>
    public sealed class NpcDungeonPool
    {
        private readonly SortedDictionary<int, List<DungeonSnapshot>> _byLayer = new SortedDictionary<int, List<DungeonSnapshot>>();

        /// <summary>JSON文字列の集まりから作る。1つでも不正なダンジョンがあれば例外。</summary>
        public static NpcDungeonPool FromJsons(IEnumerable<string> jsons, GameData data)
        {
            var pool = new NpcDungeonPool();
            foreach (var json in jsons)
            {
                var snapshot = DungeonStorage.LoadSnapshot(json);
                var result = DungeonStorage.TrySave(snapshot, data, out _);
                if (!result.IsValid)
                    throw new SnapshotFormatException($"NPCダンジョン '{snapshot.Id}' が不正です:\n{result}");
                pool.Add(snapshot);
            }
            return pool;
        }

        public void Add(DungeonSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(snapshot.Id)) throw new ArgumentException("NPCダンジョンにはIDが必要です");
            if (!_byLayer.TryGetValue(snapshot.Layer, out var list))
                _byLayer[snapshot.Layer] = list = new List<DungeonSnapshot>();
            foreach (var s in list)
                if (s.Id == snapshot.Id) throw new ArgumentException($"NPCダンジョンのIDが重複しています: {snapshot.Id}");
            list.Add(snapshot);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        public IReadOnlyList<DungeonSnapshot> ForLayer(int layer)
            => _byLayer.TryGetValue(layer, out var list) ? list : (IReadOnlyList<DungeonSnapshot>)Array.Empty<DungeonSnapshot>();

        public DungeonSnapshot Find(string id)
        {
            foreach (var list in _byLayer.Values)
            foreach (var s in list)
                if (s.Id == id) return s;
            return null;
        }
    }
}
