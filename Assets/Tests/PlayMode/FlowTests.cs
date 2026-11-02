using System.Collections;
using Alba.Core;
using Alba.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alba.Tests
{
    /// <summary>The game's flow in the real scene: menus, a chapter start, lanterns and deaths, the portal, pause, settings, the journal and the ending.</summary>
    public class FlowTests
    {
        GameDirector director;

        [UnitySetUp]
        public IEnumerator Load()
        {
            SceneManager.LoadScene("Alba");
            yield return null;
            yield return null;
            director = Object.FindAnyObjectByType<GameDirector>();
            Assert.That(director, Is.Not.Null);
            director.UseMemoryStore();
            director.ScriptedInput = true;
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title));
            Assert.That(director.Hud.MenuOpen, Is.True, "the title menu is showing");
            Assert.That(director.Hud.Page.Id, Is.EqualTo("title"));
        }

        [TearDown]
        public void Restore() => Time.timeScale = 1f;

        IEnumerator Play(int chapter)
        {
            director.StartChapter(chapter);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Intro));
            yield return null;
            Assert.That(director.Choose("Begin"), Is.True);
            yield return null;
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Hud.MenuOpen, Is.False);
        }

        static IEnumerator Wait(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        IEnumerator Die()
        {
            Stage stage = director.Run.Stage;
            stage.Player.HurtTimer = 0f;
            stage.HurtPlayer(1000, stage.Player.Position);
            Assert.That(stage.Phase, Is.EqualTo(StagePhase.Dying));
            yield return Wait(Tuning.DyingTime + 0.4f);
        }

        [UnityTest]
        public IEnumerator BeginningTheNightShowsThePrologueThenStartsTheFirstChapter()
        {
            Assert.That(director.Choose("Begin the Night"), Is.True);
            Assert.That(director.Hud.Page.Id, Is.EqualTo("prologue"));
            Assert.That(director.Hud.Page.Body, Is.EqualTo(Chapters.Prologue));
            director.Choose("Continue");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Intro));
            Assert.That(director.Hud.Page.Title, Is.EqualTo(Chapters.All[0].Title));
            director.Choose("Begin");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Run.ChapterIndex, Is.Zero);
            Assert.That(director.Run.Lives, Is.EqualTo(Tuning.StartingLives));

            director.ScriptedFrame = new InputFrame(new Vec2(0f, 1f));
            float start = director.Run.Stage.Player.Position.Z;
            yield return Wait(0.6f);
            Assert.That(director.Run.Stage.Player.Position.Z, Is.GreaterThan(start + 1f), "walked north");
            Assert.That(director.Hud.ObjectiveText, Is.EqualTo("Find the sealstones   0/3"));
            Assert.That(director.Rig.transform.position.z, Is.EqualTo(director.Run.Stage.Player.Position.Z).Within(0.3f), "the camera follows");
        }

        [UnityTest]
        public IEnumerator FallingWakesYouAtTheLitLanternAndTheLastFallOffersARetry()
        {
            yield return Play(0);
            Stage stage = director.Run.Stage;
            Lantern lantern = stage.Lanterns[0];
            director.PlacePlayer(lantern.Position + new Vec2(0f, -1f), 0f);
            yield return Wait(0.2f);
            Assert.That(lantern.Lit, Is.True);
            Assert.That(director.Hud.BannerText, Is.EqualTo("Lantern Lit"));

            director.PlacePlayer(new Vec2(-30f, -45f), 0f);
            yield return Die();
            Assert.That(director.Run.Lives, Is.EqualTo(Tuning.StartingLives - 1));
            Assert.That(stage.Phase, Is.EqualTo(StagePhase.Playing));
            Assert.That(stage.Player.Health, Is.EqualTo(Tuning.MaxHealth));
            Assert.That(Vec2.Distance(stage.Player.Position, lantern.Position), Is.LessThan(3f), "back at the lantern");
            Assert.That(director.Hud.BannerText, Is.EqualTo("You Wake"));

            yield return Die();
            yield return Die();
            yield return Wait(0.6f);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.GameOver));
            Assert.That(director.Hud.Page.Title, Is.EqualTo("You Didn't Escape"));

            director.Choose("Try Again");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Intro));
            Assert.That(director.Run.Lives, Is.EqualTo(Tuning.StartingLives));
            Assert.That(director.Run.Stage.Lanterns[0].Lit, Is.False);
        }

        [UnityTest]
        public IEnumerator SealstonesOpenThePortalAndClearingTheChapterUnlocksTheNext()
        {
            yield return Play(0);
            Stage stage = director.Run.Stage;
            foreach (Pickup p in stage.Pickups.FindAll(p => p.Kind == PickupKind.Gem))
            {
                stage.Player.HurtTimer = 100f;
                director.PlacePlayer(p.Position, 0f);
                yield return Wait(0.1f);
                Assert.That(p.Taken, Is.True);
            }
            Assert.That(stage.PortalOpen, Is.True);
            Assert.That(director.Hud.BannerText, Is.EqualTo("The Way Is Open"));
            Assert.That(director.Hud.ObjectiveText, Is.EqualTo("The way is open: reach the portal"));

            director.PlacePlayer(stage.Level.Portal, 0f);
            yield return Wait(1.8f);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.ChapterClear));
            Assert.That(director.Hud.Page.Id, Is.EqualTo("clear"));
            Assert.That(director.Records.Reached, Is.EqualTo(1));
            Assert.That(director.Records.Best, Is.EqualTo(director.Run.Score));

            director.Choose("Next Chapter");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Intro));
            Assert.That(director.Run.ChapterIndex, Is.EqualTo(1));
            Assert.That(director.Hud.Page.Title, Is.EqualTo(Chapters.All[1].Title));
        }

        [UnityTest]
        public IEnumerator PausingFreezesTheChapterAndResumingContinuesIt()
        {
            yield return Play(0);
            director.ScriptedFrame = new InputFrame(new Vec2(0f, 1f));
            yield return Wait(0.2f);
            director.TogglePause();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Paused));
            Assert.That(director.Hud.Page.Id, Is.EqualTo("pause"));
            Assert.That(Time.timeScale, Is.Zero);
            Vec2 held = director.Run.Stage.Player.Position;
            float elapsed = director.Run.Stage.Elapsed;
            yield return Wait(0.4f);
            Assert.That(director.Run.Stage.Player.Position, Is.EqualTo(held));
            Assert.That(director.Run.Stage.Elapsed, Is.EqualTo(elapsed));

            director.Choose("Resume");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return Wait(0.3f);
            Assert.That(director.Run.Stage.Player.Position.Z, Is.GreaterThan(held.Z + 0.5f));
        }

        [UnityTest]
        public IEnumerator RestartingFromThePauseMenuStartsTheChapterAfresh()
        {
            yield return Play(0);
            Stage first = director.Run.Stage;
            Pickup orb = first.Pickups.Find(p => p.Kind == PickupKind.Orb);
            director.PlacePlayer(orb.Position, 0f);
            yield return Wait(0.1f);
            Assert.That(first.OrbsFound, Is.EqualTo(1));
            Assert.That(director.Run.Score, Is.EqualTo(Tuning.OrbScore));

            director.Pause();
            director.Choose("Restart Chapter");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Intro));
            Assert.That(director.Run.Stage, Is.Not.SameAs(first));
            Assert.That(director.Run.Stage.OrbsFound, Is.Zero);
            Assert.That(director.Run.Score, Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator SettingsChangeWhenChosenAndAreKept()
        {
            director.Choose("Settings");
            Assert.That(director.Hud.Page.Id, Is.EqualTo("settings"));
            int music = director.Records.MusicVolume;
            director.Choose("Music");
            Assert.That(director.Records.MusicVolume, Is.EqualTo(music + 1));
            director.Choose("Invert look");
            Assert.That(director.Records.InvertY, Is.True);
            director.Choose("Brightness");
            Assert.That(director.Records.Brightness, Is.EqualTo(3));
            director.Choose("Back");
            Assert.That(director.Hud.Page.Id, Is.EqualTo("title"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AJournalPageIsKeptAndCanBeReadFromTheMenu()
        {
            yield return Play(0);
            Pickup page = director.Run.Stage.Pickups.Find(p => p.Kind == PickupKind.Page);
            director.Run.Stage.Player.HurtTimer = 100f;
            director.PlacePlayer(page.Position, 0f);
            yield return Wait(0.1f);
            Assert.That(director.Run.Stage.PageFound, Is.True);
            Assert.That(director.Records.HasPage(0), Is.True);
            Assert.That(director.Hud.BannerText, Is.EqualTo("Journal Page"));

            director.ToTitle();
            director.Choose("Journal");
            Assert.That(director.Hud.Page.Id, Is.EqualTo("journal"));
            Assert.That(director.Choose(Chapters.All[0].PageTitle), Is.True);
            Assert.That(director.Hud.Page.Id, Is.EqualTo("page"));
            Assert.That(director.Hud.Page.Body, Is.EqualTo(Chapters.All[0].PageText));
        }

        [UnityTest]
        public IEnumerator HoldingTheCircleUntilDawnAndSteppingThroughWinsTheNight()
        {
            yield return Play(3);
            Run run = director.Run;
            Assert.That(run.Stage.PortalOpen, Is.False);
            for (int i = 0; i < 60 * 101; i++)
            {
                run.Stage.Player.HurtTimer = 1000f;
                run.Tick(1f / 60f, InputFrame.None);
            }
            Assert.That(run.Stage.DawnBroke, Is.True);
            Assert.That(run.Stage.PortalOpen, Is.True);
            director.PlacePlayer(run.Level.Portal, 0f);
            yield return Wait(1.8f);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Victory));
            Assert.That(director.Hud.Page.Title, Is.EqualTo(Chapters.EndingTitle));
            Assert.That(director.Records.Escapes, Is.EqualTo(1));
            Assert.That(director.Records.Best, Is.EqualTo(run.Score));
            director.Choose("Main Menu");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title));
        }
    }
}
