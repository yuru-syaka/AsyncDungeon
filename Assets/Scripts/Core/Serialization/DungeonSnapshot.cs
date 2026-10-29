using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DungeonGame.Core.Determinism;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Ecology;

namespace DungeonGame.Core.Serialization
{
    /// <summary>
    /// ダンジョンのスナップショット(JSONに保存する形)。
    /// マップはマス1文字の行文字列、養分・魔分はスペース区切りの数値の行文字列で持つ(人が読める・差分が見やすい)。
    /// 中身を変えるときは FormatVersion を上げる。
    /// </summary>
    [Serializable]
    public sealed class DungeonSnapshot
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion = CurrentFormatVersion;
        /// <summary>ダンジョンID(NPCダンジョンなら "npc_l1_a" など)。</summary>
        public string Id;
        public string Name;
        /// <summary>作者(魔王)名。</summary>
        public string Author;
        public int Layer;
        public int Width;
        public int Height;
        public GoalType Goal;
        /// <summary>マス。'#'=岩盤 's'=土 '.'=通路 'E'=入口 'G'=ゴール。</summary>
        public List<string> Tiles = new List<string>();
        /// <summary>各マスの養分(行ごとにスペース区切り)。</summary>
        public List<string> Nutrient = new List<string>();
        /// <summary>各マスの魔分(行ごとにスペース区切り)。</summary>
        public List<string> Mana = new List<string>();
        /// <summary>通路に積もった骨の数(行ごとにスペース区切り)。骨が1本もなければ空。</summary>
        public List<string> Bones = new List<string>();
        /// <summary>これまでに生態系を動かしたティック数。</summary>
        public int EcologyTick;
        /// <summary>生態系の乱数の状態(続きから育てるため)。</summary>
        public RandomState? EcologyRng;
        /// <summary>次に生まれるモンスターのID。</summary>
        public int NextMonsterId = 1;
        /// <summary>土が満杯で失われた養分・魔分。</summary>
        public int LostNutrient;
        public int LostMana;
        /// <summary>住んでいるモンスター(ID昇順)。</summary>
        public List<MonsterSnapshot> Monsters = new List<MonsterSnapshot>();
    }

    /// <summary>モンスター1体の保存形式。</summary>
    [Serializable]
    public sealed class MonsterSnapshot
    {
        public int Id;
        public string Species;
        public int X;
        public int Y;
        public int Nutrient;
        public int Mana;
        public int Age;
        public int Cooldown;
        public int Hp;
        public int Digest;
    }

    /// <summary>スナップショットの形式が壊れているときの例外。</summary>
    public sealed class SnapshotFormatException : Exception
    {
        public SnapshotFormatException(string message) : base(message) { }
    }

    /// <summary>DungeonMap / DungeonState と DungeonSnapshot の相互変換。</summary>
    public static class DungeonSnapshotConverter
    {
        /// <summary>乱数状態のないスナップショット(手書きのNPCダンジョンなど)で使うシード。</summary>
        public const ulong DefaultEcologySeed = 1;

        /// <summary>モンスター・生態系の状態を含めて書き出す。</summary>
        public static DungeonSnapshot FromState(DungeonState state, string id = null, string name = null, string author = null)
        {
            var s = FromMap(state.Map, id, name, author);
            s.EcologyTick = state.EcologyTick;
            s.EcologyRng = state.Rng.GetState();
            s.NextMonsterId = state.NextMonsterId;
            s.LostNutrient = state.LostNutrient;
            s.LostMana = state.LostMana;
            foreach (var m in state.Monsters)
            {
                if (m.Dead) continue;
                s.Monsters.Add(new MonsterSnapshot
                {
                    Id = m.Id, Species = m.Species.Id, X = m.Pos.X, Y = m.Pos.Y,
                    Nutrient = m.Nutrient, Mana = m.Mana, Age = m.Age, Cooldown = m.Cooldown, Hp = m.Hp, Digest = m.Digest,
                });
            }
            return s;
        }

        /// <summary>
        /// スナップショットから完全な状態を復元する。
        /// 形式として壊れているもの(未知の種族・マップ外・同じマスに2体など)は例外にする。
        /// ルール上の妥当性(階級など)は DungeonValidator で別に調べる。
        /// </summary>
        public static DungeonState ToState(DungeonSnapshot s, Data.GameData data)
        {
            var map = ToMap(s, data.Rules);
            var rng = s.EcologyRng.HasValue
                ? DeterministicRandom.FromState(s.EcologyRng.Value)
                : new DeterministicRandom(DefaultEcologySeed);
            var state = new DungeonState(map, rng)
            {
                EcologyTick = s.EcologyTick,
                LostNutrient = s.LostNutrient,
                LostMana = s.LostMana,
            };

            int lastId = 0;
            foreach (var ms in s.Monsters ?? new List<MonsterSnapshot>())
            {
                var species = data.Monsters.Get(ms.Species);
                if (species == null) throw new SnapshotFormatException($"不明なモンスター種族 '{ms.Species}'");
                var pos = new GridPos(ms.X, ms.Y);
                if (!map.InBounds(pos)) throw new SnapshotFormatException($"モンスター{ms.Id}の位置がマップ外です");
                if (ms.Id <= lastId) throw new SnapshotFormatException("モンスターのIDが昇順になっていません");
                if (state.MonsterAt(pos) != null) throw new SnapshotFormatException($"{pos} にモンスターが重なっています");
                lastId = ms.Id;
                state.RestoreMonster(new Monster
                {
                    Id = ms.Id, Species = species, Pos = pos, Nutrient = ms.Nutrient, Mana = ms.Mana,
                    Age = ms.Age, Cooldown = ms.Cooldown, Hp = ms.Hp, Digest = ms.Digest,
                });
            }
            state.NextMonsterId = s.NextMonsterId > lastId ? s.NextMonsterId : lastId + 1;
            return state;
        }

        public static DungeonSnapshot FromMap(DungeonMap map, string id = null, string name = null, string author = null)
        {
            var s = new DungeonSnapshot
            {
                Id = id, Name = name, Author = author,
                Layer = map.Layer, Width = map.Width, Height = map.Height, Goal = map.GoalType,
            };
            var sb = new StringBuilder();
            for (int y = 0; y < map.Height; y++)
            {
                sb.Clear();
                for (int x = 0; x < map.Width; x++) sb.Append(TileChars.ToChar(map.GetTile(x, y)));
                s.Tiles.Add(sb.ToString());
                s.Nutrient.Add(RowToString(map, y, Field.Nutrient));
                s.Mana.Add(RowToString(map, y, Field.Mana));
            }
            // 骨は1本でもあるときだけ書く(ないダンジョンのJSONを短く保つ)
            bool anyBones = false;
            for (int y = 0; y < map.Height && !anyBones; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetBones(x, y) != 0) { anyBones = true; break; }
            if (anyBones)
                for (int y = 0; y < map.Height; y++) s.Bones.Add(RowToString(map, y, Field.Bones));
            return s;
        }

        private enum Field { Nutrient, Mana, Bones }

        private static int Get(DungeonMap map, int x, int y, Field f)
            => f == Field.Nutrient ? map.GetNutrient(x, y) : f == Field.Mana ? map.GetMana(x, y) : map.GetBones(x, y);

        private static string RowToString(DungeonMap map, int y, Field f)
        {
            var sb = new StringBuilder();
            for (int x = 0; x < map.Width; x++)
            {
                if (x > 0) sb.Append(' ');
                sb.Append(Get(map, x, y, f).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>スナップショットからマップを復元する。予算と資源上限は層ルールから取る。</summary>
        public static DungeonMap ToMap(DungeonSnapshot s, LayerRuleSet rules)
        {
            if (s == null) throw new SnapshotFormatException("スナップショットが空です");
            if (s.FormatVersion > DungeonSnapshot.CurrentFormatVersion)
                throw new SnapshotFormatException($"未対応の形式バージョン {s.FormatVersion}");
            if (s.Width < 3 || s.Height < 3) throw new SnapshotFormatException("サイズが不正です");
            if (s.Tiles == null || s.Tiles.Count != s.Height) throw new SnapshotFormatException("Tiles の行数が Height と違います");

            var map = new DungeonMap(s.Layer, s.Width, s.Height) { GoalType = s.Goal };
            var rule = rules?.Get(s.Layer);
            if (rule != null)
            {
                map.DigBudget = rule.DigBudget;
                map.TileResourceCap = rule.TileResourceCap;
            }

            for (int y = 0; y < s.Height; y++)
            {
                string row = s.Tiles[y];
                if (row == null || row.Length != s.Width)
                    throw new SnapshotFormatException($"Tiles の{y}行目の長さが Width と違います");
                for (int x = 0; x < s.Width; x++)
                {
                    if (!TileChars.TryParse(row[x], out var t))
                        throw new SnapshotFormatException($"不明なマス文字 '{row[x]}' ({x},{y})");
                    map.SetTile(x, y, t);
                }
            }
            ParseResource(s.Nutrient, map, Field.Nutrient);
            ParseResource(s.Mana, map, Field.Mana);
            if (s.Bones != null && s.Bones.Count > 0) ParseResource(s.Bones, map, Field.Bones); // 骨はなくてもよい
            return map;
        }

        private static void ParseResource(List<string> rows, DungeonMap map, Field field)
        {
            string label = field.ToString();
            if (rows == null || rows.Count != map.Height) throw new SnapshotFormatException($"{label} の行数が Height と違います");
            for (int y = 0; y < map.Height; y++)
            {
                var cells = (rows[y] ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (cells.Length != map.Width) throw new SnapshotFormatException($"{label} の{y}行目の個数が Width と違います");
                for (int x = 0; x < map.Width; x++)
                {
                    if (!int.TryParse(cells[x], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                        throw new SnapshotFormatException($"{label} ({x},{y}) が数値ではありません");
                    if (field == Field.Nutrient) map.SetNutrient(x, y, v);
                    else if (field == Field.Mana) map.SetMana(x, y, v);
                    else map.SetBones(x, y, v);
                }
            }
        }
    }

    /// <summary>
    /// ダンジョンの保存・読み込みの窓口。保存時に必ず検証を通す。
    /// </summary>
    public static class DungeonStorage
    {
        /// <summary>
        /// 検証してからJSONにする。検証に失敗したら json は null で、結果にエラー理由が入る。
        /// </summary>
        public static ValidationResult TrySave(DungeonSnapshot snapshot, Data.GameData data, out string json)
        {
            json = null;
            DungeonState state;
            try
            {
                state = DungeonSnapshotConverter.ToState(snapshot, data);
            }
            catch (SnapshotFormatException e)
            {
                var r = new ValidationResult();
                r.Errors.Add(new ValidationError { Code = ValidationCode.FormatError, Message = e.Message });
                return r;
            }

            var result = DungeonValidator.ValidateState(state, data);
            if (result.IsValid) json = GameJson.Serialize(snapshot);
            return result;
        }

        /// <summary>JSONを読み込み、検証も通す。不正なら例外。NPCダンジョンの読み込みなどに使う。</summary>
        public static DungeonState LoadValidated(string json, Data.GameData data)
        {
            var state = DungeonSnapshotConverter.ToState(LoadSnapshot(json), data);
            var result = DungeonValidator.ValidateState(state, data);
            if (!result.IsValid) throw new SnapshotFormatException("ダンジョンが不正です:\n" + result);
            return state;
        }

        /// <summary>JSON文字列を読み込んでスナップショットにする(形式チェックのみ)。</summary>
        public static DungeonSnapshot LoadSnapshot(string json)
        {
            var s = GameJson.Deserialize<DungeonSnapshot>(json);
            if (s == null) throw new SnapshotFormatException("JSONが空です");
            return s;
        }

        public static LayerRuleSet LoadRules(string json) => GameJson.Deserialize<LayerRuleSet>(json);
    }
}
