using Alba.Core;
using UnityEngine;

namespace Alba.Game
{
    /// <summary>
    /// The first-person view: the camera at Eilidh's eyes with walking bob, recoil and shake; the
    /// torch (a shadow-casting spot light) in her left hand; and the weapon in her right, all
    /// modelled from simple parts. Between chapters it slowly circles the stones for the menus.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        Camera view;
        Transform hands;
        Transform torchModel;
        Light torch;
        Light muzzleLight;
        Transform muzzleFlash;
        readonly Transform[] weapons = new Transform[4];
        WeaponKind shown = WeaponKind.Pistol;
        float switchTimer;
        float recoil;
        float kick;
        float shake;
        float flash;
        float swing;
        float lookSwayX, lookSwayY;
        float dyingTilt;
        Vector3 orbitTarget;
        float orbitAngle;

        public Camera Camera => view;

        public void Build(Looks looks, GameArt art)
        {
            view = GetComponent<Camera>();
            hands = new GameObject("Hands").transform;
            hands.SetParent(transform, false);

            // The torch, held low on the left.
            torchModel = Part(hands, "Torch", null, null, new Vector3(-0.26f, -0.24f, 0.5f), new Vector3(90f, 0f, 0f), Vector3.one * 0.6f);
            Part(torchModel, "Body", MeshFactory.Cylinder(8, 0.035f, 0.035f, 0.26f), looks.Iron, new Vector3(0f, -0.2f, 0f), Vector3.zero, Vector3.one);
            Part(torchModel, "Head", MeshFactory.Cylinder(8, 0.045f, 0.055f, 0.08f), looks.Brass, new Vector3(0f, 0.06f, 0f), Vector3.zero, Vector3.one);
            Part(torchModel, "Lens", MeshFactory.Disc(0.048f, 10), looks.Glowing(Palette.Torch, Palette.Torch), new Vector3(0f, 0.142f, 0f), new Vector3(90f, 0f, 0f), Vector3.one);
            Part(hands, "Left hand", MeshFactory.Ball(1, 0.2f, 2, new Vector3(0.06f, 0.05f, 0.08f)), looks.Leather, new Vector3(-0.26f, -0.25f, 0.44f), Vector3.zero, Vector3.one * 0.6f);

            var torchObject = new GameObject("Torch light");
            torchObject.transform.SetParent(transform, false);
            torchObject.transform.localPosition = new Vector3(-0.2f, -0.15f, 0.3f);
            torch = torchObject.AddComponent<Light>();
            torch.type = LightType.Spot;
            torch.color = Palette.Torch;
            torch.range = Tuning.FlashlightRange + 4f;
            torch.spotAngle = Tuning.FlashlightHalfAngle * 2f + 6f;
            torch.innerSpotAngle = Tuning.FlashlightHalfAngle;
            torch.intensity = 1.5f;
            torch.shadows = LightShadows.Hard;
            torch.shadowStrength = 0.85f;
            torch.renderMode = LightRenderMode.ForcePixel;

            weapons[(int)WeaponKind.Dirk] = BuildDirk(looks);
            weapons[(int)WeaponKind.Pistol] = BuildPistol(looks);
            weapons[(int)WeaponKind.Shotgun] = BuildShotgun(looks);
            weapons[(int)WeaponKind.Rifle] = BuildRifle(looks);

            muzzleFlash = Part(hands, "Muzzle flash", MeshFactory.Disc(0.12f, 8), looks.Additive(new Color(1f, 0.7f, 0.3f, 0.9f)), new Vector3(0.22f, -0.17f, 0.82f), Vector3.zero, Vector3.one);
            muzzleFlash.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = art.SoftDot;
            muzzleFlash.gameObject.SetActive(false);
            var muzzleObject = new GameObject("Muzzle light");
            muzzleObject.transform.SetParent(hands, false);
            muzzleObject.transform.localPosition = new Vector3(0.2f, -0.1f, 1.0f);
            muzzleLight = muzzleObject.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.color = Palette.Muzzle;
            muzzleLight.range = 6f;
            muzzleLight.intensity = 0f;
            muzzleLight.shadows = LightShadows.None;
            ShowWeapon(WeaponKind.Pistol, instant: true);
            ShowHands(false);
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
                renderer.receiveShadows = false;
            }
            return go.transform;
        }

        // ---- Weapon models (barrels point along +Z) ----

        Transform Holder(string name) => Part(hands, name, null, null, new Vector3(0.22f, -0.21f, 0.5f), Vector3.zero, Vector3.one * 0.62f);

        Transform BuildDirk(Looks looks)
        {
            Transform root = Holder("Dirk");
            Part(root, "Grip", MeshFactory.Cylinder(6, 0.022f, 0.02f, 0.13f), looks.Wood, new Vector3(0f, 0f, -0.05f), new Vector3(90f, 0f, 0f), Vector3.one);
            Part(root, "Guard", MeshFactory.Box(new Vector3(0.09f, 0.02f, 0.02f)), looks.Brass, new Vector3(0f, -0.01f, 0.08f), Vector3.zero, Vector3.one);
            Part(root, "Blade", MeshFactory.Cylinder(4, 0.022f, 0.002f, 0.32f), looks.Steel, new Vector3(0f, 0f, 0.09f), new Vector3(90f, 45f, 0f), new Vector3(1f, 1f, 0.3f));
            return root;
        }

        Transform BuildPistol(Looks looks)
        {
            Transform root = Holder("Pistol");
            Part(root, "Barrel", MeshFactory.Cylinder(8, 0.02f, 0.02f, 0.26f), looks.Steel, new Vector3(0f, 0.05f, 0.02f), new Vector3(90f, 0f, 0f), Vector3.one);
            Part(root, "Cylinder", MeshFactory.Cylinder(6, 0.04f, 0.04f, 0.07f), looks.Iron, new Vector3(0f, 0.04f, -0.02f), new Vector3(90f, 0f, 0f), Vector3.one);
            Part(root, "Frame", MeshFactory.Box(new Vector3(0.035f, 0.05f, 0.12f)), looks.Iron, new Vector3(0f, 0.01f, -0.06f), Vector3.zero, Vector3.one);
            Part(root, "Grip", MeshFactory.Box(new Vector3(0.035f, 0.13f, 0.05f)), looks.Wood, new Vector3(0f, -0.11f, -0.1f), new Vector3(-18f, 0f, 0f), Vector3.one);
            return root;
        }

        Transform BuildShotgun(Looks looks)
        {
            Transform root = Holder("Shotgun");
            Part(root, "Barrel L", MeshFactory.Cylinder(8, 0.022f, 0.022f, 0.6f), looks.Steel, new Vector3(-0.022f, 0.04f, 0.0f), new Vector3(90f, 0f, 0f), Vector3.one);
            Part(root, "Barrel R", MeshFactory.Cylinder(8, 0.022f, 0.022f, 0.6f), looks.Steel, new Vector3(0.022f, 0.04f, 0.0f), new Vector3(90f, 0f, 0f), Vector3.one);
            Part(root, "Fore", MeshFactory.Box(new Vector3(0.06f, 0.04f, 0.22f)), looks.Wood, new Vector3(0f, 0.0f, 0.12f), Vector3.zero, Vector3.one);
            Part(root, "Stock", MeshFactory.Box(new Vector3(0.05f, 0.08f, 0.32f)), looks.Wood, new Vector3(0f, -0.04f, -0.2f), new Vector3(-8f, 0f, 0f), Vector3.one);
            return root;
        }

        Transform BuildRifle(Looks looks)
        {
            Transform root = Holder("Rifle");
            Part(root, "Barrel", MeshFactory.Cylinder(8, 0.016f, 0.016f, 0.65f), looks.Steel, new Vector3(0f, 0.05f, 0.05f), new Vector3(90f, 0f, 0f), Vector3.one);
            Part(root, "Body", MeshFactory.Box(new Vector3(0.05f, 0.07f, 0.34f)), looks.Iron, new Vector3(0f, 0.0f, -0.02f), Vector3.zero, Vector3.one);
            Part(root, "Magazine", MeshFactory.Box(new Vector3(0.035f, 0.14f, 0.05f)), looks.Iron, new Vector3(0f, -0.1f, 0.04f), new Vector3(12f, 0f, 0f), Vector3.one);
            Part(root, "Stock", MeshFactory.Box(new Vector3(0.045f, 0.08f, 0.25f)), looks.Wood, new Vector3(0f, -0.03f, -0.28f), new Vector3(-6f, 0f, 0f), Vector3.one);
            Part(root, "Sight", MeshFactory.Box(new Vector3(0.012f, 0.03f, 0.012f)), looks.Iron, new Vector3(0f, 0.08f, 0.32f), Vector3.zero, Vector3.one);
            return root;
        }

        // ---- Reactions ----

        public void ShowHands(bool visible) => hands.gameObject.SetActive(visible);

        public void ShowWeapon(WeaponKind kind, bool instant = false)
        {
            if (kind == shown && !instant) return;
            shown = kind;
            switchTimer = instant ? 0f : 0.3f;
            for (int i = 0; i < weapons.Length; i++) weapons[i].gameObject.SetActive(i == (int)kind);
        }

        public void Fire(WeaponKind kind)
        {
            if (kind == WeaponKind.Dirk)
            {
                swing = 1f;
                return;
            }
            float strength = kind == WeaponKind.Shotgun ? 1.6f : kind == WeaponKind.Rifle ? 0.45f : 0.8f;
            recoil += strength;
            kick = Mathf.Min(1.5f, kick + strength * 0.6f);
            flash = 1f;
            muzzleFlash.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        /// <summary>Places the camera at the player's eyes; call every frame while playing.</summary>
        public void FollowPlayer(Player player, float ground, bool dying, float dyingTime, float dt, Vector2 lookDelta)
        {
            float bob = Mathf.Sin(player.Stride * Mathf.PI / 1.2f);
            float speed = player.Velocity.Length;
            float bobAmount = Mathf.Clamp01(speed / Tuning.WalkSpeed) * 0.045f;
            dyingTilt = dying ? Mathf.MoveTowards(dyingTilt, 1f, dt * 1.6f) : Mathf.MoveTowards(dyingTilt, 0f, dt * 3f);
            float eye = Mathf.Lerp(Tuning.EyeHeight, 0.35f, Mathf.SmoothStep(0f, 1f, dyingTilt));
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 8f);
            kick = Mathf.MoveTowards(kick, 0f, dt * 6f);
            shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 18f);
            swing = Mathf.MoveTowards(swing, 0f, dt * 3.5f);
            switchTimer = Mathf.Max(0f, switchTimer - dt);

            Vector3 jitter = shake > 0f ? Random.insideUnitSphere * shake * 0.06f : Vector3.zero;
            transform.position = new Vector3(player.Position.X, ground + eye + Mathf.Abs(bob) * bobAmount, player.Position.Z) + jitter;
            transform.rotation = Quaternion.Euler(-player.Pitch - recoil * 1.6f, player.Yaw, dyingTilt * 70f + bob * bobAmount * 8f);

            // Weapon sway, bob, kick and the switching dip.
            lookSwayX = Mathf.Lerp(lookSwayX, Mathf.Clamp(-lookDelta.x * 0.006f, -0.04f, 0.04f), dt * 10f);
            lookSwayY = Mathf.Lerp(lookSwayY, Mathf.Clamp(-lookDelta.y * 0.006f, -0.04f, 0.04f), dt * 10f);
            float dip = switchTimer > 0f ? Mathf.Sin(switchTimer / 0.3f * Mathf.PI) * 0.25f : 0f;
            hands.localPosition = new Vector3(lookSwayX + Mathf.Cos(player.Stride * Mathf.PI / 1.2f) * bobAmount * 0.5f,
                lookSwayY - Mathf.Abs(bob) * bobAmount * 0.6f - dip - dyingTilt * 0.4f, -kick * 0.06f);
            Transform weapon = weapons[(int)shown];
            float slash = Mathf.Sin(swing * Mathf.PI);
            weapon.localEulerAngles = shown == WeaponKind.Dirk
                ? new Vector3(-30f * slash, -50f * slash + 10f, 40f * slash)
                : new Vector3(-kick * 10f, 0f, 0f);
            muzzleFlash.gameObject.SetActive(flash > 0.3f && shown != WeaponKind.Dirk);
            muzzleFlash.localScale = Vector3.one * (0.6f + flash);
            muzzleLight.intensity = flash * 1.4f;

            // The torch fades and stutters when the battery runs low.
            float charge = player.Battery;
            float stutter = charge < 0.15f ? (Mathf.PerlinNoise(Time.time * 18f, 0f) > 0.35f ? 1f : 0.2f) : 1f;
            torch.enabled = player.Lit;
            torch.intensity = Mathf.Lerp(0.8f, 1.5f, Mathf.Clamp01(charge * 3f)) * stutter;
            torchModel.localEulerAngles = new Vector3(90f - lookSwayY * 50f, lookSwayX * 50f, 0f);
        }

        /// <summary>Slowly circles a point (the title and between chapters).</summary>
        public void Orbit(Vector3 target, float radius, float height, float dt)
        {
            orbitTarget = target;
            orbitAngle += dt * 4f;
            Vector3 offset = Quaternion.Euler(0f, orbitAngle, 0f) * new Vector3(0f, height, -radius);
            transform.position = orbitTarget + offset;
            transform.LookAt(orbitTarget + Vector3.up * 2f);
            torch.enabled = false;
            muzzleLight.intensity = 0f;
        }
    }
}
