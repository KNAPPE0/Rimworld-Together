using System;

namespace Shared
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class HandlesPacket : Attribute
    {
        public HandlesPacket(PacketHeader header)
        {
            this.header = header;
        }

        public readonly PacketHeader header;
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ManagesPacket : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class OnSessionStart : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class OnSessionEnd : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class OnSynchronousStart : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class OnSynchronousEnd : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class OnSynchronousUpdate : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class OnUpdate : Attribute { }

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
        VersionManager,
        WorldManager,
        PollutionManager,
        ConsoleManager,
        GlobalDataManager,
        ResponseShortcutManager,
        RecountManager,
        InformationManager,
        LeaderboardManager,
        ServerBrowserTelemetry,
        ServerBrowserListing,
        SynchronousManager,
        ServerBrowserReachability,
        DisconnectManager,
        // KMH: economy
        TreasuryManager,
        MarketplaceManager,
        QuestManager,
        GuildHallManager,
        LinkedAccountsManager,
        PlayerStatsManager,
        ItemLabelManager,
        VersionDownload
    }
}