using System.Collections;
using Alba.Core;
using Alba.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alba.Tests
{
    /// <summary>Pause keys pressed on a virtual keyboard, and leaving through the pause menu right after an escape.</summary>
    public class PauseKeyTests
    {
        GameDirector director;
        Keyboard keyboard;

        [UnitySetUp]
        public IEnumerator Load()
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            SceneManager.LoadScene("Alba");
            yield return null;
            yield return null;
            director = Object.FindAnyObjectByType<GameDirector>();
            director.UseMemoryStore();
            director.ScriptedInput = true;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        }

        static IEnumerator Wait(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        IEnumerator Begin(int chapter)
        {
            director.StartChapter(chapter);
            yield return null;
            director.Choose("Begin");
            yield return null;
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
        }

        [UnityTest]
        public IEnumerator EscPausesAndEscResumes()
        {
            yield return Begin(0);
            director.ScriptedInput = false;
            yield return Tap(Key.Escape);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Paused), "Esc pauses");
            yield return Tap(Key.Escape);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing), "Esc on the pause menu should resume");
        }

        [UnityTest]
        public IEnumerator PTogglesThePause()
        {
            yield return Begin(0);
            director.ScriptedInput = false;
            yield return Tap(Key.P);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Paused), "P pauses");
            yield return Tap(Key.P);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing), "P again should resume");
        }

        [UnityTest]
        public IEnumerator LeavingFromPauseDuringTheEscapeFadeKeepsTheUnlock()
        {
            yield return Begin(0);
            Stage stage = director.Run.Stage;
            foreach (Pickup p in stage.Pickups.FindAll(p => p.Kind == PickupKind.Gem))
            {
                stage.Player.HurtTimer = 100f;
                director.PlacePlayer(p.Position, 0f);
                yield return Wait(0.1f);
            }
            director.PlacePlayer(stage.Level.Portal, 0f);
            yield return Wait(0.3f);
            Assert.That(director.Run.Outcome, Is.EqualTo(RunOutcome.ChapterClear));
            director.Pause();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Paused));
            director.Choose("Main Menu");
            yield return null;
            Assert.That(director.Records.Reached, Is.EqualTo(1), "the cleared chapter should stay unlocked");
        }
    }
}
