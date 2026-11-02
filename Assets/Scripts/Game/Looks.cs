using System.Collections.Generic;
using UnityEngine;

namespace Alba.Game
{
    /// <summary>Material instances made from the two Standard templates in GameArt, cached by colour and texture.</summary>
    public sealed class Looks
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GlossId = Shader.PropertyToID("_Glossiness");
        static readonly int MetalId = Shader.PropertyToID("_Metallic");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int TintId = Shader.PropertyToID("_TintColor");

        readonly GameArt art;
        readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public Looks(GameArt art)
        {
            this.art = art;
            Bark = Lit(Palette.Bark, TextureFactory.Noisy("Bark", 128, 3, Color.white * 0.55f, Color.white * 1.15f, 9f, 4f));
            Needles = Lit(Palette.Needles, TextureFactory.Noisy("Needles", 64, 4, Color.white * 0.6f, Color.white * 1.2f, 14f));
            Leaves = Lit(Palette.Leaves, TextureFactory.Noisy("Leaves", 64, 5, Color.white * 0.6f, Color.white * 1.2f, 10f));
            Texture2D rock = TextureFactory.Noisy("Rock", 128, 6, Color.white * 0.6f, Color.white * 1.15f, 7f);
            Stone = Lit(Palette.Stone, rock, 0.18f);
            StandingStone = Lit(Color.Lerp(Palette.Stone, Palette.Lichen, 0.35f), rock, 0.25f);
            Wall = Lit(Color.white * 0.9f, TextureFactory.Masonry(256, 8), 0.12f);
            Iron = Lit(Palette.Iron, null, 0.45f, 0.6f);
            Steel = Lit(Palette.Steel, null, 0.6f, 0.75f);
            Wood = Lit(Palette.Wood, TextureFactory.Noisy("Wood", 64, 9, Color.white * 0.7f, Color.white * 1.1f, 6f, 5f), 0.3f);
            Brass = Lit(Palette.Brass, null, 0.6f, 0.8f);
            Leather = Lit(Palette.Hex("#3a2a20"), null, 0.2f);
            CrawlerSkin = Lit(Palette.CrawlerSkin, TextureFactory.Noisy("Carapace", 64, 10, Color.white * 0.6f, Color.white * 1.3f, 12f), 0.45f);
            BruteSkin = Lit(Palette.BruteSkin, TextureFactory.Noisy("Hide", 64, 11, Color.white * 0.7f, Color.white * 1.15f, 8f), 0.2f);
            Horn = Lit(Palette.Horn, null, 0.35f);
            Eye = Glowing(Palette.Eye, Palette.Eye * 2.2f);
            Parchment = Glowing(Color.white, Palette.Parchment * 0.25f, TextureFactory.Parchment(128));
        }

        public Material Bark { get; }
        public Material Needles { get; }
        public Material Leaves { get; }
        public Material Stone { get; }
        public Material StandingStone { get; }
        public Material Wall { get; }
        public Material Iron { get; }
        public Material Steel { get; }
        public Material Wood { get; }
        public Material Brass { get; }
        public Material Leather { get; }
        public Material CrawlerSkin { get; }
        public Material BruteSkin { get; }
        public Material Horn { get; }
        public Material Eye { get; }
        public Material Parchment { get; }

        public Material Lit(Color color, Texture texture = null, float smoothness = 0.1f, float metallic = 0f)
        {
            string key = $"lit{color}{(texture != null ? texture.GetInstanceID() : 0)}{smoothness}{metallic}";
            if (cache.TryGetValue(key, out Material cached)) return cached;
            var material = new Material(art.Lit) { name = "Lit " + color, enableInstancing = true };
            material.SetColor(ColorId, color);
            material.SetTexture(MainTexId, texture);
            material.SetFloat(GlossId, smoothness);
            material.SetFloat(MetalId, metallic);
            cache[key] = material;
            return material;
        }

        public Material Glowing(Color color, Color emission, Texture texture = null)
        {
            string key = $"glow{color}{emission}{(texture != null ? texture.GetInstanceID() : 0)}";
            if (cache.TryGetValue(key, out Material cached)) return cached;
            var material = new Material(art.Glowing) { name = "Glowing " + color, enableInstancing = true };
            material.SetColor(ColorId, color);
            material.SetTexture(MainTexId, texture);
            material.SetColor(EmissionId, emission);
            cache[key] = material;
            return material;
        }

        /// <summary>A fresh glowing material that is changed every frame (not shared).</summary>
        public Material OwnGlowing(Color color, Color emission)
        {
            var material = new Material(art.Glowing) { name = "Glowing (own)" };
            material.SetColor(ColorId, color);
            material.SetColor(EmissionId, emission);
            return material;
        }

        /// <summary>An additive soft glow (halos, the portal's heart).</summary>
        public Material Additive(Color tint)
        {
            var material = new Material(art.Glow) { name = "Additive " + tint };
            material.SetColor(TintId, tint);
            return material;
        }

        public static void SetEmission(Material material, Color emission) => material.SetColor(EmissionId, emission);

        public static void SetColor(Material material, Color color) => material.SetColor(ColorId, color);

        public static void SetTint(Material material, Color tint) => material.SetColor(TintId, tint);
    }
}
