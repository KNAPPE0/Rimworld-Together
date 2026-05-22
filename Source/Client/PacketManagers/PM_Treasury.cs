using GameClient.Managers;
using GameClient.Misc;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using Verse;
using static Shared.Misc.Printer;

namespace GameClient.PacketManagers
{
    /// <summary>
    /// Client-side treasury packet handler.
    /// Snapshots refresh the cache; result-on-deposit deducts from caravan;
    /// result-on-withdraw spawns withdrawn silver/items into the caravan.
    /// </summary>
    public class PM_Treasury : PM_Base
    {
        [HandlesPacket(PacketHeader.TreasuryManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Treasury data = Serializer.ConvertBytesToObject<PKT_Treasury>(bytes);
            if (data == null) return;

            switch (data.CurrentStep)
            {
                case PKT_Treasury.StepMode.Snapshot:
                    TreasuryClientCache.Apply(data);
                    break;

                case PKT_Treasury.StepMode.Result:
                    HandleResult(data);
                    break;
            }
        }

        // -- senders --

        public static void RequestSnapshot(string ownerKey = null)
        {
            try
            {
                PKT_Treasury req = new PKT_Treasury
                {
                    CurrentStep = PKT_Treasury.StepMode.Request,
                    OwnerKey = ownerKey ?? string.Empty
                };
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.TreasuryManager, req);
            }
            catch { }
        }

        public static void Deposit(string ownerKey, int silver, Dictionary<string, int> items)
        {
            try
            {
                PKT_Treasury req = new PKT_Treasury
                {
                    CurrentStep = PKT_Treasury.StepMode.Deposit,
                    OwnerKey = ownerKey ?? string.Empty,
                    SilverAmount = Math.Max(0, silver),
                    ItemBundle = items ?? new Dictionary<string, int>()
                };
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.TreasuryManager, req);

                // Deduct from caravan client-side. The server confirms via snapshot;
                // if it rejects (rare — perm check) we won't get a snapshot back.
                Caravan caravan = SessionHandler.ChosenCaravan;
                if (caravan != null)
                {
                    if (silver > 0)
                        TryRemoveFromCaravan(caravan, ThingDefOf.Silver.defName, silver);
                    if (items != null)
                        foreach (var kv in items)
                            if (kv.Value > 0)
                                TryRemoveFromCaravan(caravan, kv.Key, kv.Value);
                }
            }
            catch { }
        }

        public static void Withdraw(string ownerKey, int silver, Dictionary<string, int> items)
        {
            try
            {
                PKT_Treasury req = new PKT_Treasury
                {
                    CurrentStep = PKT_Treasury.StepMode.Withdraw,
                    OwnerKey = ownerKey ?? string.Empty,
                    SilverAmount = Math.Max(0, silver),
                    ItemBundle = items ?? new Dictionary<string, int>()
                };
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.TreasuryManager, req);
            }
            catch { }
        }

        // -- on result --

        private static void HandleResult(PKT_Treasury data)
        {
            // Deposit/withdraw both come back as Result. For withdraws, spawn the items.
            if (data.SilverAmount <= 0 && (data.ItemBundle == null || data.ItemBundle.Count == 0))
                return;

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            IntVec3 position = RimworldManager.GetTransferLocationInMap(map);

            try
            {
                if (data.SilverAmount > 0)
                {
                    SpawnInMap(ThingDefOf.Silver.defName, data.SilverAmount, map, position);
                }

                if (data.ItemBundle != null)
                {
                    foreach (var kv in data.ItemBundle)
                    {
                        if (kv.Value <= 0) continue;
                        SpawnInMap(kv.Key, kv.Value, map, position);
                    }
                }

                if (!string.IsNullOrEmpty(data.Note))
                    Printer.Message(data.Note, LogImportanceMode.Verbose);
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void SpawnInMap(string defName, int amount, Map map, IntVec3 position)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            Thing toMake = ThingMaker.MakeThing(def);
            toMake.stackCount = amount;
            toMake.HitPoints = def.BaseMaxHitPoints;
            RimworldManager.PlaceThingIntoMap(toMake, map, position, true);
        }

        private static void TryRemoveFromCaravan(Caravan caravan, string defName, int amount)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            if (RimworldManager.CheckIfHasEnoughItemInCaravan(caravan, defName, amount))
                RimworldManager.RemoveThingFromCaravan(caravan, def, amount);
        }
    }
}
