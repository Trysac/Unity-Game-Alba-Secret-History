using UnityEngine;

namespace Alba.Game
{
    /// <summary>
    /// The base materials (so the build keeps their shaders and keyword variants), the app icon and
    /// the synthesized audio, wired up by ProjectBuilder. Meshes and textures are made at run time.
    /// </summary>
    public sealed class GameArt : ScriptableObject
    {
        [Header("Materials")]
        [Tooltip("Standard, opaque: ground, trees, stone, creatures, weapons.")]
        public Material Lit;
        [Tooltip("Standard with emission: eyes, orbs, sealstones, lantern glass.")]
        public Material Glowing;
        [Tooltip("Additive soft dots: sparks, embers, wisps, the portal.")]
        public Material Glow;
        [Tooltip("Alpha-blended soft dots: rain, dust, smoke, ichor.")]
        public Material Puff;
        public Texture2D SoftDot;
        public Texture2D Icon;

        [Header("Music and ambience")]
        public AudioClip TitleMusic;
        public AudioClip NightMusic;
        public AudioClip KeepMusic;
        public AudioClip DawnMusic;
        public AudioClip Rain;
        public AudioClip Wind;

        [Header("Weapons")]
        public AudioClip Pistol;
        public AudioClip Shotgun;
        public AudioClip Rifle;
        public AudioClip Dirk;
        public AudioClip DryFire;
        public AudioClip Hit;
        public AudioClip Ricochet;
        public AudioClip Immune;

        [Header("Creatures")]
        public AudioClip CrawlerAlert;
        public AudioClip CrawlerWindup;
        public AudioClip CrawlerDie;
        public AudioClip WispAlert;
        public AudioClip WispDie;
        public AudioClip BruteRoar;
        public AudioClip BruteWindup;
        public AudioClip BruteSlam;
        public AudioClip BruteDie;

        [Header("Player")]
        public AudioClip Hurt;
        public AudioClip Heartbeat;
        public AudioClip Death;
        public AudioClip Potion;
        public AudioClip Step;
        public AudioClip Torch;
        public AudioClip TorchEmpty;

        [Header("World")]
        public AudioClip Orb;
        public AudioClip Gem;
        public AudioClip Page;
        public AudioClip Ammo;
        public AudioClip Lantern;
        public AudioClip PortalOpen;
        public AudioClip Escape;
        public AudioClip Wave;
        public AudioClip Dawn;

        [Header("Menus")]
        public AudioClip Move;
        public AudioClip Select;
        public AudioClip Pause;
        public AudioClip GameOver;
        public AudioClip Victory;
    }
}
