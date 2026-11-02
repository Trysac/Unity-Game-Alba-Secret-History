using Alba.Core;
using UnityEngine;

namespace Alba.Game
{
    /// <summary>The colours of the night: peat, heather, wet stone, blood-red lanterns and cold blue orbs.</summary>
    public static class Palette
    {
        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;

        public static readonly Color Bark = Hex("#3b2c22");
        public static readonly Color DarkBark = Hex("#2a211c");
        public static readonly Color Needles = Hex("#1f3324");
        public static readonly Color Leaves = Hex("#2b3b22");
        public static readonly Color Stone = Hex("#6b6a63");
        public static readonly Color Lichen = Hex("#5d6a4a");
        public static readonly Color WallStone = Hex("#585048");
        public static readonly Color Iron = Hex("#2a2a2e");
        public static readonly Color Steel = Hex("#55575e");
        public static readonly Color Wood = Hex("#5a3a24");
        public static readonly Color Brass = Hex("#a07a3a");

        public static readonly Color LanternGlass = Hex("#c41e1e");
        public static readonly Color LanternLight = Hex("#ff6a3a");
        public static readonly Color Orb = Hex("#3a7dff");
        public static readonly Color Gem = Hex("#c03cff");
        public static readonly Color Potion = Hex("#d01c2c");
        public static readonly Color Parchment = Hex("#e6d3a3");
        public static readonly Color Portal = Hex("#36d6ff");
        public static readonly Color PortalDeep = Hex("#6a3cff");

        public static readonly Color CrawlerSkin = Hex("#4a3a2a");
        public static readonly Color CrawlerBelly = Hex("#6b4a32");
        public static readonly Color Eye = Hex("#ff2a1a");
        public static readonly Color Ichor = Hex("#3a5a1a");
        public static readonly Color Wisp = Hex("#ff3a2a");
        public static readonly Color WispLit = Hex("#ffd2c0");
        public static readonly Color BruteSkin = Hex("#4e3428");
        public static readonly Color Horn = Hex("#d8cbb0");

        public static readonly Color Torch = Hex("#fff1d6");
        public static readonly Color Muzzle = Hex("#ffc060");

        /// <summary>Sunrise colours the last chapter fades to.</summary>
        public static readonly Color DawnFog = Hex("#d9906a");
        public static readonly Color DawnSun = Hex("#ffc58a");
        public static readonly Color DawnAmbient = Hex("#8a6a6a");

        public static Color Fog(ChapterDef c) => Hex(c.FogColor);
    }
}
