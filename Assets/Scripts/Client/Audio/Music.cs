using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Client.Audio
{
    /// <summary>
    /// BGM とジングル(すべてオリジナル曲)。曲は文字列の楽譜で書き、ChipSynth で音にする。
    ///
    /// 楽譜の書き方:
    ///  ・Lead … 8分音符ごとに1つ。"E5" などの音名で鳴らし、"-" で前の音を伸ばし、"." で休む。1小節 = 8つ。
    ///  ・Chords … 半小節ごとのコード。"C" "Am" "F#m" など。ベースと分散和音はここから自動で作る。
    /// 4つの音(メロディの矩形波・分散和音の細い矩形波・ベースの三角波・ノイズの打楽器)を重ねる。
    /// 曲の終わりの余韻は頭に回り込ませるので、つなぎ目なくループする。
    /// </summary>
    public static class Music
    {
        private enum Drums { Normal, Drive, Eerie }

        private sealed class Song
        {
            public int Bpm;
            public string Lead;
            public string Chords;
            public Drums Drums;
            public float Duty;
            public float LeadVolume = 0.16f;
        }

        private static readonly Dictionary<string, Song> Songs = new Dictionary<string, Song>
        {
            // タイトル:明るく、これから冒険が始まる感じ(ハ長調)
            ["title"] = new Song
            {
                Bpm = 128, Drums = Drums.Normal, Duty = 0.25f,
                Lead = "E5 - G5 - C6 - B5 A5  A5 - E5 - C5 - E5 -  F5 - A5 - C6 - A5 F5  G5 - - - D5 - G5 -  " +
                       "E5 - G5 - C6 - D6 E6  D6 - C6 - A5 - C6 -  A5 - G5 - F5 - G5 -  C6 - - - - - . .",
                Chords = "C C Am Am F F G G C C Am Am F G C C",
            },
            // 魔王の部屋:ゆっくりで不気味(イ短調)
            ["demon"] = new Song
            {
                Bpm = 96, Drums = Drums.Eerie, Duty = 0.125f, LeadVolume = 0.14f,
                Lead = "A4 - - C5 E5 - - D5  C5 - - A4 F4 - - .  D5 - - F5 A5 - G5 F5  E5 - - - G#4 - B4 -  " +
                       "A4 - - C5 E5 - A5 -  G5 F5 E5 - C5 - - .  D5 - F5 - E5 - G#4 -  A4 - - - - - . .",
                Chords = "Am Am F F Dm Dm E E Am Am F F Dm E Am Am",
            },
            // 冒険:勇者の攻略中。前に進む勢い(ト長調)
            ["adventure"] = new Song
            {
                Bpm = 140, Drums = Drums.Drive, Duty = 0.25f,
                Lead = "G5 - B5 - D6 - B5 G5  A5 - F#5 - D5 - F#5 A5  B5 - G5 - E5 - G5 B5  C6 - B5 A5 G5 - E5 -  " +
                       "G5 - B5 - D6 - E6 D6  A5 - D6 - F#5 - A5 -  G5 - E5 - F#5 - A5 -  G5 - - - D5 - G5 -",
                Chords = "G G D D Em Em C C G G D D C D G G",
            },
            // 決戦:魔王の待つ最下層。速く激しく(ホ短調)
            ["boss"] = new Song
            {
                Bpm = 160, Drums = Drums.Drive, Duty = 0.5f, LeadVolume = 0.13f,
                Lead = "E5 G5 B5 E6 D6 B5 G5 B5  C6 - G5 - E5 - G5 C6  D6 - A5 - F#5 - A5 D6  D#6 - B5 - F#5 - D#5 -  " +
                       "E6 D6 B5 G5 E5 G5 B5 E6  G6 - E6 - C6 - E6 -  A5 C6 E6 C6 B5 D#6 F#6 D#6  E6 - - - B5 - E5 -",
                Chords = "Em Em C C D D B B Em Em C C Am B Em Em",
            },
        };

        public static bool Has(string id) => Songs.ContainsKey(id);

        /// <summary>BGM を1ループ分の AudioClip にする(重いので、使うときに1回だけ作ってとっておく)。</summary>
        public static AudioClip Render(string id)
        {
            var song = Songs[id];
            float eighth = 60f / song.Bpm / 2f;
            var lead = Tokens(song.Lead);
            var chords = Tokens(song.Chords);
            int bars = lead.Count / 8;
            int loopLen = ChipSynth.Samples(eighth * 8 * bars);
            var buf = new float[loopLen + ChipSynth.Rate * 2]; // 余韻用の余白

            // メロディ(+ 少し遅れて小さく鳴らすエコーで厚みを出す)
            for (int i = 0; i < lead.Count; i++)
            {
                int midi = ChipSynth.ParseNote(lead[i]);
                if (midi < 0) continue;
                int hold = 1;
                while (i + hold < lead.Count && lead[i + hold] == "-") hold++;
                int start = ChipSynth.Samples(eighth * i);
                int len = ChipSynth.Samples(eighth * hold * 0.95f);
                float f = ChipSynth.Freq(midi);
                ChipSynth.Square(buf, start, len, f, f, song.LeadVolume, song.Duty, vibrato: hold >= 3 ? 0.006f : 0f);
                ChipSynth.Square(buf, start + ChipSynth.Samples(eighth * 3), len, f, f, song.LeadVolume * 0.28f, song.Duty);
            }

            // 分散和音(16分音符)とベース(8分音符)
            float sixteenth = eighth / 2f;
            for (int h = 0; h < chords.Count; h++)
            {
                ParseChord(chords[h], out int root, out bool minor);
                int third = root + (minor ? 3 : 4), fifth = root + 7;
                int[] arp = { root + 60, third + 60, fifth + 60, root + 72, fifth + 60, third + 60, root + 60, fifth + 60 };
                for (int k = 0; k < 8; k++)
                {
                    int s = ChipSynth.Samples(eighth * 4 * h + sixteenth * k);
                    float f = ChipSynth.Freq(arp[k]);
                    ChipSynth.Square(buf, s, ChipSynth.Samples(sixteenth * 0.9f), f, f, 0.045f, 0.125f, decay: 0.05f, sustain: 0.4f);
                }
                int[] bass = { root + 36, -1, root + 36, fifth + 36 };
                for (int k = 0; k < 4; k++)
                {
                    if (bass[k] < 0) continue;
                    int s = ChipSynth.Samples(eighth * (4 * h + k));
                    float f = ChipSynth.Freq(bass[k]);
                    ChipSynth.Triangle(buf, s, ChipSynth.Samples(eighth * 0.9f), f, f, 0.32f);
                }
            }

            // 打楽器
            for (int bar = 0; bar < bars; bar++)
            for (int k = 0; k < 8; k++)
            {
                int s = ChipSynth.Samples(eighth * (bar * 8 + k));
                switch (song.Drums)
                {
                    case Drums.Normal:
                        if (k == 0 || k == 4) ChipSynth.Kick(buf, s, 0.5f);
                        if (k == 2 || k == 6) ChipSynth.Noise(buf, s, ChipSynth.Samples(0.1f), 0.22f, 0.3f, bar * 8 + k);
                        ChipSynth.Noise(buf, s, ChipSynth.Samples(0.025f), 0.06f, 0f, k + 100);
                        break;
                    case Drums.Drive:
                        if (k == 0 || k == 3 || k == 4) ChipSynth.Kick(buf, s, 0.55f);
                        if (k == 2 || k == 6) ChipSynth.Noise(buf, s, ChipSynth.Samples(0.1f), 0.25f, 0.3f, bar * 8 + k);
                        if (bar % 4 == 3 && k == 7) ChipSynth.Noise(buf, s + ChipSynth.Samples(sixteenth), ChipSynth.Samples(0.08f), 0.2f, 0.3f, 77);
                        ChipSynth.Noise(buf, s, ChipSynth.Samples(0.02f), 0.06f, 0f, k + 200);
                        ChipSynth.Noise(buf, s + ChipSynth.Samples(sixteenth), ChipSynth.Samples(0.015f), 0.035f, 0f, k + 300);
                        break;
                    case Drums.Eerie:
                        if (k == 0) ChipSynth.Kick(buf, s, 0.4f);
                        if (k == 2 || k == 6) ChipSynth.Noise(buf, s, ChipSynth.Samples(0.05f), 0.05f, 0.6f, k + 400);
                        break;
                }
            }

            // 余韻を頭に回り込ませて、つなぎ目なくループさせる
            var loop = new float[loopLen];
            for (int i = 0; i < buf.Length; i++) loop[i % loopLen] += buf[i];
            return ChipSynth.ToClip("bgm_" + id, loop);
        }

        /// <summary>ジングル(一度だけ鳴る短い曲)。</summary>
        public static AudioClip Jingle(string id)
        {
            var buf = new float[ChipSynth.Samples(3f)];
            switch (id)
            {
                case "clear": // 層の突破
                    ChipSynth.Arpeggio(buf, 0, new[] { "G4", "C5", "E5", "G5" }, 0.09f, 0.18f);
                    ChipSynth.Square(buf, ChipSynth.Samples(0.36f), ChipSynth.Samples(0.7f), ChipSynth.Freq(84), ChipSynth.Freq(84), 0.18f, 0.25f, vibrato: 0.008f);
                    ChipSynth.Triangle(buf, ChipSynth.Samples(0.36f), ChipSynth.Samples(0.7f), ChipSynth.Freq(48), ChipSynth.Freq(48), 0.3f);
                    ChipSynth.Square(buf, ChipSynth.Samples(0.36f), ChipSynth.Samples(0.7f), ChipSynth.Freq(76), ChipSynth.Freq(76), 0.08f, 0.125f);
                    break;
                case "victory": // 魔王撃破
                    ChipSynth.Arpeggio(buf, 0, new[] { "G5", "G5", "A5", "B5", "D6" }, 0.12f, 0.17f);
                    ChipSynth.Arpeggio(buf, ChipSynth.Samples(0.6f), new[] { "B5", "D6" }, 0.12f, 0.17f);
                    ChipSynth.Square(buf, ChipSynth.Samples(0.84f), ChipSynth.Samples(1.4f), ChipSynth.Freq(91), ChipSynth.Freq(91), 0.17f, 0.25f, vibrato: 0.01f);
                    ChipSynth.Square(buf, ChipSynth.Samples(0.84f), ChipSynth.Samples(1.4f), ChipSynth.Freq(86), ChipSynth.Freq(86), 0.08f, 0.125f);
                    ChipSynth.Triangle(buf, 0, ChipSynth.Samples(0.6f), ChipSynth.Freq(43), ChipSynth.Freq(43), 0.3f);
                    ChipSynth.Triangle(buf, ChipSynth.Samples(0.84f), ChipSynth.Samples(1.4f), ChipSynth.Freq(43), ChipSynth.Freq(43), 0.32f);
                    for (int k = 0; k < 4; k++) ChipSynth.Kick(buf, ChipSynth.Samples(0.12f * k * 2), 0.4f);
                    break;
                default: // 全滅・時間切れ
                    ChipSynth.Arpeggio(buf, 0, new[] { "E5", "D#5", "D5", "C#5" }, 0.32f, 0.15f, false, 0.125f, 0.6f);
                    ChipSynth.Triangle(buf, 0, ChipSynth.Samples(1.9f), ChipSynth.Freq(45), ChipSynth.Freq(40), 0.3f, decay: 0.8f, sustain: 0.6f, release: 0.4f);
                    break;
            }
            return ChipSynth.ToClip("jingle_" + id, buf);
        }

        private static List<string> Tokens(string s)
        {
            var list = new List<string>();
            foreach (var t in s.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries)) list.Add(t);
            return list;
        }

        /// <summary>"F#m" → 根音(0〜11)と短調かどうか。</summary>
        private static void ParseChord(string c, out int root, out bool minor)
        {
            minor = c.EndsWith("m");
            string name = minor ? c.Substring(0, c.Length - 1) : c;
            root = (ChipSynth.ParseNote(name + "0") - 12) % 12;
            if (root < 0) root += 12;
        }
    }
}
