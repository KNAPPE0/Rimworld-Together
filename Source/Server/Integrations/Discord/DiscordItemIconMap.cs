using GameServer.Core;
using System;

namespace GameServer.Integrations.Discord
{
    /// <summary>
    /// Discord-side fallback for item icons. The server can't ship RimWorld's
    /// in-game textures, so we resolve a presentation in this priority order:
    ///
    ///   1. Configured CDN ({MarketplaceIconBaseUrl}/{defName}.png) → embed thumbnail
    ///   2. Category-based emoji prefix → in field titles
    ///   3. Generic 📦 fallback
    /// </summary>
    internal static class DiscordItemIconMap
    {
        /// <summary>
        /// Returns a thumbnail URL for the given item def, or null if no
        /// CDN base URL is configured.
        /// </summary>
        public static string TryGetIconUrl(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            string baseUrl = Master.ServerConfig?.MarketplaceIconBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            // Trim trailing slash so we don't double up.
            string cleaned = baseUrl.TrimEnd('/');
            // Defence-in-depth: scrub anything weird in the def name.
            string safe = SafeUrlSegment(defName);
            return $"{cleaned}/{safe}.png";
        }

        /// <summary>
        /// Returns a single-character emoji that represents the item's category,
        /// purely from defName substring matching. Cheap heuristic, server-side.
        /// </summary>
        public static string EmojiFor(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "📦";
            string lower = defName.ToLowerInvariant();

            // Silver / gold / valuables.
            if (lower.Contains("silver")) return "🪙";
            if (lower.Contains("gold")) return "🥇";
            if (lower.Contains("jade")) return "💚";
            if (lower.Contains("luci")) return "💊";
            if (lower.Contains("medicine") || lower.Contains("herbal")) return "💊";

            // Materials.
            if (lower.Contains("steel") || lower.Contains("plasteel") || lower.Contains("uranium")) return "⚙";
            if (lower.Contains("wood") || lower.Contains("log")) return "🪵";
            if (lower.Contains("blocks") || lower.Contains("brick") || lower.Contains("concrete") || lower.Contains("chunk")) return "🧱";
            if (lower.Contains("component")) return "🔧";

            // Food.
            if (lower.Contains("meat")) return "🥩";
            if (lower.Contains("meal") || lower.Contains("food")) return "🍱";
            if (lower.Contains("kibble")) return "🥣";
            if (lower.Contains("rice") || lower.Contains("corn") || lower.Contains("berry") || lower.Contains("hay") || lower.Contains("raw")) return "🌾";

            // Drugs / consumables.
            if (lower.Contains("smokeleaf")) return "🌿";
            if (lower.Contains("psychoid")) return "🍃";
            if (lower.Contains("alcohol") || lower.Contains("beer") || lower.Contains("ambrosia")) return "🍺";
            if (lower.Contains("yayo") || lower.Contains("flake")) return "❄";

            // Textiles + leather.
            if (lower.Contains("cloth") || lower.Contains("synthread") || lower.Contains("hyperweave") || lower.Contains("devilstrand")) return "🧵";
            if (lower.Contains("leather") || lower.Contains("wool") || lower.Contains("fur") || lower.Contains("skin")) return "🦊";

            // Weapons + armour.
            if (lower.Contains("rifle") || lower.Contains("gun") || lower.Contains("pistol") || lower.Contains("smg") || lower.Contains("shotgun")) return "🔫";
            if (lower.Contains("sword") || lower.Contains("blade") || lower.Contains("axe") || lower.Contains("club") || lower.Contains("knife")) return "🗡";
            if (lower.Contains("bow") || lower.Contains("arrow")) return "🏹";
            if (lower.Contains("armor") || lower.Contains("vest") || lower.Contains("helmet") || lower.Contains("shield")) return "🛡";

            // Tech / spacer.
            if (lower.Contains("spacer") || lower.Contains("archotech") || lower.Contains("ai") || lower.Contains("techprof")) return "🛰";

            // Chemfuel / power.
            if (lower.Contains("chemfuel") || lower.Contains("fuel")) return "⛽";

            return "📦";
        }

        private static string SafeUrlSegment(string s)
        {
            // Allow letters, digits, underscore, dash. Anything else → underscore.
            char[] buf = new char[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                buf[i] = (char.IsLetterOrDigit(c) || c == '_' || c == '-') ? c : '_';
            }
            return new string(buf);
        }
    }
}
