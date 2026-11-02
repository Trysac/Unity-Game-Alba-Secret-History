using System.Collections.Generic;
using Alba.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alba.Game
{
    /// <summary>
    /// Builds a chapter's scenery from its <see cref="Level"/> (ground, trees, rocks, standing stones,
    /// the keep, lanterns, the portal and every pickup) and keeps creatures, pickups, lanterns and the
    /// portal in step with the <see cref="Stage"/>. Static scenery is merged into one mesh per material.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        sealed class LanternView
        {
            public Lantern Lantern;
            public Material Glass;
            public Light Light;
        }

        GameArt art;
        Looks looks;
        Transform worldRoot;
        Level level;
        Transform scenery;
        Transform props;
        Transform creatures;
        Light moon;
        readonly Dictionary<int, CreatureView> creatureViews = new Dictionary<int, CreatureView>();
        readonly Dictionary<int, Transform> pickupViews = new Dictionary<int, Transform>();
        readonly List<LanternView> lanterns = new List<LanternView>();
        Transform portal;
        Transform portalHeart;
        Material portalMaterial;
        Light portalLight;
        ParticleSystem portalSwirl;
        float portalOpen;
        Mesh orbMesh, gemMesh, crateMesh, bandMesh, bottleMesh, neckMesh, pageMesh;

        public Level Level => level;
        public Looks Looks => looks;

        public void Init(GameArt gameArt, Light moonLight)
        {
            art = gameArt;
            moon = moonLight;
            looks = new Looks(art);
            worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform, false);
            orbMesh = MeshFactory.Ball(1, 0f, 1, Vector3.one * 0.2f, "Orb");
            gemMesh = MeshFactory.Octahedron(0.22f, 0.42f);
            crateMesh = MeshFactory.Box(new Vector3(0.5f, 0.28f, 0.34f), "Ammo box");
            bandMesh = MeshFactory.Box(new Vector3(0.52f, 0.06f, 0.36f), "Band");
            bottleMesh = MeshFactory.Ball(1, 0f, 1, new Vector3(0.16f, 0.2f, 0.16f), "Bottle");
            neckMesh = MeshFactory.Cylinder(6, 0.05f, 0.05f, 0.16f, true, "Neck");
            pageMesh = MeshFactory.Box(new Vector3(0.32f, 0.42f, 0.01f), "Page");
        }

        public float Height(Vec2 p) => level != null ? level.HeightAt(p) : 0f;

        public Vector3 Ground(Vec2 p, float lift = 0f) => new Vector3(p.X, Height(p) + lift, p.Z);

        // ---- Building ----

        public void Build(Stage stage)
        {
            Clear();
            level = stage.Level;
            scenery = new GameObject("Scenery").transform;
            scenery.SetParent(worldRoot, false);
            props = new GameObject("Props").transform;
            props.SetParent(worldRoot, false);
            creatures = new GameObject("Creatures").transform;
            creatures.SetParent(worldRoot, false);

            var ground = new GameObject("Ground");
            ground.transform.SetParent(scenery, false);
            ground.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Ground(level, 1f);
            var groundRenderer = ground.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = looks.Lit(Color.white, TextureFactory.Ground(level, 512), 0.05f);
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;

            BuildObstacles();
            foreach (Lantern lantern in stage.Lanterns) BuildLantern(lantern);
            BuildPortal();
            foreach (Pickup pickup in stage.Pickups) BuildPickup(pickup);
            portalOpen = stage.PortalOpen ? 1f : 0f;
        }

        public void Clear()
        {
            // Only the chapter's own objects: the effects and audio live on the same game object.
            foreach (Transform child in worldRoot) Destroy(child.gameObject);
            creatureViews.Clear();
            pickupViews.Clear();
            lanterns.Clear();
            level = null;
        }

        void BuildObstacles()
        {
            var batches = new Dictionary<Material, List<CombineInstance>>();
            void Add(Material material, Mesh mesh, Matrix4x4 matrix)
            {
                if (!batches.TryGetValue(material, out List<CombineInstance> list)) batches[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = matrix });
            }

            Mesh trunk = MeshFactory.Cylinder(6, 1f, 0.55f, 1f, false, "Trunk");
            Mesh cone = MeshFactory.Cylinder(7, 1f, 0f, 1f, true, "Cone");
            Mesh branch = MeshFactory.Cylinder(5, 1f, 0.3f, 1f, false, "Branch");
            var canopies = new Mesh[4];
            for (int i = 0; i < canopies.Length; i++) canopies[i] = MeshFactory.Ball(1, 0.35f, 30 + i, Vector3.one, "Canopy");
            var rocks = new Mesh[6];
            for (int i = 0; i < rocks.Length; i++) rocks[i] = MeshFactory.Rock(40 + i);
            Mesh unitBox = MeshFactory.Box(Vector3.one, "Block");

            foreach (Obstacle o in level.Obstacles)
            {
                float y = level.HeightAt(o.Center);
                var at = new Vector3(o.Center.X, y, o.Center.Z);
                Quaternion turn = Quaternion.Euler(0f, o.Yaw, 0f);
                var rng = new Rng(o.Variant + 1);
                switch (o.Kind)
                {
                    case ObstacleKind.Pine:
                    {
                        float h = o.Height;
                        Add(looks.Bark, trunk, Matrix4x4.TRS(at - Vector3.up * 0.3f, turn, new Vector3(o.Radius, h * 0.45f, o.Radius)));
                        for (int layer = 0; layer < 4; layer++)
                        {
                            float t = layer / 3f;
                            float radius = Mathf.Lerp(h * 0.27f, h * 0.1f, t) * rng.Range(0.85f, 1.1f);
                            Add(looks.Needles, cone, Matrix4x4.TRS(at + Vector3.up * (h * (0.22f + t * 0.5f)), Quaternion.Euler(0f, o.Yaw + layer * 40f, 0f),
                                new Vector3(radius, h * 0.36f, radius)));
                        }
                        break;
                    }
                    case ObstacleKind.Oak:
                    {
                        float h = o.Height;
                        Add(looks.Bark, trunk, Matrix4x4.TRS(at - Vector3.up * 0.3f, turn, new Vector3(o.Radius, h * 0.6f, o.Radius)));
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 offset = Quaternion.Euler(0f, o.Yaw + k * 95f, 0f) * new Vector3(h * 0.18f, 0f, 0f);
                            float size = h * rng.Range(0.2f, 0.3f);
                            Add(looks.Leaves, canopies[(o.Variant + k) % canopies.Length],
                                Matrix4x4.TRS(at + Vector3.up * (h * rng.Range(0.6f, 0.8f)) + (k == 3 ? Vector3.up * h * 0.12f : offset), turn, new Vector3(size, size * 0.8f, size)));
                        }
                        break;
                    }
                    case ObstacleKind.DeadTree:
                    {
                        float h = o.Height;
                        Add(looks.Bark, trunk, Matrix4x4.TRS(at - Vector3.up * 0.3f, turn, new Vector3(o.Radius, h, o.Radius)));
                        for (int k = 0; k < 4; k++)
                        {
                            float height = h * rng.Range(0.45f, 0.85f);
                            Quaternion angle = Quaternion.Euler(0f, o.Yaw + k * 90f + rng.Range(-30f, 30f), rng.Range(35f, 65f));
                            float length = h * rng.Range(0.25f, 0.4f);
                            Add(looks.Bark, branch, Matrix4x4.TRS(at + Vector3.up * height, angle, new Vector3(o.Radius * 0.35f, length, o.Radius * 0.35f)));
                        }
                        break;
                    }
                    case ObstacleKind.Rock:
                        Add(looks.Stone, rocks[o.Variant % rocks.Length], Matrix4x4.TRS(at - Vector3.up * o.Radius * 0.25f, turn,
                            new Vector3(o.Radius * 1.15f, o.Height, o.Radius * 1.15f)));
                        break;
                    case ObstacleKind.StandingStone:
                        Add(looks.StandingStone, MeshFactory.StandingStone(o.Variant, o.Height), Matrix4x4.TRS(at - Vector3.up * 0.2f, turn, Vector3.one));
                        break;
                    case ObstacleKind.Wall:
                    {
                        var size = new Vector3(o.HalfSize.X * 2f, o.Height, o.HalfSize.Z * 2f);
                        float baseY = Mathf.Min(level.HeightAt(o.Center.X - o.HalfSize.X, o.Center.Z - o.HalfSize.Z), level.HeightAt(o.Center.X + o.HalfSize.X, o.Center.Z + o.HalfSize.Z), y) - 0.5f;
                        var foot = new Vector3(o.Center.X, baseY, o.Center.Z);
                        Add(looks.Wall, MeshFactory.Box(size + Vector3.up * 0.5f, "Wall"), Matrix4x4.TRS(foot, Quaternion.identity, Vector3.one));
                        bool alongX = o.HalfSize.X > o.HalfSize.Z;
                        float length = alongX ? size.x : size.z;
                        for (float s = -length / 2f + 0.5f; s < length / 2f - 0.3f; s += 1.3f)
                        {
                            Vector3 offset = alongX ? new Vector3(s, 0f, 0f) : new Vector3(0f, 0f, s);
                            Add(looks.Wall, unitBox, Matrix4x4.TRS(foot + offset + Vector3.up * (size.y + 0.5f), Quaternion.identity, new Vector3(alongX ? 0.7f : size.x, 0.6f, alongX ? size.z : 0.7f)));
                        }
                        break;
                    }
                    case ObstacleKind.Tower:
                    {
                        var foot = at - Vector3.up * 0.6f;
                        Add(looks.Wall, MeshFactory.Cylinder(12, o.Radius, o.Radius * 0.92f, o.Height + 0.6f, true, "Tower"), Matrix4x4.TRS(foot, Quaternion.identity, Vector3.one));
                        int crenels = Mathf.RoundToInt(o.Radius * 4f);
                        for (int k = 0; k < crenels; k++)
                        {
                            Quaternion around = Quaternion.Euler(0f, k * 360f / crenels, 0f);
                            Add(looks.Wall, unitBox, Matrix4x4.TRS(foot + Vector3.up * (o.Height + 0.6f) + around * new Vector3(0f, 0f, o.Radius * 0.85f), around, new Vector3(0.6f, 0.7f, 0.4f)));
                        }
                        break;
                    }
                }
            }

            foreach (KeyValuePair<Material, List<CombineInstance>> batch in batches)
            {
                var mesh = new Mesh { name = "Scenery " + batch.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(batch.Value.ToArray(), true, true);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(scenery, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = batch.Key;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 euler, Vector3 scale, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return go.transform;
        }

        void BuildLantern(Lantern lantern)
        {
            Transform root = Part(props, "Lantern", null, null, Ground(lantern.Position), Vector3.zero, Vector3.one);
            Part(root, "Post", MeshFactory.Cylinder(6, 0.07f, 0.05f, 1.8f), looks.Iron, Vector3.zero, Vector3.zero, Vector3.one, true);
            Part(root, "Foot", MeshFactory.Cylinder(8, 0.3f, 0.18f, 0.25f), looks.Iron, Vector3.zero, Vector3.zero, Vector3.one, true);
            Part(root, "Cap", MeshFactory.Cylinder(4, 0.22f, 0f, 0.25f), looks.Iron, new Vector3(0f, 2.35f, 0f), new Vector3(0f, 45f, 0f), Vector3.one);
            Material glass = looks.OwnGlowing(Palette.LanternGlass * 0.5f, Palette.LanternGlass * 0.15f);
            Part(root, "Glass", MeshFactory.Cylinder(6, 0.16f, 0.13f, 0.55f), glass, new Vector3(0f, 1.8f, 0f), Vector3.zero, Vector3.one);
            var lightObject = new GameObject("Flame");
            lightObject.transform.SetParent(worldRoot, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Palette.LanternLight;
            light.range = 10f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            lanterns.Add(new LanternView { Lantern = lantern, Glass = glass, Light = light });
        }

        void BuildPortal()
        {
            Vec2 toStart = level.Start - level.Portal;
            portal = Part(props, "Portal", null, null, Ground(level.Portal), new Vector3(0f, toStart.Yaw, 0f), Vector3.one);
            Part(portal, "Ring", MeshFactory.Torus(1.7f, 0.28f, 18, 6), looks.StandingStone, new Vector3(0f, 2.0f, 0f), Vector3.zero, Vector3.one, true);
            Part(portal, "Plinth", MeshFactory.Box(new Vector3(3.6f, 0.3f, 1.2f)), looks.StandingStone, new Vector3(0f, -0.1f, 0f), Vector3.zero, Vector3.one, true);
            portalMaterial = looks.Additive(new Color(0.2f, 0.6f, 0.9f, 0f));
            portalHeart = Part(portal, "Heart", MeshFactory.Disc(1.5f, 24), portalMaterial, new Vector3(0f, 2.0f, 0f), Vector3.zero, Vector3.one);
            portalHeart.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = art.SoftDot;

            var swirl = new GameObject("Swirl");
            swirl.transform.SetParent(portal, false);
            swirl.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            portalSwirl = swirl.AddComponent<ParticleSystem>();
            portalSwirl.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = portalSwirl.main;
            main.startLifetime = 1.4f;
            main.startSpeed = 0.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(Palette.Portal, Palette.PortalDeep);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 300;
            ParticleSystem.EmissionModule emission = portalSwirl.emission;
            emission.rateOverTime = 90f;
            ParticleSystem.ShapeModule shape = portalSwirl.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.45f;
            shape.radiusThickness = 0.2f;
            ParticleSystem.VelocityOverLifetimeModule spin = portalSwirl.velocityOverLifetime;
            spin.enabled = true;
            spin.space = ParticleSystemSimulationSpace.Local;
            spin.orbitalZ = 1.6f;
            spin.radial = -0.5f;
            spin.x = 0f;
            spin.y = 0f;
            spin.z = 0f;
            swirl.GetComponent<ParticleSystemRenderer>().sharedMaterial = art.Glow;

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(portal, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.0f, 0.6f);
            portalLight = lightObject.AddComponent<Light>();
            portalLight.type = LightType.Point;
            portalLight.color = Palette.Portal;
            portalLight.range = 12f;
            portalLight.intensity = 0f;
            portalLight.shadows = LightShadows.None;
        }

        void BuildPickup(Pickup pickup)
        {
            Transform root = Part(props, pickup.Kind.ToString(), null, null, Ground(pickup.Position), Vector3.zero, Vector3.one);
            switch (pickup.Kind)
            {
                case PickupKind.Orb:
                    Part(root, "Orb", orbMesh, looks.Glowing(Palette.Orb * 0.8f, Palette.Orb * 0.75f), new Vector3(0f, 0.9f, 0f), Vector3.zero, Vector3.one);
                    break;
                case PickupKind.Gem:
                {
                    Part(root, "Gem", gemMesh, looks.Glowing(Palette.Gem * 0.8f, Palette.Gem * 0.9f), new Vector3(0f, 1.1f, 0f), Vector3.zero, Vector3.one);
                    var light = new GameObject("Glow").AddComponent<Light>();
                    light.transform.SetParent(worldRoot, false);
                    light.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                    light.type = LightType.Point;
                    light.color = Palette.Gem;
                    light.range = 6f;
                    light.intensity = 1.6f;
                    light.shadows = LightShadows.None;
                    break;
                }
                case PickupKind.Potion:
                    Part(root, "Bottle", bottleMesh, looks.Glowing(Palette.Potion, Palette.Potion * 0.8f), new Vector3(0f, 0.55f, 0f), Vector3.zero, Vector3.one);
                    Part(root, "Neck", neckMesh, looks.Glowing(Palette.Potion, Palette.Potion * 0.5f), new Vector3(0f, 0.7f, 0f), Vector3.zero, Vector3.one);
                    Part(root, "Cork", neckMesh, looks.Wood, new Vector3(0f, 0.86f, 0f), Vector3.zero, new Vector3(1.2f, 0.4f, 1.2f));
                    break;
                case PickupKind.Page:
                    Part(root, "Page", pageMesh, looks.Parchment, new Vector3(0f, 1.0f, 0f), new Vector3(10f, 0f, 8f), Vector3.one);
                    break;
                default:
                {
                    Color band = pickup.Kind == PickupKind.PistolAmmo ? Palette.Hex("#e0b040") : pickup.Kind == PickupKind.ShotgunAmmo ? Palette.Hex("#d04030") : Palette.Hex("#50c060");
                    Part(root, "Box", crateMesh, looks.Wood, new Vector3(0f, 0.05f, 0f), new Vector3(0f, (pickup.Id * 47) % 360, 0f), Vector3.one, true);
                    Part(root, "Band", bandMesh, looks.Glowing(band, band * 0.9f), new Vector3(0f, 0.15f, 0f), new Vector3(0f, (pickup.Id * 47) % 360, 0f), Vector3.one);
                    break;
                }
            }
            pickupViews[pickup.Id] = root;
        }

        // ---- Per frame ----

        /// <summary>Brings creatures, pickups, lanterns and the portal in line with the stage.</summary>
        public void Sync(Stage stage, float dt, float time)
        {
            if (level == null) return;
            foreach (Enemy enemy in stage.Enemies)
            {
                if (!creatureViews.TryGetValue(enemy.Id, out CreatureView view))
                {
                    if (!enemy.Alive) continue;
                    view = CreatureView.Create(enemy, looks, art, creatures);
                    creatureViews[enemy.Id] = view;
                }
                if (view == null) continue;
                view.Sync(level.HeightAt(enemy.Position), dt, time);
                if (view.Gone)
                {
                    Destroy(view.gameObject);
                    creatureViews[enemy.Id] = null;
                }
            }

            foreach (Pickup pickup in stage.Pickups)
            {
                if (!pickupViews.TryGetValue(pickup.Id, out Transform view)) BuildPickup(pickup);
                view = pickupViews[pickup.Id];
                if (view == null) continue;
                if (pickup.Taken)
                {
                    Destroy(view.gameObject);
                    pickupViews[pickup.Id] = null;
                    continue;
                }
                if (view.childCount > 0)
                {
                    Transform item = view.GetChild(0);
                    bool spins = pickup.Kind == PickupKind.Orb || pickup.Kind == PickupKind.Gem || pickup.Kind == PickupKind.Page;
                    if (spins) item.localRotation = Quaternion.Euler(pickup.Kind == PickupKind.Page ? 10f : 0f, time * 70f + pickup.Id * 30f, 0f);
                    float bob = pickup.Kind == PickupKind.Orb || pickup.Kind == PickupKind.Gem || pickup.Kind == PickupKind.Page ? Mathf.Sin(time * 2f + pickup.Id) * 0.12f : 0f;
                    view.position = Ground(pickup.Position, bob);
                }
            }

            foreach (LanternView lantern in lanterns)
            {
                if (!lantern.Lantern.Lit) continue;
                if (!lantern.Light.enabled)
                {
                    lantern.Light.enabled = true;
                    Looks.SetColor(lantern.Glass, Palette.LanternGlass);
                }
                float flicker = 1.8f + Mathf.PerlinNoise(time * 6f, lantern.Lantern.Index * 3.1f) * 0.6f;
                lantern.Light.intensity = flicker;
                Looks.SetEmission(lantern.Glass, Palette.LanternGlass * (0.9f + flicker * 0.25f));
            }

            portalOpen = Mathf.MoveTowards(portalOpen, stage.PortalOpen ? 1f : 0f, dt * 0.6f);
            Looks.SetTint(portalMaterial, new Color(0.15f, 0.55f, 0.8f, 0.55f * portalOpen * (0.85f + 0.15f * Mathf.Sin(time * 3f))));
            portalHeart.localRotation = Quaternion.Euler(0f, 0f, time * 40f);
            portalHeart.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f + 0.04f * Mathf.Sin(time * 2.3f), portalOpen);
            portalLight.intensity = portalOpen * (2.2f + 0.4f * Mathf.Sin(time * 4f));
            if (portalOpen > 0.05f && !portalSwirl.isPlaying) portalSwirl.Play();
            if (portalOpen <= 0.05f && portalSwirl.isPlaying) portalSwirl.Stop();
        }

        public CreatureView ViewOf(Enemy enemy) => enemy != null && creatureViews.TryGetValue(enemy.Id, out CreatureView view) ? view : null;

        // ---- Mood ----

        /// <summary>Fog, ambient light and moonlight for the chapter; <paramref name="dawn"/> (0..1) warms the last chapter into sunrise.</summary>
        public void ApplyMood(ChapterDef chapter, Camera camera, int brightness, float dawn)
        {
            float lift = 1f + brightness * 0.32f;
            Color fog = Color.Lerp(Palette.Hex(chapter.FogColor), Palette.DawnFog, dawn);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Lerp(chapter.FogDensity, chapter.FogDensity * 0.6f, dawn);
            RenderSettings.fogColor = fog;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(Palette.Hex(chapter.AmbientColor), Palette.DawnAmbient, dawn) * lift;
            if (camera != null) camera.backgroundColor = fog;
            if (moon != null)
            {
                moon.color = Color.Lerp(Palette.Hex(chapter.MoonColor), Palette.DawnSun, dawn);
                moon.intensity = Mathf.Lerp(chapter.MoonIntensity, 1.1f, dawn) * Mathf.Lerp(1f, 1.5f, brightness / 5f);
                moon.transform.rotation = Quaternion.Euler(Mathf.Lerp(38f, 8f, dawn), Mathf.Lerp(210f, 180f, dawn), 0f);
            }
        }
    }
}
