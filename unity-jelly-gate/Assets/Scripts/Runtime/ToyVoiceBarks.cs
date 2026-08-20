using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    // Self-contained audio keeps this prototype distributable without external sound licensing.
    // Character cues are intentionally tiny toy-instrument stings, never voiced wails.
    public sealed class ToyVoiceBarks : MonoBehaviour
    {
        private readonly Dictionary<int, AudioClip> cueClips = new();
        private readonly Dictionary<int, float> lastPlayedAt = new();
        private AudioSource musicSource;
        private AudioClip menuMusicClip;
        private AudioClip battleMusicClip;
        private float cueVolume = .46f;
        private float musicVolume = .28f;

        public void SetVolume(float value) => cueVolume = Mathf.Clamp01(value);

        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            if (musicSource != null) musicSource.volume = musicVolume;
        }

        public void PrewarmEnemySpawnCues()
        {
            // Boss arrival must never be the first place where Unity allocates a procedural
            // cue buffer. Build every enemy-family spawn sting during the loading scene.
            foreach (EnemyClass enemyClass in System.Enum.GetValues(typeof(EnemyClass)))
                GetCueClip(100 + (int)enemyClass, VoiceCue.Spawn);
        }

        public void StartBackgroundMusic(bool battle = false)
        {
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = musicVolume;
            musicSource.priority = 128;
            SetBattleMusic(battle, true);
        }

        public void SetBattleMusic(bool battle, bool restart = false)
        {
            if (musicSource == null) StartBackgroundMusic(battle);
            if (menuMusicClip == null) menuMusicClip = CreateMainMenuMusic();
            if (battleMusicClip == null) battleMusicClip = CreateBattleMusic();
            var next = battle ? battleMusicClip : menuMusicClip;
            if (!restart && musicSource.clip == next && musicSource.isPlaying) return;
            musicSource.Stop();
            musicSource.clip = next;
            musicSource.time = 0f;
            musicSource.Play();
        }

        public void Play(Transform emitter, int profile, VoiceCue cue, float gain = 1f)
        {
            // Movement is intentionally silent: speech-like ticks on every move order were noisy
            // and distracted from the tactical command feedback.
            if (cue == VoiceCue.Move || emitter == null || cueVolume <= .001f) return;
            var id = emitter.GetInstanceID();
            if (lastPlayedAt.TryGetValue(id, out var previous) &&
                Time.unscaledTime - previous < .26f) return;
            lastPlayedAt[id] = Time.unscaledTime;

            var source = emitter.GetComponent<AudioSource>();
            if (source == null) source = emitter.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = .12f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.3f;
            source.maxDistance = 10f;
            source.volume = 1f;
            source.priority = 96;
            source.PlayOneShot(GetCueClip(profile, cue), cueVolume * Mathf.Clamp01(gain));
        }

        private AudioClip GetCueClip(int profile, VoiceCue cue)
        {
            var key = profile * 32 + (int)cue;
            if (cueClips.TryGetValue(key, out var existing)) return existing;

            var duration = cue switch
            {
                VoiceCue.Attack => .075f,
                VoiceCue.Skill => .17f,
                VoiceCue.Hero => .26f,
                VoiceCue.Defeat => .13f,
                VoiceCue.Spawn => .09f,
                _ => .11f
            };
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];
            var seed = Mathf.Abs(profile * 37 + (int)cue * 19);
            var baseFrequency = 300f + seed % 8 * 28f;
            if (profile >= 100) baseFrequency *= .66f;
            if (cue == VoiceCue.Hero) baseFrequency *= 1.17f;
            if (cue == VoiceCue.Defeat) baseFrequency *= .72f;
            for (var i = 0; i < sampleCount; i++)
            {
                var time = i / (float)sampleRate;
                var phase = time / duration;
                var envelope = Mathf.Pow(1f - Mathf.Clamp01(phase), cue == VoiceCue.Hero ? 1.4f : 2.7f);
                var pitch = baseFrequency * (cue == VoiceCue.Attack ? 1.22f - phase * .28f : 1f + phase * .1f);
                var tone = Mathf.Sin(time * pitch * Mathf.PI * 2f) * .78f +
                           Mathf.Sin(time * pitch * 2f * Mathf.PI * 2f) * .14f;
                if (cue is VoiceCue.Skill or VoiceCue.Hero)
                    tone += Mathf.Sin(time * pitch * 1.5f * Mathf.PI * 2f) * .18f;
                samples[i] = tone * envelope * .23f;
            }
            var clip = AudioClip.Create($"ToyCue_{profile}_{cue}", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            cueClips[key] = clip;
            return clip;
        }

        private static AudioClip CreateMainMenuMusic()
        {
            const int sampleRate = 22050;
            const float beatDuration = .42f;
            const int beats = 64;
            var duration = beatDuration * beats;
            var samples = new float[Mathf.CeilToInt(duration * sampleRate)];
            // A fanfare-style royal overture for the main menu: broad brass-like chords,
            // marching low drums and a bright bell melody rather than the old quiet loop.
            var roots = new[] { 146.83f, 110f, 123.47f, 98f, 146.83f, 110f, 123.47f, 146.83f,
                                98f, 110f, 146.83f, 123.47f, 110f, 98f, 123.47f, 146.83f };
            var arpeggio = new[] { 0, 2, 1, 2, 3, 2, 1, 2, 0, 2, 3, 2, 1, 2, 0, 3 };
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                var beat = Mathf.FloorToInt(time / beatDuration);
                var bar = Mathf.Clamp(beat / 4, 0, roots.Length - 1);
                var root = roots[bar];
                var beatPhase = (time % beatDuration) / beatDuration;
                var arpStep = Mathf.FloorToInt(time / (beatDuration * .5f));
                var interval = arpeggio[arpStep % arpeggio.Length] switch
                {
                    0 => 1f, 1 => 1.25f, 2 => 1.5f, _ => 2f
                };
                var notePhase = (time % (beatDuration * .5f)) / (beatDuration * .5f);
                var pluck = Mathf.Pow(1f - notePhase, 2.6f);
                var barPhase = (time % (beatDuration * 4f)) / (beatDuration * 4f);
                var bass = Mathf.Sin(time * root * Mathf.PI * 2f) * (.062f * (1f - beatPhase * .35f));
                var melodyFrequency = root * 2f * interval;
                var melody = Mathf.Sin(time * melodyFrequency * Mathf.PI * 2f) * (.086f * pluck) +
                              Mathf.Sin(time * melodyFrequency * 2f * Mathf.PI * 2f) * (.021f * pluck);
                var brass = (Mathf.Sin(time * root * 2f * Mathf.PI * 2f) +
                             Mathf.Sin(time * root * 2.5f * Mathf.PI * 2f) * .7f +
                             Mathf.Sin(time * root * 3f * Mathf.PI * 2f) * .45f) *
                            (.020f * Mathf.Clamp01(1f - barPhase * .52f));
                var beatInBar = beat % 4;
                var drumPhase = (time % beatDuration) / beatDuration;
                var drum = beatInBar is 0 or 2
                    ? Mathf.Sin(time * (54f + root * .12f) * Mathf.PI * 2f) * Mathf.Pow(1f - drumPhase, 7f) * .09f
                    : Mathf.Sin(time * 210f * Mathf.PI * 2f) * Mathf.Pow(1f - drumPhase, 10f) * .018f;
                var edgeFade = Mathf.Min(Mathf.Clamp01(time / .08f), Mathf.Clamp01((duration - time) / .08f));
                samples[i] = (bass + melody + brass + drum) * edgeFade;
            }
            var clip = AudioClip.Create("ToyKingdom_MenuFanfare", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateBattleMusic()
        {
            const int sampleRate = 22050;
            const float beatDuration = .285f;
            const int beats = 96;
            var duration = beatDuration * beats;
            var samples = new float[Mathf.CeilToInt(duration * sampleRate)];
            // Faster minor-key pulse for combat: muted drums, low string rhythm and a short
            // glockenspiel reply. It deliberately shares no melody with the menu fanfare.
            var roots = new[] { 98f, 110f, 123.47f, 110f, 92.5f, 110f, 123.47f, 146.83f };
            var motif = new[] { 0, 2, 3, 2, 1, 2, 4, 3, 2, 0, 2, 1, 3, 2, 1, 0 };
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                var beat = Mathf.FloorToInt(time / beatDuration);
                var root = roots[(beat / 4) % roots.Length];
                var beatPhase = (time % beatDuration) / beatDuration;
                var pulse = Mathf.Pow(1f - beatPhase, 3.6f);
                var sub = Mathf.Sin(time * root * Mathf.PI * 2f) * (.07f * (1f - beatPhase * .35f));
                var bow = (Mathf.Sin(time * root * 2f * Mathf.PI * 2f) +
                           Mathf.Sin(time * root * 3f * Mathf.PI * 2f) * .35f) * (.026f * pulse);
                var motifStep = Mathf.FloorToInt(time / (beatDuration * .5f));
                var interval = motif[motifStep % motif.Length] switch
                {
                    0 => 1f, 1 => 1.125f, 2 => 1.25f, 3 => 1.5f, _ => 1.875f
                };
                var notePhase = (time % (beatDuration * .5f)) / (beatDuration * .5f);
                var bell = Mathf.Sin(time * root * 4f * interval * Mathf.PI * 2f) *
                           (.037f * Mathf.Pow(1f - notePhase, 4.2f));
                var drum = beat % 4 is 0 or 2
                    ? Mathf.Sin(time * 58f * Mathf.PI * 2f) * Mathf.Pow(1f - beatPhase, 8f) * .12f
                    : Mathf.Sin(time * 188f * Mathf.PI * 2f) * Mathf.Pow(1f - beatPhase, 12f) * .025f;
                var edgeFade = Mathf.Min(Mathf.Clamp01(time / .06f), Mathf.Clamp01((duration - time) / .06f));
                samples[i] = (sub + bow + bell + drum) * edgeFade;
            }
            var clip = AudioClip.Create("ToyKingdom_BattleMarch", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
