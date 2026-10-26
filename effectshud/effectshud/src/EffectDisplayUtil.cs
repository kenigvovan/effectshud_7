using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace effectshud.src
{
    /// <summary>Display helpers shared by the HUD and the character-sheet tab.</summary>
    public static class EffectDisplayUtil
    {
        private static readonly (int value, string numeral)[] RomanMap =
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
            (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        };

        public static string ToRoman(int n)
        {
            if (n <= 0) return n.ToString();
            var sb = new StringBuilder();
            foreach (var (value, numeral) in RomanMap)
                while (n >= value) { sb.Append(numeral); n -= value; }
            return sb.ToString();
        }

        /// <summary>Remaining time as m:ss, or h:mm:ss from one hour up.</summary>
        public static string FormatTime(double seconds)
        {
            int total = Math.Max(0, (int)seconds);
            if (total >= 3600) return $"{total / 3600}:{total % 3600 / 60:D2}:{total % 60:D2}";
            return $"{total / 60}:{total % 60:D2}";
        }

        /// <summary>False for effects registered with <c>shouldBeRendered: false</c> (hidden from HUD and sheet).</summary>
        public static bool ShouldRender(string typeId)
        {
            var flags = effectshud.Instance?.effectsShouldBeRendered;
            return typeId == null || flags == null || !flags.TryGetValue(typeId, out bool render) || render;
        }

        /// <summary>Localized effect name: a registered resolver, else the <c>effectshud:&lt;typeId&gt;</c> lang key,
        /// else a humanized typeId.</summary>
        public static string EffectName(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return "?";
            var resolvers = effectshud.Instance?.effectDisplayNames;
            if (resolvers != null && resolvers.TryGetValue(typeId, out var fn) && fn != null)
            {
                try { var n = fn(); if (!string.IsNullOrEmpty(n)) return n; } catch { /* fall through */ }
            }
            string key = "effectshud:" + typeId;
            return Lang.HasTranslation(key, true, false) ? Lang.Get(key) : Humanize(typeId);
        }

        private static string Humanize(string id)
        {
            string s = id.Replace('_', ' ').Replace('-', ' ').Trim();
            if (s.Length == 0) return id;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>A registered custom icon (any domain), else the effectshud png convention. May point at a
        /// nonexistent asset — callers guard against that.</summary>
        public static AssetLocation ResolveIcon(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return null;
            var custom = effectshud.Instance?.effectIcons;
            if (custom != null && custom.TryGetValue(typeId, out var loc) && loc != null) return loc;
            return new AssetLocation($"effectshud:textures/effects/{typeId}.png");
        }
    }
}
