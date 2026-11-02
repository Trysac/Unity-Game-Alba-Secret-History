using UnityEngine;

namespace Alba.Game
{
    public enum Burst
    {
        /// <summary>Hot sparks where a bullet strikes stone or bark.</summary>
        Sparks,
        /// <summary>Dark green ichor from a wounded crawler or giant.</summary>
        Ichor,
        /// <summary>Embers from a wisp.</summary>
        Embers,
        /// <summary>Dust and grit from footfalls and the giant's slam.</summary>
        Dust,
        /// <summary>Glittering motes for pickups, lanterns and the portal.</summary>
        Motes,
        /// <summary>A grey puff where a creature dissolves.</summary>
        Smoke,
    }

    /// <summary>Particle bursts made in code (no prefabs), plus the rain that follows the camera.</summary>
    public sealed class Effects : MonoBehaviour
    {
        ParticleSystem[] systems;
        ParticleSystem rain;

        public void Build(GameArt art)
        {
            systems = new ParticleSystem[6];
            systems[(int)Burst.Sparks] = Make("Sparks", art.Glow, 0.35f, 7f, 0.07f, 1.2f, Palette.Muzzle, 0.6f);
            systems[(int)Burst.Ichor] = Make("Ichor", art.Puff, 0.7f, 3.5f, 0.16f, 2.2f, Palette.Ichor, 0.25f);
            systems[(int)Burst.Embers] = Make("Embers", art.Glow, 1.1f, 1.8f, 0.22f, -0.6f, Palette.Wisp, 0.4f);
            systems[(int)Burst.Dust] = Make("Dust", art.Puff, 1.2f, 2.2f, 0.7f, -0.05f, new Color(0.45f, 0.4f, 0.35f, 0.5f), 0.6f);
            systems[(int)Burst.Motes] = Make("Motes", art.Glow, 1.4f, 1.6f, 0.18f, -0.4f, Color.white, 0.5f);
            systems[(int)Burst.Smoke] = Make("Smoke", art.Puff, 1.6f, 1.2f, 1.1f, -0.15f, new Color(0.25f, 0.25f, 0.28f, 0.6f), 0.8f);
            rain = MakeRain(art.Puff);
        }

        ParticleSystem Make(string name, Material material, float life, float speed, float size, float gravity, Color color, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.SizeOverLifetimeModule grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeRain(Material material)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.1f;
            main.startSpeed = 16f;
            main.startSize3D = true;
            main.startSizeX = 0.025f;
            main.startSizeY = 0.7f;
            main.startSizeZ = 0.025f;
            main.startColor = new Color(0.7f, 0.75f, 0.85f, 0.32f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2500;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 1500f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 30f, 0.1f);
            shape.rotation = new Vector3(90f, 0f, 0f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1f;
            return ps;
        }

        /// <summary>Starts or stops the rain (FollowRain keeps it above the camera).</summary>
        public void SetRain(bool on)
        {
            if (rain == null) return;
            if (on && !rain.isPlaying) rain.Play();
            if (!on && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void FollowRain(Vector3 position)
        {
            if (rain != null) rain.transform.position = position + Vector3.up * 12f;
        }

        public void Emit(Burst kind, Vector3 position, int count, Color? tint = null)
        {
            if (systems == null) return;
            ParticleSystem ps = systems[(int)kind];
            var parameters = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            if (tint.HasValue) parameters.startColor = tint.Value;
            ps.Emit(parameters, count);
        }

        public void Clear()
        {
            if (systems == null) return;
            foreach (ParticleSystem ps in systems) ps.Clear();
        }
    }
}
