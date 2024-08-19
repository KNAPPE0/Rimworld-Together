﻿using Shared;
using System.Reflection;

namespace GameServer
{
    // Class that handles the management of all the received packets
    public static class PacketHandler
    {
        // Packet headers in this array won't output into the logs by default
        private static readonly string[] ignoreLogPackets =
        {
            nameof(KeepAlivePacket),
            nameof(OnlineActivityPacket)
        };

        // Function that handles the action that the packet should perform, then sends it to the correct method below
        public static void HandlePacket(ServerClient client, Packet packet)
        {
            if (Master.serverConfig.VerboseLogs && !ignoreLogPackets.Contains(packet.Header)) 
                Logger.Message($"[H] > {packet.Header}");
            else if (Master.serverConfig.ExtremeVerboseLogs) 
                Logger.Message($"[H] > {packet.Header}");

            client.Listener.KAFlag = true;

            Type toUse = typeof(PacketHandler);
            MethodInfo methodInfo = toUse.GetMethod(packet.Header);

            if (methodInfo != null)
            {
                try
                {
                    methodInfo.Invoke(null, new object[] { client, packet });
                }
                catch (Exception ex)
                {
                    Logger.Error($"[PacketHandler] > Error invoking method '{packet.Header}': {ex.Message}");
                }
            }
            else
            {
                Logger.Warning($"[PacketHandler] > No method found for packet header '{packet.Header}'");
            }
        }

        // Method definitions for handling specific packet types
        public static void LoginClientPacket(ServerClient client, Packet packet) => UserLogin.TryLoginUser(client, packet);

        public static void RegisterClientPacket(ServerClient client, Packet packet) => UserRegister.TryRegisterUser(client, packet);

        public static void RequestSavePartPacket(ServerClient client, Packet packet) => SaveManager.SendSavePartToClient(client);

        public static void ReceiveSavePartPacket(ServerClient client, Packet packet) => SaveManager.ReceiveSavePartFromClient(client, packet);

        public static void GoodwillPacket(ServerClient client, Packet packet) => GoodwillManager.ChangeUserGoodwills(client, packet);

        public static void TransferPacket(ServerClient client, Packet packet) => TransferManager.ParseTransferPacket(client, packet);

        public static void MarketPacket(ServerClient client, Packet packet) => MarketManager.ParseMarketPacket(client, packet);

        public static void AidPacket(ServerClient client, Packet packet) => AidManager.ParsePacket(client, packet);

        public static void SitePacket(ServerClient client, Packet packet) => SiteManager.ParseSitePacket(client, packet);

        public static void RoadPacket(ServerClient client, Packet packet) => RoadManager.ParsePacket(client, packet);

        public static void CaravanPacket(ServerClient client, Packet packet) => CaravanManager.ParsePacket(client, packet);

        public static void OnlineActivityPacket(ServerClient client, Packet packet) => OnlineActivityManager.ParseOnlineActivityPacket(client, packet);

        public static void OfflineActivityPacket(ServerClient client, Packet packet) => OfflineActivityManager.ParseOfflineActivityPacket(client, packet);

        public static void ChatPacket(ServerClient client, Packet packet) => ChatManager.ParsePacket(client, packet);

        public static void FactionPacket(ServerClient client, Packet packet) => FactionManager.ParseFactionPacket(client, packet);

        public static void MapPacket(ServerClient client, Packet packet) => MapManager.SaveUserMap(client, packet);

        public static void SettlementPacket(ServerClient client, Packet packet) => SettlementManager.ParseSettlementPacket(client, packet);

        public static void NPCSettlementPacket(ServerClient client, Packet packet) => NPCSettlementManager.ParsePacket(client, packet);

        public static void EventPacket(ServerClient client, Packet packet) => EventManager.ParseEventPacket(client, packet);

        public static void WorldPacket(ServerClient client, Packet packet) => WorldManager.ParseWorldPacket(client, packet);

        public static void CustomDifficultyPacket(ServerClient client, Packet packet) => CustomDifficultyManager.ParseDifficultyPacket(client, packet);

        public static void ResetSavePacket(ServerClient client, Packet packet) => SaveManager.ResetClientSave(client);

        // Empty functions
        public static void KeepAlivePacket(ServerClient client, Packet packet) { /* Empty */ }

        public static void UserUnavailablePacket() { /* Empty */ }

        public static void IllegalActionPacket() { /* Empty */ }

        public static void BreakPacket() { /* Empty */ }

        public static void PlayerRecountPacket() { /* Empty */ }

        public static void ServerValuesPacket() { /* Empty */ }

        public static void CommandPacket() { /* Empty */ }

        public static void LoginResponsePacket() { /* Empty */ }
    }
}