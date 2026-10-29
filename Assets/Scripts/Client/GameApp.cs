using System.Collections.Generic;
using DungeonGame.Client.Art;
using DungeonGame.Core.Data;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Skills;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>
    /// ゲーム全体の画面を管理する(タイトル・魔王モード・勇者モード・リプレイ)。
    /// シーンに何も置かなくても、起動時に自動で生成される。
    /// 画面は IMGUI + ドット絵で描く。ロジックは DungeonGame.Core を呼ぶだけで、ここには持たない。
    /// 画面ごとの処理は GameApp.Demon.cs / GameApp.Hero.cs / GameApp.Replay.cs / GameApp.Title.cs に分けている。
    /// </summary>
    public partial class GameApp : MonoBehaviour
    {
        private enum Screen { Title, Demon, HeroSetup, Run, Replay, Book }

        /// <summary>画面設計の基準解像度。実際の画面に合わせて拡大縮小する。</summary>
        /// <remarks>縦長の画面(スマホ縦向きなど)では 720x1280 を基準にし、操作パネルを上・マップを下に置く。</remarks>
        private const float RefWidth = 1280f, RefHeight = 720f;
        private const float PanelWidth = 340f;
        private bool _portrait;

        private GameData _data;
        private string _loadError;
        private SimulationDriver _driver;
        private readonly MapRenderer _map = new MapRenderer();
        private Screen _screen = Screen.Title;
        private float _vw, _vh; // 仮想画面サイズ
        private Vector2 _panelScroll;

        // 攻略の表示用(どのモードでも共通)
        private RaidSimulation _shownRaid;
        private readonly Dictionary<int, string> _monsterNames = new Dictionary<int, string>();
        private readonly List<string> _eventLog = new List<string>();

        // 画面中央に大きく出す見出し(「第1層 突入!」など)
        private string _banner, _bannerSub;
        private float _bannerStart = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<GameApp>() != null) return;
            var go = new GameObject("GameApp");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true; // Unity やブラウザのタブが裏に回っても止めない
            _driver = gameObject.AddComponent<SimulationDriver>();
            _driver.AfterTick += OnAfterTick;
            Audio.AudioManager.Create(gameObject);
            Ui.OnClick = () => Audio.AudioManager.Instance?.Play("click");
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.04f, 0.08f);
            }
            try
            {
                _data = GameDataLoader.Load();
            }
            catch (System.Exception e)
            {
                _loadError = e.Message;
                Debug.LogException(e);
            }
        }

        private void OnGUI()
        {
            Ui.ApplySkin();
            _portrait = UnityEngine.Screen.height > UnityEngine.Screen.width;
            float refW = _portrait ? RefHeight : RefWidth, refH = _portrait ? RefWidth : RefHeight;
            float scale = Mathf.Max(0.5f, Mathf.Min(UnityEngine.Screen.width / refW, UnityEngine.Screen.height / refH));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _vw = UnityEngine.Screen.width / scale;
            _vh = UnityEngine.Screen.height / scale;

            if (Event.current.type == EventType.Layout && _loadError == null) Audio.AudioManager.Instance?.PlayBgm(DesiredBgm());
            Background();
            if (_loadError != null)
            {
                Ui.Window(new Rect(20, 20, _vw - 40, 200), "エラー");
                GUI.Label(new Rect(40, 50, _vw - 80, 160), "ゲームデータの読み込みに失敗しました:\n" + _loadError, Ui.Wrap);
                return;
            }

            // 遊び方ウィンドウを開いている間は、後ろの画面を操作できないようにする
            GUI.enabled = !_helpOpen;
            switch (_screen)
            {
                case Screen.Title: DrawTitle(); break;
                case Screen.Demon: DrawDemon(); break;
                case Screen.HeroSetup: DrawHeroSetup(); break;
                case Screen.Run: DrawRun(); break;
                case Screen.Replay: DrawReplay(); break;
                case Screen.Book: DrawBook(); break;
            }
            GUI.enabled = true;
            SoundButton();
            DrawBanner();
            DrawHelp();
        }

        /// <summary>画面全体の背景(地中の暗がり)。</summary>
        private void Background()
        {
            if (Event.current.type != EventType.Repaint) return;
            int bands = 8;
            for (int i = 0; i < bands; i++)
                Ui.Fill(new Rect(0, _vh * i / bands, _vw, _vh / bands + 1),
                    Color.Lerp(new Color(0.09f, 0.06f, 0.12f), new Color(0.03f, 0.02f, 0.05f), i / (float)bands));
        }

        private void GoTitle()
        {
            _driver.Simulation = null;
            _shownRaid = null;
            _screen = Screen.Title;
        }

        // ================= 共通:レイアウト =================

        /// <summary>操作パネルの領域(横長:左、縦長:上)。</summary>
        private const float PortraitPanelRatio = 0.36f;
        private Rect PanelRect => _portrait ? new Rect(6, 14, _vw - 12, _vh * PortraitPanelRatio - 14) : new Rect(6, 14, PanelWidth, _vh - 20);

        /// <summary>マップを描く領域(パネル以外)。</summary>
        private Rect MapArea => _portrait
            ? new Rect(4f, _vh * PortraitPanelRatio + 6f, _vw - 8f, _vh * (1f - PortraitPanelRatio) - 10f)
            : new Rect(PanelWidth + 14f, 6f, _vw - PanelWidth - 18f, _vh - 12f);

        /// <summary>マップ領域の中だけで使う座標(GUI.BeginGroup の内側)。</summary>
        private Rect MapLocal => new Rect(0, 0, MapArea.width, MapArea.height);

        /// <summary>レトロ枠のスクロールできる操作パネルを描く。</summary>
        private void Panel(string title, System.Action<Column> content, float contentHeight = 1400f)
        {
            var pr = PanelRect;
            Ui.Window(pr, title);
            var inner = new Rect(pr.x + 8, pr.y + 18, pr.width - 16, pr.height - 26);
            var view = new Rect(0, 0, inner.width - 16f, contentHeight);
            _panelScroll = GUI.BeginScrollView(inner, _panelScroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            content(new Column(16f, 6f, Mathf.Min(inner.width - 40f, 560f)));
            GUI.EndScrollView();
        }

        /// <summary>速度ボタン(一時停止・等速・2倍・4倍・8倍)。</summary>
        private void SpeedButtons(Column col)
        {
            int[] speeds = { 0, 100, 200, 400, 800 };
            int selected = _driver.Paused ? 0 : System.Array.IndexOf(speeds, _driver.SpeedPercent);
            int c = col.Buttons(new[] { "||", "x1", "x2", "x4", "x8" }, 28f, selected);
            if (c == 0) _driver.Paused = true;
            else if (c > 0) { _driver.Paused = false; _driver.SpeedPercent = speeds[c]; }
        }

        /// <summary>画面中央に大きな見出しを2秒ほど出す。</summary>
        private void ShowBanner(string text, string sub = null)
        {
            _banner = text;
            _bannerSub = sub;
            _bannerStart = Time.unscaledTime;
        }

        private void DrawBanner()
        {
            float t = Time.unscaledTime - _bannerStart;
            if (_banner == null || t > 2.2f || Event.current.type != EventType.Repaint) return;
            // 左から滑り込み、止まって、右へ抜ける
            float x = t < 0.3f ? Mathf.Lerp(-_vw, 0, t / 0.3f) : t > 1.9f ? Mathf.Lerp(0, _vw, (t - 1.9f) / 0.3f) : 0;
            var area = _screen == Screen.Title ? new Rect(0, 0, _vw, _vh) : MapArea;
            var r = new Rect(area.x + x, area.center.y - 46, area.width, 92);
            Ui.Fill(r, new Color(0, 0, 0, 0.7f));
            Ui.Fill(new Rect(r.x, r.y, r.width, 3), Ui.Gold);
            Ui.Fill(new Rect(r.x, r.yMax - 3, r.width, 3), Ui.Gold);
            var style = Ui.Huge;
            int old = style.fontSize;
            style.fontSize = 40;
            Ui.Text(new Rect(r.x, r.y + 4, r.width, 56), _banner, style, Ui.Gold);
            style.fontSize = old;
            if (_bannerSub != null) Ui.Text(new Rect(r.x, r.y + 56, r.width, 30), _bannerSub, Ui.Center, Ui.Cream);
        }

        // ================= 共通:攻略の表示 =================

        /// <summary>表示する攻略を切り替える(モンスター名の表などを作り直す)。</summary>
        private void ShowRaid(RaidSimulation raid)
        {
            if (_shownRaid == raid) return;
            _shownRaid = raid;
            _map.Reset();
            _eventLog.Clear();
            _monsterNames.Clear();
            if (raid == null) return;
            foreach (var m in raid.Dungeon.Monsters) _monsterNames[m.Id] = m.Species.Name;
            _eventLog.Add($"{raid.Rule.Name} に入った");
            // 勇者側(霧あり)は少し拡大してパーティを追いかける。魔王のテスト防衛は全体表示。
            if (_screen == Screen.Demon) _map.Zoom = 1f;
            else AutoZoom(raid.Dungeon.Map, 56f);
            ShowBanner($"第{raid.Rule.Layer}層 突入!", raid.Rule.Name + (raid.Boss != null ? " ― 魔王が待ち構えている…" : ""));
        }

        private void OnAfterTick()
        {
            if (_screen == Screen.Demon && _grow != null) TrackDemonBirths();
            var raid = CurrentlyDrivenRaid();
            if (raid == null || raid != _shownRaid) return;
            _map.OnEvents(raid.Events, raid.Heroes);
            PlayRaidSounds(raid);
            foreach (var e in raid.Events)
            {
                if (e.Kind == RaidEventKind.InterventionHeal) _map.HealAll(raid.Heroes);
                if (e.Kind == RaidEventKind.Cleared) ShowBanner(raid.Boss != null ? "魔王撃破!!" : "突破!", "勇者たちは先へ進む");
                if (e.Kind == RaidEventKind.Wiped) ShowBanner("全滅…", "勇者たちは力尽きた");
                if (e.Kind == RaidEventKind.TimeUp) ShowBanner("時間切れ…", "勇者たちは撤退した");
                string line = DescribeEvent(raid, e);
                if (line == null) continue;
                _eventLog.Add(line);
                if (_eventLog.Count > 60) _eventLog.RemoveAt(0);
            }
        }

        private RaidSimulation CurrentlyDrivenRaid()
        {
            switch (_screen)
            {
                case Screen.Demon: return _testRaid;
                case Screen.Run: return _run?.CurrentRaid;
                case Screen.Replay: return _replayer?.Run?.CurrentRaid;
                default: return null;
            }
        }

        private string DescribeEvent(RaidSimulation raid, RaidEvent e)
        {
            string hero = e.HeroId > 0 && e.HeroId <= raid.Heroes.Count ? raid.Heroes[e.HeroId - 1].Class.Name : "";
            _monsterNames.TryGetValue(e.MonsterId, out string mon);
            float sec = e.Tick / 10f;
            switch (e.Kind)
            {
                case RaidEventKind.MonsterKilled: return $"{sec:0.0}s {hero}は {mon}を たおした!";
                case RaidEventKind.HeroDown: return $"{sec:0.0}s {hero}は {mon}に やられた…";
                case RaidEventKind.InterventionHeal: return $"{sec:0.0}s 回復の祈り! (+{e.Amount})";
                case RaidEventKind.InterventionTorch: return $"{sec:0.0}s たいまつ! {e.Amount}マス見えた";
                case RaidEventKind.Cleared: return $"{sec:0.0}s ★ 層を突破した!";
                case RaidEventKind.Wiped: return $"{sec:0.0}s 勇者たちは全滅した…";
                case RaidEventKind.TimeUp: return $"{sec:0.0}s 時間切れで撤退した…";
                default: return null;
            }
        }

        /// <summary>攻略中の勇者の顔ぶれ(ドット絵・HP)をパネルに表示する。</summary>
        private void RaidStatus(Column col, RaidSimulation raid)
        {
            var info = col.Next(22f, 2f);
            Ui.Text(info, raid.Rule.Name, Ui.Label, Ui.Gold);
            var timeRow = col.Next(20f, 4f);
            Ui.IconText(timeRow, Sprites.Get("icon_hourglass"), $"{raid.CurrentTick / 10f:0.0} / {raid.Rule.RaidTimeLimitTicks / 10}秒", Ui.Small);
            Ui.Bar(new Rect(timeRow.x + 150, timeRow.y + 6, timeRow.width - 150, 8),
                raid.Rule.RaidTimeLimitTicks - raid.CurrentTick, raid.Rule.RaidTimeLimitTicks, Ui.Sky);
            if (raid.Boss != null)
            {
                var br = col.Next(24f, 4f);
                Ui.Sprite(new Rect(br.x, br.y, 22, 22), Sprites.Anim("demon_lord"));
                Ui.Text(new Rect(br.x + 26, br.y, 60, 22), "魔王", Ui.Label, Ui.Red);
                Ui.Bar(new Rect(br.x + 70, br.y + 6, br.width - 70, 12), raid.Boss.Hp, raid.Boss.CombatMaxHp, Ui.Red);
            }
            foreach (var h in raid.Heroes)
            {
                var r = col.Next(42f, 3f);
                Ui.Fill(r, new Color(0, 0, 0, 0.25f));
                Ui.Portrait(new Rect(r.x + 1, r.y + 1, 40, 40), h.Class.Id, Ui.PortraitColor(h.Class.Id), h.Id * 3, h.Dead);
                if (h.Dead) Ui.Sprite(new Rect(r.x + 12, r.y + 12, 20, 20), Sprites.Get("icon_skull"));
                string state = h.Dead ? "戦闘不能" : h.Entered ? $"撃破 {h.Kills}" : "入口で待機";
                Ui.Text(new Rect(r.x + 46, r.y + 1, 120, 20), h.Class.Name, Ui.Label, Ui.Cream);
                Ui.Text(new Rect(r.x + 130, r.y + 3, r.width - 130, 18), state, Ui.Small, Ui.Sub);
                Ui.Bar(new Rect(r.x + 46, r.y + 24, r.width - 120, 12), h.Hp, h.Stats.MaxHp);
                Ui.Text(new Rect(r.xMax - 70, r.y + 21, 70, 18), $"{h.Hp}/{h.Stats.MaxHp}", Ui.Small, Ui.Cream);
            }
        }

        /// <summary>できごとの記録(メッセージウィンドウ風)。</summary>
        private void EventLog(Column col, int lines = 8)
        {
            var r = col.Next(22f * lines + 16f);
            Ui.Fill(r, new Color(0, 0, 0, 0.35f));
            Ui.Frame(r, new Color(1, 1, 1, 0.2f));
            int start = Mathf.Max(0, _eventLog.Count - lines);
            float y = r.y + 6;
            for (int i = _eventLog.Count - 1; i >= start; i--)
            {
                float alpha = 1f - (_eventLog.Count - 1 - i) * 0.08f;
                Ui.Text(new Rect(r.x + 8, y, r.width - 16, 22), _eventLog[i], Ui.Small, new Color(1, 1, 1, alpha), outline: false);
                y += 22f;
            }
        }

        /// <summary>攻略中のマップを描く(勇者側は霧あり、魔王側は霧なし)。</summary>
        /// <param name="inside">マップ領域の中で追加で描く処理(たいまつの範囲表示など)。</param>
        private void DrawRaidMap(RaidSimulation raid, bool useFog, System.Action inside = null)
        {
            var map = raid.Dungeon.Map;
            BeginMapView(map);
            if (useFog)
            {
                // 拡大しているときは先頭の勇者(いなければ入口)を追いかける
                Hero lead = null;
                foreach (var h in raid.Heroes) if (h.Active) { lead = h; break; }
                var p = lead != null ? lead.Pos : map.Entrance;
                _map.FollowTarget(new Vector2(p.X, p.Y));
            }
            _map.DrawSurface(map);
            var fog = useFog ? raid.Fog : null;
            _map.DrawTiles(map, fog, showResources: !useFog, raid.Dungeon.Monsters);
            _map.DrawMonsters(raid.Dungeon.Monsters, fog, inRaid: true);
            _map.DrawHeroes(raid.Heroes);
            _map.DrawWaitingHeroes(raid.Heroes, map);
            _map.DrawFx();
            inside?.Invoke();
            EndMapView(showFollow: useFog);
        }

        /// <summary>
        /// マップ領域の描画を始める。領域の外にはみ出した部分は切り取られる(拡大したとき用)。
        /// 終わったら必ず EndMapView を呼ぶ。
        /// </summary>
        private void BeginMapView(Core.Dungeon.DungeonMap map)
        {
            var area = MapArea;
            if (Event.current.type == EventType.Repaint)
            {
                Ui.Fill(new Rect(area.x - 3, area.y - 3, area.width + 6, area.height + 6), Ui.Ink);
                Ui.Frame(new Rect(area.x - 3, area.y - 3, area.width + 6, area.height + 6), new Color(1, 1, 1, 0.25f), 2f);
            }
            GUI.BeginGroup(area);
            if (!_helpOpen) _map.HandleViewInput(MapLocal);
            _map.Layout(MapLocal, map);
        }

        private void EndMapView(bool showFollow)
        {
            string hint = CurrentHint();
            if (!string.IsNullOrEmpty(hint)) HintBar(MapLocal, hint);
            _map.DrawViewControls(MapLocal, showFollow);
            GUI.EndGroup();
        }

        /// <summary>マップの左上に「次にやること」を表示する。</summary>
        private void HintBar(Rect local, string text)
        {
            var r = new Rect(local.x + 8, local.y + 8, Mathf.Min(local.width - 150f, 620f), 50f);
            Ui.Fill(r, new Color(0.05f, 0.04f, 0.1f, 0.82f));
            Ui.Frame(r, Ui.Gold, 2f);
            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 2f;
            Ui.Sprite(new Rect(r.x + 6, r.y + 9 + bob, 32, 32), Sprites.Get("icon_star"));
            var style = Ui.Body;
            var old = style.normal.textColor;
            style.normal.textColor = Ui.Cream;
            GUI.Label(new Rect(r.x + 44, r.y + 4, r.width - 50, r.height - 8), text, style);
            style.normal.textColor = old;
        }

        /// <summary>拡大率を「1マスがだいたい px ピクセル」になるよう決める(勇者側の見やすさ用)。</summary>
        private void AutoZoom(Core.Dungeon.DungeonMap map, float px)
        {
            var a = MapArea;
            float fit = Mathf.Min(a.width / map.Width, a.height / (map.Height + MapRenderer.SurfaceRows));
            _map.SetZoom(px / Mathf.Max(1f, fit));
            _map.Follow = true;
            _map.Focus = new Vector2(map.Entrance.X, map.Entrance.Y);
        }

        /// <summary>マップ上でクリック(またはドラッグ)されたマス。BeginMapView〜EndMapView の間で呼ぶ。</summary>
        private GridPosEvent MapClick()
        {
            var e = Event.current;
            if (_helpOpen) return default;
            if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || e.button != 0) return default;
            if (!MapLocal.Contains(e.mousePosition)) return default;
            var p = _map.GridAt(e.mousePosition);
            if (p == null) return default;
            e.Use();
            return new GridPosEvent { Clicked = true, Pos = p.Value, IsDrag = e.type == EventType.MouseDrag };
        }

        private struct GridPosEvent
        {
            public bool Clicked;
            public bool IsDrag;
            public Core.Dungeon.GridPos Pos;
        }

        /// <summary>JSONを表示してコピーできる欄。</summary>
        private string JsonBox(Column col, string json, float height = 160f)
        {
            var r = col.Next(height);
            string result = GUI.TextArea(r, json ?? "", Ui.TextArea);
            if (col.Button("クリップボードにコピー", height: 28f)) GUIUtility.systemCopyBuffer = json ?? "";
            return result;
        }

        // ================= 共通:アイコン =================

        private static readonly string[] StatIconIds = { "icon_heart", "icon_sword", "icon_shield", "icon_boot", "icon_orb" };

        private static Texture2D StatIcon(StatKind k) => Sprites.Get(StatIconIds[(int)k]);

        /// <summary>スキルの見た目のアイコン(最初の効果で決める)。</summary>
        private static Texture2D SkillIcon(SkillDefinition def)
        {
            if (def.Effects.Count == 0) return Sprites.Get("icon_star");
            var e = def.Effects[0];
            switch (e.Type)
            {
                case SkillEffectType.StatFlat:
                case SkillEffectType.StatPercent: return StatIcon(e.Stat);
                case SkillEffectType.CritBonus: return Sprites.Get("icon_star");
                case SkillEffectType.VisionBonus:
                case SkillEffectType.ExtraCharge: return Sprites.Get("icon_torch");
                case SkillEffectType.Regen: return Sprites.Get("icon_leaf");
                case SkillEffectType.Lifesteal: return Sprites.Get("icon_drop");
                case SkillEffectType.Thorns: return Sprites.Get("icon_shield");
                case SkillEffectType.Revive: return Sprites.Get("icon_skull");
                default: return Sprites.Get("icon_heart");
            }
        }
    }
}
