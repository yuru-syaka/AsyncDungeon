using System.Collections.Generic;
using DungeonGame.Client.Audio;
using DungeonGame.Core.Ecology;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Run;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>
    /// 音まわり:画面ごとの BGM の選び方、戦闘・育成のできごとの効果音、音の設定ボタン。
    /// 音は見た目と同じく演出だけで、ゲームの結果には影響しない。
    /// </summary>
    public partial class GameApp
    {
        private static AudioManager Sound => AudioManager.Instance;

        /// <summary>今の画面で流す BGM(null なら流さない。ジングルの後など)。</summary>
        private string DesiredBgm()
        {
            switch (_screen)
            {
                case Screen.Demon:
                    if (_testRaid != null) return _testRaid.IsFinished ? null : RaidBgm(_testRaid);
                    return "demon";
                case Screen.Run:
                    if (_run == null) return "title";
                    if (_run.Phase == RunPhase.Raid) return _run.CurrentRaid.IsFinished ? null : RaidBgm(_run.CurrentRaid);
                    if (_run.Phase == RunPhase.Reward) return "title";
                    return null; // 結果画面はジングルのみ
                case Screen.Replay:
                    var raid = _replayer?.Run?.CurrentRaid;
                    if (raid == null) return "title";
                    return raid.IsFinished ? null : RaidBgm(raid);
                default:
                    return "title";
            }
        }

        /// <summary>魔王の待つ層は決戦の曲、それ以外は冒険の曲。</summary>
        private static string RaidBgm(RaidSimulation raid) => raid.Boss != null ? "boss" : "adventure";

        /// <summary>攻略のできごとに合わせて効果音・ジングルを鳴らす。</summary>
        private void PlayRaidSounds(RaidSimulation raid)
        {
            var s = Sound;
            if (s == null) return;
            foreach (var e in raid.Events)
            {
                switch (e.Kind)
                {
                    case RaidEventKind.HeroAttack:
                    {
                        var cls = e.HeroId > 0 && e.HeroId <= raid.Heroes.Count ? raid.Heroes[e.HeroId - 1].Class : null;
                        if (e.Critical) s.Play("crit", 0.05f, 0.05f);
                        else if (cls != null && cls.AttackStat == AttackStat.Magic) s.Play("magic", 0.06f, 0.08f, 0.8f);
                        else if (cls != null && cls.AttackRange >= 2) s.Play("arrow", 0.06f, 0.08f, 0.8f);
                        else s.Play("hit", 0.05f, 0.1f, 0.8f);
                        break;
                    }
                    case RaidEventKind.MonsterAttack: s.Play("hurt", 0.07f, 0.08f, 0.8f); break;
                    case RaidEventKind.MonsterKilled: s.Play("kill", 0.06f, 0.1f); break;
                    case RaidEventKind.HeroDown: s.Play("down", 0.2f); break;
                    case RaidEventKind.HeroHeal: s.Play("heal_small", 0.2f, 0f, 0.7f); break;
                    case RaidEventKind.InterventionHeal: s.Play("heal", 0.1f); break;
                    case RaidEventKind.InterventionTorch: s.Play("torch", 0.1f); break;
                    case RaidEventKind.Cleared: s.PlayJingle(raid.Boss != null ? "victory" : "clear"); break;
                    case RaidEventKind.Wiped:
                    case RaidEventKind.TimeUp: s.PlayJingle("defeat"); break;
                }
            }
        }

        // ================= 育成中の特殊な誕生・進化 =================

        private readonly Dictionary<int, string> _demonSpecies = new Dictionary<int, string>();
        private float _lastBirthSay = -10f;

        /// <summary>
        /// 育成中に、特殊な誕生(巣・墓場)や進化が起きたら、音と演出で知らせる。
        /// 前のティックの「ID→種族」と比べて、新しく現れた特殊な種族と、種族が変わった個体を見つける。
        /// </summary>
        private void TrackDemonBirths()
        {
            var monsters = _builder.State.Monsters;
            var seen = new HashSet<int>();
            foreach (var m in monsters)
            {
                seen.Add(m.Id);
                bool known = _demonSpecies.TryGetValue(m.Id, out var before);
                var p = new Vector2(m.Pos.X, m.Pos.Y);
                if (known && before != m.Species.Id)
                {
                    Sound?.Play("evolve", 0.3f);
                    _map.Fx.Burst(p, new Color(0.9f, 0.7f, 1f), 18, 4f, 0.1f);
                    _map.Fx.Popup(p, "進化!", Ui.Gold, 1.1f);
                    SayBirth($"おお…{m.Species.Name}に進化しおったわ!");
                }
                else if (!known && _demonSpecies.Count > 0 && m.Species.SpecialSpawn != SpecialSpawn.None)
                {
                    Sound?.Play("evolve", 0.3f);
                    _map.Fx.Burst(p, Ui.Gold, 14, 3f, 0.1f);
                    bool nest = m.Species.SpecialSpawn == SpecialSpawn.RoomNest;
                    _map.Fx.Popup(p, nest ? "巣を作った!" : "起き上がった!", Ui.Gold, 1f);
                    SayBirth(nest ? $"広間に{m.Species.Name}が巣を作ったぞ!" : $"積もった骨から{m.Species.Name}が起き上がった…ククク。");
                }
                _demonSpecies[m.Id] = m.Species.Id;
            }
            // いなくなった個体は忘れる
            if (_demonSpecies.Count > seen.Count)
            {
                var gone = new List<int>();
                foreach (var id in _demonSpecies.Keys) if (!seen.Contains(id)) gone.Add(id);
                foreach (var id in gone) _demonSpecies.Remove(id);
            }
        }

        /// <summary>早送り中にセリフがちらつかないよう、間隔をあけてしゃべる。</summary>
        private void SayBirth(string text)
        {
            if (Time.unscaledTime - _lastBirthSay < 1.5f) return;
            _lastBirthSay = Time.unscaledTime;
            Say(text);
        }

        // ================= 音の設定ボタン =================

        /// <summary>画面右下の音の設定ボタン(全部ON → BGMだけOFF → 全部OFF)。</summary>
        private void SoundButton()
        {
            var s = Sound;
            if (s == null) return;
            string label = s.Mode == SoundMode.All ? "音:ON" : s.Mode == SoundMode.SfxOnly ? "BGM:OFF" : "音:OFF";
            var color = s.Mode == SoundMode.All ? new Color(0.15f, 0.4f, 0.3f) : new Color(0.35f, 0.2f, 0.2f);
            var r = new Rect(_vw - 132, _vh - 38, 124, 30);
            // ボタン自体のクリック音は鳴らさず、設定を切り替えた後の音で確認できるようにする
            var old = Ui.OnClick;
            Ui.OnClick = null;
            if (Ui.Btn(r, label, true, null, color, 14)) s.CycleMode();
            Ui.OnClick = old;
        }
    }
}
