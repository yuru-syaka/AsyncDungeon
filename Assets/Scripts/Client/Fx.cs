using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>
    /// 演出(見た目だけ):浮かび上がるダメージ数字、はじける粒子、画面の揺れ。
    /// 位置はマップのマス座標で持ち、描くときに MapRenderer で画面座標に直す。
    /// ゲームの結果には一切関係しないので、ここでは UnityEngine.Random を使ってよい。
    /// </summary>
    public sealed class Fx
    {
        private sealed class Pop
        {
            public Vector2 Pos;
            public string Text;
            public Color Color;
            public float T, Life, Size;
        }

        private sealed class Particle
        {
            public Vector2 Pos, Vel;
            public Color Color;
            public float T, Life, Size;
        }

        /// <summary>飛んでいく弾(魔法・射程攻撃)。</summary>
        private sealed class Projectile
        {
            public Vector2 From, To;
            public Color Color;
            public float T, Life, Size;
        }

        /// <summary>斬撃・爪あとの線。</summary>
        private sealed class Slash
        {
            public Vector2 Center;
            public Color Color;
            public float T, Life, Size;
            public bool Claw;   // true なら3本線の爪あと
            public bool Mirror; // 斜めの向きを反転
        }

        private readonly List<Pop> _pops = new List<Pop>();
        private readonly List<Particle> _parts = new List<Particle>();
        private readonly List<Projectile> _shots = new List<Projectile>();
        private readonly List<Slash> _slashes = new List<Slash>();
        private float _shake;
        private int _lastFrame = -1;
        private GUIStyle _style;

        /// <summary>画面の揺れ(ピクセル)。マップを描くときにずらす量。</summary>
        public Vector2 ShakeOffset { get; private set; }

        public void Clear()
        {
            _pops.Clear();
            _parts.Clear();
            _shots.Clear();
            _slashes.Clear();
            _shake = 0;
        }

        /// <summary>from のマスから to のマスへ光の弾を飛ばす(着弾ではじける)。</summary>
        public void Shoot(Vector2 from, Vector2 to, Color color, float size = 0.28f)
        {
            float dist = (to - from).magnitude;
            _shots.Add(new Projectile
            {
                From = from + new Vector2(0.5f, 0.45f), To = to + new Vector2(0.5f, 0.5f),
                Color = color, Life = Mathf.Clamp(0.08f * dist, 0.12f, 0.3f), Size = size,
            });
        }

        /// <summary>マスの上に斬撃(1本の斜め線)を出す。</summary>
        public void SlashAt(Vector2 grid, Color color, float size = 1f, bool mirror = false)
        {
            _slashes.Add(new Slash { Center = grid + new Vector2(0.5f, 0.5f), Color = color, Life = 0.22f, Size = size, Mirror = mirror });
        }

        /// <summary>マスの上に爪あと(3本線)を出す。</summary>
        public void ClawAt(Vector2 grid, Color color)
        {
            _slashes.Add(new Slash { Center = grid + new Vector2(0.5f, 0.5f), Color = color, Life = 0.25f, Size = 0.8f, Claw = true, Mirror = Random.value < 0.5f });
        }

        /// <summary>マスの上に文字を浮かべる(ダメージ・回復量など)。</summary>
        public void Popup(Vector2 grid, string text, Color color, float size = 1f)
        {
            _pops.Add(new Pop
            {
                Pos = grid + new Vector2(0.5f + Random.Range(-0.15f, 0.15f), 0.1f),
                Text = text, Color = color, Life = 0.9f, Size = size,
            });
        }

        /// <summary>マスの中心から粒子を飛び散らせる。</summary>
        public void Burst(Vector2 grid, Color color, int count, float speed = 3f, float size = 0.12f)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1f) * speed;
                _parts.Add(new Particle
                {
                    Pos = grid + new Vector2(0.5f, 0.5f), Vel = new Vector2(dir.x, dir.y - speed * 0.6f),
                    Color = color, Life = Random.Range(0.35f, 0.7f), Size = size * Random.Range(0.7f, 1.3f),
                });
            }
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        /// <summary>弾の今の位置(少し山なりに飛ぶ)。</summary>
        private static Vector2 ShotPos(Projectile s)
        {
            float k = Mathf.Clamp01(s.T / s.Life);
            var p = Vector2.Lerp(s.From, s.To, k);
            p.y -= Mathf.Sin(k * Mathf.PI) * 0.35f;
            return p;
        }

        /// <summary>1フレームに1回、時間を進める。</summary>
        private void Tick()
        {
            if (Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                p.T += dt;
                p.Pos.y -= dt * 1.2f;
                if (p.T >= p.Life) _pops.RemoveAt(i);
            }
            for (int i = _parts.Count - 1; i >= 0; i--)
            {
                var p = _parts[i];
                p.T += dt;
                p.Vel.y += 9f * dt; // 重力
                p.Pos += p.Vel * dt;
                if (p.T >= p.Life) _parts.RemoveAt(i);
            }
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                s.T += dt;
                var p = ShotPos(s);
                // 弾の通り道に小さな火の粉を残す
                _parts.Add(new Particle { Pos = p, Vel = Random.insideUnitCircle * 0.5f, Color = s.Color, Life = 0.25f, Size = s.Size * 0.35f });
                if (s.T >= s.Life)
                {
                    _shots.RemoveAt(i);
                    Burst(s.To - new Vector2(0.5f, 0.5f), s.Color, 10, 3f, 0.1f);
                }
            }
            for (int i = _slashes.Count - 1; i >= 0; i--)
            {
                _slashes[i].T += dt;
                if (_slashes[i].T >= _slashes[i].Life) _slashes.RemoveAt(i);
            }
            _shake = Mathf.Max(0, _shake - dt * 30f);
            ShakeOffset = _shake > 0 ? new Vector2(Random.Range(-_shake, _shake), Random.Range(-_shake, _shake)) : Vector2.zero;
        }

        public void Draw(MapRenderer map)
        {
            if (Event.current.type != EventType.Repaint) return;
            Tick();
            float cell = map.Cell;
            foreach (var p in _parts)
            {
                var c = p.Color;
                c.a = 1f - p.T / p.Life;
                var s = Mathf.Max(2f, p.Size * cell);
                var pos = map.ToScreen(p.Pos);
                Ui.Fill(new Rect(Mathf.Round(pos.x - s / 2), Mathf.Round(pos.y - s / 2), s, s), c);
            }

            // 斬撃・爪あと:斜めの線を、ドットを並べて描く。前半で伸び、後半で消える
            foreach (var s in _slashes)
            {
                float k = s.T / s.Life;
                float grow = Mathf.Clamp01(k / 0.45f);
                var c = s.Color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                int lines = s.Claw ? 3 : 1;
                float len = 0.75f * s.Size;
                float dot = Mathf.Max(2f, cell * 0.07f * (s.Claw ? 0.8f : 1.2f));
                for (int l = 0; l < lines; l++)
                {
                    float shift = (l - (lines - 1) / 2f) * 0.18f;
                    var dir = s.Mirror ? new Vector2(-1, 1).normalized : new Vector2(1, 1).normalized;
                    var normal = new Vector2(-dir.y, dir.x);
                    var start = s.Center - dir * len / 2f + normal * shift;
                    int steps = 10;
                    for (int i = 0; i <= steps * grow; i++)
                    {
                        var g = start + dir * (len * i / steps);
                        var pos = map.ToScreen(g);
                        // 線の両端は細く、真ん中は太く
                        float thick = dot * (0.6f + 0.8f * Mathf.Sin(Mathf.PI * i / steps));
                        Ui.Fill(new Rect(Mathf.Round(pos.x - thick / 2), Mathf.Round(pos.y - thick / 2), thick, thick), c);
                    }
                }
            }

            // 弾:光の芯と、まわりのぼんやりした光
            foreach (var s in _shots)
            {
                var pos = map.ToScreen(ShotPos(s));
                float core = Mathf.Max(4f, s.Size * cell * 0.5f);
                float glow = core * 2.2f;
                var gc = s.Color;
                gc.a = 0.35f;
                Ui.Fill(new Rect(pos.x - glow / 2, pos.y - glow / 2, glow, glow), gc);
                Ui.Fill(new Rect(pos.x - core / 2, pos.y - core / 2, core, core), Color.Lerp(s.Color, Color.white, 0.6f));
            }

            if (_style == null) _style = new GUIStyle(Ui.Center) { font = Ui.DotFont }; // 数字の演出はドット風フォント
            foreach (var p in _pops)
            {
                float k = p.T / p.Life;
                // 出てすぐ少し大きく、最後に薄くなる
                float scale = k < 0.15f ? Mathf.Lerp(1.6f, 1f, k / 0.15f) : 1f;
                _style.fontSize = Mathf.Clamp((int)(cell * 0.42f * p.Size * scale), 10, 40);
                var c = p.Color;
                c.a = k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
                var pos = map.ToScreen(p.Pos);
                Ui.Text(new Rect(pos.x - 60, pos.y - 20, 120, 40), p.Text, _style, c);
            }
        }
    }
}
