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
        // per-player lifetime stats leaderboard
        PlayerStatsManager,
        // client-pushed defName → human label cache (kills the
        // raw-defName look in Discord/server-side messaging)
        ItemLabelManager,
        // Reserved for the upstream VersionDownloader
        // subsystem (server-side hosted mod-version downloads on a
        // dedicated TCP endpoint). Placed at the END of the enum so
        // KMH's existing economy/Discord headers keep their byte
        // positions — older clients/UserFiles deserialise without
        // shifting. The handler itself isn't wired yet (groundwork only),
        // but reserving the byte lets KMH ship it without breaking
        // pre-26.5.22.1 clients on the wire.
        VersionDownload
    }
}