using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Shared.Files.Guilds
{
    public class GuildFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<GuildMember> GuildMembers { get; set; } = new List<GuildMember>();

        private Semaphore SavingSemaphore = new Semaphore(1, 1);

        public void AddMember(GuildMember member)
        {
            if (!GuildMembers.Contains(member)) GuildMembers.Add(member);
            Save(Path.Combine(SavePath, Name + CommonValues.DefaultSaveFormat), this);
        }

        public void RemoveMember(GuildMember member)
        {
            if (GuildMembers.Contains(member)) GuildMembers.Remove(member);
            Save(Path.Combine(SavePath, Name + CommonValues.DefaultSaveFormat), this);
        }

        public void PromoteMember(GuildMember member)
        {
            GuildMember toFind = GuildMembers.First(fetch => fetch.Username == member.Username);
            toFind.Rank = GuildMember.GuildRanks.Moderator;
            Save(Path.Combine(SavePath, Name + CommonValues.DefaultSaveFormat), this);
        }

        public void DemoteMember(GuildMember member)
        {
            GuildMember toFind = GuildMembers.First(fetch => fetch.Username == member.Username);
            toFind.Rank = GuildMember.GuildRanks.Member;
            Save(Path.Combine(SavePath, Name + CommonValues.DefaultSaveFormat), this);
        }

        public void Delete() { File.Delete(Path.Combine(SavePath, Name + CommonValues.DefaultSaveFormat)); }
    }
}
