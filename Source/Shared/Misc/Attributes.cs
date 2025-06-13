// File: Attributes.cs  (Shared File)
using System;

namespace Shared
{
    // Used for loading in custom assemblies
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RTStartupAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class HandlesPacket : Attribute
    {
        public HandlesPacket(PacketHeader header)
        {
            this.header = header;
        }
        public readonly PacketHeader header;
    }

    public enum PacketHeader : byte
    {
        None,
        KeepAliveManager,
        LoginManager,
        TransferManager,
        ActivityManager,
        AidManager,
        CaravanManager,
        ChatManager,
        WealthManager,
        EventManager,
        GameParameterManager,
        GoodWillManager,
        GuildManager,
        MapManager,
        ModManager,
        NPCManager,
        RoadManager,
        SaveManager,
        SettlementManager,
        SiteManager,
        StatsManager,
        ConsoleManager,
        LeaderboardRequest,
        LeaderboardResponse,
        GlobalDataManager,
        RecountManager,
        VersionManager,
        WorldManager,
        PollutionManager,
        ResponseShortcutManager
    }
}