using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Client.Audio
{
    /// <summary>音の設定:全部鳴らす/効果音だけ/全部消す。</summary>
    public enum SoundMode { All = 0, SfxOnly = 1, Off = 2 }

    /// <summary>
    /// BGM と効果音を鳴らす係。
    ///  ・BGM は ID で指定すると、前の曲を少しフェードアウトしてから切り替える(同じ曲なら何もしない)
    ///  ・効果音は同時に数個まで重ねて鳴らせる。早送り中に同じ音が鳴りすぎないよう、短い間隔で間引く
    ///  ・音は使うときに初めて合成し、作ったものはとっておく
    /// 音の設定(SoundMode)は PlayerPrefs に保存する。
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const float BgmVolume = 0.42f;
        private const float SfxVolume = 0.7f;
        private const float FadeSeconds = 0.4f;
        private const string ModeKey = "sound_mode";

        private AudioSource _bgm;
        private readonly List<AudioSource> _sfx = new List<AudioSource>();
        private int _nextSfx;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();

        private string _currentBgm;
        private string _pendingBgm;
        private float _fade = 1f; // 1=通常音量、0=無音
        private bool _fadingOut;

        public SoundMode Mode { get; private set; }

        public static AudioManager Create(GameObject host)
        {
            if (Instance != null) return Instance;
            if (FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            Instance = host.AddComponent<AudioManager>();
            return Instance;
        }

        private void Awake()
        {
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            for (int i = 0; i < 8; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _sfx.Add(s);
            }
            Mode = (SoundMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 2);
        }

        /// <summary>音の設定を次へ(全部 → 効果音だけ → 消音 → 全部)。</summary>
        public void CycleMode()
        {
            Mode = (SoundMode)(((int)Mode + 1) % 3);
            PlayerPrefs.SetInt(ModeKey, (int)Mode);
            PlayerPrefs.Save();
            Play("click");
        }

        // ================= BGM =================

        /// <summary>BGM を切り替える。null で止める。</summary>
        public void PlayBgm(string id)
        {
            if (id == _currentBgm && !_fadingOut) return;
            if (id == _pendingBgm && _fadingOut) return;
            _pendingBgm = id;
            _fadingOut = _bgm.isPlaying;
            if (!_fadingOut) StartPending();
        }

        /// <summary>ジングルを鳴らす(BGM は止める)。</summary>
        public void PlayJingle(string id)
        {
            _currentBgm = null;
            _pendingBgm = null;
            _fadingOut = false;
            _bgm.Stop();
            if (Mode == SoundMode.Off) return;
            var clip = Get("jingle_" + id, () => Music.Jingle(id));
            var s = NextSfxSource();
            s.volume = SfxVolume;
            s.pitch = 1f;
            s.PlayOneShot(clip);
        }

        private void StartPending()
        {
            _currentBgm = _pendingBgm;
            _fadingOut = false;
            if (_currentBgm == null || !Music.Has(_currentBgm)) { _bgm.Stop(); return; }
            _bgm.clip = Get("bgm_" + _currentBgm, () => Music.Render(_currentBgm));
            _fade = 0f;
            _bgm.Play();
        }

        /// <summary>
        /// 先に作っておく曲(初めて画面を切り替えたときに引っかからないよう、起動後に少しずつ合成する)。
        /// </summary>
        private readonly Queue<string> _warmup = new Queue<string>(new[] { "demon", "adventure", "boss" });

        private void Update()
        {
            // 20フレームに1曲ずつ、裏で合成しておく
            if (_warmup.Count > 0 && Time.frameCount % 20 == 0)
            {
                var id = _warmup.Dequeue();
                Get("bgm_" + id, () => Music.Render(id));
            }

            float dt = Time.unscaledDeltaTime;
            if (_fadingOut)
            {
                _fade -= dt / FadeSeconds;
                if (_fade <= 0f) StartPending();
            }
            else if (_fade < 1f) _fade = Mathf.Min(1f, _fade + dt / FadeSeconds);
            _bgm.volume = Mode == SoundMode.All ? BgmVolume * Mathf.Clamp01(_fade) : 0f;
        }

        // ================= 効果音 =================

        /// <summary>
        /// 効果音を鳴らす。minInterval 秒以内に同じ音を鳴らしていたら間引く。pitchJitter で毎回少し音程を揺らす。
        /// </summary>
        public void Play(string id, float minInterval = 0.06f, float pitchJitter = 0f, float volume = 1f)
        {
            if (Mode == SoundMode.Off) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out float last) && now - last < minInterval) return;
            _lastPlayed[id] = now;
            var clip = Get("sfx_" + id, () => Sfx.Render(id));
            if (clip == null) return;
            var s = NextSfxSource();
            s.pitch = 1f + (pitchJitter > 0 ? Random.Range(-pitchJitter, pitchJitter) : 0f);
            s.volume = SfxVolume * volume;
            s.PlayOneShot(clip);
        }

        private AudioSource NextSfxSource()
        {
            var s = _sfx[_nextSfx];
            _nextSfx = (_nextSfx + 1) % _sfx.Count;
            return s;
        }

        private AudioClip Get(string key, System.Func<AudioClip> make)
        {
            if (!_clips.TryGetValue(key, out var clip) || clip == null)
            {
                clip = make();
                _clips[key] = clip;
            }
            return clip;
        }
    }
}
