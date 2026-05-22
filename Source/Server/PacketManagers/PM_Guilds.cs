using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files;
using Shared.Files.Guilds;
using Shared.Files.Sites;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static Shared.Files.Guilds.GuildMember;
using static TCPNetwork.Packets.PKT_PlayerGuild;

namespace GameServer.PacketManager
{

    public class PM_Guilds : PM_Base
    {
        [HandlesPacket(PacketHeader.GuildManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (!Master.ActionConfigs.EnableFactions)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            PKT_PlayerGuild data = Serializer.ConvertBytesToObject<PKT_PlayerGuild>(bytes);

            switch (data._stepMode)
            {
                case GuildStepMode.Create:
                    CreateFaction(client, data);
                    break;

                case GuildStepMode.Delete:
                    DeleteFaction(client, data);
                    break;

                case GuildStepMode.Invite:
                    InviteMemberToFaction(client, data);
                    break;

                case GuildStepMode.AddMember:
                    AddMemberToFaction(client, data);
                    break;

                case GuildStepMode.RemoveMember:
                    RemoveMemberFromFaction(client, data);
                    break;

                case GuildStepMode.Promote:
                    PromoteMember(client, data);
                    break;

                case GuildStepMode.Demote:
                    DemoteMember(client, data);
                    break;

                case GuildStepMode.MemberList:
                    SendMemberList(client, data);
                    break;
            }
        }

        private static void CreateFaction(ServerClient client, PKT_PlayerGuild factionManifest)
        {
            // KMH 2.7: Validate the guild name BEFORE we use it as a filename
            // or persist it. The previous code took the raw client-supplied
            // string verbatim — a malicious client could send `../admin`,
            // `con`, multi-line whitespace, or an absurdly long name and the
            // server would happily write it to disk. We:
            //   * Trim + length-cap
            //   * Restrict to letters/digits/space/dash/underscore/dot/apostrophe
            //   * Reject path-traversal sequences explicitly
            string rawName = factionManifest?._guild?.Name;
            if (!IsValidGuildName(rawName, out string sanitisedName, out string rejectReason))
            {
                // Reuse NameInUse as a generic "can't use this name" reply —
                // the client treats it the same way (show an error toast).
                factionManifest._stepMode = GuildStepMode.NameInUse;
                client.Listener.EnqueuePacket(PacketHeader.GuildManager, factionManifest);
                Shared.Misc.Printer.Warning(
                    $"[Guild] Rejected CreateFaction from {client.UserFile?.Username}: {rejectReason} (raw={rawName ?? "<null>"})");
                return;
            }
            factionManifest._guild.Name = sanitisedName;

            if (GuildManagerH.CheckIfFactionExistsByName(factionManifest._guild.Name))
            {
                factionManifest._stepMode = GuildStepMode.NameInUse;
                client.Listener.EnqueuePacket(PacketHeader.GuildManager, factionManifest);
            }

            else
            {
                factionManifest._stepMode = GuildStepMode.Create;

                GuildMember member = new GuildMember();
                member.Username = client.UserFile.Username;
                member.Rank = GuildMember.GuildRanks.Admin;
                member.JoinedUtcTicks = System.DateTime.UtcNow.Ticks;

                GuildFile factionFile = new GuildFile();
                factionFile.Name = factionManifest._guild.Name;
                factionFile.AddMember(member);

                client.UserFile.UpdateFaction(factionFile);

                foreach (SiteFile site in SiteManagerHelper.GetAllSitesFromUsername(client.UserFile.Username)) site.UpdateFaction(factionFile);
                SiteManagerHelper.InvalidateCache();

                client.Listener.EnqueuePacket(PacketHeader.GuildManager, factionManifest);

                InformationDisplayer.DisplayAddFaction(factionFile.Name);
                GameServer.Integrations.Discord.DiscordAnnouncer.GuildCreated(factionFile.Name, client.UserFile.Username);
            }
        }

        /// <summary>
        /// KMH 2.7: Strict validation for client-supplied guild names. Because
        /// the name is used as part of a filename in the guilds directory,
        /// the charset is intentionally restrictive — anything outside the
        /// allow-list is rejected rather than silently rewritten so the
        /// client sees a clean error instead of a name they don't recognise.
        /// </summary>
        private static bool IsValidGuildName(string raw, out string sanitised, out string rejectReason)
        {
            sanitised = null;
            rejectReason = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                rejectReason = "empty name";
                return false;
            }

            string trimmed = raw.Trim();
            if (trimmed.Length < 2) { rejectReason = "name too short (min 2)"; return false; }
            if (trimmed.Length > 32) { rejectReason = "name too long (max 32)"; return false; }

            // Defence-in-depth path-traversal check. We also block these via
            // the charset below, but reject loudly here so logs make it
            // obvious if a client tried.
            if (trimmed.Contains("..") || trimmed.Contains("/") || trimmed.Contains("\\"))
            {
                rejectReason = "name contains path-traversal characters";
                return false;
            }

            // Allow-list: letters, digits, space, and a small set of common
            // punctuation. Anything else (including control chars / non-ASCII
            // exploits) gets rejected.
            foreach (char c in trimmed)
            {
                bool ok = char.IsLetterOrDigit(c)
                    || c == ' ' || c == '-' || c == '_' || c == '.' || c == '\'';
                if (!ok)
                {
                    rejectReason = $"name contains disallowed character '{c}'";
                    return false;
                }
            }

            // Reserved Windows filenames — defensive even though we run on
            // Linux servers, in case operators sync the guilds folder across
            // platforms.
            string upper = trimmed.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL"
                || (upper.StartsWith("COM") && upper.Length == 4 && char.IsDigit(upper[3]))
                || (upper.StartsWith("LPT") && upper.Length == 4 && char.IsDigit(upper[3])))
            {
                rejectReason = "name is a reserved system filename";
                return false;
            }

            sanitised = trimmed;
            return true;
        }

        private static void DeleteFaction(ServerClient client, PKT_PlayerGuild factionManifest)
        {
            GuildFile guild = GuildManagerH.GetFactionFromName(client.UserFile.GuildName);

            if (GuildManagerH.GetMemberRank(guild, client.UserFile.Username) != GuildRanks.Admin) ResponseShortcutManager.SendNoPowerPacket(client);
            else
            {
                foreach (UserFile userFile in GuildManagerH.GetUsersFromFactionMembers(guild)) userFile.UpdateFaction(null);

                foreach (SiteFile site in GuildManagerH.GetFactionSites(guild)) site.UpdateFaction(null);
                SiteManagerHelper.InvalidateCache();

                factionManifest._stepMode = GuildStepMode.Delete;
                foreach (ServerClient toUpdateConnected in GuildManagerH.GetConnectedFactionMembers(guild))
                {
                    toUpdateConnected.UserFile.UpdateFaction(null);
                    toUpdateConnected.Listener.EnqueuePacket(PacketHeader.GuildManager, factionManifest);
                    PM_Goodwills.UpdateClientGoodwills(toUpdateConnected);
                }

                guild.Delete();

                InformationDisplayer.DisplayRemoveFaction(guild.Name);
            }
        }

        private static void InviteMemberToFaction(ServerClient client, PKT_PlayerGuild guildManifest)
        {
            // KMH 2.7: Validate inputs before dereferencing. A client can send
            // an arbitrary tile in `_dataInt` — the previous code blindly
            // followed `settlement.Username` and `toAdd.UserFile.GuildName`
            // and crashed the server thread if the tile didn't resolve to a
            // settlement or the target wasn't online.
            GuildFile guild = GuildManagerH.GetFactionFromName(client.UserFile?.GuildName);
            if (guild == null) return;

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(guildManifest._dataInt);
            if (settlement == null || string.IsNullOrEmpty(settlement.Username)) return;

            ServerClient toAdd = ServerNetwork.GetConnectedClientFromUsername(settlement.Username);
            if (toAdd == null || toAdd.UserFile == null) return;

            if (GuildManagerH.GetMemberRank(guild, client.UserFile.Username) == GuildRanks.Member)
            {
                ResponseShortcutManager.SendNoPowerPacket(client);
                return;
            }

            if (toAdd.UserFile.GuildName != null) return;

            guildManifest._guild.Name = guild.Name;
            toAdd.Listener.EnqueuePacket(PacketHeader.GuildManager, guildManifest);
        }

        private static void AddMemberToFaction(ServerClient client, PKT_PlayerGuild factionManifest)
        {
            GuildFile guild = GuildManagerH.GetFactionFromName(factionManifest._guild.Name);

            if (!GuildManagerH.CheckIfUserIsInFaction(guild, client.UserFile.Username))
            {
                GuildMember member = new GuildMember();
                member.Username = client.UserFile.Username;
                member.Rank = GuildRanks.Member;
                member.JoinedUtcTicks = System.DateTime.UtcNow.Ticks;
                guild.AddMember(member);

                client.UserFile.UpdateFaction(guild);

                foreach (SiteFile site in SiteManagerHelper.GetAllSitesFromUsername(client.UserFile.Username)) site.UpdateFaction(guild);
                SiteManagerHelper.InvalidateCache();

                foreach (ServerClient sc in GuildManagerH.GetConnectedFactionMembers(guild)) PM_Goodwills.UpdateClientGoodwills(sc);

                GameServer.Integrations.Discord.DiscordAnnouncer.GuildMemberJoined(guild.Name, client.UserFile.Username);
            }
        }

        private static void RemoveMemberFromFaction(ServerClient client, PKT_PlayerGuild guildManifest)
        {
            // KMH 2.7: Same NPE shield as InviteMemberToFaction — never trust
            // the tile to resolve to a real settlement.
            GuildFile guild = GuildManagerH.GetFactionFromName(client.UserFile?.GuildName);
            if (guild == null) return;

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(guildManifest._dataInt);
            if (settlement == null || string.IsNullOrEmpty(settlement.Username)) return;

            UserFile toRemoveOffline = UserManagerH.GetUserFileFromName(settlement.Username);
            if (toRemoveOffline == null) return;

            ServerClient toRemoveOnline = ServerNetwork.GetConnectedClientFromUsername(settlement.Username);

            GuildRanks userRank = GuildManagerH.GetMemberRank(guild, client.UserFile.Username);

            if (settlement.Username == client.UserFile.Username)
            {
                if (userRank != GuildRanks.Admin) Remove();
                else
                {
                    guildManifest._stepMode = GuildStepMode.AdminProtection;
                    client.Listener.EnqueuePacket(PacketHeader.GuildManager, guildManifest);
                }
            }

            else
            {
                if (userRank == GuildRanks.Member || userRank == GuildRanks.Moderator) ResponseShortcutManager.SendNoPowerPacket(client);
                else Remove();
            }

            void Remove()
            {
                if (toRemoveOnline != null)
                {
                    toRemoveOnline.UserFile.UpdateFaction(null);

                    PM_Goodwills.UpdateClientGoodwills(toRemoveOnline);

                    toRemoveOnline.Listener.EnqueuePacket(PacketHeader.GuildManager, guildManifest);
                }

                toRemoveOffline.UpdateFaction(null);

                guild.RemoveMember(guild.GuildMembers.FirstOrDefault(fetch => fetch.Username == toRemoveOffline.Username));

                foreach (SiteFile site in SiteManagerHelper.GetAllSitesFromUsername(toRemoveOffline.Username)) site.UpdateFaction(null);
                SiteManagerHelper.InvalidateCache();

                foreach (ServerClient member in GuildManagerH.GetConnectedFactionMembers(guild)) PM_Goodwills.UpdateClientGoodwills(member);
            }
        }

        private static void PromoteMember(ServerClient client, PKT_PlayerGuild factionManifest)
        {
            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(factionManifest._dataInt);
            GuildFile guild = GuildManagerH.GetFactionFromName(client.UserFile.GuildName);

            GuildRanks rank = GuildManagerH.GetMemberRank(guild, client.UserFile.Username);
            if (rank == GuildRanks.Member) ResponseShortcutManager.SendNoPowerPacket(client);
            else
            {
                UserFile toPromoteOffline = UserManagerH.GetUserFileFromName(settlement.Username);
                if (GuildManagerH.GetMemberRank(guild, toPromoteOffline.Username) != GuildRanks.Member) ResponseShortcutManager.SendNoPowerPacket(client);
                else
                {
                    GuildMember member = GuildManagerH.GetAllFactionMembers(guild).FirstOrDefault(fetch => fetch.Username == toPromoteOffline.Username);
                    guild.PromoteMember(member);

                    ServerClient toPromoteOnline = ServerNetwork.GetConnectedClientFromUsername(toPromoteOffline.Username);
                    if (toPromoteOnline != null) toPromoteOnline.Listener.EnqueuePacket(PacketHeader.GuildManager, factionManifest);
                }
            }
        }

        private static void DemoteMember(ServerClient client, PKT_PlayerGuild factionManifest)
        {
            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(factionManifest._dataInt);
            GuildFile guild = GuildManagerH.GetFactionFromName(client.UserFile.GuildName);

            GuildRanks rank = GuildManagerH.GetMemberRank(guild, client.UserFile.Username);
            if (rank != GuildRanks.Admin) ResponseShortcutManager.SendNoPowerPacket(client);
            else
            {
                UserFile toDemoteOffline = UserManagerH.GetUserFileFromName(settlement.Username);
                if (GuildManagerH.GetMemberRank(guild, toDemoteOffline.Username) != GuildRanks.Moderator) ResponseShortcutManager.SendNoPowerPacket(client);
                else
                {
                    GuildMember member = GuildManagerH.GetAllFactionMembers(guild).FirstOrDefault(fetch => fetch.Username == toDemoteOffline.Username);
                    guild.DemoteMember(member);

                    ServerClient toDemoteOnline = ServerNetwork.GetConnectedClientFromUsername(toDemoteOffline.Username);
                    if (toDemoteOnline != null) toDemoteOnline.Listener.EnqueuePacket(PacketHeader.GuildManager, factionManifest);
                }
            }
        }

        private static void SendMemberList(ServerClient client, PKT_PlayerGuild factionManifest)
        {
            factionManifest._guild = GuildManagerH.GetFactionFromName(client.UserFile.GuildName);
            client.Listener.EnqueuePacket(PacketHeader.GuildManager, factionManifest);
        }
    }

    public class GuildManagerH
    {
        // KMH 26.5.20.1: Guild-file cache. The previous GetAllFactions did
        // a full Directory.GetFiles + deserialize-every-file scan on EVERY
        // call — and GetFactionFromName called GetAllFactions then did a
        // LINQ FirstOrDefault. Login, PostLogin, every guild-hall action,
        // every chat message in a guild, every diplomacy lookup all hit it.
        //
        // Invalidation is automatic via GuildFile.OnSaved / OnDeleted
        // events so we don't have to remember to invalidate at every call
        // site. The static ctor subscribes once.
        private static readonly object FactionCacheLock = new object();
        private static Dictionary<string, GuildFile> _factionByName;
        private static GuildFile[] _factionsCached;
        private static bool _subscribed;

        static GuildManagerH()
        {
            EnsureSubscribed();
        }

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            GuildFile.OnSaved += _ => InvalidateFactionCache();
            GuildFile.OnDeleted += _ => InvalidateFactionCache();
        }

        public static void InvalidateFactionCache()
        {
            lock (FactionCacheLock) { _factionsCached = null; _factionByName = null; }
        }

        public static GuildFile[] GetAllFactions()
        {
            EnsureSubscribed();
            lock (FactionCacheLock)
            {
                if (_factionsCached != null) return _factionsCached;

                Dictionary<string, GuildFile> dict = new Dictionary<string, GuildFile>(StringComparer.OrdinalIgnoreCase);
                List<GuildFile> list = new List<GuildFile>();
                try
                {
                    foreach (string path in Directory.GetFiles(Master.GuildsPath))
                    {
                        try
                        {
                            GuildFile gf = Serializer.SerializeFromFile<GuildFile>(path);
                            if (gf == null || string.IsNullOrEmpty(gf.Name)) continue;
                            dict[gf.Name] = gf;
                            list.Add(gf);
                        }
                        catch { }
                    }
                }
                catch (Exception e) { Shared.Misc.Printer.Error($"[Guilds] Cache build failed: {e.Message}"); }

                _factionByName = dict;
                _factionsCached = list.ToArray();
                return _factionsCached;
            }
        }

        public static GuildFile GetFactionFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            GetAllFactions(); // ensure cache is populated
            lock (FactionCacheLock)
            {
                return _factionByName != null && _factionByName.TryGetValue(name, out GuildFile gf) ? gf : null;
            }
        }

        public static GuildMember[] GetAllFactionMembers(GuildFile file) { return file.GuildMembers.ToArray(); }

        public static bool CheckIfUserIsInFaction(GuildFile factionFile, string usernameToCheck)
        {
            return GetAllFactionMembers(factionFile).FirstOrDefault(fetch => fetch.Username == usernameToCheck) != null;
        }

        public static GuildRanks GetMemberRank(GuildFile factionFile, string usernameToCheck)
        {
            // KMH 2.7: Don't crash if the user isn't in this guild. The previous
            // implementation crashed the server thread on FirstOrDefault().Rank
            // when called with a username that didn't belong to the guild —
            // trivially triggerable by a client sending a foreign settlement
            // tile to RemoveMember / Promote / Demote.
            if (factionFile == null || string.IsNullOrEmpty(usernameToCheck))
                return GuildRanks.Member;
            GuildMember m = GetAllFactionMembers(factionFile)
                .FirstOrDefault(fetch => fetch.Username == usernameToCheck);
            return m?.Rank ?? GuildRanks.Member;
        }

        public static SiteFile[] GetFactionSites(GuildFile factionFile)
        {
            return SiteManagerHelper.GetAllSites().Where(fetch => fetch.GuildName == factionFile.Name).ToArray();
        }

        public static ServerClient[] GetConnectedFactionMembers(GuildFile factionFile)
        {
            return ServerNetwork.GetConnectedClients().Where(fetch => fetch.UserFile.GuildName == factionFile.Name).ToArray();
        }

        public static UserFile[] GetUsersFromFactionMembers(GuildFile factionFile)
        {
            return UserManagerH.GetAllUserFiles().Where(fetch => fetch.GuildName == factionFile.Name).ToArray();
        }

        public static bool CheckIfFactionExistsByName(string nameToCheck)
        {
            // KMH 26.5.20.1: O(1) lookup via the cache dictionary; was an
            // O(n) LINQ scan that allocated a new array of all factions.
            return GetFactionFromName(nameToCheck) != null;
        }
    }
}
