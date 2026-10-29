using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>
    /// レトロRPG風の IMGUI 部品(ウィンドウ枠・立体ボタン・目盛り付きゲージ・縁取り文字・ドット絵アイコン)。
    /// 見出しやボタンはドット風フォント DotGothic16、細かい説明文は読みやすい BIZ UDゴシック(どちらも SIL OFL)。
    /// </summary>
    public static class Ui
    {
        public static readonly Color Navy = new Color(0.07f, 0.08f, 0.2f);
        public static readonly Color NavyLight = new Color(0.16f, 0.18f, 0.38f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        public static readonly Color Cream = new Color(1f, 0.97f, 0.88f);
        public static readonly Color Ink = new Color(0.06f, 0.04f, 0.09f);
        public static readonly Color Red = new Color(0.95f, 0.3f, 0.25f);
        public static readonly Color Green = new Color(0.4f, 0.9f, 0.35f);
        public static readonly Color Sky = new Color(0.55f, 0.85f, 1f);

        private static Texture2D _white;
        private static Font _dot, _body;
        private static GUIStyle _label, _small, _title, _huge, _wrap, _textArea, _center, _button, _bodyStyle;

        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                }
                return _white;
            }
        }

        /// <summary>フォントと文字スタイルを用意する。OnGUI の最初に呼ぶ。</summary>
        public static void ApplySkin()
        {
            if (_dot == null) _dot = Resources.Load<Font>("Fonts/DotGothic16-Regular");
            if (_body == null) _body = Resources.Load<Font>("Fonts/BIZUDGothic-Regular");
            GUI.skin.font = _body;
            // ドット風フォントは拡大してもぼやけないよう、文字のテクスチャをポイントフィルタにする
            // (文字が増えるとテクスチャが作り直されるので毎回設定する)
            if (_dot != null && _dot.material != null && _dot.material.mainTexture != null)
                _dot.material.mainTexture.filterMode = FilterMode.Point;
            if (_label != null) return;

            // 読みやすさ優先:本文・ラベル・補足は BIZ UDゴシック、見出し・ボタン・数字演出だけドット風フォント
            _label = new GUIStyle(GUI.skin.label) { font = _body, fontSize = 16, wordWrap = false, richText = true, clipping = TextClipping.Clip };
            _label.normal.textColor = Cream;
            _small = new GUIStyle(_label) { fontSize = 14 };
            _wrap = new GUIStyle(_small) { fontSize = 15, wordWrap = true };
            _title = new GUIStyle(_label) { font = _dot, fontSize = 28, clipping = TextClipping.Overflow };
            _heading = new GUIStyle(_title) { fontSize = 22 };
            _huge = new GUIStyle(_title) { fontSize = 52, alignment = TextAnchor.MiddleCenter };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            _ribbon = new GUIStyle(_title) { fontSize = 16 };
            _textArea = new GUIStyle(GUI.skin.textArea) { font = _body, fontSize = 13, wordWrap = true };
            _bodyStyle = new GUIStyle(_small) { fontSize = 17, wordWrap = true };
        }

        private static GUIStyle _heading, _ribbon;

        /// <summary>カードの名前などの中見出し(ドット風・22)。</summary>
        public static GUIStyle Heading => _heading;

        /// <summary>補足の文字色(説明・注記)。背景が暗くても読めるよう、明るめにしている。</summary>
        public static readonly Color Sub = new Color(0.88f, 0.9f, 0.98f);

        public static GUIStyle Label => _label;
        public static GUIStyle Small => _small;
        public static GUIStyle Wrap => _wrap;
        public static GUIStyle Title => _title;
        public static GUIStyle Huge => _huge;
        public static GUIStyle Center => _center;
        public static GUIStyle TextArea => _textArea;
        /// <summary>説明文用(読みやすい大きめの本文)。</summary>
        public static GUIStyle Body => _bodyStyle;
        public static Font DotFont => _dot;

        private static bool Linear => QualitySettings.activeColorSpace == ColorSpace.Linear;

        /// <summary>GUI.color に入れる色(リニア色空間でも指定どおりの色に見えるよう変換)。</summary>
        public static Color Tint(Color c) => Linear ? c.linear : c;

        // ================= 基本の描画 =================

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = Tint(c);
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        public static void Frame(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>ドット絵を描く(flip で左右反転)。</summary>
        public static void Sprite(Rect r, Texture2D tex, Color? tint = null, bool flip = false)
        {
            if (tex == null) return;
            var old = GUI.color;
            GUI.color = Tint(tint ?? Color.white);
            if (flip) GUI.DrawTextureWithTexCoords(r, tex, new Rect(1, 0, -1, 1));
            else GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
            GUI.color = old;
        }

        public static Color Hex(string hex, Color fallback)
            => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        /// <summary>影付きの文字(右下に濃い影を落として、背景が何色でも読めるようにする)。</summary>
        public static void Text(Rect r, string text, GUIStyle style, Color color, bool outline = true)
        {
            r = FitHeight(r, style);
            var old = style.normal.textColor;
            if (outline)
            {
                style.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, color.a);
                float d = style.fontSize >= 24 ? 2f : 1f;
                GUI.Label(new Rect(r.x + d, r.y + d, r.width, r.height), text, style);
                GUI.Label(new Rect(r.x, r.y + d, r.width, r.height), text, style);
            }
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        /// <summary>
        /// 枠が文字の高さより低いと上下が切れるので、足りない分だけ広げる。
        /// 上寄せの文字は下へ、中央寄せの文字は上下均等に広げる(見た目の位置は変わらない)。
        /// </summary>
        private static Rect FitHeight(Rect r, GUIStyle style)
        {
            float need = style.lineHeight + style.padding.vertical + 4f;
            if (r.height >= need) return r;
            float extra = need - r.height;
            switch (style.alignment)
            {
                case TextAnchor.MiddleLeft:
                case TextAnchor.MiddleCenter:
                case TextAnchor.MiddleRight:
                    return new Rect(r.x, r.y - extra / 2f, r.width, need);
                case TextAnchor.LowerLeft:
                case TextAnchor.LowerCenter:
                case TextAnchor.LowerRight:
                    return new Rect(r.x, r.y - extra, r.width, need);
                default:
                    return new Rect(r.x, r.y, r.width, need);
            }
        }

        // ================= ウィンドウ =================

        /// <summary>レトロRPG風のウィンドウ(角を落とした白枠+紺の地)。title があれば上辺にリボンを付ける。</summary>
        public static void Window(Rect r, string title = null, Color? fill = null)
        {
            var bg = fill ?? new Color(Navy.r, Navy.g, Navy.b, 0.94f);
            // 影
            Fill(new Rect(r.x + 4, r.y + 4, r.width, r.height), new Color(0, 0, 0, 0.35f));
            Fill(new Rect(r.x + 2, r.y, r.width - 4, r.height), Ink);
            Fill(new Rect(r.x, r.y + 2, r.width, r.height - 4), Ink);
            Fill(new Rect(r.x + 3, r.y + 2, r.width - 6, r.height - 4), Cream);
            Fill(new Rect(r.x + 2, r.y + 3, r.width - 4, r.height - 6), Cream);
            Fill(new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10), Ink);
            Fill(new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12), bg);
            if (!string.IsNullOrEmpty(title))
            {
                var size = _ribbon.CalcSize(new GUIContent(title));
                var rib = new Rect(r.x + 14, r.y - 12, size.x + 24, 24);
                Fill(new Rect(rib.x - 2, rib.y - 2, rib.width + 4, rib.height + 4), Ink);
                Fill(rib, new Color(0.62f, 0.12f, 0.16f));
                Fill(new Rect(rib.x, rib.y, rib.width, 3), new Color(0.85f, 0.3f, 0.3f));
                Text(new Rect(rib.x + 12, rib.y + 1, rib.width, rib.height), title, _ribbon, Gold);
            }
        }

        // ================= ボタン =================

        /// <summary>
        /// 立体的なレトロボタン。マウスが乗ると明るくなり、左に ▶ が点滅する。
        /// icon を渡すと左にドット絵を描く。
        /// </summary>
        public static bool Btn(Rect r, string text, bool enabled = true, Texture2D icon = null, Color? baseColor = null, int fontSize = 0)
        {
            var e = Event.current;
            bool hover = enabled && GUI.enabled && r.Contains(e.mousePosition);
            bool pressed = false; // IMGUI では押下中の判定が不安定なので、押し込み表現はしない
            var baseC = baseColor ?? new Color(0.24f, 0.3f, 0.62f);
            if (!enabled) baseC = new Color(0.22f, 0.22f, 0.26f);
            else if (hover) baseC = Color.Lerp(baseC, Color.white, 0.18f);

            var body = pressed ? new Rect(r.x, r.y + 2, r.width, r.height - 2) : r;
            if (Event.current.type == EventType.Repaint)
            {
                Fill(new Rect(r.x + 1, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.4f)); // 影
                Fill(body, Ink);
                var inner = new Rect(body.x + 2, body.y + 2, body.width - 4, body.height - 4);
                Fill(inner, baseC);
                Fill(new Rect(inner.x, inner.y, inner.width, 2), Color.Lerp(baseC, Color.white, 0.35f));
                Fill(new Rect(inner.x, inner.yMax - 3, inner.width, 3), Color.Lerp(baseC, Color.black, 0.35f));
                if (hover) Frame(new Rect(body.x - 2, body.y - 2, body.width + 4, body.height + 4), Gold, 2f);

                float textX = body.x;
                float textW = body.width;
                if (icon != null)
                {
                    float s = Mathf.Min(body.height - 8, 24f);
                    Sprite(new Rect(body.x + 8, body.y + (body.height - s) / 2f, s, s), icon, enabled ? (Color?)null : new Color(0.6f, 0.6f, 0.6f));
                    textX += s + 8;
                    textW -= s + 8;
                }
                var style = _button;
                int oldSize = style.fontSize;
                style.fontSize = fontSize > 0 ? fontSize : (body.height < 30 ? 15 : 18);
                Text(new Rect(textX, body.y, textW, body.height), text, style, enabled ? Cream : new Color(0.55f, 0.55f, 0.6f));
                style.fontSize = oldSize;
                if (hover && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f)
                    Text(new Rect(body.x - 18, body.y, 18, body.height), "▶", _center, Gold);
            }
            bool clicked = enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (clicked) OnClick?.Invoke();
            return clicked;
        }

        // ================= ゲージ =================

        /// <summary>目盛り付きゲージ(HP用:残りに応じて緑→黄→赤)。</summary>
        public static void Bar(Rect r, int value, int max, Color? color = null)
        {
            Fill(r, Ink);
            var inner = new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2);
            Fill(inner, new Color(0.18f, 0.12f, 0.16f));
            float t = max <= 0 ? 0 : Mathf.Clamp01(value / (float)max);
            var c = color ?? (t > 0.5f ? Green : t > 0.25f ? new Color(1f, 0.8f, 0.2f) : Red);
            var f = new Rect(inner.x, inner.y, Mathf.Round(inner.width * t), inner.height);
            Fill(f, c);
            if (inner.height >= 4) Fill(new Rect(f.x, f.y, f.width, 1), Color.Lerp(c, Color.white, 0.5f));
            if (inner.width > 40)
                for (float x = inner.x + 6; x < inner.xMax; x += 6) Fill(new Rect(x, inner.y, 1, inner.height), new Color(0, 0, 0, 0.25f));
        }

        /// <summary>アイコン+文字の1行。</summary>
        public static void IconText(Rect r, Texture2D icon, string text, GUIStyle style = null, Color? color = null)
        {
            float s = Mathf.Min(r.height, 20f);
            Sprite(new Rect(r.x, r.y + (r.height - s) / 2f, s, s), icon);
            Text(new Rect(r.x + s + 4, r.y, r.width - s - 4, r.height), text, style ?? Label, color ?? Cream);
        }

        /// <summary>
        /// 額縁付きの肖像(勇者・魔王・モンスターの顔)。背景は色のグラデーション、枠は金の飾り角付き。
        /// dim で暗く(戦闘不能など)、seed で待機アニメのタイミングをずらす。
        /// </summary>
        public static void Portrait(Rect r, string id, Color bg, float seed = 0f, bool dim = false, bool gold = true)
        {
            if (Event.current.type != EventType.Repaint) return;
            Fill(r, Ink);
            var inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            int bands = 5;
            for (int i = 0; i < bands; i++)
                Fill(new Rect(inner.x, inner.y + inner.height * i / bands, inner.width, inner.height / bands + 1),
                    Color.Lerp(Color.Lerp(bg, Color.white, 0.18f), Color.Lerp(bg, Color.black, 0.45f), i / (float)(bands - 1)));
            // 足元の台
            Fill(new Rect(inner.x + inner.width * 0.15f, inner.yMax - inner.height * 0.12f, inner.width * 0.7f, inner.height * 0.06f), new Color(0, 0, 0, 0.35f));
            var tint = dim ? new Color(0.3f, 0.3f, 0.35f) : (Color?)null;
            Sprite(new Rect(inner.x + inner.width * 0.06f, inner.y + inner.height * 0.02f, inner.width * 0.88f, inner.height * 0.88f),
                Art.Sprites.Anim(id, seed), tint);
            var frame = gold ? Gold : new Color(0.75f, 0.75f, 0.85f);
            Frame(r, frame, 2f);
            float c = Mathf.Max(4f, r.width * 0.12f);
            // 飾り角
            Fill(new Rect(r.x, r.y, c, 3), frame); Fill(new Rect(r.x, r.y, 3, c), frame);
            Fill(new Rect(r.xMax - c, r.y, c, 3), frame); Fill(new Rect(r.xMax - 3, r.y, 3, c), frame);
            Fill(new Rect(r.x, r.yMax - 3, c, 3), frame); Fill(new Rect(r.x, r.yMax - c, 3, c), frame);
            Fill(new Rect(r.xMax - c, r.yMax - 3, c, 3), frame); Fill(new Rect(r.xMax - 3, r.yMax - c, 3, c), frame);
        }

        /// <summary>ボタンが押されたときに呼ばれる(効果音用)。</summary>
        public static System.Action OnClick;

        /// <summary>キャラクターごとの肖像の背景色。</summary>
        public static Color PortraitColor(string id)
        {
            switch (id)
            {
                case "warrior": return new Color(0.6f, 0.2f, 0.18f);
                case "mage": return new Color(0.35f, 0.2f, 0.6f);
                case "priest": return new Color(0.55f, 0.5f, 0.3f);
                case "thief": return new Color(0.15f, 0.42f, 0.25f);
                case "hunter": return new Color(0.45f, 0.35f, 0.15f);
                case "paladin": return new Color(0.2f, 0.3f, 0.6f);
                case "monk": return new Color(0.6f, 0.32f, 0.1f);
                case "moss_slime": return new Color(0.2f, 0.45f, 0.2f);
                case "mana_mite": return new Color(0.15f, 0.3f, 0.6f);
                case "cave_lizard": return new Color(0.55f, 0.35f, 0.12f);
                case "abyss_wyrm": return new Color(0.35f, 0.12f, 0.45f);
                case "demon_lord": return new Color(0.45f, 0.05f, 0.1f);
                case "glow_shroom": return new Color(0.45f, 0.4f, 0.12f);
                case "shadow_bat": return new Color(0.2f, 0.16f, 0.32f);
                case "crystal_golem": return new Color(0.15f, 0.4f, 0.5f);
                case "armor_centipede": return new Color(0.5f, 0.15f, 0.1f);
                case "skull_mage": return new Color(0.25f, 0.1f, 0.3f);
                case "abyss_dragon": return new Color(0.08f, 0.22f, 0.32f);
                default: return new Color(0.25f, 0.25f, 0.35f);
            }
        }

        /// <summary>★を n 個(最大 max 個)並べる。</summary>
        public static void Stars(Rect r, int n, int max)
        {
            float s = r.height;
            for (int i = 0; i < max; i++)
                Sprite(new Rect(r.x + i * (s + 4), r.y, s, s), Art.Sprites.Get(i < n ? "icon_star" : "icon_star_empty"));
        }
    }

    /// <summary>上から順に部品を並べるための簡単なレイアウト補助。</summary>
    public sealed class Column
    {
        private readonly float _x;
        public readonly float Width;
        public float Y;

        public Column(float x, float y, float width) { _x = x; Y = y; Width = width; }

        public Rect Next(float height, float gap = 4f)
        {
            var r = new Rect(_x, Y, Width, height);
            Y += height + gap;
            return r;
        }

        public void Label(string text, GUIStyle style = null, float height = 22f, Color? color = null)
            => Ui.Text(Next(height, 2f), text, style ?? Ui.Label, color ?? Ui.Cream);

        public bool Button(string text, bool enabled = true, float height = 34f, Texture2D icon = null, Color? color = null)
            => Ui.Btn(Next(height, 6f), text, enabled, icon, color);

        /// <summary>横に等分割したボタン列。押されたボタンの番号(なければ -1)。selected は金色の枠で示す。</summary>
        public int Buttons(string[] labels, float height = 32f, int selected = -1, bool enabled = true)
        {
            var row = Next(height, 6f);
            float w = (row.width - 6f * (labels.Length - 1)) / labels.Length;
            int clicked = -1;
            for (int i = 0; i < labels.Length; i++)
            {
                var r = new Rect(row.x + i * (w + 6f), row.y, w, row.height);
                var color = i == selected ? new Color(0.62f, 0.42f, 0.1f) : (Color?)null;
                if (Ui.Btn(r, labels[i], enabled, null, color)) clicked = i;
            }
            return clicked;
        }

        public void Space(float h = 8f) => Y += h;
    }
}
