using System.Text;
using Alba.Core;
using UnityEngine;

namespace Alba.Game
{
    public enum GameScreen
    {
        Title,
        Intro,
        Playing,
        Paused,
        ChapterClear,
        GameOver,
        Victory,
    }

    /// <summary>
    /// Runs the game: menus and story cards, the chapter being played, and the sound, effects,
    /// HUD and camera reactions to what happens in it. The rules live in Alba.Core; this class
    /// only turns input into <see cref="InputFrame"/>s and <see cref="StageEvent"/>s into feedback.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        [SerializeField] GameArt art;
        [SerializeField] Hud hud;
        [SerializeField] CameraRig rig;
        [SerializeField] WorldView world;
        [SerializeField] Effects effects;
        [SerializeField] AudioDirector audioDirector;
        [SerializeField] Light moon;

        readonly GameInput input = new GameInput();
        Run backdrop;
        float stepDistance;
        float heartbeat;
        float outcomeTimer;
        float toastCooldown;
        bool immuneHinted;
        bool hadLock;
        int seed = 1;
        float time;

        public GameScreen Screen { get; private set; } = GameScreen.Title;
        public Run Run { get; private set; }
        public Records Records { get; private set; }
        public Hud Hud => hud;
        public CameraRig Rig => rig;
        public WorldView World => world;

        /// <summary>For tests and screenshots: ignore the devices and use <see cref="ScriptedFrame"/>.</summary>
        public bool ScriptedInput { get; set; }
        public InputFrame ScriptedFrame { get; set; } = InputFrame.None;
        /// <summary>For screenshots: hold the world still while the camera keeps rendering.</summary>
        public bool Frozen { get; set; }

        void Awake()
        {
            Records = new Records(new PlayerPrefsStore());
            hud.Build();
            hud.ItemClicked += index => Choose(index);
            world.Init(art, moon);
            rig.Build(world.Looks, art);
            effects.Build(art);
            audioDirector.Build();
            ApplySettings();
        }

        void Start() => ToTitle();

        /// <summary>Tests and screenshots keep their records in memory instead of PlayerPrefs.</summary>
        public void UseMemoryStore()
        {
            Records = new Records(new MemoryStore());
            ApplySettings();
        }

        public void Seed(int value) => seed = value;

        void ApplySettings()
        {
            audioDirector.SetVolumes(Records.MusicVolume, Records.SoundVolume);
        }

        // ---- Screens ----

        public void ToTitle()
        {
            RecordPendingOutcome();
            Screen = GameScreen.Title;
            Time.timeScale = 1f;
            Run = null;
            backdrop = new Run(0, seed);
            world.Build(backdrop.Stage);
            effects.Clear();
            effects.SetRain(backdrop.Chapter.Rain);
            rig.ShowHands(false);
            hud.ShowPlay(false);
            hud.HideBanner();
            hud.FadeTo(0f);
            audioDirector.PlayMusic(art.TitleMusic);
            audioDirector.SetAmbience(art.Rain, art.Wind, backdrop.Chapter.Rain, 0.35f);
            audioDirector.PauseWorld(false);
            ShowTitleMenu();
        }

        void ShowTitleMenu(int selected = 0)
        {
            var page = new MenuPage { Id = "title", Logo = true, Footer = Footer() };
            page.Add("Begin the Night", StartCampaign);
            page.Add("Chapters", ShowChapters);
            page.Add("Journal", ShowJournal);
            page.Add("How to Play", () => ShowHowTo(() => ShowTitleMenu(3)));
            page.Add("Settings", () => ShowSettings(() => ShowTitleMenu(4)));
            if (Application.platform != RuntimePlatform.WebGLPlayer) page.Add("Quit", Quit);
            hud.ShowMenu(page, selected);
            SetCursor(false);
        }

        string Footer()
        {
            string best = Records.Best > 0 ? $"Best score {Records.Best:N0}" : "No escape yet";
            string escapes = Records.Escapes > 0 ? $"   |   Escaped {Records.Escapes}x" : "";
            return $"{best}{escapes}   |   Journal {Records.PagesFound}/{Chapters.All.Length}";
        }

        void ShowChapters()
        {
            var page = new MenuPage { Id = "chapters", Kicker = "CHOOSE WHERE TO BEGIN", Title = "Chapters", Footer = "Clear a chapter to unlock the next.", Back = () => ShowTitleMenu(1) };
            for (int i = 0; i < Chapters.All.Length; i++)
            {
                int index = i;
                bool open = i <= Records.Reached;
                page.Add($"{Roman(i + 1)}.  {(open ? Chapters.All[i].Title : "Locked")}", () => StartChapter(index), enabled: open);
            }
            page.Add("Back", () => ShowTitleMenu(1));
            hud.ShowMenu(page);
        }

        void ShowJournal()
        {
            var page = new MenuPage { Id = "journal", Kicker = "CALUM ROSS'S JOURNAL", Title = "Journal", Back = () => ShowTitleMenu(2),
                Footer = Records.PagesFound == 0 ? "One page is hidden in every chapter." : $"{Records.PagesFound} of {Chapters.All.Length} pages found." };
            for (int i = 0; i < Chapters.All.Length; i++)
            {
                int index = i;
                bool found = Records.HasPage(i);
                page.Add(found ? Chapters.All[i].PageTitle : $"Page {Roman(i + 1)}: not found", () => ShowPage(index), enabled: found);
            }
            page.Add("Back", () => ShowTitleMenu(2));
            hud.ShowMenu(page);
        }

        void ShowPage(int chapter)
        {
            ChapterDef c = Chapters.All[chapter];
            var page = new MenuPage { Id = "page", Kicker = c.Title.ToUpperInvariant(), Title = c.PageTitle, Body = c.PageText, Wide = true, Back = ShowJournal };
            page.Add("Back", ShowJournal);
            hud.ShowMenu(page);
        }

        void ShowHowTo(System.Action back)
        {
            var page = new MenuPage
            {
                Id = "howto",
                Title = "How to Play",
                Wide = true,
                Back = back,
                Body =
                    "Keyboard and mouse: WASD to move, Shift to sprint, mouse to look, left click to shoot, " +
                    "1-4 or the wheel for the dirk, pistol, shotgun and rifle, F for the torch, Q to drink a potion, P or Esc to pause.\n" +
                    "Gamepad: left stick to move (click or LT to sprint), right stick to look, RT to shoot, LB/RB or the d-pad for weapons, " +
                    "Y for the torch, X for a potion, Start to pause.\n\n" +
                    "Find the purple sealstones to open the portal in the stones. Light the red lanterns: if you fall, you wake at the last one. " +
                    "Crawlers rear up before they strike, so back away. Wisps can only be hurt inside your torch beam. " +
                    "The torch drains while it is on and recovers while it is off.",
            };
            page.Add("Back", back);
            hud.ShowMenu(page);
        }

        void ShowSettings(System.Action back)
        {
            var page = new MenuPage { Id = "settings", Title = "Settings", Back = back };
            page.Add("Music", null, () => Records.MusicVolume.ToString(), step => { Records.MusicVolume += step; ApplySettings(); });
            page.Add("Sound", null, () => Records.SoundVolume.ToString(), step => { Records.SoundVolume += step; ApplySettings(); audioDirector.Play(art.Orb); });
            page.Add("Look speed", null, () => Records.LookSpeed.ToString(), step => Records.LookSpeed += step);
            page.Add("Invert look", () => Records.InvertY = !Records.InvertY, () => Records.InvertY ? "On" : "Off", _ => Records.InvertY = !Records.InvertY);
            page.Add("Brightness", null, () => Records.Brightness.ToString(), step => { Records.Brightness += step; RefreshMood(); });
            page.Add("Back", back);
            hud.ShowMenu(page);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- Runs and chapters ----

        public void StartCampaign()
        {
            Run = new Run(0, seed);
            Screen = GameScreen.Intro;
            var page = new MenuPage { Id = "prologue", Kicker = "SAMHAIN", Title = "Alba: Secret History", Body = Chapters.Prologue, Wide = true, Back = ToTitle };
            page.Add("Continue", ShowChapterIntro);
            hud.ShowMenu(page);
            audioDirector.Play(art.Page);
        }

        public void StartChapter(int chapter)
        {
            Run = new Run(chapter, seed);
            ShowChapterIntro();
        }

        public void ShowChapterIntro()
        {
            Screen = GameScreen.Intro;
            Time.timeScale = 1f;
            PrepareChapter();
            ChapterDef c = Run.Chapter;
            var page = new MenuPage
            {
                Id = "intro",
                Kicker = $"CHAPTER {Roman(c.Index + 1)}  |  {c.Place.ToUpperInvariant()}",
                Title = c.Title,
                Body = c.Intro,
                Wide = true,
                Back = ToTitle,
            };
            page.Add("Begin", BeginChapter);
            page.Add("Main Menu", ToTitle);
            hud.ShowMenu(page);
            SetCursor(false);
        }

        void PrepareChapter()
        {
            world.Build(Run.Stage);
            effects.Clear();
            ChapterDef c = Run.Chapter;
            effects.SetRain(c.Rain);
            audioDirector.PlayMusic(Music(c));
            audioDirector.SetAmbience(art.Rain, art.Wind, c.Rain, c.Index == 1 ? 0.25f : 0.45f);
            immuneHinted = false;
            stepDistance = 0f;
            RefreshMood();
            hud.FadeTo(0f);
        }

        AudioClip Music(ChapterDef c) => c.MusicTheme switch
        {
            2 => art.KeepMusic,
            3 => art.DawnMusic,
            _ => art.NightMusic,
        };

        public void BeginChapter()
        {
            if (Run == null) return;
            Screen = GameScreen.Playing;
            Time.timeScale = 1f;
            hud.HideMenu();
            hud.ShowPlay(true);
            rig.ShowHands(true);
            rig.ShowWeapon(Run.Stage.Player.Arsenal.Current, instant: true);
            hud.Banner(Run.Chapter.Title, Run.Chapter.Place);
            audioDirector.PauseWorld(false);
            outcomeTimer = 0f;
            SetCursor(true);
            audioDirector.Play(art.Select);
        }

        public void NextChapter()
        {
            if (Run == null || Run.Outcome != RunOutcome.ChapterClear) return;
            Run.NextChapter();
            ShowChapterIntro();
        }

        public void RetryChapter()
        {
            if (Run == null) return;
            RecordPendingOutcome();
            Run.RetryChapter();
            ShowChapterIntro();
        }

        public void TogglePause()
        {
            if (Screen == GameScreen.Playing) Pause();
            else if (Screen == GameScreen.Paused) Resume();
        }

        public void Pause()
        {
            if (Screen != GameScreen.Playing) return;
            Screen = GameScreen.Paused;
            Time.timeScale = 0f;
            audioDirector.PauseWorld(true);
            audioDirector.Play(art.Pause);
            SetCursor(false);
            ShowPauseMenu();
        }

        void ShowPauseMenu(int selected = 0)
        {
            var page = new MenuPage { Id = "pause", Kicker = Run.Chapter.Title.ToUpperInvariant(), Title = "Paused", Back = Resume };
            page.Add("Resume", Resume);
            page.Add("Restart Chapter", RetryChapter);
            page.Add("Settings", () => ShowSettings(() => ShowPauseMenu(2)));
            page.Add("How to Play", () => ShowHowTo(() => ShowPauseMenu(3)));
            page.Add("Main Menu", ToTitle);
            hud.ShowMenu(page, selected);
        }

        public void Resume()
        {
            if (Screen != GameScreen.Paused) return;
            Screen = GameScreen.Playing;
            Time.timeScale = 1f;
            hud.HideMenu();
            audioDirector.PauseWorld(false);
            SetCursor(true);
        }

        void SetCursor(bool locked)
        {
            if (ScriptedInput || Application.isBatchMode) return;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            hadLock = false;
        }

        // ---- Frame ----

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            time += dt;
            hud.Tick(dt);
            toastCooldown = Mathf.Max(0f, toastCooldown - dt);

            GameScreen screenBefore = Screen;
            MenuPage pageBefore = hud.MenuOpen ? hud.Page : null;
            if (hud.MenuOpen) HandleMenu();
            // A key that acted on the menu (Esc as Back, say) must not act again on the new screen.
            bool menuActed = Screen != screenBefore || (hud.MenuOpen ? hud.Page : null) != pageBefore;

            switch (Screen)
            {
                case GameScreen.Playing:
                    if (!menuActed) Play(dt);
                    break;
                case GameScreen.Paused:
                    if (!menuActed && !ScriptedInput && input.Pause && hud.MenuOpen && hud.Page.Id == "pause")
                    {
                        Resume();
                        break;
                    }
                    rig.FollowPlayer(Run.Stage.Player, world.Height(Run.Stage.Player.Position), false, 0f, 0f, Vector2.zero);
                    break;
                default:
                    Stage shown = Run != null ? Run.Stage : backdrop?.Stage;
                    if (shown != null)
                    {
                        world.Sync(shown, dt, time);
                        Vec2 focus = shown.Level.Portal;
                        rig.Orbit(world.Ground(focus), 13f, 4.5f, dt);
                        effects.FollowRain(rig.transform.position);
                        float dawn = Screen == GameScreen.Victory ? 1f : 0f;
                        world.ApplyMood(shown.Level.Chapter, rig.Camera, Records.Brightness, dawn);
                    }
                    break;
            }
        }

        void HandleMenu()
        {
            MenuPage page = hud.Page;
            int vertical = input.VerticalStep();
            if (vertical != 0)
            {
                hud.Move(vertical);
                audioDirector.Play(art.Move, 0.6f);
            }
            int horizontal = input.HorizontalStep();
            if (horizontal != 0 && page.Items.Count > 0)
            {
                MenuItem item = page.Items[hud.Selected];
                if (item.Adjust != null)
                {
                    item.Adjust(horizontal);
                    hud.Refresh();
                    audioDirector.Play(art.Move, 0.6f);
                }
            }
            if (input.Confirm) Choose(hud.Selected);
            else if (input.Back && page.Back != null && Screen != GameScreen.Playing)
            {
                audioDirector.Play(art.Move, 0.6f);
                page.Back();
            }
        }

        /// <summary>Activates a menu item, as Enter, the gamepad's A button or a click does.</summary>
        public void Choose(int index)
        {
            MenuPage page = hud.Page;
            if (page == null || index < 0 || index >= page.Items.Count || !page.Items[index].Enabled) return;
            MenuItem item = page.Items[index];
            audioDirector.Play(art.Select, 0.8f);
            if (item.Activate != null) item.Activate();
            else item.Adjust?.Invoke(1);
            if (hud.Page == page) hud.Refresh();
        }

        /// <summary>Finds a menu item by its label and chooses it (tests and screenshots).</summary>
        public bool Choose(string label)
        {
            if (hud.Page == null) return false;
            for (int i = 0; i < hud.Page.Items.Count; i++)
            {
                if (hud.Page.Items[i].Label != label) continue;
                hud.Select(i);
                Choose(i);
                return true;
            }
            return false;
        }

        void Play(float dt)
        {
            Stage stage = Run.Stage;
            if (!ScriptedInput)
            {
                if (input.Pause)
                {
                    Pause();
                    return;
                }
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                if (locked) hadLock = true;
                else if (hadLock && Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    // The browser let go of the mouse (Esc or a lost focus): pause instead of playing blind.
                    Pause();
                    return;
                }
                else if (input.Clicked)
                {
                    SetCursor(true);
                }
            }

            InputFrame frame = ScriptedInput ? ScriptedFrame : input.Read(Records.LookSpeed, Records.InvertY, dt);
            if (Run.Outcome == RunOutcome.Playing && !Frozen)
            {
                Run.Tick(dt, frame);
                React(stage);
            }

            Player player = stage.Player;
            bool dying = stage.Phase == StagePhase.Dying || Run.Outcome == RunOutcome.GameOver;
            world.Sync(stage, Frozen ? 0f : dt, time);
            rig.FollowPlayer(player, world.Height(player.Position), dying, stage.PhaseTime, Frozen ? 0f : dt, input.LastLook);
            effects.FollowRain(rig.transform.position);
            world.ApplyMood(Run.Chapter, rig.Camera, Records.Brightness, Run.Chapter.SurvivalSeconds > 0f ? Mathf.SmoothStep(0f, 1f, stage.DawnProgress) : 0f);
            hud.UpdatePlay(Run, Objective(stage), input.UsingGamepad);

            Footsteps(player);
            if (player.Alive && player.Health <= 30)
            {
                heartbeat -= dt;
                if (heartbeat <= 0f)
                {
                    audioDirector.Play(art.Heartbeat, 0.7f);
                    heartbeat = 0.6f + player.Health / 30f * 0.6f;
                }
            }

            if (Run.Outcome != RunOutcome.Playing)
            {
                outcomeTimer += dt;
                float wait = Run.Outcome == RunOutcome.GameOver ? 0.4f : 1.4f;
                hud.FadeTo(Run.Outcome == RunOutcome.GameOver ? 0.6f : 1f);
                if (outcomeTimer >= wait) ShowOutcome();
            }
        }

        void Footsteps(Player player)
        {
            if (!player.Alive) return;
            if (player.Stride - stepDistance > 1.7f)
            {
                stepDistance = player.Stride;
                audioDirector.Play(art.Step, 0.35f, Random.Range(0.9f, 1.1f));
            }
        }

        public string Objective(Stage stage)
        {
            if (stage.SurvivalTotal > 0f)
            {
                if (!stage.DawnBroke)
                {
                    int seconds = Mathf.CeilToInt(stage.SurvivalLeft);
                    return $"Hold the circle until dawn   {seconds / 60}:{seconds % 60:00}";
                }
                return "Dawn! Step through the door in the stones";
            }
            if (stage.PortalOpen) return "The way is open: reach the portal";
            if (stage.GemsFound < stage.GemsTotal) return $"Find the sealstones   {stage.GemsFound}/{stage.GemsTotal}";
            Enemy boss = stage.Boss;
            if (boss != null && boss.Alive) return "Bring down Am Fomhair to open the gate";
            return "Reach the portal";
        }

        // ---- Reactions to the rules ----

        void React(Stage stage)
        {
            foreach (StageEvent e in stage.Events)
            {
                Vector3 at = world.Ground(e.Position, 1.1f);
                switch (e.Kind)
                {
                    case StageEventKind.Fired:
                    {
                        var kind = (WeaponKind)e.Amount;
                        rig.Fire(kind);
                        audioDirector.Play(kind == WeaponKind.Shotgun ? art.Shotgun : kind == WeaponKind.Rifle ? art.Rifle : art.Pistol, 0.85f, Random.Range(0.95f, 1.05f));
                        break;
                    }
                    case StageEventKind.Swung:
                        rig.Fire(WeaponKind.Dirk);
                        audioDirector.Play(art.Dirk, 0.8f, Random.Range(0.9f, 1.1f));
                        break;
                    case StageEventKind.DryFire:
                        audioDirector.Play(art.DryFire);
                        Hint("Out of ammunition: switch weapons (1-4) or find more.");
                        break;
                    case StageEventKind.Impact:
                        if (e.Amount == 1)
                        {
                            effects.Emit(Burst.Sparks, at, 6);
                            audioDirector.PlayAt(art.Ricochet, at, 0.45f);
                        }
                        break;
                    case StageEventKind.EnemyHit:
                    {
                        world.ViewOf(e.Enemy)?.Flash();
                        Vector3 body = at + Vector3.up * (e.Enemy.Kind == EnemyKind.Brute ? 1.2f : e.Enemy.Kind == EnemyKind.Wisp ? 0.3f : -0.4f);
                        effects.Emit(e.Enemy.Kind == EnemyKind.Wisp ? Burst.Embers : Burst.Ichor, body, e.Enemy.Kind == EnemyKind.Brute ? 10 : 6);
                        audioDirector.PlayAt(art.Hit, body, 0.6f);
                        break;
                    }
                    case StageEventKind.EnemyImmune:
                        if (!immuneHinted)
                        {
                            immuneHinted = true;
                            Hint("Bullets pass through a wisp in the dark. Catch it in your torch beam (F).", force: true);
                        }
                        audioDirector.PlayAt(art.Immune, at, 0.5f);
                        break;
                    case StageEventKind.EnemyKilled:
                        Killed(e.Enemy, at);
                        break;
                    case StageEventKind.EnemyAlerted:
                        audioDirector.PlayAt(e.Enemy.Kind == EnemyKind.Wisp ? art.WispAlert : e.Enemy.Kind == EnemyKind.Brute ? art.BruteRoar : art.CrawlerAlert, at, 0.9f);
                        break;
                    case StageEventKind.EnemyWindup:
                        audioDirector.PlayAt(e.Enemy.Kind == EnemyKind.Brute ? art.BruteWindup : art.CrawlerWindup, at, 0.8f);
                        break;
                    case StageEventKind.EnemyMissed:
                        if (e.Enemy.Kind == EnemyKind.Brute) Slam(e.Enemy);
                        break;
                    case StageEventKind.EnemySpawned:
                        effects.Emit(Burst.Smoke, world.Ground(e.Position, 0.6f), 10);
                        break;
                    case StageEventKind.EnemyDissolved:
                        effects.Emit(Burst.Smoke, world.Ground(e.Position, 0.8f), 12);
                        effects.Emit(Burst.Embers, world.Ground(e.Position, 0.8f), 8, Palette.DawnSun);
                        break;
                    case StageEventKind.BossEnraged:
                        audioDirector.PlayAt(art.BruteRoar, at, 1f, 0.02f);
                        rig.Shake(0.8f);
                        hud.Banner("Am Fomhair Rages", "It calls its brood");
                        break;
                    case StageEventKind.BossDefeated:
                        hud.Banner("Am Fomhair Falls", "The gate can open now");
                        rig.Shake(0.6f);
                        break;
                    case StageEventKind.PlayerHurt:
                        audioDirector.Play(art.Hurt, 0.9f, Random.Range(0.9f, 1.1f));
                        hud.Hurt(Mathf.Clamp01(0.4f + e.Amount / 30f));
                        rig.Shake(0.25f + e.Amount / 60f);
                        if (e.Enemy != null && e.Enemy.Kind == EnemyKind.Brute) Slam(e.Enemy);
                        break;
                    case StageEventKind.PlayerDied:
                        audioDirector.Play(art.Death);
                        hud.FadeTo(0.85f);
                        hud.Banner("You Fall", Run.Lives > 0 ? (Run.Lives == 1 ? "1 life left" : $"{Run.Lives} lives left") : "The night takes you");
                        break;
                    case StageEventKind.Respawned:
                        hud.FadeTo(0f);
                        hud.Banner("You Wake", "by the last lantern you lit");
                        rig.ShowWeapon(stage.Player.Arsenal.Current, instant: true);
                        break;
                    case StageEventKind.PickedUp:
                        PickedUp(e, at);
                        break;
                    case StageEventKind.PotionDrunk:
                        audioDirector.Play(art.Potion);
                        effects.Emit(Burst.Motes, rig.transform.position + rig.transform.forward * 0.8f - Vector3.up * 0.3f, 10, Palette.Potion);
                        break;
                    case StageEventKind.PotionRefused:
                        Hint(e.Amount == 0 ? "Your flask is empty." : "You are not hurt.");
                        audioDirector.Play(art.DryFire, 0.5f);
                        break;
                    case StageEventKind.WeaponSwitched:
                        rig.ShowWeapon(stage.Player.Arsenal.Current);
                        audioDirector.Play(art.Ammo, 0.5f);
                        break;
                    case StageEventKind.TorchToggled:
                        audioDirector.Play(art.Torch, 0.8f);
                        break;
                    case StageEventKind.TorchEmpty:
                        audioDirector.Play(art.TorchEmpty);
                        Hint("Your torch is flat. Switch it off (F) and it will recover.", force: true);
                        break;
                    case StageEventKind.LanternLit:
                        audioDirector.PlayAt(art.Lantern, at + Vector3.up, 1f, 0f);
                        effects.Emit(Burst.Motes, at + Vector3.up, 16, Palette.LanternLight);
                        hud.Banner("Lantern Lit", "If you fall, you will wake here");
                        break;
                    case StageEventKind.PortalOpened:
                        audioDirector.Play(art.PortalOpen);
                        hud.Banner("The Way Is Open", "Reach the portal in the stones");
                        break;
                    case StageEventKind.Escaped:
                        audioDirector.Play(art.Escape);
                        effects.Emit(Burst.Motes, world.Ground(e.Position, 2f), 40, Palette.Portal);
                        break;
                    case StageEventKind.WaveStarted:
                        audioDirector.Play(art.Wave, 0.8f);
                        Hint(e.Amount == 1 ? "They are coming. Hold the circle!" : $"Wave {e.Amount}", force: true);
                        break;
                    case StageEventKind.Dawn:
                        audioDirector.Play(art.Dawn);
                        hud.Banner("Dawn", "The door in the stones is open");
                        break;
                }
            }
        }

        void Killed(Enemy enemy, Vector3 at)
        {
            switch (enemy.Kind)
            {
                case EnemyKind.Crawler:
                    audioDirector.PlayAt(art.CrawlerDie, at, 0.9f);
                    effects.Emit(Burst.Ichor, at - Vector3.up * 0.4f, 16);
                    break;
                case EnemyKind.Wisp:
                    audioDirector.PlayAt(art.WispDie, at, 0.9f);
                    effects.Emit(Burst.Embers, at + Vector3.up * 0.3f, 30);
                    break;
                default:
                    audioDirector.PlayAt(art.BruteDie, at, 1f, 0f);
                    effects.Emit(Burst.Dust, world.Ground(enemy.Position, 0.3f), 30);
                    break;
            }
        }

        void Slam(Enemy brute)
        {
            Vector3 ground = world.Ground(brute.Position + Vec2.FromYaw(brute.Yaw) * 2.2f, 0.2f);
            audioDirector.PlayAt(art.BruteSlam, ground, 1f, 0.03f);
            effects.Emit(Burst.Dust, ground, 24);
            float distance = Vec2.Distance(brute.Position, Run.Stage.Player.Position);
            rig.Shake(Mathf.Clamp01(1.2f - distance / 12f));
        }

        void PickedUp(StageEvent e, Vector3 at)
        {
            Stage stage = Run.Stage;
            switch (e.Pickup)
            {
                case PickupKind.Orb:
                    audioDirector.Play(art.Orb, 0.6f, Random.Range(0.95f, 1.08f));
                    effects.Emit(Burst.Motes, at - Vector3.up * 0.2f, 8, Palette.Orb);
                    break;
                case PickupKind.Gem:
                    audioDirector.Play(art.Gem);
                    effects.Emit(Burst.Motes, at, 24, Palette.Gem);
                    if (stage.GemsFound < stage.GemsTotal) hud.Banner("Sealstone", $"{stage.GemsFound} of {stage.GemsTotal}");
                    break;
                case PickupKind.Potion:
                    audioDirector.Play(art.Potion, 0.6f);
                    Hint($"Potion: {stage.Player.Potions} in the flask (Q to drink).");
                    break;
                case PickupKind.Page:
                    audioDirector.Play(art.Page);
                    Records.FindPage(Run.ChapterIndex);
                    hud.Banner("Journal Page", Run.Chapter.PageTitle + ": read it from the Journal");
                    break;
                default:
                    audioDirector.Play(art.Ammo);
                    WeaponKind kind = e.Pickup == PickupKind.PistolAmmo ? WeaponKind.Pistol : e.Pickup == PickupKind.ShotgunAmmo ? WeaponKind.Shotgun : WeaponKind.Rifle;
                    Hint($"+{e.Amount} {WeaponSpec.Of(kind).Name.ToLowerInvariant()} rounds");
                    break;
            }
        }

        void Hint(string text, bool force = false)
        {
            if (!force && toastCooldown > 0f) return;
            hud.Toast(text);
            toastCooldown = 1.2f;
        }

        // ---- Outcomes ----

        /// <summary>
        /// Leaving through the pause menu during the short fade after a clear, a death or the dawn
        /// would skip the outcome screen, which is where the records are saved; save them here instead.
        /// </summary>
        void RecordPendingOutcome()
        {
            if (Run == null || Screen != GameScreen.Paused) return;
            switch (Run.Outcome)
            {
                case RunOutcome.ChapterClear:
                    Records.Unlock(Run.ChapterIndex + 1);
                    Records.Submit(Run.Score);
                    break;
                case RunOutcome.GameOver:
                    Records.Submit(Run.Score);
                    break;
                case RunOutcome.Victory:
                    Records.Submit(Run.Score);
                    Records.AddEscape();
                    break;
            }
        }

        void ShowOutcome()
        {
            switch (Run.Outcome)
            {
                case RunOutcome.ChapterClear:
                    ShowChapterClear();
                    break;
                case RunOutcome.GameOver:
                    ShowGameOver();
                    break;
                case RunOutcome.Victory:
                    ShowVictory();
                    break;
            }
        }

        void ShowChapterClear()
        {
            Screen = GameScreen.ChapterClear;
            Records.Unlock(Run.ChapterIndex + 1);
            Records.Submit(Run.Score);
            hud.ShowPlay(false);
            rig.ShowHands(false);
            hud.FadeTo(0f);
            hud.HideBanner();
            SetCursor(false);
            Stage stage = Run.Stage;
            var page = new MenuPage
            {
                Id = "clear",
                Kicker = $"CHAPTER {Roman(Run.ChapterIndex + 1)} CLEARED",
                Title = Run.Chapter.Title,
                Subtitle = $"Score {Run.Score:N0}",
                Body = Breakdown(stage),
            };
            page.Add("Next Chapter", NextChapter);
            page.Add("Main Menu", ToTitle);
            hud.ShowMenu(page);
            audioDirector.Play(art.Victory, 0.7f);
        }

        string Breakdown(Stage stage)
        {
            var text = new StringBuilder();
            text.AppendLine($"Orbs  {stage.OrbsFound} x {Tuning.OrbScore}  =  {stage.OrbsFound * Tuning.OrbScore}");
            if (stage.GemsTotal > 0) text.AppendLine($"Sealstones  {stage.GemsFound} x {Tuning.GemScore}  =  {stage.GemsFound * Tuning.GemScore}");
            text.AppendLine($"Creatures  {stage.Kills}  =  {stage.KillScore}");
            text.AppendLine($"Journal page  =  {(stage.PageFound ? Tuning.PageScore : 0)}");
            text.AppendLine($"Chapter bonus  =  {Run.LastClearBonus}");
            text.Append($"Without falling  =  {Run.LastFlawlessBonus}");
            return text.ToString();
        }

        void ShowGameOver()
        {
            Screen = GameScreen.GameOver;
            bool best = Records.Submit(Run.Score);
            hud.ShowPlay(false);
            rig.ShowHands(false);
            hud.HideBanner();
            SetCursor(false);
            var page = new MenuPage
            {
                Id = "gameover",
                Kicker = Run.Chapter.Title.ToUpperInvariant(),
                Title = "You Didn't Escape",
                Subtitle = best ? $"New best score: {Run.Score:N0}" : $"Score {Run.Score:N0}   |   Best {Records.Best:N0}",
                Body = "The night closes over you. The lanterns are cold, but the chapter can be tried again from its start.",
            };
            page.Add("Try Again", RetryChapter);
            page.Add("Main Menu", ToTitle);
            hud.ShowMenu(page);
            audioDirector.Play(art.GameOver);
        }

        void ShowVictory()
        {
            Screen = GameScreen.Victory;
            bool best = Records.Submit(Run.Score);
            Records.AddEscape();
            hud.ShowPlay(false);
            rig.ShowHands(false);
            hud.FadeTo(0f);
            hud.HideBanner();
            effects.SetRain(false);
            SetCursor(false);
            var page = new MenuPage
            {
                Id = "victory",
                Kicker = "DAWN",
                Title = Chapters.EndingTitle,
                Subtitle = best ? $"New best score: {Run.Score:N0}" : $"Score {Run.Score:N0}   |   Best {Records.Best:N0}",
                Body = Chapters.Ending,
                Wide = true,
            };
            page.Add("Main Menu", ToTitle);
            hud.ShowMenu(page);
            audioDirector.Play(art.Victory);
            audioDirector.PlayMusic(art.TitleMusic);
        }

        void RefreshMood()
        {
            Stage stage = Run?.Stage ?? backdrop?.Stage;
            if (stage == null) return;
            world.ApplyMood(stage.Level.Chapter, rig.Camera, Records.Brightness, Screen == GameScreen.Victory ? 1f : stage.DawnProgress);
        }

        static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => n.ToString() };

        // ---- Test and screenshot hooks ----

        /// <summary>Moves the player (and the camera) to a point, for tests and screenshots.</summary>
        public void PlacePlayer(Vec2 position, float yaw)
        {
            Player player = Run.Stage.Player;
            player.Position = position;
            player.Yaw = yaw;
            player.Velocity = Vec2.Zero;
            stepDistance = player.Stride;
        }
    }
}
