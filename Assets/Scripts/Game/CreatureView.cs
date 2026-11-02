using System.Collections.Generic;
using Alba.Core;
using UnityEngine;

namespace Alba.Game
{
    /// <summary>
    /// A creature drawn from simple parts and animated in code: a crawler on eight jointed legs,
    /// a wisp of red flame, or Am Famhair, the giant. It mirrors an <see cref="Enemy"/> every frame.
    /// </summary>
    public sealed class CreatureView : MonoBehaviour
    {
        sealed class Leg
        {
            public Transform Hip;
            public Transform Knee;
            public float Phase;
            public float RestYaw;
        }

        Enemy enemy;
        Transform body;
        Transform head;
        readonly List<Leg> legs = new List<Leg>();
        Transform leftArm, rightArm, leftLeg, rightLeg;
        Material eyeMaterial;
        Material skinMaterial;
        ParticleSystem flame;
        Light glow;
        float deadTime;
        float hitFlash;
        float scale = 1f;

        public Enemy Enemy => enemy;
        /// <summary>True once the death animation has finished and the view can be removed.</summary>
        public bool Gone => enemy != null && !enemy.Alive && deadTime > (enemy.Kind == EnemyKind.Brute ? 3f : 1.4f);

        public static CreatureView Create(Enemy enemy, Looks looks, GameArt art, Transform parent)
        {
            var go = new GameObject($"{enemy.Kind} #{enemy.Id}");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CreatureView>();
            view.enemy = enemy;
            switch (enemy.Kind)
            {
                case EnemyKind.Crawler: view.BuildCrawler(looks, enemy.Id); break;
                case EnemyKind.Wisp: view.BuildWisp(looks, art); break;
                default: view.BuildBrute(looks); break;
            }
            return view;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 euler, Vector3 scale)
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
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go.transform;
        }

        static Transform Pivot(Transform parent, string name, Vector3 position) => Part(parent, name, null, null, position, Vector3.zero, Vector3.one);

        // ---- Crawler: the spider-like brood of the moor ----

        void BuildCrawler(Looks looks, int seed)
        {
            scale = 1.1f;
            transform.localScale = Vector3.one * scale;
            skinMaterial = looks.CrawlerSkin;
            eyeMaterial = looks.Eye;
            body = Pivot(transform, "Body", new Vector3(0f, 0.62f, 0f));
            Part(body, "Thorax", MeshFactory.Ball(1, 0.3f, seed, new Vector3(0.55f, 0.38f, 0.7f)), skinMaterial, Vector3.zero, Vector3.zero, Vector3.one);
            Part(body, "Abdomen", MeshFactory.Ball(1, 0.25f, seed + 1, new Vector3(0.45f, 0.42f, 0.55f)), skinMaterial, new Vector3(0f, 0.12f, -0.65f), new Vector3(-12f, 0f, 0f), Vector3.one);
            head = Pivot(body, "Head", new Vector3(0f, 0.05f, 0.62f));
            Part(head, "Skull", MeshFactory.Ball(1, 0.2f, seed + 2, new Vector3(0.3f, 0.24f, 0.32f)), skinMaterial, Vector3.zero, Vector3.zero, Vector3.one);
            Mesh eye = MeshFactory.Ball(0, 0f, 1, Vector3.one * 0.06f);
            Part(head, "Eye L", eye, eyeMaterial, new Vector3(-0.12f, 0.08f, 0.24f), Vector3.zero, Vector3.one);
            Part(head, "Eye R", eye, eyeMaterial, new Vector3(0.12f, 0.08f, 0.24f), Vector3.zero, Vector3.one);
            Part(head, "Eye C", eye, eyeMaterial, new Vector3(0f, 0.15f, 0.22f), Vector3.zero, Vector3.one * 0.7f);
            Mesh fang = MeshFactory.Cylinder(5, 0.05f, 0f, 0.35f, false, "Fang");
            Part(head, "Fang L", fang, looks.Horn, new Vector3(-0.1f, -0.1f, 0.22f), new Vector3(110f, -10f, 0f), Vector3.one);
            Part(head, "Fang R", fang, looks.Horn, new Vector3(0.1f, -0.1f, 0.22f), new Vector3(110f, 10f, 0f), Vector3.one);
            Mesh spike = MeshFactory.Cylinder(5, 0.07f, 0f, 0.4f, false, "Spike");
            for (int i = 0; i < 3; i++) Part(body, "Spike", spike, looks.Horn, new Vector3(0f, 0.32f + i * 0.04f, 0.2f - i * 0.35f), new Vector3(-25f - i * 10f, 0f, 0f), Vector3.one);

            Mesh upper = MeshFactory.Cylinder(5, 0.06f, 0.045f, 0.65f, false, "Upper leg");
            Mesh lower = MeshFactory.Cylinder(5, 0.045f, 0.01f, 1.0f, false, "Lower leg");
            for (int i = 0; i < 8; i++)
            {
                float side = i < 4 ? -1f : 1f;
                int row = i % 4;
                // Each hip turns so its local +X points out from the body, the front pair angled forward.
                float spread = (1.5f - row) * 28f;
                float yaw = side > 0f ? -spread : 180f + spread;
                Transform hip = Pivot(body, "Hip", new Vector3(side * 0.3f, 0f, 0.36f - row * 0.24f));
                hip.localEulerAngles = new Vector3(0f, yaw, 0f);
                // Up and out to the knee, then down to the ground.
                Part(hip, "Upper", upper, skinMaterial, Vector3.zero, new Vector3(0f, 0f, -55f), Vector3.one);
                Transform knee = Pivot(hip, "Knee", new Vector3(0.53f, 0.37f, 0f));
                Part(knee, "Lower", lower, skinMaterial, Vector3.zero, new Vector3(0f, 0f, -200f), Vector3.one);
                legs.Add(new Leg { Hip = hip, Knee = knee, Phase = ((row % 2 == 0) == (side < 0f) ? 0f : Mathf.PI), RestYaw = yaw });
            }
        }

        // ---- Wisp: a drifting red flame that only the torch can wound ----

        void BuildWisp(Looks looks, GameArt art)
        {
            body = Pivot(transform, "Body", new Vector3(0f, 1.3f, 0f));
            eyeMaterial = looks.OwnGlowing(Palette.Wisp, Palette.Wisp * 1.5f);
            Part(body, "Core", MeshFactory.Ball(1, 0.25f, enemy.Id, Vector3.one * 0.22f), eyeMaterial, Vector3.zero, Vector3.zero, Vector3.one);

            var flameObject = new GameObject("Flame");
            flameObject.transform.SetParent(body, false);
            flame = flameObject.AddComponent<ParticleSystem>();
            flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = flame.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = new Color(1f, 0.25f, 0.15f, 0.8f);
            main.gravityModifier = -0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            ParticleSystem.EmissionModule emission = flame.emission;
            emission.rateOverTime = 45f;
            ParticleSystem.ShapeModule shape = flame.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            ParticleSystem.ColorOverLifetimeModule fade = flame.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            flameObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = art.Glow;
            flame.Play();

            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(body, false);
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = Palette.Wisp;
            glow.range = 5f;
            glow.intensity = 1.2f;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.ForceVertex;
        }

        // ---- Am Famhair: the giant of Dunadd ----

        void BuildBrute(Looks looks)
        {
            scale = 1.25f;
            transform.localScale = Vector3.one * scale;
            skinMaterial = new Material(looks.BruteSkin);
            eyeMaterial = looks.OwnGlowing(Palette.Eye, Palette.Eye * 1.5f);
            Mesh limb = MeshFactory.Cylinder(7, 0.22f, 0.17f, 1.05f, true, "Limb");
            leftLeg = Pivot(transform, "Leg L", new Vector3(-0.45f, 1.15f, 0f));
            rightLeg = Pivot(transform, "Leg R", new Vector3(0.45f, 1.15f, 0f));
            foreach (Transform leg in new[] { leftLeg, rightLeg })
            {
                Part(leg, "Thigh", limb, skinMaterial, Vector3.zero, new Vector3(180f, 0f, 0f), new Vector3(1.3f, 1f, 1.3f));
                Part(leg, "Foot", MeshFactory.Ball(1, 0.2f, 3, new Vector3(0.3f, 0.18f, 0.42f)), skinMaterial, new Vector3(0f, -1.05f, 0.12f), Vector3.zero, Vector3.one);
            }
            body = Pivot(transform, "Body", new Vector3(0f, 1.2f, 0f));
            Part(body, "Belly", MeshFactory.Ball(1, 0.15f, 4, new Vector3(0.75f, 0.6f, 0.6f)), skinMaterial, new Vector3(0f, 0.35f, 0f), Vector3.zero, Vector3.one);
            Part(body, "Chest", MeshFactory.Ball(1, 0.18f, 5, new Vector3(1.0f, 0.75f, 0.7f)), skinMaterial, new Vector3(0f, 1.1f, 0.1f), new Vector3(15f, 0f, 0f), Vector3.one);
            head = Pivot(body, "Head", new Vector3(0f, 1.75f, 0.45f));
            Part(head, "Skull", MeshFactory.Ball(1, 0.2f, 6, new Vector3(0.38f, 0.4f, 0.42f)), skinMaterial, Vector3.zero, Vector3.zero, Vector3.one);
            Mesh eye = MeshFactory.Ball(0, 0f, 1, Vector3.one * 0.07f);
            Part(head, "Eye L", eye, eyeMaterial, new Vector3(-0.15f, 0.06f, 0.36f), Vector3.zero, Vector3.one);
            Part(head, "Eye R", eye, eyeMaterial, new Vector3(0.15f, 0.06f, 0.36f), Vector3.zero, Vector3.one);
            Mesh horn = MeshFactory.Cylinder(6, 0.1f, 0f, 0.6f, false, "Horn");
            Part(head, "Horn L", horn, looks.Horn, new Vector3(-0.28f, 0.25f, 0f), new Vector3(-20f, 0f, 40f), Vector3.one);
            Part(head, "Horn R", horn, looks.Horn, new Vector3(0.28f, 0.25f, 0f), new Vector3(-20f, 0f, -40f), Vector3.one);
            leftArm = Pivot(body, "Arm L", new Vector3(-1.05f, 1.45f, 0.1f));
            rightArm = Pivot(body, "Arm R", new Vector3(1.05f, 1.45f, 0.1f));
            foreach (Transform arm in new[] { leftArm, rightArm })
            {
                Part(arm, "Upper", limb, skinMaterial, Vector3.zero, new Vector3(180f, 0f, 0f), new Vector3(1.1f, 1f, 1.1f));
                Part(arm, "Fore", limb, skinMaterial, new Vector3(0f, -1.0f, 0f), new Vector3(180f, 0f, 0f), new Vector3(1.2f, 0.95f, 1.2f));
                Part(arm, "Fist", MeshFactory.Ball(1, 0.2f, 7, Vector3.one * 0.38f), skinMaterial, new Vector3(0f, -2.05f, 0f), Vector3.zero, Vector3.one);
            }
        }

        // ---- Per frame ----

        public void Flash() => hitFlash = 1f;

        /// <summary>Mirrors the creature's position, facing and state; <paramref name="ground"/> is the height under it.</summary>
        public void Sync(float ground, float dt, float time)
        {
            transform.position = new Vector3(enemy.Position.X, ground, enemy.Position.Z);
            transform.rotation = Quaternion.Euler(0f, enemy.Yaw, 0f);
            hitFlash = Mathf.Max(0f, hitFlash - dt * 6f);
            if (!enemy.Alive) deadTime += dt;

            switch (enemy.Kind)
            {
                case EnemyKind.Crawler: AnimateCrawler(time); break;
                case EnemyKind.Wisp: AnimateWisp(time, dt); break;
                default: AnimateBrute(time); break;
            }
        }

        void AnimateCrawler(float time)
        {
            float stride = enemy.Stride * 3.2f;
            float rear = 0f;
            if (enemy.State == EnemyState.Windup) rear = Mathf.Clamp01(enemy.StateTime / enemy.Spec.Windup) * 30f;
            else if (enemy.State == EnemyState.Recover) rear = Mathf.Lerp(-12f, 0f, Mathf.Clamp01(enemy.StateTime / 0.3f));
            float shake = enemy.State == EnemyState.Stagger ? Mathf.Sin(time * 80f) * 6f : 0f;
            float breathe = Mathf.Sin(time * 3f + enemy.Id) * 0.02f;
            if (!enemy.Alive)
            {
                float t = Mathf.Clamp01(deadTime / 0.5f);
                body.localPosition = new Vector3(0f, Mathf.Lerp(0.62f, 0.3f, t) - Mathf.Max(0f, deadTime - 0.8f) * 0.8f, 0f);
                body.localEulerAngles = new Vector3(0f, 0f, t * 25f);
                foreach (Leg leg in legs) leg.Hip.localEulerAngles = new Vector3(0f, leg.RestYaw, -45f * t);
                return;
            }
            body.localPosition = new Vector3(0f, 0.62f + breathe + Mathf.Abs(Mathf.Sin(stride)) * 0.04f, 0f);
            body.localEulerAngles = new Vector3(-rear, shake, 0f);
            head.localEulerAngles = new Vector3(-rear * 0.5f + Mathf.Sin(time * 5f + enemy.Id) * 4f, 0f, 0f);
            for (int i = 0; i < legs.Count; i++)
            {
                Leg leg = legs[i];
                float swing = Mathf.Sin(stride + leg.Phase);
                float lift = Mathf.Max(0f, Mathf.Cos(stride + leg.Phase));
                float raise = i % 4 == 0 ? rear * 1.8f : 0f;
                leg.Hip.localEulerAngles = new Vector3(0f, leg.RestYaw + swing * 14f, lift * 16f + raise);
            }
            Tint(time);
        }

        void AnimateWisp(float time, float dt)
        {
            float bob = Mathf.Sin(time * 2.2f + enemy.Id) * 0.18f;
            body.localPosition = new Vector3(Mathf.Sin(time * 1.3f + enemy.Id) * 0.1f, 1.3f + bob, 0f);
            bool lit = enemy.Lit && enemy.Alive;
            Color core = lit ? Palette.WispLit : Palette.Wisp;
            Looks.SetEmission(eyeMaterial, core * (lit ? 2.4f : 1.1f) + Color.white * hitFlash);
            ParticleSystem.MainModule main = flame.main;
            main.startColor = lit ? new Color(1f, 0.75f, 0.6f, 0.9f) : new Color(1f, 0.22f, 0.12f, 0.55f);
            glow.intensity = Mathf.Lerp(glow.intensity, lit ? 2.2f : 1f, dt * 6f);
            if (!enemy.Alive)
            {
                if (flame.isEmitting) flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                glow.intensity = Mathf.Max(0f, 1.5f - deadTime * 2f);
                body.localScale = Vector3.one * Mathf.Max(0.01f, 1f - deadTime * 2f);
            }
        }

        void AnimateBrute(float time)
        {
            float stride = enemy.Stride * 1.6f;
            float swing = Mathf.Sin(stride);
            float raise = 0f, slam = 0f;
            if (enemy.State == EnemyState.Windup) raise = Mathf.SmoothStep(0f, 1f, enemy.StateTime / enemy.Spec.Windup);
            else if (enemy.State == EnemyState.Recover) slam = Mathf.Clamp01(1f - enemy.StateTime / 0.5f);
            if (!enemy.Alive)
            {
                float t = Mathf.SmoothStep(0f, 1f, deadTime / 1.2f);
                transform.position += Vector3.down * Mathf.Max(0f, deadTime - 2f) * 0.8f;
                transform.rotation *= Quaternion.Euler(-80f * t, 0f, 0f);
                return;
            }
            // Both fists go up overhead during the windup, then slam down in front.
            float left = swing * 25f, right = -swing * 25f;
            if (raise > 0f)
            {
                left = Mathf.Lerp(left, -160f, raise);
                right = Mathf.Lerp(right, -160f, raise);
            }
            else if (slam > 0f)
            {
                left = Mathf.Lerp(left, -50f, slam);
                right = Mathf.Lerp(right, -50f, slam);
            }
            leftArm.localEulerAngles = new Vector3(left, 0f, -8f);
            rightArm.localEulerAngles = new Vector3(right, 0f, 8f);
            leftLeg.localEulerAngles = new Vector3(-swing * 22f, 0f, 0f);
            rightLeg.localEulerAngles = new Vector3(swing * 22f, 0f, 0f);
            body.localPosition = new Vector3(0f, 1.2f + Mathf.Abs(Mathf.Cos(stride)) * 0.08f - slam * 0.25f, 0f);
            body.localEulerAngles = new Vector3(12f + slam * 18f - raise * 10f, Mathf.Sin(stride) * 4f, 0f);
            head.localEulerAngles = new Vector3(Mathf.Sin(time * 1.7f) * 4f, 0f, 0f);
            Tint(time);
        }

        void Tint(float time)
        {
            if (skinMaterial != null && enemy.Kind == EnemyKind.Brute)
            {
                Color skin = enemy.Enraged ? Color.Lerp(Palette.BruteSkin, Palette.Hex("#8a2a1a"), 0.4f + 0.2f * Mathf.Sin(time * 6f)) : Palette.BruteSkin;
                Looks.SetColor(skinMaterial, Color.Lerp(skin, Color.white, hitFlash * 0.5f));
                Looks.SetEmission(eyeMaterial, Palette.Eye * (enemy.Enraged ? 3.5f : 1.5f));
            }
        }
    }
}
