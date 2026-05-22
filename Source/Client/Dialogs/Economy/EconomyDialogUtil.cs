using RimWorld;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Shared dialog helpers for the economy UI: render an item icon
    /// + label, format silver, etc.
    /// </summary>
    internal static class EconomyDialogUtil
    {
        /// <summary>
        /// Draw a left-aligned icon for <paramref name="defName"/> followed by
        /// the def's label (or the raw defName as fallback). Returns the icon
        /// width so callers can advance their cursor.
        /// </summary>
        public static float DrawItemIconAndLabel(Rect rect, string defName, string trailing = null, string overrideLabel = null)
        {
            const float iconSize = 22f;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

            Rect iconRect = new Rect(rect.x, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
            if (def != null)
            {
                try { Widgets.ThingIcon(iconRect, def); }
                catch
                {
                    // Some defs (no graphic) throw; just draw a placeholder box.
                    GUI.DrawTexture(iconRect, BaseContent.GreyTex);
                }
            }
            else
            {
                GUI.DrawTexture(iconRect, BaseContent.GreyTex);
            }

            Rect labelRect = new Rect(rect.x + iconSize + 4f, rect.y, rect.width - iconSize - 4f, rect.height);
            string label = !string.IsNullOrEmpty(overrideLabel)
                ? overrideLabel
                : (def?.label.CapitalizeFirst() ?? defName);
            if (!string.IsNullOrEmpty(trailing)) label = $"{label}  {trailing}";

            Widgets.Label(labelRect, label);
            // Tooltip for full text — handles truncation gracefully when the
            // rect can't fit the whole string.
            TooltipHandler.TipRegion(labelRect, label);
            return iconSize + 4f;
        }

        /// <summary>
        /// KMH 2.7: Single source of truth for client-side defName → friendly
        /// label resolution. Tries RimWorld's DefDatabase first (handles the
        /// player's loaded mods), then falls back to a humanizer so unloaded
        /// defs still render reasonably (e.g. `Apparel_PowerArmor` → `Power
        /// Armor`) instead of dumping the raw code on the user.
        ///
        /// Anywhere an item identifier hits the UI (treasury logs, quest rows,
        /// withdraw prompts, marketplace listings), this is the helper to use.
        /// Cheap — single DefDatabase lookup, no allocations in the hit path.
        /// </summary>
        public static string ResolveLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return string.Empty;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
                return def.label.CapitalizeFirst();
            return Humanize(defName);
        }

        /// <summary>
        /// KMH 2.7: Fallback humanizer for defNames the client's DefDatabase
        /// doesn't know about (e.g. modded items on the server that the local
        /// player doesn't have installed). Matches the server-side
        /// <c>DefNameHumanizer</c> so labels stay consistent across server
        /// chat and client dialogs.
        ///
        /// Strips common scope prefixes (Apparel_, MeleeWeapon_, Gun_, Drug_,
        /// Plant_, Building_, RT_, VFE_), then splits underscores into spaces
        /// and inserts spaces at CamelCase boundaries.
        /// </summary>
        public static string Humanize(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return string.Empty;
            string s = defName;

            // Strip the most common scope prefixes.
            string[] prefixes = { "Apparel_", "MeleeWeapon_", "Gun_", "Drug_", "Plant_", "Building_", "RT_", "VFE_" };
            foreach (string p in prefixes)
            {
                if (s.StartsWith(p)) { s = s.Substring(p.Length); break; }
            }

            // Underscores → spaces.
            s = s.Replace('_', ' ');

            // Insert a space at every CamelCase boundary (lowercase→Uppercase).
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().Trim().CapitalizeFirst();
        }

        public static string FormatSilver(long amount) => $"{amount}s";

        public static string FormatTimeRemaining(long expiresUtcTicks)
        {
            long now = System.DateTime.UtcNow.Ticks;
            long diff = expiresUtcTicks - now;
            if (diff <= 0) return "expired";

            System.TimeSpan ts = new System.TimeSpan(diff);
            if (ts.TotalDays >= 1.0) return $"{(int)ts.TotalDays}d {ts.Hours}h";
            if (ts.TotalHours >= 1.0) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{(int)ts.TotalMinutes}m";
        }
    }
}
