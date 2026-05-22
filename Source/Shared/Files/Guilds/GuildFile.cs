using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Shared.Files.Guilds
{
    public class GuildFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<GuildMember> GuildMembers { get; set; } = new List<GuildMember>();

        // KMH: economy + governance.
        public GuildSettings Settings { get; set; } = new GuildSettings();
        public GuildPerks Perks { get; set; } = new GuildPerks();

        // KMH: diplomacy. Two-way relationships keyed by other guild's name.
        // Allies see each other's marketplace listings even when guild-only,
        // can claim each other's quests, and share the /a chat channel.
        // Hostile is informational only (no PvP enforcement; surfaces in UI).
        public System.Collections.Generic.Dictionary<string, AllianceRelation> Relationships { get; set; }
            = new System.Collections.Generic.Dictionary<string, AllianceRelation>(System.StringComparer.OrdinalIgnoreCase);

        private Semaphore SavingSemaphore = new Semaphore(1, 1);

        // Fires after Save() persists this guild to disk and
        // after Delete() removes it. GuildManagerH subscribes so its
        // in-memory cache invalidates automatically — no per-call-site
        // invalidation required.
        public static event System.Action<GuildFile> OnSaved;
        public static event System.Action<string> OnDeleted;

        private void PersistAndNotify()
        {
            Save(Path.Combine(SavePath, Name + CommonValues.DefaultSaveFormat), this);
            try { OnSaved?.Invoke(this); } catch { }
        }

        public void Persist() { PersistAndNotify(); }

        public void AddMember(GuildMember member)
        {
            if (!GuildMembers.Contains(member)) GuildMembers.Add(member);
            PersistAndNotify();
        }

        public void RemoveMember(GuildMember member)
        {
            if (GuildMembers.Contains(member)) GuildMembers.Remove(member);
            PersistAndNotify();
        }

        public void PromoteMember(GuildMember member)
        {
            GuildMember toFind = GuildMembers.FirstOrDefault(fetch => fetch.Username == member.Username);
            toFind.Rank = GuildMember.GuildRanks.Moderator;
            PersistAndNotify();
        }

        public void DemoteMember(GuildMember member)
        {
            GuildMember toFind = GuildMembers.FirstOrDefault(fetch => fetch.Username == member.Username);
            toFind.Rank = GuildMember.GuildRanks.Member;
            PersistAndNotify();
        }

        public void Delete()
        {
            string n = Name;
            File.Delete(Path.Combine(SavePath, Name + CommonValues.DefaultSaveFormat));
            try { OnDeleted?.Invoke(n); } catch { }
        }
    }
}
