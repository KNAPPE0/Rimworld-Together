﻿using Shared;
using System.Collections.Generic;

namespace GameClient
{
    public static class ServerValues
    {
        public static bool AllowCustomScenarios;

        public static bool isAdmin;

        public static bool hasFaction;

        public static int currentPlayers;

        public static List<string> currentPlayerNames = new List<string>();

        public static void SetServerParameters(ServerGlobalData serverGlobalData)
        {
            AllowCustomScenarios = serverGlobalData.AllowCustomScenarios;
        }

        public static void SetAccountData(ServerGlobalData serverGlobalData)
        {
            isAdmin = serverGlobalData.IsClientAdmin;

            hasFaction = serverGlobalData.IsClientFactionMember;
        }

        public static void SetServerPlayers(Packet packet)
        {
            PlayerRecountData playerRecountData = Serializer.ConvertBytesToObject<PlayerRecountData>(packet.Contents);
            currentPlayers = int.Parse(playerRecountData.CurrentPlayers);
            currentPlayerNames = playerRecountData.CurrentPlayerNames;
        }

        public static void CleanValues()
        {
            AllowCustomScenarios = false;

            isAdmin = false;

            hasFaction = false;

            currentPlayers = 0;

            currentPlayerNames.Clear();
        }
    }
}
