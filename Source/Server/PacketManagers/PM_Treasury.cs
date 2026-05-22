using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Files.Economy;
using Shared.Files.Guilds;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Server-side handler for treasury operations: snapshot/deposit/withdraw.
    /// Permission decisions are delegated to <see cref="TreasuryManager"/>.
    ///
    /// Items in deposit packets are deducted from the player's caravan client-side
    /// (the response signals success). For withdrawals the server drives delivery
    /// via the existing PM_Sites reward path — items are sent back to the client
    /// in a Snapshot reply that includes the freshly-deducted bundle.
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
                case PKT_Treasury.StepMode.Request: SendSnapshot(client, data); break;
                case PKT_Treasury.StepMode.Deposit: HandleDeposit(client, data); break;
                case PKT_Treasury.StepMode.Withdraw: HandleWithdraw(client, data); break;
            }
        }

        // -- helpers --

        private static TreasuryFile ResolveTreasury(ServerClient client, string requestedKey)
        {
            string key = string.IsNullOrEmpty(requestedKey)
                ? TreasuryManager.ResolveKeyForUser(client)
                : requestedKey;
            if (string.IsNullOrEmpty(key)) return null;
            bool isGuild = !key.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
            return TreasuryManager.GetOrCreate(key, isGuild);
        }

        private static void SendSnapshot(ServerClient client, PKT_Treasury request, string overrideNote = null)
        {
            TreasuryFile t = ResolveTreasury(client, request?.OwnerKey);
            if (t == null)
            {
                Reply(client, new PKT_Treasury { CurrentStep = PKT_Treasury.StepMode.Result, Note = "Treasury not found." });
                return;
            }

            // Permission check — players can only snapshot a treasury they have access to.
            if (!TreasuryManager.ClientCanAccessTreasury(client, t, needsWithdrawPermission: false))
            {
                Reply(client, new PKT_Treasury { CurrentStep = PKT_Treasury.StepMode.Result, Note = "Permission denied." });
                return;
            }

            GuildMember.GuildRanks? rank = t.IsGuildOwned
                ? TreasuryManager.ResolveRankInGuild(client.UserFile.Username, t.OwnerKey)
                : null;

            PKT_Treasury reply = new PKT_Treasury
            {
                CurrentStep = PKT_Treasury.StepMode.Snapshot,
                OwnerKey = t.OwnerKey,
                IsGuildOwned = t.IsGuildOwned,
                SilverAmount = t.SilverBalance,
                ItemBundle = new Dictionary<string, int>(t.Items, StringComparer.OrdinalIgnoreCase),
                LifetimeSilverIn = t.LifetimeSilverIn,
                LifetimeSilverOut = t.LifetimeSilverOut,
                RecentTransactions = new List<TreasuryTransaction>(t.RecentTransactions),
                CanDeposit = t.CanDeposit(client.UserFile.Username, rank),
                CanWithdraw = t.CanWithdraw(client.UserFile.Username, rank),
                Note = overrideNote ?? string.Empty
            };
            Reply(client, reply);
        }

        private static void HandleDeposit(ServerClient client, PKT_Treasury data)
        {
            TreasuryFile t = ResolveTreasury(client, data.OwnerKey);
            if (t == null) { Reply(client, ResultPacket("Treasury not found.")); return; }

            if (!TreasuryManager.ClientCanAccessTreasury(client, t, needsWithdrawPermission: false))
            { Reply(client, ResultPacket("Permission denied.")); return; }

            string username = client.UserFile.Username;

            if (data.SilverAmount > 0)
                TreasuryManager.DepositSilverForUser(username, data.SilverAmount, TreasuryTransaction.TxKind.Deposit, "manual");

            if (data.ItemBundle != null)
            {
                // KMH 26.5.20.1 security: cap entry count + per-entry sizes
                // so a forged packet can't trigger 100k disk writes via
                // 100k separate deposit calls.
                int processed = 0;
                const int MaxDepositEntries = 64;
                foreach (var kv in data.ItemBundle)
                {
                    if (++processed > MaxDepositEntries) break;
                    if (kv.Value <= 0) continue;
                    string key = kv.Key ?? string.Empty;
                    if (key.Length > 96) key = key.Substring(0, 96);
                    int amt = kv.Value > 1_000_000 ? 1_000_000 : kv.Value;
                    TreasuryManager.DepositItemForUser(username, key, amt, TreasuryTransaction.TxKind.Deposit, "manual");
                }
            }

            // Reply with fresh snapshot so the client UI can refresh + so the
            // client can deduct the deposited amounts from the local caravan.
            SendSnapshot(client, data, overrideNote: "Deposited.");

            // KMH: Push update to every other watcher of this treasury.
            BroadcastTreasurySnapshot(t.OwnerKey);
        }

        private static void HandleWithdraw(ServerClient client, PKT_Treasury data)
        {
            TreasuryFile t = ResolveTreasury(client, data.OwnerKey);
            if (t == null) { Reply(client, ResultPacket("Treasury not found.")); return; }

            if (!TreasuryManager.ClientCanAccessTreasury(client, t, needsWithdrawPermission: true))
            { Reply(client, ResultPacket("Permission denied — moderator+ required for guild treasuries.")); return; }

            string username = client.UserFile.Username;
            Dictionary<string, int> actuallyWithdrew = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int silverOut = 0;
            if (data.SilverAmount > 0)
            {
                // KMH: Enforce per-rank daily silver withdraw cap on guild treasuries.
                if (t.IsGuildOwned)
                {
                    var (capOk, capNote) = GuildManager.ReserveDailyWithdraw(username, t.OwnerKey, data.SilverAmount);
                    if (!capOk)
                    {
                        Reply(client, ResultPacket(capNote));
                        SendSnapshot(client, data);
                        return;
                    }
                }

                if (TreasuryManager.TryWithdrawSilver(t, username, data.SilverAmount, TreasuryTransaction.TxKind.Withdraw, "manual"))
                    silverOut = data.SilverAmount;
            }

            if (data.ItemBundle != null)
            {
                // KMH 26.5.20.1 security: same caps as deposit path.
                int processed = 0;
                const int MaxWithdrawEntries = 64;
                foreach (var kv in data.ItemBundle)
                {
                    if (++processed > MaxWithdrawEntries) break;
                    if (kv.Value <= 0) continue;
                    string key = kv.Key ?? string.Empty;
                    if (key.Length > 96) key = key.Substring(0, 96);
                    int amt = kv.Value > 1_000_000 ? 1_000_000 : kv.Value;
                    int taken = TreasuryManager.TryWithdrawItem(t, username, key, amt, TreasuryTransaction.TxKind.Withdraw, "manual");
                    if (taken > 0) actuallyWithdrew[key] = taken;
                }
            }

            // Reply contains the actual withdrawn amounts so the client can spawn them in caravan.
            PKT_Treasury reply = new PKT_Treasury
            {
                CurrentStep = PKT_Treasury.StepMode.Result,
                OwnerKey = t.OwnerKey,
                IsGuildOwned = t.IsGuildOwned,
                SilverAmount = silverOut,
                ItemBundle = actuallyWithdrew,
                Note = "Withdraw delivered."
            };
            Reply(client, reply);

            // Then push a fresh snapshot so dialog refreshes.
            SendSnapshot(client, data);

            // KMH: Notify other guild watchers as well.
            BroadcastTreasurySnapshot(t.OwnerKey);
        }

        private static PKT_Treasury ResultPacket(string note) =>
            new PKT_Treasury { CurrentStep = PKT_Treasury.StepMode.Result, Note = note };

        private static void Reply(ServerClient client, PKT_Treasury packet)
        {
            client.Listener.EnqueuePacket(PacketHeader.TreasuryManager, packet);
        }

        /// <summary>
        /// KMH: Push a fresh treasury snapshot to every online client who can
        /// see this treasury (the personal owner, or every guild member when
        /// it's a guild-owned vault). Call this after any state-changing
        /// operation so all watchers refresh in real time.
        /// </summary>
        public static void BroadcastTreasurySnapshot(string ownerKey)
        {
            if (string.IsNullOrEmpty(ownerKey)) return;
            try
            {
                bool isGuild = !ownerKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
                TreasuryFile t = TreasuryManager.GetOrCreate(ownerKey, isGuild);
                if (t == null) return;

                foreach (ServerClient sc in ServerNetwork.GetConnectedClients())
                {
                    string username = sc?.UserFile?.Username;
                    if (string.IsNullOrEmpty(username)) continue;

                    // Eligibility: personal owner, or guild member (any rank — read access).
                    bool eligible;
                    if (!isGuild)
                        eligible = string.Equals(ownerKey, TreasuryFile.PersonalKeyFor(username), StringComparison.OrdinalIgnoreCase);
                    else
                        eligible = string.Equals(sc.UserFile.GuildName, ownerKey, StringComparison.OrdinalIgnoreCase);
                    if (!eligible) continue;

                    GuildMember.GuildRanks? rank = isGuild
                        ? TreasuryManager.ResolveRankInGuild(username, ownerKey)
                        : null;

                    PKT_Treasury snap = new PKT_Treasury
                    {
                        CurrentStep = PKT_Treasury.StepMode.Snapshot,
                        OwnerKey = t.OwnerKey,
                        IsGuildOwned = t.IsGuildOwned,
                        SilverAmount = t.SilverBalance,
                        ItemBundle = new Dictionary<string, int>(t.Items, StringComparer.OrdinalIgnoreCase),
                        LifetimeSilverIn = t.LifetimeSilverIn,
                        LifetimeSilverOut = t.LifetimeSilverOut,
                        RecentTransactions = new List<TreasuryTransaction>(t.RecentTransactions),
                        CanDeposit = t.CanDeposit(username, rank),
                        CanWithdraw = t.CanWithdraw(username, rank)
                    };
                    sc.Listener.EnqueuePacket(PacketHeader.TreasuryManager, snap);
                }
            }
            catch (Exception e) { Printer.Warning($"[Treasury] BroadcastTreasurySnapshot failed: {e}"); }
        }
    }
}
