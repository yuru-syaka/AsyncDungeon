using System.Linq;
using DungeonGame.Core.Data;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Ecology;
using DungeonGame.Core.Serialization;
using NUnit.Framework;

namespace DungeonGame.Core.Tests
{
    /// <summary>マイルストーン3:生態系シミュレーションのテスト。</summary>
    public class EcologyTests
    {
        private GameData _data;

        [SetUp]
        public void SetUp() => _data = TestData.LoadGameData();

        private MonsterSpecies Sp(string id) => _data.Monsters.Get(id);

        /// <summary>入口の下を掘れる状態にし、指定の養分・魔分を仕込んでから掘る。</summary>
        private DigOutcome DigPrepared(DungeonBuilder b, int nutrient, int mana)
        {
            var map = b.Map;
            int x = map.Entrance.X, y = map.Entrance.Y + 1;
            map.SetNutrient(x, y, nutrient);
            map.SetMana(x, y, mana);
            return b.Dig(x, y);
        }

        // ---------- 誕生 ----------

        [Test]
        public void モンスター定義がJSONから読める()
        {
            Assert.GreaterOrEqual(_data.Monsters.Species.Count, 4);
            Assert.AreEqual(1, Sp("moss_slime").Tier);
            Assert.AreEqual(Diet.Predator, Sp("cave_lizard").Diet);
            Assert.IsTrue(Sp("abyss_wyrm").CanEat(Sp("cave_lizard")));
            Assert.IsFalse(Sp("moss_slime").CanEat(Sp("mana_mite")));
        }

        [Test]
        public void 養分の多い土を掘ると下位モンスターが生まれる()
        {
            var b = DungeonBuilder.CreateNew(_data, 1, 1);
            var o = DigPrepared(b, 6, 0);
            Assert.IsNotNull(o.Spawned);
            Assert.AreEqual("moss_slime", o.Spawned.Species.Id);
            Assert.AreEqual(o.Dig.Pos, o.Spawned.Pos);
        }

        [Test]
        public void 養分が少ない土を掘っても何も生まれず養分は周りに還る()
        {
            var b = DungeonBuilder.CreateNew(_data, 1, 1);
            var map = b.Map;
            map.SetNutrient(map.Entrance.X, map.Entrance.Y + 1, 2);
            map.SetMana(map.Entrance.X, map.Entrance.Y + 1, 0);
            int before = b.State.GrandTotalNutrient;
            var o = b.Dig(map.Entrance.X, map.Entrance.Y + 1);
            Assert.IsNull(o.Spawned);
            Assert.AreEqual(0, b.State.Monsters.Count);
            Assert.AreEqual(0, map.GetNutrient(o.Dig.Pos.X, o.Dig.Pos.Y));
            Assert.AreEqual(before, b.State.GrandTotalNutrient, "掘った土の養分は周りの土に移っただけ");
        }

        [Test]
        public void 生まれる種族は層ではなく土の濃さで決まる()
        {
            // 1層でも、とても濃い土を掘れば上位が生まれる(層による制限はない)
            var b1 = DungeonBuilder.CreateNew(_data, 1, 1);
            Assert.AreEqual(3, DigPrepared(b1, 12, 12).Spawned.Species.Tier);

            // 同じ層でも、土がうすければ下位、中くらいなら中位
            var b2 = DungeonBuilder.CreateNew(_data, 5, 1);
            Assert.AreEqual(1, DigPrepared(b2, 4, 0).Spawned.Species.Tier);
            var b3 = DungeonBuilder.CreateNew(_data, 5, 1);
            Assert.AreEqual(2, DigPrepared(b3, 9, 4).Spawned.Species.Tier);

            // 同じ土なら層が違っても同じ種族
            var a = DungeonBuilder.CreateNew(_data, 2, 1);
            var c = DungeonBuilder.CreateNew(_data, 4, 1);
            Assert.AreEqual(DigPrepared(a, 7, 7).Spawned.Species.Id, DigPrepared(c, 7, 7).Spawned.Species.Id);
        }

        [Test]
        public void 全モンスターに図鑑の解説とドット絵用のIDがあり各階級に複数の種族がいる()
        {
            foreach (var s in _data.Monsters.Species)
                Assert.IsFalse(string.IsNullOrEmpty(s.Description), $"{s.Id} の解説");
            Assert.GreaterOrEqual(_data.Monsters.Species.Count, 10);
            for (int tier = 1; tier <= 3; tier++)
                Assert.GreaterOrEqual(_data.Monsters.Species.Count(s => !s.IsBoss && s.Tier == tier), 3, $"階級{tier}の種族数");
            Assert.GreaterOrEqual(_data.Monsters.Species.Count(s => s.Tier == 4), 1);
        }

        [Test]
        public void 同じ階級では誕生条件の厳しい種族が優先される()
        {
            // ヒカリダケ(養分6・魔分2)とコケスライム(養分4)の両方を満たす土からはヒカリダケ
            var b = DungeonBuilder.CreateNew(_data, 1, 1);
            Assert.AreEqual("glow_shroom", DigPrepared(b, 7, 3).Spawned.Species.Id);
            // 魔分が足りなければコケスライム
            var b2 = DungeonBuilder.CreateNew(_data, 1, 1);
            Assert.AreEqual("moss_slime", DigPrepared(b2, 7, 1).Spawned.Species.Id);
        }

        [Test]
        public void 魔分が多い土からは魔分系の下位が生まれる()
        {
            var b = DungeonBuilder.CreateNew(_data, 1, 1);
            Assert.AreEqual("mana_mite", DigPrepared(b, 1, 6).Spawned.Species.Id);
        }

        // ---------- 食物連鎖・死亡 ----------

        [Test]
        public void 中位は隣の下位を捕食して体が大きくなる()
        {
            var b = TestData.BuildTypical(_data, 2, 3);
            var s = b.State;
            s.ClearMonsters();
            var p1 = new GridPos(b.Map.Entrance.X, 3);
            var p2 = new GridPos(b.Map.Entrance.X, 4);
            var prey = s.AddMonster(Sp("moss_slime"), p1, 5, 0);
            var lizard = s.AddMonster(Sp("cave_lizard"), p2, 6, 4);
            lizard.Cooldown = 0;
            int before = lizard.Biomass;

            new EcologySimulation(s).Step();

            Assert.IsTrue(prey.Dead);
            Assert.IsFalse(s.Monsters.Contains(prey));
            Assert.AreEqual(before + 5, lizard.Biomass);
        }

        [Test]
        public void 寿命で死ぬと体の養分魔分が土に還る()
        {
            var b = TestData.BuildTypical(_data, 1, 3);
            var s = b.State;
            s.ClearMonsters();
            var pos = new GridPos(b.Map.Entrance.X, 4);
            var m = s.AddMonster(Sp("moss_slime"), pos, 7, 3);
            m.Age = m.Species.Lifespan - 1;
            int soilN = b.Map.TotalNutrient, soilM = b.Map.TotalMana;

            new EcologySimulation(s).Step();

            Assert.AreEqual(0, s.Monsters.Count);
            Assert.AreEqual(soilN + 7, b.Map.TotalNutrient);
            Assert.AreEqual(soilM + 3, b.Map.TotalMana);
        }

        [Test]
        public void 下位は土の養分を吸って分裂して増える()
        {
            var b = TestData.BuildTypical(_data, 1, 11);
            var s = b.State;
            s.ClearMonsters();
            s.AddMonster(Sp("moss_slime"), new GridPos(b.Map.Entrance.X, 3), 4, 0);
            b.Grow(800);
            Assert.Greater(s.Monsters.Count, 1, "スライムが増えていること");
        }

        // ---------- 保存則・上限 ----------

        [Test]
        public void 養分と魔分の総量は生態系を動かしても変わらない()
        {
            for (int layer = 1; layer <= 5; layer++)
            {
                var b = DungeonBuilder.CreateNew(_data, layer, (ulong)(50 + layer));
                int n0 = b.State.GrandTotalNutrient, m0 = b.State.GrandTotalMana;

                var typical = TestData.BuildTypical(_data, layer, (ulong)(50 + layer));
                Assert.AreEqual(n0, typical.State.GrandTotalNutrient, $"層{layer} 掘削後");
                Assert.AreEqual(m0, typical.State.GrandTotalMana, $"層{layer} 掘削後");

                typical.Grow(typical.Rule.MaxEcologyTicks);
                Assert.AreEqual(n0, typical.State.GrandTotalNutrient, $"層{layer} 育成後");
                Assert.AreEqual(m0, typical.State.GrandTotalMana, $"層{layer} 育成後");
            }
        }

        [Test]
        public void 育成は層ルールの上限ティックまでしか進まない()
        {
            var b = TestData.BuildTypical(_data, 1, 1);
            int max = b.Rule.MaxEcologyTicks;
            Assert.AreEqual(max - 100, b.Grow(max - 100));
            Assert.AreEqual(100, b.Grow(5000));
            Assert.AreEqual(0, b.Grow(10));
            Assert.AreEqual(max, b.State.EcologyTick);
        }

        // ---------- 決定論(必須テスト) ----------

        [Test]
        public void 同じシードと同じ掘り方なら育成結果が完全に同じになる()
        {
            var a = TestData.BuildTypical(_data, 3, 2024);
            var b = TestData.BuildTypical(_data, 3, 2024);
            a.Grow(2000);
            b.Grow(2000);
            Assert.AreEqual(a.State.ComputeHash(), b.State.ComputeHash());
            Assert.Greater(a.State.Monsters.Count, 0, "モンスターがいる状態で比べていること");
        }

        [Test]
        public void 途中で保存して読み込んでも続きの育成結果が同じになる()
        {
            var continuous = TestData.BuildTypical(_data, 2, 77);
            continuous.Grow(700);
            continuous.Grow(900);

            var first = TestData.BuildTypical(_data, 2, 77);
            first.Grow(700);
            Assert.IsTrue(DungeonStorage.TrySave(first.ToSnapshot("x"), _data, out var json).IsValid);
            var resumed = DungeonBuilder.FromSnapshot(DungeonStorage.LoadSnapshot(json), _data);
            resumed.Grow(900);

            Assert.AreEqual(continuous.State.ComputeHash(), resumed.State.ComputeHash());
        }

        // ---------- 検証・JSON ----------

        [Test]
        public void モンスター入りスナップショットもJSON往復で変わらない()
        {
            var b = TestData.BuildTypical(_data, 3, 9);
            b.Grow(1000);
            var snap = b.ToSnapshot("l3", "深い穴", "テスト魔王");
            Assert.Greater(snap.Monsters.Count, 0);

            string json1 = GameJson.Serialize(snap);
            var loaded = DungeonStorage.LoadSnapshot(json1);
            Assert.AreEqual(json1, GameJson.Serialize(loaded));

            var restored = DungeonSnapshotConverter.ToState(loaded, _data);
            Assert.AreEqual(b.State.ComputeHash(), restored.ComputeHash());
        }

        [Test]
        public void 魔王をダンジョンに置いたスナップショットは保存時に弾かれる()
        {
            var b = TestData.BuildTypical(_data, 1, 4);
            var snap = b.ToSnapshot();
            snap.Monsters.Clear();
            snap.Monsters.Add(new MonsterSnapshot
            {
                Id = snap.NextMonsterId, Species = "demon_lord",
                X = b.Map.Entrance.X, Y = b.Map.Entrance.Y + 1, Hp = 100,
            });
            var result = DungeonStorage.TrySave(snap, _data, out var json);
            Assert.IsTrue(result.Has(ValidationCode.MonsterNotAllowed), result.ToString());
            Assert.IsNull(json);
        }

        [Test]
        public void 層に関係なくどの種族でも置ける()
        {
            // 1層に上位のアビスワームがいても保存できる(生まれるかどうかは土しだい)
            var b = TestData.BuildTypical(_data, 1, 4);
            var snap = b.ToSnapshot();
            snap.Monsters.Clear();
            snap.Monsters.Add(new MonsterSnapshot
            {
                Id = snap.NextMonsterId, Species = "abyss_wyrm",
                X = b.Map.Entrance.X, Y = b.Map.Entrance.Y + 1, Nutrient = 7, Mana = 7, Hp = 50,
            });
            var result = DungeonStorage.TrySave(snap, _data, out _);
            Assert.IsTrue(result.IsValid, result.ToString());
        }

        // ---------- 特殊な生まれ方 ----------

        /// <summary>入口の下に3×3の部屋を掘った1層のダンジョン(モンスターなし、部屋のまわりの土は豊か)。</summary>
        private DungeonBuilder RoomDungeon()
        {
            var b = DungeonBuilder.CreateNew(_data, 1, 1);
            var map = b.Map;
            int cx = map.Entrance.X;
            for (int y = map.Entrance.Y + 1; y < map.Goal.Y; y++) map.Dig(cx, y);
            for (int y = 3; y <= 5; y++)
            for (int x = cx - 2; x <= cx + 2; x++)
                if (map.GetTile(x, y) == TileType.Soil) map.Dig(x, y);
            // 部屋のまわりの土を豊かにしておく(誕生の材料)
            for (int y = 1; y < map.Height - 1; y++)
            for (int x = 1; x < map.Width - 1; x++)
                if (map.GetTile(x, y) == TileType.Soil) { map.SetNutrient(x, y, 8); map.SetMana(x, y, 8); }
            b.State.ClearMonsters();
            return b;
        }

        [Test]
        public void 巣_広い部屋を掘っておくとカゲコウモリが巣を作る()
        {
            var b = RoomDungeon();
            int n0 = b.State.GrandTotalNutrient, m0 = b.State.GrandTotalMana;
            b.Grow(600);
            Assert.IsTrue(b.State.Monsters.Any(m => m.Species.Id == "shadow_bat"), "カゲコウモリが生まれている");
            Assert.AreEqual(n0, b.State.GrandTotalNutrient, "体の分は土から取るので総量は変わらない");
            Assert.AreEqual(m0, b.State.GrandTotalMana);
        }

        [Test]
        public void 巣_狭い通路だけのダンジョンにはカゲコウモリは生まれない()
        {
            var b = TestData.BuildTypical(_data, 2, 3); // 幅1マスの通路だけ
            b.Grow(b.Rule.MaxEcologyTicks);
            Assert.IsFalse(b.State.Monsters.Any(m => m.Species.Id == "shadow_bat"));
        }

        [Test]
        public void 墓場_骨が積もるとドクロ魔導士が起き上がり骨が減る()
        {
            var b = RoomDungeon();
            var p = new GridPos(b.Map.Entrance.X, b.Map.Goal.Y - 1);
            int need = Sp("skull_mage").SpecialBones;
            b.Map.SetBones(p.X, p.Y, need);
            b.Grow(400);
            Assert.IsTrue(b.State.Monsters.Any(m => m.Species.Id == "skull_mage"), "ドクロ魔導士が生まれている");
            Assert.Less(b.Map.GetBones(p.X, p.Y), need, "骨が使われた");
        }

        [Test]
        public void 墓場_モンスターが死ぬと通路に骨が残る()
        {
            var b = TestData.BuildTypical(_data, 1, 3);
            var s = b.State;
            s.ClearMonsters();
            var pos = new GridPos(b.Map.Entrance.X, 4);
            var m = s.AddMonster(Sp("moss_slime"), pos, 4, 0);
            m.Age = m.Species.Lifespan - 1;
            new EcologySimulation(s).Step();
            Assert.AreEqual(1, b.Map.GetBones(pos.X, pos.Y));

            // 骨はスナップショットに保存され、読み込んでも残る
            var restored = DungeonSnapshotConverter.ToState(DungeonStorage.LoadSnapshot(GameJson.Serialize(b.ToSnapshot())), _data);
            Assert.AreEqual(1, restored.Map.GetBones(pos.X, pos.Y));
        }

        [Test]
        public void 進化_育ち切ったコケスライムはヒカリダケになる()
        {
            var b = RoomDungeon();
            var slime = Sp("moss_slime");
            var m = b.State.AddMonster(slime, new GridPos(b.Map.Entrance.X, 3), slime.ReproduceThreshold, 0);
            m.Age = slime.EvolveAge;
            m.Cooldown = 0;
            int id = m.Id;
            new EcologySimulation(b.State, b.Rule, _data.Monsters).Step();
            var after = b.State.FindById(id);
            Assert.IsNotNull(after);
            Assert.AreEqual("glow_shroom", after.Species.Id);
        }

        [Test]
        public void 特殊な生まれ方の種族は土を掘っても生まれない()
        {
            // どんなに濃い土でも、カゲコウモリ・ドクロ魔導士・冥竜は掘削では生まれない
            for (int n = 0; n <= 20; n += 2)
            for (int mana = 0; mana <= 20; mana += 2)
            {
                var sp = MonsterSpawner.Choose(_data.Monsters, n, mana);
                if (sp == null) continue;
                Assert.IsTrue(sp.DigSpawn, $"養分{n} 魔分{mana} で {sp.Id}");
                Assert.IsFalse(sp.IsBoss);
            }
        }

        [Test]
        public void 土の中にいるモンスターや未知の種族は弾かれる()
        {
            var b = TestData.BuildTypical(_data, 1, 4);
            var snap = b.ToSnapshot();
            snap.Monsters.Clear();
            snap.Monsters.Add(new MonsterSnapshot { Id = 1, Species = "moss_slime", X = 1, Y = 1, Nutrient = 4, Hp = 10 });
            Assert.IsTrue(DungeonStorage.TrySave(snap, _data, out _).Has(ValidationCode.InvalidMonster));

            snap.Monsters[0].Species = "unknown_monster";
            Assert.IsTrue(DungeonStorage.TrySave(snap, _data, out _).Has(ValidationCode.FormatError));
        }
    }
}
