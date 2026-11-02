using System;
using System.Collections;
using System.IO;
using Alba.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Alba.Game
{
    /// <summary>
    /// Opt-in capture of README screenshots: <c>Alba.exe -screenshot &lt;folder&gt; -scenario title,stones,wood,wisp,keep,giant,dawn,pause,clear,gameover,victory</c>.
    /// Without the -screenshot argument nothing is created, so normal runs and WebGL builds (which
    /// have no command line) are unaffected. The same hook runs inside the editor
    /// (ProjectBuilder.CaptureScreenshots), which renders the camera and the HUD off screen and blends them.
    /// </summary>
    public sealed class ScreenshotScenarios : MonoBehaviour
    {
        const string DefaultScenarios = "title,intro,stones,wood,wisp,keep,giant,dawn,pause,clear,gameover,victory,journal,howto";
        const int Width = 1280;
        const int Height = 720;

        string folder;
        string[] scenarios;
        RenderTexture sceneTarget;
        RenderTexture uiTarget;
        GameDirector director;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWhenRequested()
        {
            string target = Argument("-screenshot");
            if (string.IsNullOrEmpty(target)) return;
            var host = new GameObject(nameof(ScreenshotScenarios)).AddComponent<ScreenshotScenarios>();
            host.folder = target;
            host.scenarios = (Argument("-scenario") ?? DefaultScenarios).Split(',');
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            // A failed scenario must not leave the editor running forever.
            Application.logMessageReceived += (message, stack, type) =>
            {
                // Only our own failures: the editor logs unrelated exceptions of its own while indexing.
                if (type != LogType.Exception || stack == null || !stack.Contains("Alba.")) return;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.Exit(1);
#else
                Application.Quit(1);
#endif
            };
            director = FindAnyObjectByType<GameDirector>();
            director.UseMemoryStore();
            if (Application.isEditor) RenderOffscreen();
            yield return new WaitForSecondsRealtime(0.5f);
            foreach (string raw in scenarios)
            {
                string scenario = raw.Trim();
                director.ScriptedInput = true;
                director.Frozen = false;
                yield return Stage(scenario);
                Texture2D shot;
                if (Application.isEditor)
                {
                    yield return null;
                    shot = CaptureOffscreen();
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                    shot = ScreenCapture.CaptureScreenshotAsTexture();
                }
                File.WriteAllBytes(Path.Combine(folder, scenario + ".png"), shot.EncodeToPNG());
                Destroy(shot);
                Debug.Log($"Saved screenshot {scenario}.png");
                director.Frozen = false;
                Time.timeScale = 1f;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        IEnumerator Stage(string scenario)
        {
            switch (scenario)
            {
                case "title":
                    director.ToTitle();
                    yield return Wait(3f);
                    break;
                case "intro":
                    director.ToTitle();
                    director.StartChapter(1);
                    yield return Wait(2f);
                    break;
                case "journal":
                    director.Records.FindPage(0);
                    director.Records.FindPage(1);
                    director.ToTitle();
                    director.Choose("Journal");
                    director.Choose(Chapters.All[1].PageTitle);
                    yield return Wait(1.2f);
                    break;
                case "howto":
                    director.ToTitle();
                    director.Choose("How to Play");
                    yield return Wait(1.2f);
                    break;
                case "stones":
                {
                    yield return Begin(0);
                    Stage stage = director.Run.Stage;
                    TakeSome(stage, PickupKind.Orb, 7);
                    TakeSome(stage, PickupKind.Gem, 1);
                    director.PlacePlayer(new Vec2(4f, 8f), 6f);
                    stage.Player.Pitch = 4f;
                    WakeNear(stage, new Vec2(6f, 24f), 0);
                    yield return Advance(1.2f);
                    break;
                }
                case "wood":
                {
                    yield return Begin(1);
                    Stage stage = director.Run.Stage;
                    TakeSome(stage, PickupKind.Orb, 11);
                    TakeSome(stage, PickupKind.Gem, 2);
                    director.PlacePlayer(new Vec2(-26f, -20f), 10f);
                    stage.Player.Pitch = 2f;
                    Enemy crawler = stage.Enemies.Find(e => e.Kind == EnemyKind.Crawler);
                    crawler.Position = new Vec2(-25f, -11f);
                    crawler.State = EnemyState.Chase;
                    director.ScriptedFrame = new InputFrame(Vec2.Zero, fireHeld: false);
                    yield return Advance(0.4f);
                    break;
                }
                case "wisp":
                {
                    yield return Begin(1);
                    Stage stage = director.Run.Stage;
                    director.PlacePlayer(new Vec2(-24f, -6f), 345f);
                    Enemy wisp = stage.Enemies.Find(e => e.Kind == EnemyKind.Wisp);
                    wisp.Position = new Vec2(-26.5f, 2f);
                    wisp.State = EnemyState.Chase;
                    stage.Player.Arsenal.Select(WeaponKind.Shotgun);
                    director.Rig.ShowWeapon(WeaponKind.Shotgun, instant: true);
                    yield return Advance(0.5f);
                    director.ScriptedFrame = new InputFrame(Vec2.Zero, firePressed: true, fireHeld: true);
                    yield return Advance(0.06f);
                    break;
                }
                case "keep":
                {
                    yield return Begin(2);
                    Stage stage = director.Run.Stage;
                    TakeSome(stage, PickupKind.Orb, 14);
                    TakeSome(stage, PickupKind.Gem, 2);
                    director.PlacePlayer(new Vec2(0f, -14f), 0f);
                    stage.Player.Pitch = 6f;
                    yield return Advance(0.6f);
                    break;
                }
                case "giant":
                {
                    yield return Begin(2);
                    Stage stage = director.Run.Stage;
                    stage.Player.Arsenal.AddAmmo(WeaponKind.Rifle, 120);
                    stage.Player.Arsenal.Select(WeaponKind.Rifle);
                    director.Rig.ShowWeapon(WeaponKind.Rifle, instant: true);
                    director.PlacePlayer(new Vec2(0f, 8f), 0f);
                    stage.Player.Pitch = 10f;
                    stage.Player.HurtTimer = 100f;
                    Enemy boss = stage.Boss;
                    boss.Position = new Vec2(0f, 15f);
                    boss.TakeHit(260, stage.Player.Position);
                    boss.State = EnemyState.Chase;
                    yield return Advance(0.35f);
                    director.ScriptedFrame = new InputFrame(Vec2.Zero, firePressed: true, fireHeld: true);
                    yield return Advance(0.5f);
                    break;
                }
                case "dawn":
                {
                    yield return Begin(3);
                    Stage stage = director.Run.Stage;
                    stage.Player.HurtTimer = 1000f;
                    stage.Player.Arsenal.AddAmmo(WeaponKind.Shotgun, 20);
                    stage.Player.Arsenal.Select(WeaponKind.Shotgun);
                    director.Rig.ShowWeapon(WeaponKind.Shotgun, instant: true);
                    for (int i = 0; i < 60 * 70; i++)
                    {
                        stage.Player.HurtTimer = 1000f;
                        director.Run.Tick(1f / 60f, InputFrame.None);
                    }
                    // Line a few creatures of the waves up in front, and send the rest behind.
                    const float yaw = 0f;
                    stage.Player.Position = new Vec2(0f, -7f);
                    int shown = 0;
                    foreach (Enemy e in stage.Enemies)
                    {
                        if (!e.Alive) continue;
                        float angle = shown < 4 ? yaw + (shown - 1.5f) * 18f : yaw + 180f;
                        float distance = shown < 4 ? 7f + shown * 2f : 30f;
                        e.Position = stage.Level.Resolve(stage.Player.Position + Vec2.FromYaw(angle) * distance, e.Spec.Radius);
                        shown++;
                    }
                    director.PlacePlayer(stage.Player.Position, yaw);
                    stage.Player.Pitch = 3f;
                    yield return Advance(0.3f);
                    director.Hud.HideBanner();
                    break;
                }
                case "pause":
                    yield return Begin(0);
                    director.PlacePlayer(new Vec2(-12f, -26f), 330f);
                    yield return Advance(0.3f);
                    director.Pause();
                    yield return Wait(0.6f);
                    break;
                case "clear":
                {
                    yield return Begin(0);
                    Stage stage = director.Run.Stage;
                    TakeSome(stage, PickupKind.Orb, 19);
                    TakeSome(stage, PickupKind.Page, 1);
                    TakeSome(stage, PickupKind.Gem, 3);
                    director.Run.Tick(1f / 60f, InputFrame.None);
                    director.PlacePlayer(stage.Level.Portal, 0f);
                    director.Run.Tick(1f / 60f, InputFrame.None);
                    yield return Wait(2.8f);
                    break;
                }
                case "gameover":
                {
                    yield return Begin(1);
                    Stage stage = director.Run.Stage;
                    TakeSome(stage, PickupKind.Orb, 9);
                    for (int i = 0; i < Tuning.StartingLives; i++)
                    {
                        stage.Player.HurtTimer = 0f;
                        stage.HurtPlayer(1000, stage.Player.Position);
                        float t = 0f;
                        while (t < Tuning.DyingTime + 0.2f && director.Run.Outcome == RunOutcome.Playing)
                        {
                            director.Run.Tick(1f / 60f, InputFrame.None);
                            t += 1f / 60f;
                        }
                    }
                    yield return Wait(2f);
                    break;
                }
                case "victory":
                {
                    director.Records.Submit(2840);
                    yield return Begin(3);
                    Stage stage = director.Run.Stage;
                    for (int i = 0; i < 60 * 101; i++)
                    {
                        stage.Player.HurtTimer = 1000f;
                        director.Run.Tick(1f / 60f, InputFrame.None);
                    }
                    director.PlacePlayer(stage.Level.Portal, 0f);
                    director.Run.Tick(1f / 60f, InputFrame.None);
                    yield return Wait(3.5f);
                    break;
                }
                default:
                    Debug.LogWarning($"Unknown screenshot scenario '{scenario}'.");
                    break;
            }
        }

        /// <summary>Starts a chapter straight away, with nothing pressed.</summary>
        IEnumerator Begin(int chapter)
        {
            director.ToTitle();
            director.StartChapter(chapter);
            director.BeginChapter();
            director.ScriptedFrame = InputFrame.None;
            director.Hud.HideBanner();
            yield return Wait(0.3f);
            director.Hud.HideBanner();
        }

        /// <summary>Marks the nearest few pickups of a kind as found, as if the player had walked there.</summary>
        static void TakeSome(Stage stage, PickupKind kind, int count)
        {
            foreach (Pickup p in stage.Pickups)
            {
                if (count == 0) break;
                if (p.Kind != kind || p.Taken) continue;
                Vec2 keep = stage.Player.Position;
                stage.Player.Position = p.Position;
                stage.Tick(1f / 60f, InputFrame.None);
                stage.Player.Position = keep;
                count--;
            }
        }

        static void WakeNear(Stage stage, Vec2 point, int count)
        {
            foreach (Enemy e in stage.Enemies)
            {
                if (count-- <= 0) break;
                e.Position = point;
            }
        }

        IEnumerator Advance(float seconds)
        {
            director.Frozen = false;
            yield return Wait(seconds);
            director.Frozen = true;
            yield return Wait(0.15f);
        }

        static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

        void RenderOffscreen()
        {
            sceneTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Camera.main.targetTexture = sceneTarget;
            var document = director.Hud.GetComponent<UIDocument>();
            PanelSettings panel = Instantiate(document.panelSettings);
            panel.targetTexture = uiTarget;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            document.panelSettings = panel;
        }

        Texture2D CaptureOffscreen()
        {
            Color[] scene = Read(sceneTarget);
            Color[] ui = Read(uiTarget);
            for (int i = 0; i < scene.Length; i++)
            {
                Color over = ui[i];
                Color result = Color.Lerp(scene[i], new Color(over.r, over.g, over.b), over.a);
                result.a = 1f;
                scene[i] = result;
            }
            var shot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            shot.SetPixels(scene);
            shot.Apply();
            return shot;
        }

        static Color[] Read(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Color[] pixels = texture.GetPixels();
            Destroy(texture);
            return pixels;
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
