using GameServer.Core;
using Shared;
using Shared.Files.Economy;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameServer.Managers
{
    /// <summary>
    /// Server-side quest board.
    ///
    /// Posts escrow the bounty (silver + items) from the poster's treasury into
    /// a "house held" pool inside the quest itself. On completion the bounty is
    /// transferred to the completer's treasury. On cancel/expire it returns to
    /// the poster.
    ///
    /// All storage flows through <see cref="TreasuryManager"/> so audit trails
    /// stay consistent with the rest of the economy.
    /// </summary>
    public static class QuestManager
    {
        private const int DefaultLifetimeHours = 168; // 7 days
        private const int MaxOpenQuestsPerUser = 10;
        private const int MaxTitleLength = 80;
        private const int MaxDescriptionLength = 1024;

        private static readonly object Lock = new object();
        private static QuestBoard _cached;
        private static long _lastExpirySweepTicks;
        private static long _nextQuestId = 1;

        // -- bootstrap --

        private static QuestBoard Get()
        {
            lock (Lock)
            {
                if (_cached != null) return _cached;

                if (File.Exists(Master.QuestBoardFilePath))
                {
                    try { _cached = Serializer.SerializeFromFile<QuestBoard>(Master.QuestBoardFilePath); }
                    catch (Exception e) { Printer.Warning($"[Quests] Failed to load: {e}"); }
                }
                if (_cached == null) _cached = new QuestBoard();
                if (_cached.Quests == null) _cached.Quests = new List<QuestFile>();

                _nextQuestId = _cached.Quests.Count == 0 ? 1 : _cached.Quests.Max(q => q.Id) + 1;
                return _cached;
            }
        }

        private static void SaveLocked()
        {
            try { Serializer.SerializeToFile(Master.QuestBoardFilePath, _cached); }
            catch (Exception e) { Printer.Warning($"[Quests] Save failed: {e}"); }
        }

        // -- expiry sweep --

        private static void SweepLocked()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            if (nowTicks - _lastExpirySweepTicks < TimeSpan.FromSeconds(60).Ticks) return;
            _lastExpirySweepTicks = nowTicks;

            for (int i = 0; i < _cached.Quests.Count; i++)
            {
                QuestFile q = _cached.Quests[i];
                if (q.IsExpired(nowTicks))
                {
                    q.State = QuestState.Expired;
                    RefundEscrow(q, "expired");
                    Printer.Warning($"[Quests] #{q.Id} ({q.Title}) expired — bounty refunded to {q.PosterUsername}.");
                }
            }

            // Drop completed/expired older than 7 days from the snapshot we serve.
            // Keep them in storage 1 day so clients see "recently completed" briefly.
            long cutoff = nowTicks - TimeSpan.FromDays(1).Ticks;
            _cached.Quests.RemoveAll(q =>
                (q.State == QuestState.Completed || q.State == QuestState.Expired || q.State == QuestState.Cancelled)
                && q.CompletedUtcTicks > 0
                && q.CompletedUtcTicks < cutoff);

            SaveLocked();
        }

        // -- snapshot --

        public static QuestBoard Snapshot()
        {
            lock (Lock)
            {
                QuestBoard b = Get();
                SweepLocked();
                return new QuestBoard
                {
                    Quests = b.Quests.Select(q => q).ToList(),
                    LifetimeQuestsPosted = b.LifetimeQuestsPosted,
                    LifetimeQuestsCompleted = b.LifetimeQuestsCompleted,
                    LifetimeBountySilverPaid = b.LifetimeBountySilverPaid
                };
            }
        }

        // -- post --

        public static (bool ok, string note, QuestFile quest) PostQuest(string posterUsername, QuestFile draft)
        {
            if (string.IsNullOrEmpty(posterUsername)) return (false, "No poster.", null);
            if (draft == null) return (false, "Empty quest.", null);

            string posterTreasuryKey = TreasuryManager.ResolveKeyForUsername(posterUsername);
            if (string.IsNullOrEmpty(posterTreasuryKey)) return (false, "No treasury for poster.", null);

            // Sanitise text.
            string title = (draft.Title ?? string.Empty).Trim();
            string desc = (draft.Description ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(title)) return (false, "Quest needs a title.", null);
            if (title.Length > MaxTitleLength) title = title.Substring(0, MaxTitleLength);
            if (desc.Length > MaxDescriptionLength) desc = desc.Substring(0, MaxDescriptionLength);

            if (draft.BountySilver < 0) draft.BountySilver = 0;

            // Validate by kind.
            if (draft.Kind == QuestKind.DeliverItem)
            {
                if (string.IsNullOrEmpty(draft.TargetItemDefName)) return (false, "Pick an item to deliver.", null);
                if (draft.TargetItemQty <= 0) return (false, "Quantity must be > 0.", null);
                // KMH 26.5.20.1 security: cap client-supplied fields so a
                // hostile poster can't push 5000-char defNames (slow label
                // lookups on every render) or absurd quantities into the
                // persisted QuestBoard.json. Real defNames < 64 chars; real
                // qty rarely > 10k.
                if (draft.TargetItemDefName.Length > 64)
                    draft.TargetItemDefName = draft.TargetItemDefName.Substring(0, 64);
                if (draft.TargetItemQty > 100_000) draft.TargetItemQty = 100_000;
            }

            // KMH 26.5.20.1 security: cap bounty silver so multiplication
            // downstream can't overflow.
            if (draft.BountySilver > 10_000_000) draft.BountySilver = 10_000_000;

            lock (Lock)
            {
                QuestBoard board = Get();
                SweepLocked();

                int existing = board.Quests.Count(q =>
                    string.Equals(q.PosterUsername, posterUsername, StringComparison.OrdinalIgnoreCase) &&
                    (q.State == QuestState.Open || q.State == QuestState.Claimed || q.State == QuestState.Submitted));
                if (existing >= MaxOpenQuestsPerUser)
                    return (false, $"You already have the max {MaxOpenQuestsPerUser} active quests.", null);

                // Escrow bounty from poster's treasury.
                bool isGuild = !posterTreasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
                TreasuryFile t = TreasuryManager.GetOrCreate(posterTreasuryKey, isGuild);

                if (draft.BountySilver > 0)
                {
                    if (!TreasuryManager.TryWithdrawSilver(t, posterUsername, draft.BountySilver,
                            TreasuryTransaction.TxKind.Withdraw, "quest-escrow"))
                        return (false, $"Treasury short on silver ({draft.BountySilver} required).", null);
                }

                Dictionary<string, int> escrowedItems = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (draft.BountyItems != null)
                {
                    // KMH 26.5.20.1 security: cap the BountyItems collection
                    // size and individual entry sizes. A forged packet
                    // could otherwise pass a 100k-entry dictionary or
                    // 5000-char defName keys and burn server time / disk
                    // for the resulting persisted QuestFile.
                    int processed = 0;
                    const int MaxBountyItems = 20;
                    foreach (var kv in draft.BountyItems)
                    {
                        if (++processed > MaxBountyItems) break;
                        if (kv.Value <= 0) continue;
                        string itemKey = kv.Key ?? string.Empty;
                        if (itemKey.Length > 64) itemKey = itemKey.Substring(0, 64);
                        int itemVal = kv.Value > 1_000_000 ? 1_000_000 : kv.Value;
                        int taken = TreasuryManager.TryWithdrawItem(t, posterUsername, itemKey, itemVal,
                            TreasuryTransaction.TxKind.Withdraw, "quest-escrow");
                        if (taken < itemVal)
                        {
                            // Refund what we already took, abort.
                            if (taken > 0)
                                TreasuryManager.DepositItemForUser(posterUsername, itemKey, taken,
                                    TreasuryTransaction.TxKind.MarketplaceRefund, "quest-post-failed");
                            foreach (var prev in escrowedItems)
                                TreasuryManager.DepositItemForUser(posterUsername, prev.Key, prev.Value,
                                    TreasuryTransaction.TxKind.MarketplaceRefund, "quest-post-failed");
                            if (draft.BountySilver > 0)
                                TreasuryManager.DepositSilverForUser(posterUsername, draft.BountySilver,
                                    TreasuryTransaction.TxKind.Deposit, "quest-post-failed");
                            return (false, $"Treasury short on {itemKey}.", null);
                        }
                        escrowedItems[itemKey] = taken;
                    }
                }

                // KMH: Solo posters can't pick GuildOnly visibility — there's
                // no guild scope to honour. Quietly downgrade to Public.
                QuestVisibility visibility = draft.Visibility;
                bool isGuildPost = !posterTreasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
                if (visibility == QuestVisibility.GuildOnly && !isGuildPost)
                    visibility = QuestVisibility.Public;

                long now = DateTime.UtcNow.Ticks;
                QuestFile quest = new QuestFile
                {
                    Id = _nextQuestId++,
                    Kind = draft.Kind,
                    Visibility = visibility,
                    State = QuestState.Open,
                    PosterUsername = posterUsername,
                    PosterTreasuryKey = posterTreasuryKey,
                    Title = title,
                    Description = desc,
                    BountySilver = draft.BountySilver,
                    BountyItems = escrowedItems,
                    TargetItemDefName = draft.TargetItemDefName ?? string.Empty,
                    TargetItemQty = draft.TargetItemQty,
                    TargetTreasuryKey = string.IsNullOrEmpty(draft.TargetTreasuryKey) ? posterTreasuryKey : draft.TargetTreasuryKey,
                    PostedUtcTicks = now,
                    ExpiresUtcTicks = now + TimeSpan.FromHours(DefaultLifetimeHours).Ticks
                };

                board.Quests.Add(quest);
                board.LifetimeQuestsPosted += 1;
                SaveLocked();

                // KMH 2.7: Lifetime stats for the player leaderboard.
                try { PlayerStatsManager.RecordQuestPosted(posterUsername); } catch { }

                return (true, $"Posted quest #{quest.Id}: {quest.Title}.", quest);
            }
        }

        // -- claim / abandon --

        public static (bool ok, string note) Claim(string username, long questId)
        {
            if (string.IsNullOrEmpty(username)) return (false, "No user.");
            lock (Lock)
            {
                QuestFile q = Get().Quests.FirstOrDefault(x => x.Id == questId);
                if (q == null) return (false, "Quest not found.");
                if (q.State != QuestState.Open) return (false, "Quest is not open.");
                if (string.Equals(q.PosterUsername, username, StringComparison.OrdinalIgnoreCase))
                    return (false, "You can't claim your own quest.");

                // KMH: Server-side visibility check — prevents claim-by-id
                // from bypassing guild-only filtering on the client.
                if (q.Visibility == QuestVisibility.GuildOnly)
                {
                    var uf = UserManagerH.GetUserFileFromName(username);
                    string viewerGuild = uf?.GuildName ?? string.Empty;
                    if (!q.IsVisibleTo(username, viewerGuild))
                        return (false, "This quest isn't open to you.");
                }

                q.State = QuestState.Claimed;
                q.ClaimedByUsername = username;
                q.ClaimedUtcTicks = DateTime.UtcNow.Ticks;
                SaveLocked();
                return (true, $"Claimed quest #{q.Id}.");
            }
        }

        public static (bool ok, string note) Abandon(string username, long questId)
        {
            if (string.IsNullOrEmpty(username)) return (false, "No user.");
            lock (Lock)
            {
                QuestFile q = Get().Quests.FirstOrDefault(x => x.Id == questId);
                if (q == null) return (false, "Quest not found.");
                if (q.State != QuestState.Claimed && q.State != QuestState.Submitted) return (false, "Quest is not claimed.");
                if (!string.Equals(q.ClaimedByUsername, username, StringComparison.OrdinalIgnoreCase))
                    return (false, "Only the claimer can abandon.");

                q.State = QuestState.Open;
                q.ClaimedByUsername = string.Empty;
                q.ClaimedUtcTicks = 0;
                SaveLocked();
                return (true, $"Abandoned quest #{q.Id} — back on the board.");
            }
        }

        // -- delivery / completion --

        /// <summary>
        /// Submit a delivery for a DeliverItem quest. The server moves items from
        /// the claimer's treasury to the target treasury. Caller has already
        /// deducted from caravan client-side or stored to treasury.
        /// </summary>
        public static (bool ok, string note) SubmitDelivery(string username, long questId)
        {
            if (string.IsNullOrEmpty(username)) return (false, "No user.");
            lock (Lock)
            {
                QuestFile q = Get().Quests.FirstOrDefault(x => x.Id == questId);
                if (q == null) return (false, "Quest not found.");
                if (q.Kind != QuestKind.DeliverItem) return (false, "Wrong quest type.");
                if (q.State != QuestState.Claimed && q.State != QuestState.Open) return (false, "Quest not in claimable state.");
                if (q.State == QuestState.Claimed && !string.Equals(q.ClaimedByUsername, username, StringComparison.OrdinalIgnoreCase))
                    return (false, "Quest is claimed by someone else.");

                // Pull items from completer's treasury (where the client should have deposited).
                string completerKey = TreasuryManager.ResolveKeyForUsername(username);
                if (string.IsNullOrEmpty(completerKey)) return (false, "No treasury found for completer.");
                bool completerIsGuild = !completerKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
                TreasuryFile completerT = TreasuryManager.GetOrCreate(completerKey, completerIsGuild);

                int taken = TreasuryManager.TryWithdrawItem(completerT, username, q.TargetItemDefName, q.TargetItemQty,
                    TreasuryTransaction.TxKind.Withdraw, $"quest#{q.Id}-submit");

                if (taken < q.TargetItemQty)
                {
                    if (taken > 0)
                        TreasuryManager.DepositItemForUser(username, q.TargetItemDefName, taken,
                            TreasuryTransaction.TxKind.MarketplaceRefund, $"quest#{q.Id}-shortfall");
                    // KMH 2.7: Friendly label instead of raw defName.
                    string itemLabel = ItemLabelCache.LabelFor(q.TargetItemDefName);
                    return (false, $"Treasury only has {taken}/{q.TargetItemQty} {itemLabel}. Deposit them and try again.");
                }

                // Deliver to target treasury.
                bool targetIsGuild = !q.TargetTreasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
                TreasuryFile targetT = TreasuryManager.GetOrCreate(q.TargetTreasuryKey, targetIsGuild);

                // Direct dictionary update (server-managed).
                lock (TreasuryLock(targetT))
                {
                    if (!targetT.Items.TryGetValue(q.TargetItemDefName, out int cur)) cur = 0;
                    targetT.Items[q.TargetItemDefName] = cur + q.TargetItemQty;
                    targetT.RecordTransaction(new TreasuryTransaction
                    {
                        UtcTicks = DateTime.UtcNow.Ticks,
                        Username = username,
                        Kind = TreasuryTransaction.TxKind.SiteRewardItem,
                        Amount = q.TargetItemQty,
                        ItemDefName = q.TargetItemDefName,
                        Note = $"quest#{q.Id} delivery"
                    });
                    TreasuryManager.Save(targetT);
                }

                PayoutBounty(q, username);
                q.State = QuestState.Completed;
                q.CompletedUtcTicks = DateTime.UtcNow.Ticks;
                Get().LifetimeQuestsCompleted += 1;
                Get().LifetimeBountySilverPaid += q.BountySilver;

                SaveLocked();

                // KMH 2.7: Lifetime stats — completer gets credit.
                try { PlayerStatsManager.RecordQuestCompleted(username); } catch { }

                return (true, $"Delivered. Bounty paid to your treasury.");
            }
        }

        public static (bool ok, string note) ConfirmBountyCompletion(string posterUsername, long questId, string completerUsername)
        {
            if (string.IsNullOrEmpty(posterUsername)) return (false, "No poster.");
            lock (Lock)
            {
                QuestFile q = Get().Quests.FirstOrDefault(x => x.Id == questId);
                if (q == null) return (false, "Quest not found.");
                if (q.Kind != QuestKind.Bounty) return (false, "Wrong quest type.");
                if (!string.Equals(q.PosterUsername, posterUsername, StringComparison.OrdinalIgnoreCase))
                    return (false, "Only the poster can confirm.");

                string toPay = !string.IsNullOrEmpty(completerUsername) ? completerUsername : q.ClaimedByUsername;
                if (string.IsNullOrEmpty(toPay)) return (false, "Nobody to pay (quest was never claimed).");

                PayoutBounty(q, toPay);
                q.State = QuestState.Completed;
                q.CompletedUtcTicks = DateTime.UtcNow.Ticks;
                Get().LifetimeQuestsCompleted += 1;
                Get().LifetimeBountySilverPaid += q.BountySilver;

                SaveLocked();

                // KMH 2.7: Lifetime stats — completer (or claimer) gets credit.
                try { PlayerStatsManager.RecordQuestCompleted(toPay); } catch { }

                return (true, $"Quest #{q.Id} marked complete; bounty paid to {toPay}.");
            }
        }

        public static (bool ok, string note) Cancel(string posterUsername, long questId)
        {
            if (string.IsNullOrEmpty(posterUsername)) return (false, "No poster.");
            lock (Lock)
            {
                QuestFile q = Get().Quests.FirstOrDefault(x => x.Id == questId);
                if (q == null) return (false, "Quest not found.");
                if (!string.Equals(q.PosterUsername, posterUsername, StringComparison.OrdinalIgnoreCase))
                    return (false, "Only the poster can cancel.");
                if (q.State == QuestState.Completed || q.State == QuestState.Cancelled || q.State == QuestState.Expired)
                    return (false, "Quest already finalised.");

                q.State = QuestState.Cancelled;
                q.CompletedUtcTicks = DateTime.UtcNow.Ticks;
                RefundEscrow(q, "cancelled");
                SaveLocked();
                return (true, $"Cancelled quest #{q.Id}; bounty refunded to your treasury.");
            }
        }

        // -- helpers --

        private static void PayoutBounty(QuestFile q, string completerUsername)
        {
            if (q.BountySilver > 0)
                TreasuryManager.DepositSilverForUser(completerUsername, q.BountySilver,
                    TreasuryTransaction.TxKind.Deposit, $"quest#{q.Id}-bounty");

            if (q.BountyItems != null)
            {
                foreach (var kv in q.BountyItems)
                {
                    if (kv.Value <= 0) continue;
                    TreasuryManager.DepositItemForUser(completerUsername, kv.Key, kv.Value,
                        TreasuryTransaction.TxKind.Deposit, $"quest#{q.Id}-bounty");
                }
            }
        }

        private static void RefundEscrow(QuestFile q, string reason)
        {
            if (q.BountySilver > 0)
                TreasuryManager.DepositSilverForUser(q.PosterUsername, q.BountySilver,
                    TreasuryTransaction.TxKind.Deposit, $"quest#{q.Id}-{reason}-refund");

            if (q.BountyItems != null)
            {
                foreach (var kv in q.BountyItems)
                {
                    if (kv.Value <= 0) continue;
                    TreasuryManager.DepositItemForUser(q.PosterUsername, kv.Key, kv.Value,
                        TreasuryTransaction.TxKind.Deposit, $"quest#{q.Id}-{reason}-refund");
                }
            }
        }

        // No real treasury-instance lock, just route through the file's surface API
        // — this exists to make intent visible at the call site above.
        private static object TreasuryLock(TreasuryFile t) => t;
    }
}
