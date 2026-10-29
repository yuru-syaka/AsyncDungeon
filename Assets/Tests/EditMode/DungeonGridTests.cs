using DungeonGame.Core.Data;
using DungeonGame.Core.Determinism;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Serialization;
using NUnit.Framework;

namespace DungeonGame.Core.Tests
{
    /// <summary>マイルストーン2:グリッド・掘削・検証・JSON保存のテスト。</summary>
    public class DungeonGridTests
    {
        private LayerRuleSet _rules;
        private GameData _data;

        [SetUp]
        public void SetUp() { _data = TestData.LoadGameData(); _rules = _data.Rules; }

        private static ulong Hash(DungeonMap m)
        {
            var h = new StateHasher();
            m.AddToHash(ref h);
            return h.Value;
        }

        // ---------- 層ルール ----------

        [Test]
        public void 層ルールがJSONから全層ぶん読める()
        {
            Assert.AreEqual(5, _rules.LayerCount);
            for (int layer = 1; layer <= 5; layer++)
            {
                if (layer > 1)
                    Assert.LessOrEqual(_rules.Get(layer - 1).SoilNutrientMax, _rules.Get(layer).SoilNutrientMax, "深い層ほど土が豊か");
                // 最下層だけ魔王、それ以外は階段
                Assert.AreEqual(layer == 5 ? GoalType.DemonLord : GoalType.Stairs, _rules.Get(layer).Goal, $"層{layer}のゴール");
                if (layer > 1) Assert.Less(_rules.Get(layer - 1).DigBudget, _rules.Get(layer).DigBudget, "深い層ほど掘削予算が多い");
            }
        }

        // ---------- 初期生成 ----------

        [Test]
        public void 初期ダンジョンは同じシードなら同じになる()
        {
            var a = DungeonFactory.CreateEmpty(_rules.Get(2), 100);
            var b = DungeonFactory.CreateEmpty(_rules.Get(2), 100);
            var c = DungeonFactory.CreateEmpty(_rules.Get(2), 101);
            Assert.AreEqual(Hash(a), Hash(b));
            Assert.AreNotEqual(Hash(a), Hash(c));
        }

        [Test]
        public void 初期ダンジョンは外周が岩盤で入口とゴールがある()
        {
            var m = DungeonFactory.CreateEmpty(_rules.Get(1), 5);
            Assert.AreEqual(TileType.Bedrock, m.GetTile(0, 0));
            Assert.AreEqual(TileType.Entrance, m.GetTile(m.Entrance));
            Assert.AreEqual(TileType.Goal, m.GetTile(m.Goal));
            Assert.AreEqual(0, m.DugCount);
            Assert.Greater(m.TotalNutrient, 0);
        }

        // ---------- 掘削 ----------

        [Test]
        public void 入口に隣接する土は掘れて養分魔分が取り出される()
        {
            var m = DungeonFactory.CreateEmpty(_rules.Get(1), 5);
            int x = m.Entrance.X, y = m.Entrance.Y + 1;
            int n = m.GetNutrient(x, y), mana = m.GetMana(x, y);
            var r = m.Dig(x, y);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(n, r.Nutrient);
            Assert.AreEqual(mana, r.Mana);
            Assert.AreEqual(TileType.Floor, m.GetTile(x, y));
            Assert.AreEqual(0, m.GetNutrient(x, y));
            Assert.AreEqual(1, m.DugCount);
        }

        [Test]
        public void 通路に隣接していない土や岩盤は掘れない()
        {
            var m = DungeonFactory.CreateEmpty(_rules.Get(1), 5);
            Assert.AreEqual(DigError.NotConnected, m.Dig(3, 6).Error);
            Assert.AreEqual(DigError.NotSoil, m.Dig(0, 0).Error);
            Assert.AreEqual(DigError.OutOfBounds, m.Dig(-1, 3).Error);
        }

        [Test]
        public void 予算を使い切るとそれ以上掘れない()
        {
            var rule = _rules.Get(1);
            var m = DungeonFactory.CreateEmpty(rule, 5);
            m.DigBudget = 3;
            int x = m.Entrance.X;
            Assert.IsTrue(m.Dig(x, 2).Success);
            Assert.IsTrue(m.Dig(x, 3).Success);
            Assert.IsTrue(m.Dig(x, 4).Success);
            Assert.AreEqual(DigError.BudgetExceeded, m.Dig(x, 5).Error);
        }

        // ---------- 経路探索 ----------

        [Test]
        public void 経路探索_まっすぐな通路で最短経路が見つかる()
        {
            var m = TestData.StraightCorridor(_rules, 1);
            var path = PathFinder.FindPath(m, m.Entrance, m.Goal);
            Assert.IsNotNull(path);
            Assert.AreEqual(m.Goal.Y - m.Entrance.Y, path.Count);
            Assert.AreEqual(m.Goal, path[path.Count - 1]);
        }

        // ---------- 保存時の検証(必須テスト) ----------

        [Test]
        public void 有効なダンジョンは保存できる()
        {
            var m = TestData.StraightCorridor(_rules, 1);
            var result = DungeonStorage.TrySave(DungeonSnapshotConverter.FromMap(m, "t1"), _data, out var json);
            Assert.IsTrue(result.IsValid, result.ToString());
            Assert.IsNotNull(json);
        }

        [Test]
        public void 経路が封鎖されたダンジョンは保存時に弾かれる()
        {
            // 何も掘っていない=入口とゴールがつながっていない
            var m = DungeonFactory.CreateEmpty(_rules.Get(1), 5);
            var result = DungeonStorage.TrySave(DungeonSnapshotConverter.FromMap(m), _data, out var json);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Has(ValidationCode.PathBlocked));
            Assert.IsNull(json);

            // 途中まで掘ったが最後の1マスを掘っていない
            var m2 = TestData.StraightCorridor(_rules, 1);
            var s2 = DungeonSnapshotConverter.FromMap(m2);
            int y = m2.Goal.Y - 1;
            s2.Tiles[y] = s2.Tiles[y].Substring(0, m2.Goal.X) + "s" + s2.Tiles[y].Substring(m2.Goal.X + 1);
            Assert.IsTrue(DungeonStorage.TrySave(s2, _data, out _).Has(ValidationCode.PathBlocked));
        }

        [Test]
        public void 予算超過のダンジョンは保存時に弾かれる()
        {
            // JSONを直接書き換えて全面を通路にした(=改ざん)スナップショット
            var m = TestData.StraightCorridor(_rules, 1);
            var s = DungeonSnapshotConverter.FromMap(m);
            for (int y = 1; y < s.Height - 1; y++)
                s.Tiles[y] = s.Tiles[y].Replace('s', '.');

            var result = DungeonStorage.TrySave(s, _data, out var json);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Has(ValidationCode.BudgetExceeded), result.ToString());
            Assert.IsFalse(result.Has(ValidationCode.PathBlocked));
            Assert.IsNull(json);
        }

        [Test]
        public void 外周の岩盤を壊したダンジョンや入口が2つあるダンジョンは弾かれる()
        {
            var m = TestData.StraightCorridor(_rules, 1);
            var s = DungeonSnapshotConverter.FromMap(m);
            s.Tiles[0] = "." + s.Tiles[0].Substring(1);
            s.Tiles[5] = s.Tiles[5].Substring(0, 2) + "E" + s.Tiles[5].Substring(3);
            var result = DungeonStorage.TrySave(s, _data, out _);
            Assert.IsTrue(result.Has(ValidationCode.BorderNotBedrock));
            Assert.IsTrue(result.Has(ValidationCode.EntranceCount));
        }

        [Test]
        public void 層ルールと違うゴールやサイズは弾かれる()
        {
            var m = TestData.StraightCorridor(_rules, 1);
            var s = DungeonSnapshotConverter.FromMap(m);
            s.Goal = GoalType.DemonLord;
            Assert.IsTrue(DungeonStorage.TrySave(s, _data, out _).Has(ValidationCode.GoalTypeMismatch));

            s.Layer = 2; // 層2はサイズが違う
            Assert.IsTrue(DungeonStorage.TrySave(s, _data, out _).Has(ValidationCode.SizeMismatch));
        }

        [Test]
        public void 壊れたJSONは形式エラーになる()
        {
            var s = DungeonSnapshotConverter.FromMap(TestData.StraightCorridor(_rules, 1));
            s.Tiles.RemoveAt(0);
            Assert.IsTrue(DungeonStorage.TrySave(s, _data, out _).Has(ValidationCode.FormatError));
        }

        // ---------- JSON往復(必須テスト) ----------

        [Test]
        public void JSONにシリアライズして戻しても内容が変わらない()
        {
            var m = TestData.StraightCorridor(_rules, 3, seed: 777);
            m.Dig(m.Entrance.X - 1, m.Entrance.Y + 1); // 横にも少し掘る
            var original = DungeonSnapshotConverter.FromMap(m, "id_x", "テストの迷宮", "まおう");
            original.EcologyTick = 123;
            original.EcologyRng = new DeterministicRandom(9).GetState();

            Assert.IsTrue(DungeonStorage.TrySave(original, _data, out var json1).IsValid);
            var loaded = DungeonStorage.LoadSnapshot(json1);
            string json2 = GameJson.Serialize(loaded);

            Assert.AreEqual(json1, json2, "JSON → オブジェクト → JSON で文字列が一致すること");
            Assert.AreEqual("テストの迷宮", loaded.Name);
            Assert.AreEqual(123, loaded.EcologyTick);
            Assert.AreEqual(original.EcologyRng.Value.W, loaded.EcologyRng.Value.W);

            var restored = DungeonSnapshotConverter.ToMap(loaded, _rules);
            Assert.AreEqual(Hash(m), Hash(restored), "マップの中身(マス・養分・魔分)が一致すること");
            Assert.AreEqual(m.Entrance, restored.Entrance);
            Assert.AreEqual(m.Goal, restored.Goal);
            Assert.AreEqual(m.DugCount, restored.DugCount);
        }

        [Test]
        public void 列挙型は名前でJSONに書かれる()
        {
            var s = DungeonSnapshotConverter.FromMap(TestData.StraightCorridor(_rules, _rules.LayerCount)); // 最下層=魔王の層
            string json = GameJson.Serialize(s);
            StringAssert.Contains("\"DemonLord\"", json);
        }
    }
}
