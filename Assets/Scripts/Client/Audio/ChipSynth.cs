using UnityEngine;

namespace DungeonGame.Client.Audio
{
    /// <summary>
    /// ファミコン風の音をプログラムで合成する小さなシンセサイザー。
    /// 矩形波(メロディ)・三角波(ベース)・ノイズ(打楽器・効果音)を float の配列に足し込み、AudioClip にする。
    /// 音の素材ファイルを使わないので、ライセンスを気にせず使えるオリジナル音源になる。
    /// </summary>
    public static class ChipSynth
    {
        public const int Rate = 22050;

        /// <summary>MIDIノート番号 → 周波数(Hz)。69 = A4 = 440Hz。</summary>
        public static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>
        /// 音名("C4", "F#5", "Bb3")→ MIDIノート番号。読めなければ -1。
        /// </summary>
        public static int ParseNote(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            int[] baseSemi = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G
            char c = char.ToUpperInvariant(s[0]);
            if (c < 'A' || c > 'G') return -1;
            int semi = baseSemi[c - 'A'];
            int i = 1;
            if (i < s.Length && s[i] == '#') { semi++; i++; }
            else if (i < s.Length && s[i] == 'b') { semi--; i++; }
            if (!int.TryParse(s.Substring(i), out int oct)) return -1;
            return 12 * (oct + 1) + semi;
        }

        public static int Samples(float seconds) => Mathf.Max(1, (int)(seconds * Rate));

        /// <summary>音量の時間変化(立ち上がり → 減衰して持続 → 最後に消える)。</summary>
        private static float Envelope(int i, int len, float attack, float decay, float sustain, float release)
        {
            float t = i / (float)Rate;
            float total = len / (float)Rate;
            float a = attack > 0 ? Mathf.Clamp01(t / attack) : 1f;
            float d = decay > 0 ? Mathf.Lerp(1f, sustain, Mathf.Clamp01((t - attack) / decay)) : sustain;
            float r = release > 0 ? Mathf.Clamp01((total - t) / release) : 1f;
            return a * (t < attack ? 1f : d) * r;
        }

        /// <summary>矩形波(duty = 音の明るさ。0.125 で細く、0.5 で太い)。周波数は fromHz → toHz へ滑らかに変化させられる。</summary>
        public static void Square(float[] buf, int start, int len, float fromHz, float toHz, float vol, float duty = 0.25f,
            float attack = 0.004f, float decay = 0.08f, float sustain = 0.7f, float release = 0.03f, float vibrato = 0f)
        {
            double phase = 0;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                if (start + i < 0) continue;
                float k = i / (float)len;
                float f = Mathf.Lerp(fromHz, toHz, k) * (1f + vibrato * Mathf.Sin(i / (float)Rate * 2f * Mathf.PI * 6f));
                phase += f / Rate;
                float s = (phase - System.Math.Floor(phase)) < duty ? 1f : -1f;
                buf[start + i] += s * vol * Envelope(i, len, attack, decay, sustain, release);
            }
        }

        /// <summary>三角波(やわらかい音。ベースや回復音に)。</summary>
        public static void Triangle(float[] buf, int start, int len, float fromHz, float toHz, float vol,
            float attack = 0.004f, float decay = 0.1f, float sustain = 0.8f, float release = 0.03f)
        {
            double phase = 0;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                if (start + i < 0) continue;
                float f = Mathf.Lerp(fromHz, toHz, i / (float)len);
                phase += f / Rate;
                float p = (float)(phase - System.Math.Floor(phase));
                float s = 4f * Mathf.Abs(p - 0.5f) - 1f;
                buf[start + i] += s * vol * Envelope(i, len, attack, decay, sustain, release);
            }
        }

        /// <summary>
        /// ノイズ(打楽器・爆発・土を掘る音)。smooth を大きくするほど低くこもった音になる。
        /// seed で毎回同じノイズにする(同じ効果音が同じ音で鳴るように)。
        /// </summary>
        public static void Noise(float[] buf, int start, int len, float vol, float smooth = 0f, int seed = 1,
            float attack = 0.002f, float decay = 0.05f, float sustain = 0f, float release = 0.01f)
        {
            uint x = (uint)(seed * 2654435761u + 12345u);
            float last = 0f;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                if (start + i < 0) continue;
                x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                float n = (x & 0xffff) / 32768f - 1f;
                last = Mathf.Lerp(n, last, smooth);
                buf[start + i] += last * vol * Envelope(i, len, attack, decay, sustain, release);
            }
        }

        /// <summary>サイン波の下降(バスドラム)。</summary>
        public static void Kick(float[] buf, int start, float vol)
        {
            int len = Samples(0.12f);
            double phase = 0;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float k = i / (float)len;
                phase += Mathf.Lerp(150f, 45f, k) / Rate;
                buf[start + i] += Mathf.Sin((float)(phase * 2 * System.Math.PI)) * vol * (1f - k) * (1f - k);
            }
        }

        /// <summary>音割れしないよう、やわらかく頭を抑えて AudioClip にする。</summary>
        public static AudioClip ToClip(string name, float[] buf)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                float v = buf[i];
                buf[i] = v / (1f + Mathf.Abs(v)) * 1.4f; // ソフトクリップ
            }
            var clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        /// <summary>
        /// 音の列を順に鳴らす(ジングル・効果音用)。notes は音名、step は1音の長さ(秒)。最後の音は last 秒伸ばす。
        /// </summary>
        public static void Arpeggio(float[] buf, int start, string[] notes, float step, float vol, bool triangle = false,
            float duty = 0.25f, float last = 0f)
        {
            for (int i = 0; i < notes.Length; i++)
            {
                int midi = ParseNote(notes[i]);
                if (midi < 0) continue;
                float len = step + (i == notes.Length - 1 ? last : 0f);
                int s = start + Samples(step * i);
                float f = Freq(midi);
                if (triangle) Triangle(buf, s, Samples(len), f, f, vol, release: 0.04f);
                else Square(buf, s, Samples(len), f, f, vol, duty, release: 0.04f);
            }
        }
    }
}
