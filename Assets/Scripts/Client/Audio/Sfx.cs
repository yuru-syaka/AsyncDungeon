using UnityEngine;

namespace DungeonGame.Client.Audio
{
    /// <summary>
    /// 効果音(すべて ChipSynth で合成するオリジナル音)。ID で呼び出す。
    /// 新しい効果音を足すときは、ここに case を1つ増やすだけでよい。
    /// </summary>
    public static class Sfx
    {
        public static AudioClip Render(string id)
        {
            var buf = new float[ChipSynth.Samples(0.8f)];
            int S(float sec) => ChipSynth.Samples(sec);
            float F(string note) => ChipSynth.Freq(ChipSynth.ParseNote(note));

            switch (id)
            {
                case "click": // ボタン
                    ChipSynth.Square(buf, 0, S(0.035f), 1300f, 1500f, 0.18f, 0.25f, decay: 0.02f, sustain: 0.3f);
                    break;
                case "select": // スキル・職業を選ぶ
                    ChipSynth.Arpeggio(buf, 0, new[] { "E6", "B6" }, 0.06f, 0.16f, false, 0.25f, 0.05f);
                    break;
                case "deny": // できない操作
                    ChipSynth.Square(buf, 0, S(0.08f), 160f, 150f, 0.18f, 0.5f);
                    ChipSynth.Square(buf, S(0.1f), S(0.1f), 140f, 120f, 0.18f, 0.5f);
                    break;
                case "dig": // 土を掘る(ザクッ)
                    ChipSynth.Noise(buf, 0, S(0.14f), 0.4f, 0.55f, 11, decay: 0.12f);
                    ChipSynth.Square(buf, 0, S(0.06f), 140f, 70f, 0.15f, 0.5f);
                    break;
                case "spawn": // モンスター誕生(キラキラ上昇)
                    ChipSynth.Arpeggio(buf, 0, new[] { "C6", "E6", "G6", "C7" }, 0.045f, 0.14f, false, 0.125f, 0.08f);
                    break;
                case "grow": // 育成開始
                    ChipSynth.Arpeggio(buf, 0, new[] { "C5", "G5", "C6", "E6", "G6" }, 0.05f, 0.12f, true, 0.25f, 0.1f);
                    break;
                case "hit": // 剣で斬る
                    ChipSynth.Noise(buf, 0, S(0.06f), 0.32f, 0.1f, 21, decay: 0.05f);
                    ChipSynth.Square(buf, 0, S(0.06f), 520f, 180f, 0.12f, 0.5f);
                    break;
                case "crit": // 会心の一撃
                    ChipSynth.Noise(buf, 0, S(0.1f), 0.4f, 0.05f, 22, decay: 0.08f);
                    ChipSynth.Square(buf, 0, S(0.05f), 900f, 400f, 0.14f, 0.5f);
                    ChipSynth.Square(buf, S(0.05f), S(0.12f), 1600f, 1700f, 0.12f, 0.125f);
                    break;
                case "magic": // 魔法の弾
                    ChipSynth.Square(buf, 0, S(0.16f), 500f, 1500f, 0.12f, 0.125f, vibrato: 0.05f);
                    ChipSynth.Noise(buf, S(0.12f), S(0.08f), 0.15f, 0.2f, 23);
                    break;
                case "arrow": // 矢・射程攻撃
                    ChipSynth.Noise(buf, 0, S(0.09f), 0.18f, 0f, 24, attack: 0.03f, decay: 0.05f);
                    ChipSynth.Square(buf, S(0.07f), S(0.03f), 900f, 600f, 0.1f, 0.25f);
                    break;
                case "hurt": // 勇者がダメージを受ける
                    ChipSynth.Square(buf, 0, S(0.1f), 260f, 110f, 0.16f, 0.5f);
                    ChipSynth.Noise(buf, 0, S(0.05f), 0.15f, 0.3f, 25);
                    break;
                case "kill": // モンスター撃破(ボンッ)
                    ChipSynth.Noise(buf, 0, S(0.22f), 0.42f, 0.4f, 26, decay: 0.2f);
                    ChipSynth.Square(buf, 0, S(0.15f), 880f, 160f, 0.1f, 0.25f);
                    break;
                case "down": // 勇者が倒れる
                    ChipSynth.Arpeggio(buf, 0, new[] { "G4", "E4", "C4" }, 0.11f, 0.17f, false, 0.5f, 0.12f);
                    break;
                case "heal": // 回復(パーティ全体)
                    ChipSynth.Arpeggio(buf, 0, new[] { "C6", "E6", "G6", "C7", "E7" }, 0.05f, 0.16f, true, 0.25f, 0.2f);
                    ChipSynth.Arpeggio(buf, S(0.025f), new[] { "G5", "C6", "E6", "G6" }, 0.05f, 0.05f, false, 0.125f);
                    break;
                case "heal_small": // 僧侶・聖騎士の回復
                    ChipSynth.Arpeggio(buf, 0, new[] { "E6", "G6" }, 0.04f, 0.07f, true);
                    break;
                case "torch": // たいまつ(ボワッ)
                    ChipSynth.Noise(buf, 0, S(0.35f), 0.35f, 0.75f, 27, attack: 0.08f, decay: 0.25f);
                    ChipSynth.Square(buf, 0, S(0.25f), 200f, 600f, 0.06f, 0.125f);
                    break;
                case "evolve": // 進化・特殊な誕生
                    ChipSynth.Arpeggio(buf, 0, new[] { "C5", "F5", "Bb5", "D6", "F6" }, 0.06f, 0.13f, false, 0.25f, 0.15f);
                    break;
                default:
                    return null;
            }
            return ChipSynth.ToClip("sfx_" + id, buf);
        }
    }
}
