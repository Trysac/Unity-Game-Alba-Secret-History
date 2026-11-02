using System;

namespace Alba.Core
{
    public enum RunOutcome
    {
        Playing,
        ChapterClear,
        GameOver,
        Victory,
    }

    /// <summary>
    /// One night's attempt: the chapter being played, lives, the banked score and the kit carried
    /// from chapter to chapter. A retry after a game over restores the chapter's starting kit and score.
    /// </summary>
    public sealed class Run
    {
        readonly int seed;
        readonly Player chapterKit = new Player();
        int chapterStartScore;
        int countedDeaths;

        public Run(int chapter = 0, int seed = 1)
        {
            this.seed = seed;
            chapterKit.CopyKit(DefaultKit(chapter));
            Begin(chapter);
        }

        public int ChapterIndex { get; private set; }
        public ChapterDef Chapter => Chapters.All[ChapterIndex];
        public Level Level { get; private set; }
        public Stage Stage { get; private set; }
        public int Lives { get; private set; }
        /// <summary>Points banked from finished chapters (including clear and flawless bonuses).</summary>
        public int BankedScore { get; private set; }
        public RunOutcome Outcome { get; private set; }
        public int LastClearBonus { get; private set; }
        public int LastFlawlessBonus { get; private set; }
        public int TotalDeaths { get; private set; }

        /// <summary>The score shown on the HUD: banked points plus this chapter's.</summary>
        public int Score => BankedScore + (Outcome == RunOutcome.Playing || Outcome == RunOutcome.GameOver ? Stage.Score : 0);

        public bool IsLastChapter => ChapterIndex == Chapters.All.Length - 1;

        /// <summary>What Eilidh carries when a chapter is started from the menu: more supplies the further in.</summary>
        public static Player DefaultKit(int chapter)
        {
            var kit = new Player { Potions = 1 + chapter };
            kit.Arsenal.AddAmmo(WeaponKind.Pistol, 12 * chapter);
            kit.Arsenal.AddAmmo(WeaponKind.Shotgun, 4 * chapter);
            kit.Arsenal.AddAmmo(WeaponKind.Rifle, chapter >= 2 ? 60 * (chapter - 1) : 0);
            return kit;
        }

        void Begin(int chapter)
        {
            ChapterIndex = Math.Clamp(chapter, 0, Chapters.All.Length - 1);
            Level = LevelBuilder.Build(Chapter);
            Stage = new Stage(Level, seed * 31 + ChapterIndex, chapterKit);
            Stage.Player.Health = Tuning.MaxHealth;
            Lives = Tuning.StartingLives;
            countedDeaths = 0;
            chapterStartScore = BankedScore;
            Outcome = RunOutcome.Playing;
        }

        public void Tick(float dt, in InputFrame input)
        {
            if (Outcome != RunOutcome.Playing) return;
            Stage.Tick(dt, input);
            // Counted from the stage so a death caused between ticks (a test, a scripted fall) is not missed.
            if (Stage.Deaths > countedDeaths)
            {
                int fresh = Stage.Deaths - countedDeaths;
                countedDeaths = Stage.Deaths;
                Lives = Math.Max(0, Lives - fresh);
                TotalDeaths += fresh;
            }

            if (Stage.Phase == StagePhase.Dying && Stage.PhaseTime >= Tuning.DyingTime)
            {
                if (Lives > 0) Stage.Respawn();
                else Outcome = RunOutcome.GameOver;
            }
            else if (Stage.Phase == StagePhase.Escaped)
            {
                LastClearBonus = Tuning.ChapterScore;
                LastFlawlessBonus = Stage.Deaths == 0 ? Tuning.FlawlessScore : 0;
                BankedScore += Stage.Score + LastClearBonus + LastFlawlessBonus;
                Outcome = IsLastChapter ? RunOutcome.Victory : RunOutcome.ChapterClear;
            }
        }

        /// <summary>Moves on after a chapter clear, carrying the kit (health is restored).</summary>
        public void NextChapter()
        {
            if (Outcome != RunOutcome.ChapterClear) throw new InvalidOperationException("The chapter is not cleared yet.");
            chapterKit.CopyKit(Stage.Player);
            Begin(ChapterIndex + 1);
        }

        /// <summary>Plays the chapter again from its start, with the kit and score it began with.</summary>
        public void RetryChapter()
        {
            BankedScore = chapterStartScore;
            Begin(ChapterIndex);
        }
    }
}
