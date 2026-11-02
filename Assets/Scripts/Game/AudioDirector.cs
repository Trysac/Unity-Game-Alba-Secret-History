using UnityEngine;

namespace Alba.Game
{
    /// <summary>
    /// Music with cross-fades, looping rain and wind, flat UI sounds and positional sounds in the
    /// world (a crawler's chitter comes from where the crawler is). Volumes follow the settings.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const int WorldVoices = 14;

        AudioSource musicA, musicB, rain, wind, ui;
        AudioSource[] voices;
        int nextVoice;
        bool aPlaying;
        float musicVolume = 0.6f;
        float soundVolume = 0.8f;
        float fade = 1f;
        float rainLevel, windLevel, rainTarget, windTarget;

        public AudioClip CurrentMusic { get; private set; }

        public void Build()
        {
            musicA = Source("Music A", true);
            musicB = Source("Music B", true);
            rain = Source("Rain", true);
            wind = Source("Wind", true);
            ui = Source("Interface", false);
            voices = new AudioSource[WorldVoices];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = Source("Voice " + i, false);
                voices[i].spatialBlend = 1f;
                voices[i].rolloffMode = AudioRolloffMode.Linear;
                voices[i].minDistance = 3f;
                voices[i].maxDistance = 40f;
                voices[i].dopplerLevel = 0f;
            }
        }

        AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        /// <summary>Sets the volumes from the 0-10 settings.</summary>
        public void SetVolumes(int music, int sound)
        {
            musicVolume = music / 10f * 0.75f;
            soundVolume = sound / 10f;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == CurrentMusic) return;
            CurrentMusic = clip;
            AudioSource next = aPlaying ? musicB : musicA;
            next.clip = clip;
            next.volume = 0f;
            if (clip != null) next.Play();
            aPlaying = !aPlaying;
            fade = 0f;
        }

        public void SetAmbience(AudioClip rainClip, AudioClip windClip, bool raining, float windAmount)
        {
            if (rain.clip != rainClip) { rain.clip = rainClip; if (rainClip != null) rain.Play(); }
            if (wind.clip != windClip) { wind.clip = windClip; if (windClip != null) wind.Play(); }
            rainTarget = raining ? 0.55f : 0f;
            windTarget = windAmount;
        }

        /// <summary>A sound with no position (menus, pickups, the player's own weapon).</summary>
        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            ui.pitch = pitch;
            ui.PlayOneShot(clip, volume * soundVolume);
        }

        /// <summary>A sound from a point in the world, slightly varied in pitch.</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchSpread = 0.08f)
        {
            if (clip == null) return;
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.transform.position = position;
            voice.pitch = 1f + Random.Range(-pitchSpread, pitchSpread);
            voice.clip = clip;
            voice.volume = volume * soundVolume;
            voice.Play();
        }

        void Update()
        {
            if (musicA == null) return;
            float dt = Time.unscaledDeltaTime;
            fade = Mathf.MoveTowards(fade, 1f, dt / 1.5f);
            AudioSource current = aPlaying ? musicA : musicB;
            AudioSource previous = aPlaying ? musicB : musicA;
            current.volume = musicVolume * fade;
            previous.volume = musicVolume * (1f - fade);
            if (fade >= 1f && previous.isPlaying) previous.Stop();
            rainLevel = Mathf.MoveTowards(rainLevel, rainTarget, dt * 0.5f);
            windLevel = Mathf.MoveTowards(windLevel, windTarget, dt * 0.5f);
            rain.volume = rainLevel * soundVolume;
            wind.volume = windLevel * soundVolume;
        }

        public void PauseWorld(bool paused)
        {
            foreach (AudioSource voice in voices)
            {
                if (paused) voice.Pause();
                else voice.UnPause();
            }
        }
    }
}
