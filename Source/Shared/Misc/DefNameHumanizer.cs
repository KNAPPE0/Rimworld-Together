using System;
using System.Text;

namespace Shared.Misc
{
    /// <summary>
    /// KMH 2.7: Server-safe defName → human label fallback. Used wherever
    /// the real RimWorld <c>ThingDef.label</c> isn't available (server, Discord
    /// bot output) and the in-game label cache hasn't yet seen the def.
    ///
    /// Examples:
    ///   "Plasteel"            → "Plasteel"
    ///   "MeleeWeapon_Knife"   → "Melee Weapon Knife"
    ///   "Apparel_TribalwearM" → "Tribalwear M"   (drops common defName prefixes)
    ///   "RT_CustomOutpost"    → "Custom Outpost"
    /// </summary>
    public static class DefNameHumanizer
    {
        // Common defName prefixes that read as junk to a human (they're scoping
        // tags, not part of the noun). Trim them off when humanising.
        private static readonly string[] StripPrefixes =
        {
            "Apparel_", "MeleeWeapon_", "RangedWeapon_", "Gun_",
            "Bow_", "Drug_", "Plant_", "Tree_", "Mech_",
            "Animal_", "RT_", "VFE_", "RT", "VFE"
        };

        public static string Humanize(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return string.Empty;

            string s = defName;

            // 1. Strip a known scope prefix (only when followed by a capital).
            foreach (string p in StripPrefixes)
            {
                if (s.StartsWith(p, StringComparison.Ordinal) && s.Length > p.Length)
                {
                    char next = s[p.Length];
                    if (char.IsUpper(next) || char.IsDigit(next))
                    {
                        s = s.Substring(p.Length);
                        break;
                    }
                }
            }

            // 2. Replace underscores with spaces.
            s = s.Replace('_', ' ');

            // 3. Insert spaces between camelCase/PascalCase boundaries.
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c))
                {
                    char prev = s[i - 1];
                    bool prevLower = char.IsLower(prev) || char.IsDigit(prev);
                    bool nextLower = i + 1 < s.Length && char.IsLower(s[i + 1]);
                    bool prevSpace = prev == ' ';
                    if (!prevSpace && (prevLower || nextLower))
                        sb.Append(' ');
                }
                sb.Append(c);
            }

            // 4. Collapse double spaces (defense in depth).
            string result = sb.ToString();
            while (result.Contains("  ")) result = result.Replace("  ", " ");
            return result.Trim();
        }
    }
}
