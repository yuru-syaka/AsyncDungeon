using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Client.Art
{
    /// <summary>
    /// 文字列で描いたドット絵から Texture2D を作る。1文字=1ピクセル、'.' は透明、'|' は読みやすさ用の区切り(無視される)。
    /// 色は下のパレット(1文字→色)で決まる。絵を直したいときは Sprites.cs の文字列を書き換えるだけでよい。
    ///
    /// キャラクター用(BuildCharacter)は次の処理を自動で行う:
    ///  ・外側に1ピクセルの輪郭を付ける(描くときに輪郭を気にしなくてよい)
    ///  ・足元をそろえる(絵の一番下の行が、どのキャラでも同じ高さに来る)
    ///  ・待機アニメの2コマ目(上半身が1ピクセル沈む「息づかい」)を作る
    /// 描画時は拡大してもぼやけないよう、ポイントフィルタにする。
    /// </summary>
    public static class PixelArt
    {
        private static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
        {
            ['k'] = new Color32(22, 16, 28, 255),    // 輪郭(黒)
            ['w'] = new Color32(255, 255, 255, 255), // 白
            ['i'] = new Color32(214, 214, 226, 255), // 明るい灰
            ['e'] = new Color32(150, 150, 165, 255), // 灰
            ['E'] = new Color32(78, 76, 96, 255),    // 暗い灰
            ['x'] = new Color32(40, 36, 58, 255),    // ほぼ黒(紺)
            ['g'] = new Color32(92, 196, 72, 255),   // 緑
            ['G'] = new Color32(38, 118, 52, 255),   // 濃い緑
            ['l'] = new Color32(178, 236, 120, 255), // 明るい緑
            ['L'] = new Color32(20, 70, 42, 255),    // 深い緑
            ['b'] = new Color32(72, 140, 232, 255),  // 青
            ['B'] = new Color32(34, 62, 146, 255),   // 濃い青
            ['c'] = new Color32(132, 224, 255, 255), // 水色
            ['C'] = new Color32(20, 30, 82, 255),    // 深い紺
            ['r'] = new Color32(222, 58, 52, 255),   // 赤
            ['R'] = new Color32(128, 22, 36, 255),   // 濃い赤
            ['q'] = new Color32(255, 132, 120, 255), // 明るい赤
            ['o'] = new Color32(242, 150, 52, 255),  // 橙
            ['O'] = new Color32(176, 88, 30, 255),   // 濃い橙
            ['u'] = new Color32(255, 216, 142, 255), // 明るい橙(腹)
            ['y'] = new Color32(252, 222, 84, 255),  // 黄
            ['Y'] = new Color32(188, 136, 32, 255),  // 金(暗)
            ['j'] = new Color32(255, 248, 184, 255), // 淡い黄(光)
            ['p'] = new Color32(158, 72, 204, 255),  // 紫
            ['P'] = new Color32(88, 30, 122, 255),   // 濃い紫
            ['v'] = new Color32(206, 156, 255, 255), // 明るい紫
            ['d'] = new Color32(46, 18, 60, 255),    // 深い紫
            ['m'] = new Color32(244, 122, 196, 255), // 桃
            ['s'] = new Color32(252, 206, 164, 255), // 肌
            ['S'] = new Color32(212, 150, 110, 255), // 肌(影)
            ['F'] = new Color32(160, 100, 70, 255),  // 肌(濃い影)
            ['n'] = new Color32(146, 94, 52, 255),   // 茶
            ['N'] = new Color32(84, 52, 30, 255),    // こげ茶
            ['h'] = new Color32(190, 130, 70, 255),  // 明るい茶
            ['z'] = new Color32(160, 180, 210, 255), // 鋼
            ['Z'] = new Color32(84, 100, 136, 255),  // 鋼(影)
            ['t'] = new Color32(60, 180, 170, 255),  // 青緑
            ['T'] = new Color32(30, 110, 110, 255),  // 濃い青緑
            ['a'] = new Color32(240, 232, 200, 255), // 骨・牙
        };

        private static readonly Color32 Outline = new Color32(22, 16, 28, 255);
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        /// <summary>文字列の絵からテクスチャを作る(アイコン用。輪郭の自動付加はしない)。</summary>
        public static Texture2D Build(string[] rows)
        {
            var grid = Parse(rows, out int w, out int h);
            return ToTexture(grid, w, h);
        }

        /// <summary>
        /// キャラクターの絵を作る。返り値は [待機1コマ目, 待機2コマ目]。
        /// 1ピクセルの余白を足して輪郭を付け、足元を下から2行目にそろえる。
        /// </summary>
        public static Texture2D[] BuildCharacter(string[] rows)
        {
            var src = Parse(rows, out int sw, out int sh);
            int w = sw + 2, h = sh + 2;

            // 余白付きの画像に写し、足元(一番下の不透明な行)を h-2 にそろえる
            int lowest = -1;
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
                if (src[y * sw + x].a > 0) lowest = y;
            int shift = (h - 2) - (lowest + 1);
            var a = new Color32[w * h];
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                int ny = y + shift;
                if (ny < 0 || ny >= h) continue;
                a[ny * w + x + 1] = src[y * sw + x];
            }

            // 2コマ目:足元から6行より上を1ピクセル下げる(息を吐いて沈むように見える)
            int pivot = (h - 2) - 6;
            var b = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (y > pivot) b[y * w + x] = a[y * w + x];
                else if (y > 0) b[y * w + x] = a[(y - 1) * w + x];
            }

            return new[] { ToTexture(AddOutline(a, w, h), w, h), ToTexture(AddOutline(b, w, h), w, h) };
        }

        /// <summary>不透明な絵のまわりの透明ピクセルを輪郭色で塗る。</summary>
        private static Color32[] AddOutline(Color32[] px, int w, int h)
        {
            var o = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a > 0) continue;
                bool edge = (x > 0 && px[y * w + x - 1].a > 0) || (x < w - 1 && px[y * w + x + 1].a > 0) ||
                            (y > 0 && px[(y - 1) * w + x].a > 0) || (y < h - 1 && px[(y + 1) * w + x].a > 0);
                if (edge) o[y * w + x] = Outline;
            }
            return o;
        }

        /// <summary>文字列を色の配列にする(上の行が y=0)。</summary>
        private static Color32[] Parse(string[] rows, out int w, out int h)
        {
            var clean = new string[rows.Length];
            w = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                clean[i] = rows[i].Replace("|", "");
                if (clean[i].Length > w) w = clean[i].Length;
            }
            h = rows.Length;
            var grid = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = x < clean[y].Length ? clean[y][x] : '.';
                grid[y * w + x] = Palette.TryGetValue(c, out var col) ? col : Clear;
            }
            return grid;
        }

        private static Texture2D ToTexture(Color32[] grid, int w, int h)
        {
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            // テクスチャは下から上なので、行を上下反転して入れる
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = grid[y * w + x];
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static Texture2D NewTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
        }

        /// <summary>座標から決まる疑似乱数(タイルの模様のばらつき用。ゲームロジックには使わない)。</summary>
        public static int Hash(int x, int y, int salt = 0)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }
    }
}
