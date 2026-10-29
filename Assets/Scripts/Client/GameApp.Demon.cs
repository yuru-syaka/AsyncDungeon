using System.Linq;
using DungeonGame.Client.Art;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Serialization;
using DungeonGame.Core.Simulation;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>魔王モード:掘る・育てる・テスト防衛・保存。魔王がひとことしゃべる。</summary>
    public partial class GameApp
    {
        private DungeonBuilder _builder;
        private int _demonLayer = 1;
        private RaidSimulation _testRaid;
        private string _demonJson;
        private GrowSimulation _grow;
        private int _digCount;

        // 魔王のセリフ(1文字ずつ表示する)
        private string _speech = "";
        private float _speechStart;

        private static readonly string[] DigLines =
        {
            "掘れ掘れ~!", "ふむ、良い土だ。", "我が城が広がってゆく…", "もっと深く…もっと暗く…", "ククク、いい穴だ。",
        };

        /// <summary>生態系を画面で見せながら指定ティック数だけ育てるためのラッパー。</summary>
        private sealed class GrowSimulation : ITickSimulation
        {
            private readonly DungeonBuilder _b;
            private int _remaining;
            public GrowSimulation(DungeonBuilder b, int ticks) { _b = b; _remaining = ticks; }
            public int CurrentTick => _b.State.EcologyTick;
            public bool IsFinished => _remaining <= 0 || _b.RemainingEcologyTicks <= 0;
            public void Step() { if (_b.GrowStep()) _remaining--; else _remaining = 0; }
        }

        private void Say(string text)
        {
            if (text == _speech) return;
            _speech = text;
            _speechStart = Time.unscaledTime;
        }

        private void EnterDemon()
        {
            _screen = Screen.Demon;
            if (_builder == null) NewDungeon(_demonLayer);
            OpenHelpFirstTime("demon", DemonHelp);
        }

        private void NewDungeon(int layer)
        {
            _demonLayer = layer;
            ulong seed = (ulong)System.DateTime.Now.Ticks; // 新しい土の配置(ここは毎回違ってよい)
            _builder = DungeonBuilder.CreateNew(_data, layer, seed);
            StopDemonSimulation();
            _map.Reset();
            _demonJson = null;
            _digCount = 0;
            _demonSpecies.Clear();
            _map.Zoom = 1f;
            Say($"よく来た、新米魔王よ。ここは{_builder.Rule.Name}。入口から掘り進め、勇者どもを迎え撃つのだ!");
        }

        private void StopDemonSimulation()
        {
            _driver.Simulation = null;
            _grow = null;
            _testRaid = null;
            ShowRaid(null);
        }

        private bool DemonBusy => _grow != null && !_grow.IsFinished;

        private void DrawDemon()
        {
            if (_testRaid != null) { DrawTestDefense(); return; }
            if (_grow != null && _grow.IsFinished)
            {
                _grow = null;
                Say($"命が巡る…ククク。モンスターは今 {_builder.State.Monsters.Count}匹だ。");
            }

            var b = _builder;
            var rule = b.Rule;
            Panel("魔王の部屋", col =>
            {
                DemonPortrait(col);
                HelpButton(col);

                int pick = col.Buttons(_data.Rules.Layers.Select(l => $"{l.Layer}層").ToArray(), 30f, _demonLayer - 1, !DemonBusy);
                if (pick >= 0) NewDungeon(pick + 1);
                col.Space(4);

                Gauge(col, Sprites.Get("icon_pickaxe"), "掘削", b.Map.DugCount, rule.DigBudget, new Color(0.85f, 0.6f, 0.3f));
                Gauge(col, Sprites.Get("icon_leaf"), "育成", b.State.EcologyTick / 10, rule.MaxEcologyTicks / 10, Ui.Green, "秒");
                Ui.Text(col.Next(18f, 4f), $"土の豊かさ:養分 最大{rule.SoilNutrientMax}・魔分 最大{rule.SoilManaMax} / ゴール:{(rule.Goal == GoalType.Stairs ? "階段" : "魔王の玉座")}", Ui.Small, Ui.Sub, false);

                // 手下の一覧
                var groups = b.State.Monsters.GroupBy(m => m.Species).OrderBy(g => g.Key.Tier).ToList();
                var list = col.Next(Mathf.Max(30f, 26f * groups.Count + 8f));
                Ui.Fill(list, new Color(0, 0, 0, 0.3f));
                if (groups.Count == 0)
                    Ui.Text(new Rect(list.x + 8, list.y + 6, list.width, 18), "手下はまだいない。濃い土を掘れ!", Ui.Small, Ui.Sub, false);
                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    var r = new Rect(list.x + 6, list.y + 4 + i * 26, list.width - 12, 24);
                    Ui.Sprite(new Rect(r.x, r.y, 24, 24), Sprites.Anim(g.Key.Id, i * 2));
                    Ui.Text(new Rect(r.x + 30, r.y + 2, r.width - 90, 22), $"{g.Key.Name}", Ui.Label, Ui.Cream);
                    Ui.Text(new Rect(r.xMax - 60, r.y + 2, 60, 22), $"×{g.Count()}", Ui.Label, Ui.Gold);
                }
                col.Space(4);

                Ui.Text(col.Next(18f, 2f), "育てる(生態系を動かす)", Ui.Small, Ui.Cream, false);
                int g2 = col.Buttons(new[] { "10秒", "60秒", "全部" }, 30f, -1, !DemonBusy && b.RemainingEcologyTicks > 0);
                if (g2 >= 0) StartGrow(g2 == 0 ? 100 : g2 == 1 ? 600 : b.RemainingEcologyTicks);
                if (DemonBusy && col.Button("育成を止める", height: 28f)) { StopDemonSimulation(); Say("ふむ、今はこのくらいにしておくか。"); }

                if (col.Button("テスト防衛(勇者を送り込む)", !DemonBusy, 36f, Sprites.Get("icon_sword"), new Color(0.55f, 0.15f, 0.2f))) StartTestDefense();
                if (col.Button("10回まとめて試す", !DemonBusy, 28f)) QuickTestDefense();
                int s = col.Buttons(new[] { "保存", "読込", "JSON" }, 30f, -1, !DemonBusy);
                if (s == 0) SaveDungeon();
                else if (s == 1) LoadDungeon();
                else if (s == 2) _demonJson = _demonJson == null ? GameJson.Serialize(b.ToSnapshot("my_dungeon", "自作ダンジョン", "あなた")) : null;
                if (_demonJson != null) JsonBox(col, _demonJson);
                col.Space(4);
                HoverInfo(col);
                if (col.Button("タイトルへ", height: 28f)) GoTitle();
                Ui.Text(col.Next(18f), "土の模様:緑の苔=養分 青い結晶=魔分", Ui.Small, Ui.Sub, false);
            });

            BeginMapView(b.Map);
            _map.DrawSurface(b.Map);
            _map.DrawTiles(b.Map, null, showResources: true, b.State.Monsters);
            // 作成中は手下が自分の周りをうろうろ歩く(見た目だけ。育成中はロジックどおりに動くので止める)
            _map.DrawMonsters(b.State.Monsters, null, inRaid: false, wanderMap: DemonBusy ? null : b.Map);
            bool inMap = MapLocal.Contains(Event.current.mousePosition);
            var hover = inMap ? _map.GridAt(Event.current.mousePosition) : null;
            if (hover != null && !DemonBusy && !_helpOpen) _map.DrawDigCursor(hover.Value, b.Map.CanDig(hover.Value.X, hover.Value.Y) == DigError.None);
            _map.DrawFx();
            var click = MapClick();
            if (click.Clicked && !DemonBusy) Dig(click.Pos, click.IsDrag);
            _hoverTile = hover;
            EndMapView(showFollow: false);
        }

        /// <summary>マウスが乗っているマス(マップの外なら null)。</summary>
        private GridPos? _hoverTile;

        /// <summary>魔王の顔と吹き出し。セリフは1文字ずつ出る。</summary>
        private void DemonPortrait(Column col)
        {
            var r = col.Next(92f, 8f);
            var face = new Rect(r.x, r.y + 6, 76, 76);
            Ui.Portrait(face, "demon_lord", Ui.PortraitColor("demon_lord"));
            // 吹き出し
            var bubble = new Rect(face.xMax + 12, r.y, r.width - face.width - 12, r.height);
            Ui.Fill(bubble, Ui.Cream);
            Ui.Frame(bubble, Ui.Ink, 2f);
            Ui.Fill(new Rect(bubble.x - 8, bubble.y + 30, 10, 10), Ui.Cream);
            int shown = Mathf.Min(_speech.Length, (int)((Time.unscaledTime - _speechStart) * 30f));
            var style = Ui.Wrap;
            var oldColor = style.normal.textColor;
            style.normal.textColor = Ui.Ink;
            GUI.Label(new Rect(bubble.x + 8, bubble.y + 6, bubble.width - 16, bubble.height - 12), _speech.Substring(0, shown), style);
            style.normal.textColor = oldColor;
        }

        private static void Gauge(Column col, Texture2D icon, string label, int value, int max, Color color, string unit = "")
        {
            var r = col.Next(22f, 4f);
            Ui.Sprite(new Rect(r.x, r.y + 1, 20, 20), icon);
            Ui.Text(new Rect(r.x + 24, r.y, 50, 22), label, Ui.Label, Ui.Cream);
            Ui.Bar(new Rect(r.x + 74, r.y + 5, r.width - 160, 12), value, max, color);
            Ui.Text(new Rect(r.xMax - 82, r.y + 2, 82, 20), $"{value}/{max}{unit}", Ui.Small, Ui.Cream);
        }

        private static string TierName(int tier) => tier <= 1 ? "下位" : tier == 2 ? "中位" : tier == 3 ? "上位" : "最上位";

        private void HoverInfo(Column col)
        {
            var p = _hoverTile;
            var r = col.Next(44f);
            Ui.Fill(r, new Color(0, 0, 0, 0.3f));
            if (p == null)
            {
                Ui.Text(new Rect(r.x + 8, r.y + 12, r.width, 20), "マップにマウスを乗せると土の中身が見える", Ui.Small, Ui.Sub, false);
                return;
            }
            var map = _builder.Map;
            int x = p.Value.X, y = p.Value.Y;
            Ui.IconText(new Rect(r.x + 6, r.y + 2, 120, 20), Sprites.Get("icon_leaf"), $"養分 {map.GetNutrient(x, y)}", Ui.Small);
            Ui.IconText(new Rect(r.x + 120, r.y + 2, 120, 20), Sprites.Get("icon_drop"), $"魔分 {map.GetMana(x, y)}", Ui.Small);
            var m = _builder.State.MonsterAt(p.Value);
            string line = m != null ? $"{m.Species.Name}  体 {m.Biomass}/{m.Species.MaxBiomass}  {m.Age / 10}秒" : TileName(map.GetTile(x, y));
            Ui.Text(new Rect(r.x + 8, r.y + 22, r.width - 16, 20), line, Ui.Small, Ui.Cream, false);
        }

        private static string TileName(TileType t) => t switch
        {
            TileType.Bedrock => "岩盤(掘れない)",
            TileType.Soil => "土",
            TileType.Floor => "通路",
            TileType.Entrance => "入口",
            _ => "ゴール",
        };

        private void Dig(GridPos p, bool isDrag)
        {
            var error = _builder.Map.CanDig(p.X, p.Y);
            if (error != DigError.None)
            {
                if (!isDrag) Sound?.Play("deny");
                if (!isDrag) Say(error switch
                {
                    DigError.NotConnected => "そこはまだ掘れぬ。通路か入口の隣から掘るのだ。",
                    DigError.BudgetExceeded => "掘る力が尽きた…あとは育てて守りを固めよ。",
                    DigError.NotSoil => "そこは土ではないわ!",
                    _ => "そこは掘れぬ。",
                });
                return;
            }
            var outcome = _builder.Dig(p.X, p.Y);
            Sound?.Play("dig", 0.04f, 0.12f);
            if (outcome.Spawned != null) Sound?.Play("spawn", 0.05f);
            _demonJson = null;
            _digCount++;
            var gp = new Vector2(p.X, p.Y);
            _map.Fx.Burst(gp, new Color(0.55f, 0.38f, 0.22f), 10, 3f);
            if (outcome.Spawned != null)
            {
                _map.Fx.Burst(gp, Ui.Gold, 16, 4f, 0.1f);
                _map.Fx.Popup(gp, "誕生!", Ui.Gold, 1.1f);
                Say($"{outcome.Spawned.Species.Name}が生まれおったわ!よしよし。");
            }
            else if (_builder.Map.RemainingBudget == 0)
                Say("掘る力が尽きた…あとは育てて守りを固めよ。");
            else if (_digCount % 5 == 1)
                Say(DigLines[_digCount / 5 % DigLines.Length]);
        }

        private void StartGrow(int ticks)
        {
            _grow = new GrowSimulation(_builder, ticks);
            _driver.Simulation = _grow;
            _driver.Paused = false;
            _driver.SpeedPercent = 2000; // 20倍速で見せる
            _demonJson = null;
            Say("育て、育て…食うがよい、増えるがよい…");
            Sound?.Play("grow");
        }

        private void StartTestDefense()
        {
            var v = _builder.Validate();
            if (!v.IsValid) { Say("まだ勇者を呼べぬ:" + Describe(v)); Sound?.Play("deny"); return; }
            _testRaid = _builder.CreateTestDefense((ulong)System.DateTime.Now.Ticks);
            ShowRaid(_testRaid);
            _driver.Simulation = _testRaid;
            _driver.Paused = false;
            _driver.SpeedPercent = 200;
            Say("来たな勇者ども…我が手下の力、見せてくれるわ!");
        }

        private void QuickTestDefense()
        {
            var v = _builder.Validate();
            if (!v.IsValid) { Say("まだ勇者を呼べぬ:" + Describe(v)); Sound?.Play("deny"); return; }
            int cleared = 0;
            for (ulong s = 1; s <= 10; s++)
                if (_builder.RunTestDefense(s).Outcome == RaidOutcome.Cleared) cleared++;
            Say($"勇者10組を送り込んだ結果、突破されたのは{cleared}組。" +
                (cleared == 0 ? "完璧な守りだ!フハハハ!" : cleared <= 3 ? "なかなかやるではないか。" : "ぐぬぬ…まだまだ甘いわ。"));
        }

        private void DrawTestDefense()
        {
            var raid = _testRaid;
            if (raid.IsFinished && _speechStart < Time.unscaledTime - 0.1f && !_speech.StartsWith("結果"))
                Say(raid.Outcome == RaidOutcome.Cleared ? "結果:ぐぬぬ…突破されてしまった!" : "結果:見たか!勇者どもを返り討ちにしたぞ!");
            Panel("テスト防衛", col =>
            {
                DemonPortrait(col);
                RaidStatus(col, raid);
                col.Space(4);
                SpeedButtons(col);
                if (col.Button("編集に戻る", height: 34f)) { StopDemonSimulation(); Say("さて、守りを見直すとしよう。"); }
                col.Space(4);
                EventLog(col, 9);
            });
            DrawRaidMap(raid, useFog: false);
        }

        private void SaveDungeon()
        {
            var snapshot = _builder.ToSnapshot($"my_l{_demonLayer}", $"自作ダンジョン{_demonLayer}層", "あなた");
            var result = DungeonStorage.TrySave(snapshot, _data, out var json);
            if (!result.IsValid)
            {
                Say("保存できぬ!" + Describe(result));
                Sound?.Play("deny");
                return;
            }
            SaveStore.Save(SaveStore.DungeonKey(_demonLayer), json);
            Say($"我がダンジョンを記録したぞ。({SaveStore.FileLocation})");
            Sound?.Play("select");
        }

        private void LoadDungeon()
        {
            string json = SaveStore.Load(SaveStore.DungeonKey(_demonLayer));
            if (json == null) { Say($"{_demonLayer}層の記録はまだないようだ。"); return; }
            try
            {
                _builder = DungeonBuilder.FromSnapshot(DungeonStorage.LoadSnapshot(json), _data);
                _demonSpecies.Clear();
                StopDemonSimulation();
                _map.Reset();
                Say("以前のダンジョンを呼び戻したぞ。");
            }
            catch (System.Exception e)
            {
                Say("読み込めぬ…" + e.Message);
            }
        }

        private static string Describe(ValidationResult r)
            => string.Join(" ", r.Errors.Select(e => e.Message));
    }
}
