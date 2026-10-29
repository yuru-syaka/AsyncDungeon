using DungeonGame.Client.Art;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>タイトル画面:地面の断面図を背景に、地上を歩く勇者と地下で待つ魔王を動かして見せる。</summary>
    public partial class GameApp
    {
        private void DrawTitle()
        {
            DrawTitleScene();

            // ロゴ
            float logoY = _vh * 0.07f;
            var huge = Ui.Huge;
            int old = huge.fontSize;
            huge.fontSize = _portrait ? 60 : 72;
            float wobble = Mathf.Sin(Time.unscaledTime * 2f) * 3f;
            Ui.Text(new Rect(4, logoY + 4 + wobble, _vw, 80), "ゆうしゃとまおう", huge, new Color(0.35f, 0.08f, 0.1f), outline: false);
            Ui.Text(new Rect(0, logoY + wobble, _vw, 80), "ゆうしゃとまおう", huge, Ui.Gold);
            huge.fontSize = old;
            Ui.Text(new Rect(0, logoY + 78, _vw, 30), "― 掘れ!育てろ!迎え撃て! ―", Ui.Center, Ui.Cream);

            // メニュー
            float w = Mathf.Min(460f, _vw - 40f);
            OpenHelpFirstTime("title", TitleHelp);
            var win = new Rect((_vw - w) / 2f, _portrait ? _vh - 310f : _vh * 0.4f, w, 274);
            Ui.Window(win, "メニュー");
            var col = new Column(win.x + 30, win.y + 26, w - 60);
            if (col.Button("勇者モード", height: 40f, icon: Sprites.Get("icon_sword"))) EnterHeroSetup();
            Ui.Text(col.Next(16f, 4f), $"3人パーティで{_data.Rules.LayerCount}層を攻略し、魔王を倒す(はじめての方はこちら)", Ui.Small, Ui.Sub, false);
            if (col.Button("魔王モード", height: 40f, icon: Sprites.Get("icon_pickaxe"))) EnterDemon();
            Ui.Text(col.Next(16f, 4f), "ダンジョンを掘ってモンスターを育て、勇者を迎え撃つ", Ui.Small, Ui.Sub, false);
            int b = col.Buttons(new[] { "リプレイ", "図鑑", "? 遊び方" }, 36f);
            if (b == 0) EnterReplay();
            if (b == 1) OpenBook();
            if (b == 2) OpenHelp(TitleHelp);

            Ui.Text(new Rect(10, _vh - 22, _vw - 20, 20),
                $"PROTOTYPE {_data.Rules.LayerCount}層版 / フォント:DotGothic16・BIZ UDゴシック(SIL Open Font License 1.1)", Ui.Small, new Color(1, 1, 1, 0.6f), false);
        }

        /// <summary>タイトルの背景:空・草地・地中のトンネル・モンスター・魔王・歩く勇者。</summary>
        private void DrawTitleScene()
        {
            if (Event.current.type != EventType.Repaint) return;
            // 縦長の画面ではマスを大きくし、地下の様子がメニューに隠れないようにする
            float cell = Mathf.Round(_portrait ? _vw / 13f : Mathf.Max(32f, _vw / 24f));
            int cols = Mathf.CeilToInt(_vw / cell) + 1;
            float groundY = _vh * (_portrait ? 0.26f : 0.34f);
            int rows = Mathf.CeilToInt((_vh - groundY) / cell) + 1;
            int mid = cols / 2;

            // 空
            for (int i = 0; i < 6; i++)
                Ui.Fill(new Rect(0, groundY * i / 6f, _vw, groundY / 6f + 1),
                    Color.Lerp(new Color(0.3f, 0.55f, 0.95f), new Color(0.75f, 0.9f, 1f), i / 5f));
            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.Repeat(Time.unscaledTime * (12f + i * 6f) + i * _vw / 4f, _vw + 200) - 150;
                float y = groundY * (0.15f + 0.18f * (i % 3));
                Ui.Fill(new Rect(x, y + 12, 140, 24), new Color(1, 1, 1, 0.9f));
                Ui.Fill(new Rect(x + 30, y, 60, 30), new Color(1, 1, 1, 0.9f));
            }

            // 地中
            for (int gy = 0; gy < rows; gy++)
            for (int gx = 0; gx < cols; gx++)
            {
                var r = new Rect(gx * cell - cell / 2f, groundY + gy * cell, cell + 1f, cell + 1f); // 1ピクセル重ねてすき間を防ぐ
                if (TitleTunnel(gx, gy, mid))
                {
                    GUI.DrawTexture(r, TileArt.Floor(gx, gy));
                    if (!TitleTunnel(gx, gy - 1, mid) && gy > 0) GUI.DrawTexture(r, TileArt.FloorShadowTop);
                }
                else
                {
                    GUI.DrawTexture(r, TileArt.Soil(gx, gy));
                    int h = PixelArt.Hash(gx, gy, 99) % 14;
                    if (h == 0) GUI.DrawTexture(r, TileArt.Nutrient(2));
                    else if (h == 1) GUI.DrawTexture(r, TileArt.Mana(2));
                }
            }
            // 草地(入口の縦穴の上は開けておく)
            for (int gx = 0; gx < cols; gx++)
            {
                if (gx == mid) continue;
                var r = new Rect(gx * cell - cell / 2f, groundY - cell / 2f, cell, cell / 2f);
                GUI.DrawTextureWithTexCoords(r, TileArt.Grass, new Rect(0, 0.5f, 1, 0.5f));
            }

            // 地下のモンスターと魔王
            float t = Time.unscaledTime;
            void Mon(string id, int gx, int gy, float size, float phase, bool flip = false)
            {
                var c = new Rect(gx * cell - cell / 2f, groundY + gy * cell, cell, cell);
                float s = cell * size;
                float bob = Mathf.Abs(Mathf.Sin(t * 4f + phase)) * cell * 0.08f;
                Ui.Fill(new Rect(c.center.x - s * 0.3f, c.yMax - cell * 0.1f, s * 0.6f, cell * 0.08f), new Color(0, 0, 0, 0.35f));
                Ui.Sprite(new Rect(c.center.x - s / 2f, c.yMax - s - bob, s, s), Sprites.Anim(id, gx * 7 + gy), null, flip);
            }
            Mon("moss_slime", mid - 3, 3, 0.8f, 0f);
            Mon("moss_slime", mid + 2, 3, 0.8f, 1.3f, true);
            Mon("mana_mite", mid - 4, 5, 0.8f, 0.6f);
            Mon("cave_lizard", mid + 4, 5, 0.95f, 2f, true);
            Mon("abyss_wyrm", mid - 2, 7, 1.1f, 0.3f);
            var throne = new Rect(mid * cell - cell / 2f, groundY + 7 * cell, cell, cell);
            GUI.DrawTexture(throne, TileArt.Throne);
            Mon("demon_lord", mid, 7, 1.5f, 0f);

            // 地上を歩く勇者たち(右へ進んで、また左から)
            string[] party = { "warrior", "mage", "priest", "thief", "hunter", "paladin", "monk" };
            float span = _vw + cell * 6;
            for (int i = 0; i < party.Length; i++)
            {
                float x = Mathf.Repeat(t * cell * 1.2f - i * cell * 1.1f, span) - cell * 3;
                float bob = Mathf.Abs(Mathf.Sin(t * 8f + i)) * cell * 0.08f;
                float s = cell * 0.95f;
                Ui.Sprite(new Rect(x, groundY - cell * 0.42f - s - bob + cell * 0.42f - cell * 0.02f, s, s), Sprites.Anim(party[i], i * 3));
            }
        }

        /// <summary>タイトル背景のトンネルの形(縦穴→横道→小部屋→魔王の間)。</summary>
        private static bool TitleTunnel(int gx, int gy, int mid)
        {
            if (gy < 0) return false;
            int dx = gx - mid;
            if (dx == 0 && gy <= 3) return true;                       // 入口からの縦穴
            if (gy == 3 && dx >= -4 && dx <= 3) return true;            // 1段目の横道
            if ((dx == -4 || dx == 3) && gy >= 3 && gy <= 5) return true;
            if (gy == 5 && dx >= -5 && dx <= 5) return true;            // 2段目の横道
            if ((dx == -5 || dx == 5) && gy >= 5 && gy <= 7) return true;
            if (gy == 7 && dx >= -5 && dx <= 5) return true;            // 魔王の間
            return false;
        }
    }
}
