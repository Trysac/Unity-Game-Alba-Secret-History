using System;
using System.IO;
using Alba.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Alba.EditorTools
{
    /// <summary>
    /// Generates the materials, font atlases, art asset, icon, scene, quality and player settings from
    /// code, and builds the players, so every binary asset can be recreated from text. Batch mode:
    /// <code>
    /// Unity -batchmode -quit -projectPath . -executeMethod Alba.EditorTools.ProjectBuilder.SetUpProject
    /// Unity -batchmode -quit -projectPath . -executeMethod Alba.EditorTools.ProjectBuilder.BuildWindows -buildOutput &lt;folder&gt;
    /// Unity -batchmode -quit -projectPath . -executeMethod Alba.EditorTools.ProjectBuilder.BuildWebGL -buildOutput &lt;folder&gt;
    /// Unity -batchmode -projectPath . -executeMethod Alba.EditorTools.ProjectBuilder.CaptureScreenshots -screenshot &lt;folder&gt;
    /// </code>
    /// Run <c>python tools/audio/build.py</c> first to regenerate the sounds.
    /// </summary>
    public static class ProjectBuilder
    {
        const string ScenePath = "Assets/Scenes/Alba.unity";
        const string ArtFolder = "Assets/Art";
        const string ArtPath = "Assets/Art/GameArt.asset";
        const string SoftDotPath = "Assets/Art/SoftDot.png";
        const string IconPath = "Assets/Art/AppIcon.png";
        const string AudioFolder = "Assets/Audio";
        const string DisplayFontSource = "Assets/Fonts/Exo2-Black.ttf";
        const string DisplayFontPath = "Assets/UI/ExoBlack.asset";
        const string TextFontSource = "Assets/Fonts/Exo2-SemiBold.ttf";
        const string TextFontPath = "Assets/UI/ExoSemiBold.asset";
        const string StylePath = "Assets/UI/Hud.uss";
        const string ThemePath = "Assets/UI/HudTheme.tss";
        const string PanelSettingsPath = "Assets/UI/HudPanelSettings.asset";
        const string Ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        [UnityEditor.MenuItem("Alba/Regenerate Art, Scene and Player Settings")]
        public static void SetUpProject()
        {
            AssetDatabase.Refresh();
            CreateFont(DisplayFontSource, DisplayFontPath, "ExoBlack");
            CreateFont(TextFontSource, TextFontPath, "ExoSemiBold");
            // The style sheet refers to the fonts by path; re-import it so it picks up the new assets.
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate);
            GameArt art = CreateArt();
            PanelSettings panel = CreatePanelSettings();
            CreateScene(art, panel);
            ConfigureQuality();
            ConfigurePlayer(art.Icon);
            AssetDatabase.SaveAssets();
            Debug.Log("Alba project set up.");
        }

        /// <summary>Enters play mode so ScreenshotScenarios captures the README screenshots, then exits. Run without -quit.</summary>
        public static void CaptureScreenshots()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Alba.exe");

        public static void BuildWebGL() => Build(BuildTarget.WebGL, string.Empty);

        // ---- Fonts ----

        /// <summary>A static font atlas of the printable ASCII range (the Exo 2 font is used unmodified under the SIL OFL).</summary>
        static FontAsset CreateFont(string sourcePath, string assetPath, string name)
        {
            if (AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            FontAsset font = FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            font.name = name;
            if (!font.TryAddCharacters(Ascii, out string missing)) Debug.LogWarning($"{name} is missing: {missing}");
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, assetPath);
            foreach (Texture2D atlas in font.atlasTextures)
            {
                atlas.name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            return font;
        }

        // ---- Art ----

        static GameArt CreateArt()
        {
            Directory.CreateDirectory(ArtFolder);
            var art = AssetDatabase.LoadAssetAtPath<GameArt>(ArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<GameArt>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }

            Shader standard = Shader.Find("Standard");
            art.Lit = MaterialAt("Lit", standard);
            art.Lit.enableInstancing = true;
            art.Lit.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(art.Lit);
            art.Glowing = MaterialAt("Glowing", standard);
            art.Glowing.enableInstancing = true;
            art.Glowing.EnableKeyword("_EMISSION");
            art.Glowing.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            art.Glowing.SetColor("_EmissionColor", Color.white);
            EditorUtility.SetDirty(art.Glowing);

            art.SoftDot = CreateSoftDot();
            art.Glow = MaterialAt("Glow", Shader.Find("Legacy Shaders/Particles/Additive"));
            art.Glow.mainTexture = art.SoftDot;
            art.Glow.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(art.Glow);
            art.Puff = MaterialAt("Puff", Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            art.Puff.mainTexture = art.SoftDot;
            art.Puff.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(art.Puff);
            art.Icon = CreateIcon();

            art.TitleMusic = Clip("title_theme", music: true);
            art.NightMusic = Clip("night_theme", music: true);
            art.KeepMusic = Clip("keep_theme", music: true);
            art.DawnMusic = Clip("dawn_theme", music: true);
            art.Rain = Clip("rain", music: true);
            art.Wind = Clip("wind", music: true);
            art.Pistol = Clip("pistol");
            art.Shotgun = Clip("shotgun");
            art.Rifle = Clip("rifle");
            art.Dirk = Clip("dirk");
            art.DryFire = Clip("dry_fire");
            art.Hit = Clip("hit");
            art.Ricochet = Clip("ricochet");
            art.Immune = Clip("immune");
            art.CrawlerAlert = Clip("crawler_alert");
            art.CrawlerWindup = Clip("crawler_windup");
            art.CrawlerDie = Clip("crawler_die");
            art.WispAlert = Clip("wisp_alert");
            art.WispDie = Clip("wisp_die");
            art.BruteRoar = Clip("brute_roar");
            art.BruteWindup = Clip("brute_windup");
            art.BruteSlam = Clip("brute_slam");
            art.BruteDie = Clip("brute_die");
            art.Hurt = Clip("hurt");
            art.Heartbeat = Clip("heartbeat");
            art.Death = Clip("death");
            art.Potion = Clip("potion");
            art.Step = Clip("step");
            art.Torch = Clip("torch");
            art.TorchEmpty = Clip("torch_empty");
            art.Orb = Clip("orb");
            art.Gem = Clip("gem");
            art.Page = Clip("page");
            art.Ammo = Clip("ammo");
            art.Lantern = Clip("lantern");
            art.PortalOpen = Clip("portal_open");
            art.Escape = Clip("escape");
            art.Wave = Clip("wave");
            art.Dawn = Clip("dawn");
            art.Move = Clip("move");
            art.Select = Clip("select");
            art.Pause = Clip("pause");
            art.GameOver = Clip("game_over");
            art.Victory = Clip("victory");
            EditorUtility.SetDirty(art);
            return art;
        }

        static Material MaterialAt(string name, Shader shader)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        /// <summary>A small white disc with a soft edge for every particle and glow.</summary>
        static Texture2D CreateSoftDot()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha * (3f - 2f * alpha)));
                }
            }
            texture.Apply();
            File.WriteAllBytes(SoftDotPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SoftDotPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SoftDotPath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
        }

        static AudioClip Clip(string name, bool music = false)
        {
            string path = $"{AudioFolder}/{name}.ogg";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogWarning($"Missing {path}: run python tools/audio/build.py first.");
                return null;
            }
            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            sample.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = sample;
            importer.forceToMono = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ---- UI ----

        static PanelSettings CreatePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // ---- Scene ----

        static void CreateScene(GameArt art, PanelSettings panel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Fog is on in the saved scene so the build keeps the fog shader variants the chapters use.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = new Color(0.23f, 0.25f, 0.31f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.22f);
            RenderSettings.skybox = null;

            var moonObject = new GameObject("Moon");
            moonObject.transform.rotation = Quaternion.Euler(38f, 210f, 0f);
            var moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.61f, 0.78f);
            moon.intensity = 0.45f;
            moon.shadows = LightShadows.None;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 140f;
            camera.fieldOfView = 70f;
            cameraObject.AddComponent<AudioListener>();
            CameraRig rig = cameraObject.AddComponent<CameraRig>();

            var ui = new GameObject("HUD");
            var document = ui.AddComponent<UIDocument>();
            document.panelSettings = panel;
            Hud hud = ui.AddComponent<Hud>();

            var game = new GameObject("Game");
            WorldView world = game.AddComponent<WorldView>();
            Effects effects = game.AddComponent<Effects>();
            AudioDirector audio = game.AddComponent<AudioDirector>();
            var director = game.AddComponent<GameDirector>();
            var serialized = new SerializedObject(director);
            serialized.FindProperty("art").objectReferenceValue = art;
            serialized.FindProperty("hud").objectReferenceValue = hud;
            serialized.FindProperty("rig").objectReferenceValue = rig;
            serialized.FindProperty("world").objectReferenceValue = world;
            serialized.FindProperty("effects").objectReferenceValue = effects;
            serialized.FindProperty("audioDirector").objectReferenceValue = audio;
            serialized.FindProperty("moon").objectReferenceValue = moon;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Enough per-pixel lights for the torch, lanterns, sealstones and portal at every quality level.</summary>
        static void ConfigureQuality()
        {
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 6);
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = 35f;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void ConfigurePlayer(Texture2D icon)
        {
            PlayerSettings.companyName = "Trysac";
            PlayerSettings.productName = "Alba: Secret History";
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var standaloneIcons = new Texture2D[sizes.Length];
            Array.Fill(standaloneIcons, icon);
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, standaloneIcons, IconKind.Any);
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            // Static hosts such as GitHub Pages do not send Content-Encoding for .br/.gz files.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
        }

        // ---- Icon: three standing stones before a blood moon ----

        static Texture2D CreateIcon()
        {
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = IconPixel(x + 0.5f - size / 2f, y + 0.5f - size / 2f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        static Color IconPixel(float x, float y)
        {
            const float aa = 1.5f;
            float tile = RoundedBox(x, y, 470f, 470f, 150f);
            float t = Mathf.Clamp01((y + 470f) / 940f);
            Color color = Color.Lerp(Hex(0x1a1220), Hex(0x2a1c3c), t);
            color.a = Coverage(-tile, aa);

            // The blood moon with its halo.
            float moon = Length(x - 40f, y - 150f) - 230f;
            color = Over(color, Hex(0xc0202a), Mathf.Clamp01(1f - moon / 220f) * 0.45f * color.a);
            color = Over(color, Hex(0xd8343a), Coverage(-moon, aa) * color.a);
            color = Over(color, Hex(0xe8584a), Coverage(-(Length(x + 20f, y - 210f) - 70f), aa) * 0.35f * color.a);

            // The moor and three stones in silhouette.
            float hill = y - (-250f + 40f * Mathf.Sin(x * 0.008f + 0.6f));
            Color ink = Hex(0x0c080c);
            color = Over(color, ink, Coverage(-hill, aa) * color.a);
            color = Over(color, ink, Coverage(-Stone(x + 250f, y + 240f, 70f, 330f, 0.12f), aa) * color.a);
            color = Over(color, ink, Coverage(-Stone(x - 10f, y + 240f, 85f, 470f, -0.05f), aa) * color.a);
            color = Over(color, ink, Coverage(-Stone(x - 270f, y + 240f, 65f, 290f, -0.15f), aa) * color.a);

            // A sealstone glowing at the foot of the tallest stone.
            float gem = Mathf.Abs(x - 10f) / 55f + Mathf.Abs(y + 300f) / 90f - 1f;
            color = Over(color, Hex(0xc86bff), Mathf.Clamp01(1f - gem / 1.2f) * 0.5f * color.a);
            color = Over(color, Hex(0xd48aff), Coverage(-gem * 50f, aa) * color.a);
            return color;
        }

        /// <summary>A rough standing stone: a box that narrows and leans towards the top.</summary>
        static float Stone(float x, float y, float halfWidth, float height, float lean)
        {
            float rise = Mathf.Clamp01(y / height);
            float width = halfWidth * (1f - 0.35f * rise);
            float shifted = x - lean * y;
            float sides = Mathf.Abs(shifted) - width;
            float top = y - height + Mathf.Abs(shifted) * 0.4f;
            return Mathf.Max(sides, Mathf.Max(top, -y - 400f));
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float RoundedBox(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(x) - (halfWidth - radius);
            float qy = Mathf.Abs(y) - (halfHeight - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        static float Coverage(float inside, float width) => Mathf.Clamp01(inside / width + 0.5f);

        static Color Over(Color below, Color above, float amount)
        {
            Color mixed = Color.Lerp(below, above, amount);
            mixed.a = below.a;
            return mixed;
        }

        // ---- Builds ----

        static void Build(BuildTarget target, string fileName)
        {
            string folder = Argument("-buildOutput");
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("Pass -buildOutput <folder> outside the repository.");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = target,
                locationPathName = string.IsNullOrEmpty(fileName) ? folder : Path.Combine(folder, fileName),
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"Build {target}: {summary.result}, {summary.totalSize / (1024 * 1024f):0.0} MB, {summary.totalErrors} errors.");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
