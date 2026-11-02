using System;
using System.Collections.Generic;

namespace Alba.Core
{
    /// <summary>Somewhere to keep integers between sessions (PlayerPrefs in the game, memory in tests).</summary>
    public interface IRecordStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void Save();
    }

    public sealed class MemoryStore : IRecordStore
    {
        readonly Dictionary<string, int> values = new Dictionary<string, int>();

        public int GetInt(string key, int fallback) => values.TryGetValue(key, out int value) ? value : fallback;

        public void SetInt(string key, int value) => values[key] = value;

        public void Save() => SaveCount++;

        public int SaveCount { get; private set; }
    }

    /// <summary>The best score, chapters reached, journal pages found and the settings.</summary>
    public sealed class Records
    {
        const string BestKey = "alba.best";
        const string ReachedKey = "alba.reached";
        const string PagesKey = "alba.pages";
        const string EscapesKey = "alba.escapes";
        const string MusicKey = "alba.music";
        const string SoundKey = "alba.sound";
        const string LookKey = "alba.look";
        const string InvertKey = "alba.invert";
        const string BrightnessKey = "alba.brightness";

        public const int MaxVolume = 10;
        public const int MinLook = 1;
        public const int MaxLook = 10;
        public const int MaxBrightness = 5;

        readonly IRecordStore store;

        public Records(IRecordStore store)
        {
            this.store = store;
        }

        public int Best => store.GetInt(BestKey, 0);

        /// <summary>The furthest chapter unlocked (0-based); chapters up to it can be chosen from the menu.</summary>
        public int Reached => Math.Clamp(store.GetInt(ReachedKey, 0), 0, Chapters.All.Length - 1);

        public int Escapes => store.GetInt(EscapesKey, 0);

        public int PagesMask => store.GetInt(PagesKey, 0);

        public bool HasPage(int chapter) => (PagesMask & (1 << chapter)) != 0;

        public int PagesFound
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Chapters.All.Length; i++) if (HasPage(i)) count++;
                return count;
            }
        }

        /// <summary>Records a finished or failed run's score; returns true if it is a new best.</summary>
        public bool Submit(int score)
        {
            if (score <= Best) return false;
            store.SetInt(BestKey, score);
            store.Save();
            return true;
        }

        public void Unlock(int chapter)
        {
            chapter = Math.Clamp(chapter, 0, Chapters.All.Length - 1);
            if (chapter <= Reached) return;
            store.SetInt(ReachedKey, chapter);
            store.Save();
        }

        /// <summary>Notes a journal page; returns true the first time it is found.</summary>
        public bool FindPage(int chapter)
        {
            if (HasPage(chapter)) return false;
            store.SetInt(PagesKey, PagesMask | (1 << chapter));
            store.Save();
            return true;
        }

        public void AddEscape()
        {
            store.SetInt(EscapesKey, Escapes + 1);
            store.Save();
        }

        // ---- Settings ----

        public int MusicVolume
        {
            get => Math.Clamp(store.GetInt(MusicKey, 6), 0, MaxVolume);
            set => Set(MusicKey, Math.Clamp(value, 0, MaxVolume));
        }

        public int SoundVolume
        {
            get => Math.Clamp(store.GetInt(SoundKey, 8), 0, MaxVolume);
            set => Set(SoundKey, Math.Clamp(value, 0, MaxVolume));
        }

        public int LookSpeed
        {
            get => Math.Clamp(store.GetInt(LookKey, 5), MinLook, MaxLook);
            set => Set(LookKey, Math.Clamp(value, MinLook, MaxLook));
        }

        public bool InvertY
        {
            get => store.GetInt(InvertKey, 0) != 0;
            set => Set(InvertKey, value ? 1 : 0);
        }

        /// <summary>0 (as dark as intended) to 5 (much brighter, for bright rooms and dim screens).</summary>
        public int Brightness
        {
            get => Math.Clamp(store.GetInt(BrightnessKey, 2), 0, MaxBrightness);
            set => Set(BrightnessKey, Math.Clamp(value, 0, MaxBrightness));
        }

        void Set(string key, int value)
        {
            store.SetInt(key, value);
            store.Save();
        }
    }
}
