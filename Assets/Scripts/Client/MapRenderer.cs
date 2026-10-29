using System.Collections.Generic;
using DungeonGame.Client.Art;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Ecology;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Raid;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>
    /// ダンジョンを「地面の断面図」として描く(IMGUI + ドット絵)。描画専用で、ロジックの状態は読むだけ。
    ///  ・マップの上に空と草地(地上)を描き、入口には地上へ抜ける穴を開ける
    ///  ・掘った通路は暗い洞窟、土には養分(苔)と魔分(結晶)を重ねて描く
    ///  ・モンスターと勇者はドット絵で、ぴょこぴょこ跳ね、進む向きに顔を向ける
    /// 表示位置はロジック上のマスへ毎フレーム少しずつ近づける(描画とロジックの分離)。
    /// </summary>
    public sealed class MapRenderer
    {
        /// <summary>マップの上に描く地上の高さ(マス数)。</summary>
        public const float SurfaceRows = 1.0f;

        public const float MinZoom = 1f, MaxZoom = 4f;

        /// <summary>拡大率(1=マップ全体が入る大きさ)。</summary>
        public float Zoom = 1f;
        /// <summary>拡大時に画面の中心に来るマス(小数可)。</summary>
        public Vector2 Focus;
        /// <summary>拡大時に勇者を自動で追いかけるか(右ドラッグで動かすと解除)。</summary>
        public bool Follow = true;

        private readonly Dictionary<int, Vector2> _monsterVis = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> _heroVis = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, bool> _monsterFlip = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _heroFlip = new Dictionary<int, bool>();
        private readonly Dictionary<int, float> _monsterFlash = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _heroFlash = new Dictionary<int, float>();
        private int _lastFrame = -1;
        private float _dt;

        private Vector2 _origin;
        private float _cell;
        private int _w, _h;
        private Rect _area;

        public float Cell => _cell;
        public Fx Fx { get; } = new Fx();

        /// <summary>別のダンジョン・別の攻略を表示するときに呼ぶ(表示位置の記憶と演出を消す)。</summary>
        public void Reset()
        {
            _monsterVis.Clear(); _heroVis.Clear();
            _monsterFlip.Clear(); _heroFlip.Clear();
            _monsterFlash.Clear(); _heroFlash.Clear();
            _monsterLunge.Clear(); _heroLunge.Clear();
            _wander.Clear();
            Fx.Clear();
        }

        /// <summary>
        /// マスの大きさと位置を決める。拡大率1で「地上+マップ」がちょうど収まり、
        /// 拡大すると Focus のマスを中心に表示する(マップの端より外は見せない)。
        /// </summary>
        public void Layout(Rect area, DungeonMap map)
        {
            _area = area;
            _w = map.Width;
            _h = map.Height;
            float fit = Mathf.Floor(Mathf.Min(area.width / _w, area.height / (_h + SurfaceRows)));
            _cell = Mathf.Max(4f, Mathf.Floor(fit * Zoom));
            float totalW = _cell * _w, totalH = _cell * (_h + SurfaceRows);

            float ox = totalW <= area.width
                ? area.x + (area.width - totalW) / 2f
                : Mathf.Clamp(area.center.x - (Focus.x + 0.5f) * _cell, area.xMax - totalW, area.x);
            float top = totalH <= area.height
                ? area.y + (area.height - totalH) / 2f
                : Mathf.Clamp(area.center.y - (Focus.y + 0.5f + SurfaceRows) * _cell, area.yMax - totalH, area.y);
            _origin = new Vector2(Mathf.Round(ox), Mathf.Round(top + SurfaceRows * _cell)) + Fx.ShakeOffset;

            if (Event.current.type == EventType.Repaint && Time.frameCount != _lastFrame)
            {
                _lastFrame = Time.frameCount;
                _dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            }
            else _dt = 0f;
        }

        /// <summary>
        /// マップ上の操作:マウスホイールで拡大縮小、右(または中)ドラッグで表示位置を動かす。
        /// Layout より前に呼ぶ。area はマップを描く領域。
        /// </summary>
        public void HandleViewInput(Rect area)
        {
            var e = Event.current;
            if (!area.Contains(e.mousePosition)) return;
            if (e.type == EventType.ScrollWheel)
            {
                SetZoom(Zoom * (e.delta.y < 0 ? 1.2f : 1f / 1.2f));
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2) && _cell > 0)
            {
                Focus -= e.delta / _cell;
                Focus = new Vector2(Mathf.Clamp(Focus.x, 0, _w - 1), Mathf.Clamp(Focus.y, -1, _h - 1));
                Follow = false;
                e.Use();
            }
        }

        public void SetZoom(float zoom) => Zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);

        /// <summary>追いかけ中なら、表示の中心を target へなめらかに寄せる。</summary>
        public void FollowTarget(Vector2 target)
        {
            if (!Follow) return;
            if (Focus == Vector2.zero) Focus = target;
            Focus = Vector2.Lerp(Focus, target, 1f - Mathf.Exp(-Mathf.Max(_dt, 0.016f) * 4f));
        }

        /// <summary>
        /// マップ右上の表示操作ボタン(拡大・縮小・全体・追従)。showFollow で「追従」ボタンを出す。
        /// </summary>
        public void DrawViewControls(Rect area, bool showFollow)
        {
            float s = 34f;
            float x = area.xMax - s - 6f;
            float y = area.y + 6f;
            if (Ui.Btn(new Rect(x, y, s, s), "+", Zoom < MaxZoom)) SetZoom(Zoom * 1.4f);
            if (Ui.Btn(new Rect(x, y + s + 6, s, s), "-", Zoom > MinZoom)) SetZoom(Zoom / 1.4f);
            if (Ui.Btn(new Rect(x - 62, y, 56, s), "全体", Zoom > MinZoom, null, null, 13)) { Zoom = 1f; }
            if (showFollow && Ui.Btn(new Rect(x - 62, y + s + 6, 56, s), "追従", !Follow, null, null, 13)) Follow = true;
        }

        public Rect CellRect(float x, float y) => new Rect(_origin.x + x * _cell, _origin.y + y * _cell, _cell, _cell);

        /// <summary>マス座標(小数可)→画面座標。</summary>
        public Vector2 ToScreen(Vector2 grid) => _origin + grid * _cell;

        /// <summary>画面上の位置(IMGUI座標)にあるマス。マップ外なら null。</summary>
        public GridPos? GridAt(Vector2 guiPos)
        {
            if (_cell <= 0) return null;
            int x = Mathf.FloorToInt((guiPos.x - _origin.x) / _cell);
            int y = Mathf.FloorToInt((guiPos.y - _origin.y) / _cell);
            if (x < 0 || y < 0 || x >= _w || y >= _h) return null;
            return new GridPos(x, y);
        }

        private static float Time01(float period) => Mathf.Repeat(Time.unscaledTime, period) / period;

        // ================= 地上 =================

        /// <summary>マップの上に空・雲・草地を描く。</summary>
        public void DrawSurface(DungeonMap map, bool night = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            float top = _origin.y - _cell * SurfaceRows;
            var sky = new Rect(_origin.x, top, _cell * _w, _cell * SurfaceRows);
            // 空のグラデーション(帯で塗る)
            var c1 = night ? new Color(0.08f, 0.06f, 0.2f) : new Color(0.36f, 0.66f, 0.98f);
            var c2 = night ? new Color(0.25f, 0.12f, 0.3f) : new Color(0.72f, 0.9f, 1f);
            int bands = 6;
            for (int i = 0; i < bands; i++)
                Ui.Fill(new Rect(sky.x, sky.y + sky.height * i / bands, sky.width, sky.height / bands + 1), Color.Lerp(c1, c2, i / (float)(bands - 1)));
            // 雲
            for (int i = 0; i < 3; i++)
            {
                float speed = 0.02f + i * 0.01f;
                float x = sky.x + Mathf.Repeat(Time.unscaledTime * speed * sky.width + i * sky.width / 3f, sky.width + _cell * 2) - _cell * 2;
                float y = sky.y + _cell * (0.15f + 0.25f * (i % 2));
                Cloud(new Rect(x, y, _cell * 1.6f, _cell * 0.45f), sky);
            }
            // 草地(マップの最上段=岩盤の上に乗る)
            for (int x = 0; x < _w; x++)
            {
                var r = CellRect(x, -0.5f);
                r.height = _cell * 0.5f;
                if (x == map.Entrance.X) continue;
                GUI.DrawTextureWithTexCoords(r, TileArt.Grass, new Rect(0, 0.5f, 1, 0.5f));
            }
        }

        private void Cloud(Rect r, Rect clip)
        {
            if (r.xMax < clip.x || r.x > clip.xMax) return;
            var white = new Color(1f, 1f, 1f, 0.92f);
            var parts = new[]
            {
                new Rect(r.x, r.y + r.height * 0.4f, r.width, r.height * 0.6f),
                new Rect(r.x + r.width * 0.2f, r.y, r.width * 0.45f, r.height * 0.7f),
                new Rect(r.x + r.width * 0.5f, r.y + r.height * 0.15f, r.width * 0.35f, r.height * 0.6f),
            };
            foreach (var p in parts)
            {
                var q = Rect.MinMaxRect(Mathf.Max(p.x, clip.x), p.y, Mathf.Min(p.xMax, clip.xMax), p.yMax);
                if (q.width > 0) Ui.Fill(q, white);
            }
        }

        // ================= マス =================

        // ---------------- モンスターの住みか(土の見た目) ----------------

        private string[] _skinId;
        private float[] _skinStrength;
        private int[] _skinTier;

        /// <summary>
        /// 土の各マスが、どのモンスターの「住みか」になっているかを決める(見た目だけ)。
        /// モンスターのまわり(上位以上は2マス、それ以外は1マス)の土に、その種族の模様がつく。
        /// 重なったら、近い方・階級の高い方が勝つ。
        /// </summary>
        private void ComputeSkins(DungeonMap map, IEnumerable<Monster> monsters)
        {
            int n = map.Width * map.Height;
            if (_skinId == null || _skinId.Length != n)
            {
                _skinId = new string[n];
                _skinStrength = new float[n];
                _skinTier = new int[n];
            }
            System.Array.Clear(_skinId, 0, n);
            System.Array.Clear(_skinStrength, 0, n);
            System.Array.Clear(_skinTier, 0, n);
            if (monsters == null) return;
            foreach (var m in monsters)
            {
                if (m.Dead || m.Species.IsBoss) continue;
                int radius = m.Species.Tier >= 3 ? 2 : 1;
                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = m.Pos.X + dx, y = m.Pos.Y + dy;
                    if (!map.InBounds(x, y) || map.GetTile(x, y) != TileType.Soil) continue;
                    int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    float s = d <= 1 ? 1f : 0.55f;
                    int i = map.Index(x, y);
                    if (s > _skinStrength[i] || (Mathf.Approximately(s, _skinStrength[i]) && m.Species.Tier > _skinTier[i]))
                    {
                        _skinId[i] = m.Species.Id;
                        _skinStrength[i] = s;
                        _skinTier[i] = m.Species.Tier;
                    }
                }
            }
        }

        /// <param name="monsters">渡すと、モンスターのまわりの土にその種族の住みかの模様をつける。</param>
        public void DrawTiles(DungeonMap map, FogOfWar fog, bool showResources, IEnumerable<Monster> monsters = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            ComputeSkins(map, monsters);
            int cap = Mathf.Max(1, map.TileResourceCap);
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                // 拡大縮小で境目にすき間が出ないよう、1ピクセル重ねて描く
                var r = CellRect(x, y);
                r.width += 1f; r.height += 1f;
                // 入口の真上(地上へ抜ける穴)は通路として描く
                bool shaft = y == 0 && x == map.Entrance.X;
                if (fog != null && !fog.IsKnown(x, y) && !shaft)
                {
                    GUI.DrawTexture(r, TileArt.Fog(x, y));
                    continue;
                }
                var t = shaft ? TileType.Floor : map.GetTile(x, y);
                switch (t)
                {
                    case TileType.Bedrock:
                        GUI.DrawTexture(r, TileArt.Bedrock(x, y));
                        break;
                    case TileType.Soil:
                        GUI.DrawTexture(r, TileArt.Soil(x, y));
                        if (showResources)
                        {
                            int n = Level(map.GetNutrient(x, y), cap), m = Level(map.GetMana(x, y), cap);
                            if (n > 0) GUI.DrawTexture(r, TileArt.Nutrient(n));
                            if (m > 0) GUI.DrawTexture(r, TileArt.Mana(m));
                        }
                        // モンスターの住みかの模様
                        {
                            int si = map.Index(x, y);
                            var skin = TileArt.Skin(_skinId[si], x, y);
                            if (skin != null)
                            {
                                var old = GUI.color;
                                GUI.color = new Color(1, 1, 1, _skinStrength[si]);
                                GUI.DrawTexture(r, skin);
                                GUI.color = old;
                            }
                        }
                        break;
                    default:
                        GUI.DrawTexture(r, TileArt.Floor(x, y));
                        // 積もった骨(墓場からの誕生の目印)
                        int bones = map.GetBones(x, y);
                        if (bones > 0) GUI.DrawTexture(r, TileArt.BonesOverlay(bones >= 6 ? 3 : bones >= 3 ? 2 : 1));
                        // 上や左が土なら影を落として立体的に
                        if (y > 0 && !map.IsWalkable(x, y - 1) && !shaft) GUI.DrawTexture(r, TileArt.FloorShadowTop);
                        if (x > 0 && !map.IsWalkable(x - 1, y)) GUI.DrawTexture(r, TileArt.FloorShadowSide);
                        if (t == TileType.Entrance) GUI.DrawTexture(Inset(r, 0.86f), TileArt.Entrance);
                        else if (t == TileType.Goal)
                        {
                            if (map.GoalType == GoalType.DemonLord) GUI.DrawTexture(r, TileArt.Throne);
                            else
                            {
                                GUI.DrawTexture(r, TileArt.Stairs);
                                // 階段の光
                                float a = 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f);
                                Ui.Fill(r, new Color(1f, 0.9f, 0.4f, a));
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>養分・魔分の量 → 重ね絵の段階(0=なし、1〜3)。</summary>
        private static int Level(int v, int cap)
        {
            if (v <= 2) return 0;
            if (v <= 4) return 1;
            if (v <= 7) return 2;
            return 3;
        }

        /// <summary>マウスが乗っているマスを示す(掘れるならツルハシ)。</summary>
        public void DrawDigCursor(GridPos p, bool canDig)
        {
            if (Event.current.type != EventType.Repaint) return;
            var r = CellRect(p.X, p.Y);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            Ui.Frame(r, canDig ? new Color(1f, 0.95f, 0.5f, 0.6f + 0.4f * pulse) : new Color(1f, 0.3f, 0.3f, 0.7f), 2f);
            if (canDig)
            {
                float swing = Mathf.Sin(Time.unscaledTime * 10f) * 0.08f;
                var ir = new Rect(r.x + r.width * (0.35f + swing), r.y - r.height * 0.45f, r.width * 0.8f, r.height * 0.8f);
                Ui.Sprite(ir, Sprites.Get("icon_pickaxe"));
            }
        }

        public void Highlight(GridPos p, Color c, float thickness = 2f) => Ui.Frame(CellRect(p.X, p.Y), c, thickness);

        /// <summary>中心から半径 radius の範囲(たいまつ)を光で示す。</summary>
        public void HighlightArea(GridPos center, int radius, Color c)
        {
            var a = CellRect(center.X - radius, center.Y - radius);
            var r = new Rect(a.x, a.y, _cell * (radius * 2 + 1), _cell * (radius * 2 + 1));
            float pulse = 0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
            Ui.Fill(r, new Color(c.r, c.g, c.b, pulse));
            Ui.Frame(r, c, 2f);
            Ui.Sprite(Inset(CellRect(center.X, center.Y), 0.9f), Sprites.Get("icon_torch"));
        }

        // ================= モンスター・勇者 =================

        /// <param name="wanderMap">
        /// 指定すると、モンスターが自分のマスの周り(2マス以内の通路)を見た目の上だけでうろうろ歩く(ダンジョン作成中用)。
        /// ロジック上の位置は変わらないので、保存内容や育成の結果には影響しない。
        /// </param>
        public void DrawMonsters(IEnumerable<Monster> monsters, FogOfWar fog, bool inRaid, DungeonMap wanderMap = null)
        {
            foreach (var m in monsters)
            {
                if (m.Dead) continue;
                if (fog != null && !fog.IsKnown(m.Pos)) continue;
                var target = wanderMap != null ? WanderTarget(m, wanderMap) : new Vector2(m.Pos.X, m.Pos.Y);
                var pos = Smooth(_monsterVis, _monsterFlip, m.Id, target, wanderMap != null ? 3f : 14f);
                bool walking = (pos - target).sqrMagnitude > 0.003f;
                pos += LungeOffset(_monsterLunge, _monsterFlip, m.Id);
                bool boss = m.Species.IsBoss;
                float size = boss ? 1.45f : m.Species.Tier == 1 ? 0.8f : m.Species.Tier == 2 ? 0.95f : m.Species.Tier == 3 ? 1.1f : 1.3f;
                // 歩いているときは速く跳ねる
                float speed = boss ? 2f : walking ? 11f : 5f;
                float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * speed + m.Id)) * _cell * (walking ? 0.09f : 0.06f);
                var r = Feet(CellRect(pos.x, pos.y), size, bob);
                if (Event.current.type == EventType.Repaint)
                {
                    Shadow(CellRect(pos.x, pos.y), size);
                    bool flash = Flashing(_monsterFlash, m.Id);
                    _monsterFlip.TryGetValue(m.Id, out bool flip);
                    Ui.Sprite(r, Sprites.Anim(m.Species.Id, m.Id), flash ? new Color(1f, 0.55f, 0.55f) : (Color?)null, flip);
                    if (inRaid && m.CombatMaxHp > 0 && (m.Hp < m.CombatMaxHp || boss))
                    {
                        var c = CellRect(pos.x, pos.y);
                        Ui.Bar(new Rect(c.x + c.width * 0.1f, r.y - 5, c.width * 0.8f, 5), m.Hp, m.CombatMaxHp, new Color(1f, 0.35f, 0.3f));
                    }
                }
            }
        }

        public void DrawHeroes(IReadOnlyList<Hero> heroes)
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var h in heroes)
            {
                if (!h.Active) continue;
                var pos = Smooth(_heroVis, _heroFlip, h.Id, new Vector2(h.Pos.X, h.Pos.Y), 14f);
                pos += LungeOffset(_heroLunge, _heroFlip, h.Id);
                float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f + h.Id * 1.3f)) * _cell * 0.07f;
                var cell = CellRect(pos.x, pos.y);
                var r = Feet(cell, 0.95f, bob);
                Shadow(cell, 0.9f);
                bool flash = Flashing(_heroFlash, h.Id);
                _heroFlip.TryGetValue(h.Id, out bool flip);
                Ui.Sprite(r, Sprites.Anim(h.Class.Id, h.Id * 3), flash ? new Color(1f, 0.45f, 0.45f) : (Color?)null, flip);
                Ui.Bar(new Rect(cell.x + cell.width * 0.1f, cell.yMax - 4, cell.width * 0.8f, 5), h.Hp, h.Stats.MaxHp);
            }
        }

        /// <summary>入口の外(地上)で待っている勇者を描く。</summary>
        public void DrawWaitingHeroes(IReadOnlyList<Hero> heroes, DungeonMap map)
        {
            if (Event.current.type != EventType.Repaint) return;
            int i = 0;
            foreach (var h in heroes)
            {
                if (h.Entered || h.Dead) continue;
                float x = map.Entrance.X - 1.2f - i * 0.9f;
                var cell = CellRect(x, -1.05f);
                float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f + h.Id)) * _cell * 0.06f;
                Ui.Sprite(Feet(cell, 0.9f, bob), Sprites.Anim(h.Class.Id, h.Id * 3));
                i++;
            }
        }

        /// <summary>攻略のできごとを演出にする(踏み込み・斬撃・弾・数字・粒子・点滅)。</summary>
        public void OnEvents(List<RaidEvent> events, IReadOnlyList<Hero> heroes)
        {
            foreach (var e in events)
            {
                var p = new Vector2(e.Pos.X, e.Pos.Y);
                switch (e.Kind)
                {
                    case RaidEventKind.HeroAttack:
                    {
                        var hero = e.HeroId > 0 && e.HeroId <= heroes.Count ? heroes[e.HeroId - 1] : null;
                        var from = hero == null ? p : _heroVis.TryGetValue(e.HeroId, out var hv) ? hv : new Vector2(hero.Pos.X, hero.Pos.Y);
                        bool magic = hero != null && hero.Class.AttackStat == AttackStat.Magic;
                        bool ranged = magic || (from - p).sqrMagnitude > 2.5f;
                        if (ranged)
                        {
                            // 魔法・射程攻撃:軽く構えて弾を飛ばす
                            StartLunge(_heroLunge, e.HeroId, (p - from) * 0.4f);
                            Fx.Shoot(from, p, magic ? new Color(0.75f, 0.5f, 1f) : new Color(1f, 0.95f, 0.6f));
                        }
                        else
                        {
                            // 近接攻撃:相手に踏み込んで斬る
                            StartLunge(_heroLunge, e.HeroId, p - from);
                            Fx.SlashAt(p, e.Critical ? Ui.Gold : Color.white, e.Critical ? 1.4f : 1f, from.x > p.x);
                            if (e.Critical) Fx.SlashAt(p, Ui.Gold, 1.2f, from.x <= p.x);
                        }
                        _monsterFlash[e.MonsterId] = 0.14f;
                        Fx.Popup(p, e.Amount.ToString(), e.Critical ? Ui.Gold : Color.white, e.Critical ? 1.5f : 1f);
                        if (e.Critical) { Fx.Popup(p + new Vector2(0, -0.4f), "会心!", Ui.Gold, 0.9f); Fx.Shake(3f); }
                        Fx.Burst(p, new Color(1f, 1f, 0.8f), 3, 2f, 0.08f);
                        break;
                    }
                    case RaidEventKind.MonsterAttack:
                    {
                        // モンスターが勇者に飛びかかって爪(牙)で攻撃する
                        var from = _monsterVis.TryGetValue(e.MonsterId, out var mv) ? mv : p;
                        StartLunge(_monsterLunge, e.MonsterId, p - from);
                        Fx.ClawAt(p, new Color(1f, 0.3f, 0.25f));
                        _heroFlash[e.HeroId] = 0.15f;
                        Fx.Popup(p, e.Amount.ToString(), new Color(1f, 0.35f, 0.3f));
                        if (e.Amount >= 15) Fx.Shake(2.5f);
                        break;
                    }
                    case RaidEventKind.HeroHeal:
                        Fx.Popup(p, "+" + e.Amount, Ui.Green);
                        Fx.Burst(p, Ui.Green, 5, 1.5f, 0.08f);
                        break;
                    case RaidEventKind.MonsterKilled:
                        Fx.Burst(p, new Color(0.85f, 0.75f, 1f), 14, 4f);
                        Fx.Burst(p, Color.white, 6, 2f);
                        break;
                    case RaidEventKind.HeroDown:
                        Fx.Burst(p, Ui.Red, 18, 4f);
                        Fx.Popup(p, "ぐはっ!", Ui.Red, 1.1f);
                        Fx.Shake(6f);
                        break;
                    case RaidEventKind.InterventionTorch:
                        Fx.Burst(p, new Color(1f, 0.7f, 0.2f), 24, 5f, 0.14f);
                        break;
                }
            }
        }

        /// <summary>パーティ全体の回復演出(回復の介入スキル)。</summary>
        public void HealAll(IReadOnlyList<Hero> heroes)
        {
            foreach (var h in heroes)
            {
                if (!h.Active) continue;
                var p = new Vector2(h.Pos.X, h.Pos.Y);
                Fx.Burst(p, Ui.Green, 10, 2.5f, 0.1f);
                Fx.Popup(p, "回復!", Ui.Green, 0.9f);
            }
        }

        public void DrawFx() => Fx.Draw(this);

        // ================= 補助 =================

        private bool Flashing(Dictionary<int, float> flashes, int id)
        {
            if (!flashes.TryGetValue(id, out float t)) return false;
            t -= _dt;
            if (t <= 0) { flashes.Remove(id); return false; }
            flashes[id] = t;
            return true;
        }

        /// <summary>表示位置を目標へ近づける。rate が大きいほど速く追いつく。</summary>
        private Vector2 Smooth(Dictionary<int, Vector2> dict, Dictionary<int, bool> flips, int id, Vector2 t, float rate)
        {
            if (!dict.TryGetValue(id, out var v) || (v - t).sqrMagnitude > 9f) v = t; // 初登場・遠すぎるときは瞬間移動
            if (Mathf.Abs(t.x - v.x) > 0.05f) flips[id] = t.x < v.x; // 左へ動いたら左を向く
            v = Vector2.Lerp(v, t, 1f - Mathf.Exp(-_dt * rate));
            dict[id] = v;
            return v;
        }

        // ---------------- 攻撃の踏み込み ----------------

        private struct Lunge
        {
            public Vector2 Dir;
            public float T;
        }

        private const float LungeTime = 0.22f;
        private readonly Dictionary<int, Lunge> _monsterLunge = new Dictionary<int, Lunge>();
        private readonly Dictionary<int, Lunge> _heroLunge = new Dictionary<int, Lunge>();

        private static void StartLunge(Dictionary<int, Lunge> d, int id, Vector2 toward)
        {
            if (toward.sqrMagnitude < 0.0001f) return;
            d[id] = new Lunge { Dir = toward.normalized, T = LungeTime };
        }

        /// <summary>踏み込みのずれ(行って戻る)。踏み込む方向を向かせる。</summary>
        private Vector2 LungeOffset(Dictionary<int, Lunge> d, Dictionary<int, bool> flips, int id)
        {
            if (!d.TryGetValue(id, out var l)) return Vector2.zero;
            l.T -= _dt;
            if (l.T <= 0) { d.Remove(id); return Vector2.zero; }
            d[id] = l;
            if (Mathf.Abs(l.Dir.x) > 0.1f) flips[id] = l.Dir.x < 0;
            float k = 1f - l.T / LungeTime;
            // 前半で素早く踏み込み、後半でゆっくり戻る
            float amount = k < 0.35f ? k / 0.35f : 1f - (k - 0.35f) / 0.65f;
            return l.Dir * 0.38f * amount;
        }

        // ---------------- 作成中のうろうろ歩き ----------------

        private sealed class Wander
        {
            public GridPos Home;
            public Vector2 Target;
            public float Next;
        }

        private readonly Dictionary<int, Wander> _wander = new Dictionary<int, Wander>();

        /// <summary>
        /// 見た目だけのうろうろ歩きの目標地点。ときどき隣の通路マス(本来のマスから2マス以内)へ歩き、また戻ってくる。
        /// </summary>
        private Vector2 WanderTarget(Monster m, DungeonMap map)
        {
            float now = Time.unscaledTime;
            if (!_wander.TryGetValue(m.Id, out var w) || w.Home != m.Pos)
            {
                w = new Wander { Home = m.Pos, Target = new Vector2(m.Pos.X, m.Pos.Y), Next = now + Random.Range(0.2f, 1.5f) };
                _wander[m.Id] = w;
            }
            if (now >= w.Next)
            {
                var cur = new GridPos(Mathf.RoundToInt(w.Target.x), Mathf.RoundToInt(w.Target.y));
                var options = new List<GridPos> { w.Home };
                foreach (var d in GridPos.Dir4)
                {
                    var n = cur + d;
                    if (!map.InBounds(n) || map.GetTile(n) != TileType.Floor) continue;
                    if (Core.Determinism.IntMath.Manhattan(n.X, n.Y, w.Home.X, w.Home.Y) > 2) continue;
                    options.Add(n);
                    options.Add(n); // 本来のマスより、隣へ歩く方を少し選ばれやすくする
                }
                var pick = options[Random.Range(0, options.Count)];
                w.Target = new Vector2(pick.X, pick.Y);
                // 大きい(階級の高い)モンスターほどのんびり歩く
                float slow = 1f + 0.4f * (m.Species.Tier - 1);
                w.Next = now + Random.Range(0.7f, 2.2f) * slow;
            }
            return w.Target;
        }

        /// <summary>足元をマスの下端にそろえた、大きさ scale の四角(大きい絵は上にはみ出す)。</summary>
        private static Rect Feet(Rect cell, float scale, float bob)
        {
            float s = cell.width * scale;
            return new Rect(cell.x + (cell.width - s) / 2f, cell.yMax - s - bob, s, s);
        }

        private static void Shadow(Rect cell, float scale)
        {
            float w = cell.width * 0.6f * scale;
            Ui.Fill(new Rect(cell.x + (cell.width - w) / 2f, cell.yMax - cell.height * 0.1f, w, cell.height * 0.08f), new Color(0, 0, 0, 0.35f));
        }

        private static Rect Inset(Rect r, float scale)
        {
            float w = r.width * scale, h = r.height * scale;
            return new Rect(r.x + (r.width - w) / 2f, r.y + (r.height - h) / 2f, w, h);
        }
    }
}
