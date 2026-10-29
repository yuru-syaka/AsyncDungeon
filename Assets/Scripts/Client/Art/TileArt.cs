using UnityEngine;

namespace DungeonGame.Client.Art
{
    /// <summary>
    /// 地面のタイル(16×16)をプログラムで描く。土は数種類の模様を用意し、マスの座標で選んで単調にならないようにする。
    /// 養分は緑の苔、魔分は青い結晶として土の上に重ねて描く(量に応じて3段階)。
    /// </summary>
    public static class TileArt
    {
        public const int Size = 16;
        private const int Variants = 4;

        private static Texture2D[] _soil, _bedrock, _floor, _fogTex;
        private static Texture2D[] _nutrient, _mana;
        private static Texture2D _entrance, _stairs, _throne, _grass, _floorShadowTop, _floorShadowSide;

        private static readonly Color32 SoilMid = new Color32(122, 80, 46, 255);
        private static readonly Color32 SoilDark = new Color32(92, 58, 34, 255);
        private static readonly Color32 SoilLight = new Color32(148, 102, 60, 255);
        private static readonly Color32 Pebble = new Color32(160, 150, 140, 255);
        private static readonly Color32 CaveDark = new Color32(34, 24, 22, 255);
        private static readonly Color32 CaveMid = new Color32(48, 34, 30, 255);
        private static readonly Color32 CaveSpeck = new Color32(64, 48, 40, 255);

        public static Texture2D Soil(int x, int y) => Pick(ref _soil, BuildSoil, x, y);
        public static Texture2D Bedrock(int x, int y) => Pick(ref _bedrock, BuildBedrock, x, y);
        public static Texture2D Floor(int x, int y) => Pick(ref _floor, BuildFloor, x, y);
        public static Texture2D Fog(int x, int y) => Pick(ref _fogTex, BuildFog, x, y);

        /// <summary>養分の重ね絵(level 1〜3)。</summary>
        public static Texture2D Nutrient(int level) => Levels(ref _nutrient, true)[Mathf.Clamp(level, 1, 3) - 1];
        /// <summary>魔分の重ね絵(level 1〜3)。</summary>
        public static Texture2D Mana(int level) => Levels(ref _mana, false)[Mathf.Clamp(level, 1, 3) - 1];

        public static Texture2D Entrance => _entrance != null ? _entrance : (_entrance = BuildEntrance());
        public static Texture2D Stairs => _stairs != null ? _stairs : (_stairs = BuildStairs());
        public static Texture2D Throne => _throne != null ? _throne : (_throne = BuildThrone());
        public static Texture2D Grass => _grass != null ? _grass : (_grass = BuildGrass());
        /// <summary>通路の上辺・横辺に落ちる影(土との境目を立体的に見せる)。</summary>
        public static Texture2D FloorShadowTop => _floorShadowTop != null ? _floorShadowTop : (_floorShadowTop = BuildShadow(true));
        public static Texture2D FloorShadowSide => _floorShadowSide != null ? _floorShadowSide : (_floorShadowSide = BuildShadow(false));

        // ================= モンスターの住みか(土の見た目) =================

        private static readonly System.Collections.Generic.Dictionary<string, Texture2D[]> SkinCache =
            new System.Collections.Generic.Dictionary<string, Texture2D[]>();
        private static Texture2D[] _bones;

        /// <summary>
        /// モンスターが住みついた土の見た目(土の上に重ねる絵)。種族ごとに違う。
        /// 例:スライムはぬめり、ゴーレムは結晶、ドクロ魔導士は骨、冥竜は焼け焦げ。該当しなければ null。
        /// </summary>
        public static Texture2D Skin(string speciesId, int x, int y)
        {
            if (speciesId == null) return null;
            if (!SkinCache.TryGetValue(speciesId, out var arr))
            {
                arr = new[] { BuildSkin(speciesId, 0), BuildSkin(speciesId, 1) };
                SkinCache[speciesId] = arr;
            }
            return arr[0] == null ? null : arr[PixelArt.Hash(x, y, 31) % 2];
        }

        /// <summary>通路に積もった骨の重ね絵(level 1〜3)。</summary>
        public static Texture2D BonesOverlay(int level)
        {
            if (_bones == null)
            {
                _bones = new Texture2D[3];
                for (int lv = 1; lv <= 3; lv++)
                {
                    var px = Blank();
                    for (int i = 0; i < lv * 2; i++)
                    {
                        int bx = PixelArt.Hash(i, lv, 61) % 10 + 3, by = PixelArt.Hash(lv, i, 62) % 4 + 11;
                        DrawBone(px, bx, by, i % 2 == 0);
                    }
                    if (lv == 3) DrawSkull(px, 6, 9);
                    _bones[lv - 1] = Finish(px);
                }
            }
            return _bones[Mathf.Clamp(level, 1, 3) - 1];
        }

        private static readonly Color32 BoneC = new Color32(236, 228, 200, 255);
        private static readonly Color32 BoneShade = new Color32(170, 160, 130, 255);

        private static void DrawBone(Color32[] px, int x, int y, bool flat)
        {
            if (flat)
            {
                Set(px, x - 2, y, BoneC); Set(px, x - 1, y, BoneC); Set(px, x, y, BoneC); Set(px, x + 1, y, BoneC); Set(px, x + 2, y, BoneC);
                Set(px, x - 2, y - 1, BoneShade); Set(px, x + 2, y - 1, BoneShade);
            }
            else
            {
                Set(px, x - 1, y - 1, BoneC); Set(px, x, y, BoneC); Set(px, x + 1, y + 1, BoneC);
                Set(px, x - 2, y - 1, BoneShade); Set(px, x + 2, y + 1, BoneShade);
            }
        }

        private static void DrawSkull(Color32[] px, int x, int y)
        {
            var dark = new Color32(40, 30, 40, 255);
            for (int dy = 0; dy < 3; dy++)
            for (int dx = 0; dx < 4; dx++) Set(px, x + dx, y + dy, BoneC);
            Set(px, x + 1, y + 1, dark); Set(px, x + 2, y + 1, dark);
            Set(px, x + 1, y + 3, BoneShade); Set(px, x + 2, y + 3, BoneShade);
        }

        /// <summary>種族ごとの住みかの絵を描く。色の薄い「にじみ」+種族らしい小物。</summary>
        private static Texture2D BuildSkin(string id, int v)
        {
            var px = Blank();
            Color32 wash;
            switch (id)
            {
                case "moss_slime": wash = new Color32(90, 200, 80, 70); break;
                case "mana_mite": wash = new Color32(70, 120, 220, 55); break;
                case "glow_shroom": wash = new Color32(250, 230, 120, 50); break;
                case "cave_lizard": wash = new Color32(220, 140, 60, 45); break;
                case "shadow_bat": wash = new Color32(30, 20, 50, 110); break;
                case "crystal_golem": wash = new Color32(120, 220, 240, 60); break;
                case "abyss_wyrm": wash = new Color32(150, 60, 190, 70); break;
                case "armor_centipede": wash = new Color32(200, 60, 40, 55); break;
                case "skull_mage": wash = new Color32(60, 30, 70, 90); break;
                case "abyss_dragon": wash = new Color32(20, 16, 20, 150); break;
                default: return null;
            }
            // にじみ(ところどころ濃さを変えて、まだらにする)
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var c = wash;
                if (PixelArt.Hash(x, y, v + 7) % 4 == 0) c.a = (byte)(wash.a / 2);
                Set(px, x, y, c);
            }

            int h(int i, int s) => PixelArt.Hash(i, v, s);
            switch (id)
            {
                case "moss_slime": // ぬめり:上から垂れる緑のしずく
                    for (int i = 0; i < 4; i++)
                    {
                        int x = h(i, 1) % 14 + 1, len = h(i, 2) % 5 + 2;
                        for (int y = 0; y < len; y++) Set(px, x, y, new Color32(140, 230, 110, 220));
                        Set(px, x, len, new Color32(190, 250, 150, 255));
                    }
                    break;
                case "mana_mite": // 小さな巣穴
                    for (int i = 0; i < 3; i++)
                    {
                        int x = h(i, 3) % 12 + 2, y = h(i, 4) % 12 + 2;
                        Set(px, x, y, new Color32(20, 20, 40, 255)); Set(px, x + 1, y, new Color32(20, 20, 40, 255));
                        Set(px, x - 1, y, new Color32(110, 170, 255, 255)); Set(px, x + 2, y, new Color32(110, 170, 255, 255));
                    }
                    break;
                case "glow_shroom": // 小さな光るキノコ
                    for (int i = 0; i < 3; i++)
                    {
                        int x = h(i, 5) % 12 + 2, y = h(i, 6) % 10 + 4;
                        Set(px, x - 1, y, new Color32(252, 222, 84, 255)); Set(px, x, y, new Color32(255, 248, 184, 255)); Set(px, x + 1, y, new Color32(252, 222, 84, 255));
                        Set(px, x, y + 1, new Color32(255, 216, 142, 255));
                    }
                    break;
                case "cave_lizard": // 爪あと
                    for (int i = 0; i < 2; i++)
                    {
                        int x = h(i, 7) % 8 + 3, y = h(i, 8) % 8 + 3;
                        for (int k = 0; k < 3; k++)
                        for (int s = 0; s < 4; s++) Set(px, x + k * 2 + s, y + s, new Color32(70, 40, 20, 200));
                    }
                    break;
                case "shadow_bat": // 暗がりに白いフン
                    for (int i = 0; i < 5; i++) Set(px, h(i, 9) % 15, h(i, 10) % 15, new Color32(220, 220, 210, 255));
                    break;
                case "crystal_golem": // 結晶の柱
                    for (int i = 0; i < 2; i++)
                    {
                        int x = h(i, 11) % 11 + 2, y = h(i, 12) % 6 + 8;
                        for (int k = 0; k < 5; k++) { Set(px, x, y - k, new Color32(150, 235, 255, 255)); Set(px, x + 1, y - k, new Color32(80, 170, 220, 255)); }
                        Set(px, x, y - 5, new Color32(255, 255, 255, 255));
                    }
                    break;
                case "abyss_wyrm": // くねった掘り跡
                    {
                        int y = h(0, 13) % 8 + 4;
                        for (int x = 0; x < Size; x++)
                        {
                            int yy = y + (int)Mathf.Round(Mathf.Sin((x + v * 3) * 0.7f) * 2f);
                            Set(px, x, yy, new Color32(40, 20, 50, 230)); Set(px, x, yy + 1, new Color32(160, 80, 200, 200));
                        }
                    }
                    break;
                case "armor_centipede": // 赤い殻のかけら
                    for (int i = 0; i < 4; i++)
                    {
                        int x = h(i, 14) % 13 + 1, y = h(i, 15) % 13 + 1;
                        Set(px, x, y, new Color32(222, 58, 52, 255)); Set(px, x + 1, y, new Color32(128, 22, 36, 255));
                    }
                    break;
                case "skull_mage": // 埋まった骨
                    DrawBone(px, h(0, 16) % 8 + 4, h(0, 17) % 6 + 4, true);
                    DrawBone(px, h(1, 16) % 8 + 4, h(1, 17) % 5 + 10, false);
                    break;
                case "abyss_dragon": // 焼け焦げと燃えさし
                    for (int i = 0; i < 6; i++)
                    {
                        int x = h(i, 18) % 15, y = h(i, 19) % 15;
                        Set(px, x, y, i % 2 == 0 ? new Color32(255, 120, 40, 255) : new Color32(200, 40, 30, 255));
                    }
                    break;
            }
            return Finish(px);
        }

        private delegate Texture2D Builder(int variant);

        private static Texture2D Pick(ref Texture2D[] arr, Builder build, int x, int y)
        {
            if (arr == null)
            {
                arr = new Texture2D[Variants];
                for (int i = 0; i < Variants; i++) arr[i] = build(i);
            }
            return arr[PixelArt.Hash(x, y) % Variants];
        }

        // ---------------- 描画の補助 ----------------

        private static Color32[] Blank() => new Color32[Size * Size];

        private static void Set(Color32[] px, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            px[(Size - 1 - y) * Size + x] = c; // 上が0になるよう反転
        }

        private static Texture2D Finish(Color32[] px)
        {
            var t = PixelArt.NewTexture(Size, Size);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        // ---------------- 各タイル ----------------

        private static Texture2D BuildSoil(int v)
        {
            var px = Blank();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int h = PixelArt.Hash(x, y, v + 11) % 100;
                Set(px, x, y, h < 18 ? SoilDark : h < 30 ? SoilLight : SoilMid);
            }
            // 小石と地層の線
            for (int i = 0; i < 3; i++)
            {
                int sx = PixelArt.Hash(i, v, 3) % 14 + 1, sy = PixelArt.Hash(v, i, 5) % 14 + 1;
                Set(px, sx, sy, Pebble);
                Set(px, sx + 1, sy, SoilDark);
            }
            int layer = PixelArt.Hash(v, 9) % 12 + 2;
            for (int x = 0; x < Size; x++) if (PixelArt.Hash(x, layer, v) % 3 != 0) Set(px, x, layer, SoilDark);
            return Finish(px);
        }

        private static Texture2D BuildBedrock(int v)
        {
            var px = Blank();
            var stone = new Color32(96, 94, 112, 255);
            var light = new Color32(124, 122, 140, 255);
            var mortar = new Color32(52, 50, 64, 255);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                bool row = y % 8 == 7;
                int offset = (y / 8) % 2 == 0 ? 0 : 4;
                bool col = (x + offset + v) % 8 == 7;
                var c = row || col ? mortar : (y % 8 == 0 ? light : stone);
                if (!row && !col && PixelArt.Hash(x, y, v + 40) % 9 == 0) c = light;
                Set(px, x, y, c);
            }
            return Finish(px);
        }

        private static Texture2D BuildFloor(int v)
        {
            var px = Blank();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int h = PixelArt.Hash(x, y, v + 70) % 100;
                Set(px, x, y, h < 10 ? CaveSpeck : h < 45 ? CaveMid : CaveDark);
            }
            // 地面(下側)に小石
            for (int x = 0; x < Size; x++)
                if (PixelArt.Hash(x, v, 77) % 4 == 0) Set(px, x, Size - 1, CaveSpeck);
            return Finish(px);
        }

        private static Texture2D BuildFog(int v)
        {
            var px = Blank();
            var a = new Color32(10, 8, 16, 255);
            var b = new Color32(18, 14, 28, 255);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                Set(px, x, y, PixelArt.Hash(x, y, v + 90) % 7 == 0 ? b : a);
            return Finish(px);
        }

        private static Texture2D[] Levels(ref Texture2D[] arr, bool nutrient)
        {
            if (arr != null) return arr;
            arr = new Texture2D[3];
            for (int level = 1; level <= 3; level++)
            {
                var px = Blank();
                int count = level == 1 ? 2 : level == 2 ? 4 : 7;
                for (int i = 0; i < count; i++)
                {
                    int x = PixelArt.Hash(i, level, nutrient ? 1 : 2) % 13 + 1;
                    int y = PixelArt.Hash(level, i, nutrient ? 3 : 4) % 13 + 1;
                    if (nutrient)
                    {
                        // 苔のかたまり
                        var g = new Color32(92, 196, 72, 255);
                        var l = new Color32(178, 236, 120, 255);
                        Set(px, x, y, g); Set(px, x + 1, y, l); Set(px, x, y - 1, g);
                        if (level >= 2) Set(px, x - 1, y, g);
                    }
                    else
                    {
                        // 青い結晶(ひし形)
                        var b = new Color32(72, 140, 232, 255);
                        var c = new Color32(150, 230, 255, 255);
                        Set(px, x, y - 1, c); Set(px, x - 1, y, b); Set(px, x, y, c); Set(px, x + 1, y, b); Set(px, x, y + 1, b);
                    }
                }
                arr[level - 1] = Finish(px);
            }
            return arr;
        }

        private static Texture2D BuildEntrance()
        {
            var px = Blank();
            var wood = new Color32(146, 94, 52, 255);
            var woodDark = new Color32(84, 52, 30, 255);
            var light = new Color32(255, 236, 150, 255);
            var light2 = new Color32(252, 200, 90, 255);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                bool frame = x <= 2 || x >= 13 || y <= 2;
                bool inside = !frame;
                Color32 c = frame ? ((x + y) % 3 == 0 ? woodDark : wood) : (y < 8 ? light : light2);
                if (inside && (x == 3 || x == 12)) c = woodDark;
                Set(px, x, y, c);
            }
            return Finish(px);
        }

        private static Texture2D BuildStairs()
        {
            var px = Blank();
            var step = new Color32(214, 186, 120, 255);
            var stepDark = new Color32(150, 116, 64, 255);
            var hole = new Color32(16, 10, 10, 255);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int stepIndex = x / 4;
                int top = 3 + stepIndex * 3; // 右に行くほど深くなる階段
                Color32 c = y < top ? hole : (y == top ? step : stepDark);
                Set(px, x, y, c);
            }
            return Finish(px);
        }

        private static Texture2D BuildThrone()
        {
            var px = Blank();
            var carpet = new Color32(128, 22, 36, 255);
            var carpet2 = new Color32(160, 30, 46, 255);
            var gold = new Color32(252, 222, 84, 255);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Color32 c = (x + y) % 4 == 0 ? carpet2 : carpet;
                if (x == 1 || x == 14 || y == 14) c = gold;
                Set(px, x, y, c);
            }
            return Finish(px);
        }

        private static Texture2D BuildGrass()
        {
            var px = Blank();
            var g = new Color32(92, 196, 72, 255);
            var gd = new Color32(38, 118, 52, 255);
            var l = new Color32(178, 236, 120, 255);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Color32 c;
                if (y < 4) c = PixelArt.Hash(x, 0, 5) % 3 == 0 && y < 2 ? new Color32(0, 0, 0, 0) : (y == 0 ? l : g);
                else if (y < 7) c = PixelArt.Hash(x, y, 6) % 2 == 0 ? gd : g;
                else c = PixelArt.Hash(x, y, 7) % 5 == 0 ? SoilDark : SoilMid;
                Set(px, x, y, c);
            }
            return Finish(px);
        }

        private static Texture2D BuildShadow(bool top)
        {
            var px = Blank();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int d = top ? y : x;
                byte a = d == 0 ? (byte)170 : d == 1 ? (byte)110 : d == 2 ? (byte)50 : (byte)0;
                Set(px, x, y, new Color32(0, 0, 0, a));
            }
            return Finish(px);
        }
    }
}
