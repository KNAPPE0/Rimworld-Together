using GameClient.Dialogs.Default;
using GameClient.PacketManagers;
using Shared.Files.Guilds;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Admin-only edit screen for <see cref="GuildSettings"/>. Tax rates,
    /// withdraw caps, and MOTD all live here.
    /// </summary>
    public class DLG_GuildSettings : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(600f, 540f);

        private GuildSettings _editing;

        // KMH 26.5.20.1: Per-field text buffers so the user can fully clear
        // a field and retype it without the displayed integer constantly
        // re-echoing into the input. Initialised from the source values
        // on first DoWindowContents.
        private string _siteTaxBuf;
        private string _marketTaxBuf;
        private string _memberCapBuf;
        private string _officerCapBuf;
        private string _modCapBuf;
        private string _adminCapBuf;
        private bool _buffersInit;

        public DLG_GuildSettings(GuildSettings current)
        {
            // Work on a copy so cancel doesn't mutate the cache.
            _editing = current == null ? new GuildSettings() : new GuildSettings
            {
                MessageOfTheDay = current.MessageOfTheDay,
                SiteRewardSilverTaxPercent = current.SiteRewardSilverTaxPercent,
                MarketplaceSaleTaxPercent = current.MarketplaceSaleTaxPercent,
                MemberDailyWithdrawCap = current.MemberDailyWithdrawCap,
                OfficerDailyWithdrawCap = current.OfficerDailyWithdrawCap,
                ModeratorDailyWithdrawCap = current.ModeratorDailyWithdrawCap,
                AdminDailyWithdrawCap = current.AdminDailyWithdrawCap,
                DefaultListingsGuildOnly = current.DefaultListingsGuildOnly
            };

            Title = "Guild Settings";
            closeOnCancel = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            // KMH 26.5.20.1: Initialise the text buffers on first draw with
            // the current values. Subsequent draws preserve whatever the
            // user typed, so they can clear the field and retype freely.
            if (!_buffersInit)
            {
                _siteTaxBuf = _editing.SiteRewardSilverTaxPercent.ToString();
                _marketTaxBuf = _editing.MarketplaceSaleTaxPercent.ToString();
                _memberCapBuf = _editing.MemberDailyWithdrawCap.ToString();
                _officerCapBuf = _editing.OfficerDailyWithdrawCap.ToString();
                _modCapBuf = _editing.ModeratorDailyWithdrawCap.ToString();
                _adminCapBuf = _editing.AdminDailyWithdrawCap.ToString();
                _buffersInit = true;
            }

            float y = DialogLayout.DrawTitle(rect, "Edit Guild Settings");
            DialogLayout.DrawSectionDivider(rect, ref y);

            Widgets.Label(new Rect(0f, y, 220f, 22f), "Message of the Day:");
            _editing.MessageOfTheDay = Widgets.TextArea(
                new Rect(220f, y, rect.width - 220f, 60f),
                _editing.MessageOfTheDay ?? "");
            y += 70f;

            int siteTax = _editing.SiteRewardSilverTaxPercent;
            y = DrawBufferedIntField(y, "Site Reward Tax %", ref _siteTaxBuf, ref siteTax, 0, 50);
            _editing.SiteRewardSilverTaxPercent = siteTax;

            int marketTax = _editing.MarketplaceSaleTaxPercent;
            y = DrawBufferedIntField(y, "Marketplace Sale Tax %", ref _marketTaxBuf, ref marketTax, 0, 50);
            _editing.MarketplaceSaleTaxPercent = marketTax;

            DialogLayout.DrawSectionDivider(rect, ref y);
            Widgets.Label(new Rect(0f, y, rect.width, 20f), "<b>Daily silver withdraw caps</b>  <color=grey>(-1 = unlimited)</color>");
            y += 24f;

            int memberCap = _editing.MemberDailyWithdrawCap;
            y = DrawBufferedIntField(y, "Member cap", ref _memberCapBuf, ref memberCap, -1, int.MaxValue);
            _editing.MemberDailyWithdrawCap = memberCap;

            int officerCap = _editing.OfficerDailyWithdrawCap;
            y = DrawBufferedIntField(y, "Officer cap", ref _officerCapBuf, ref officerCap, -1, int.MaxValue);
            _editing.OfficerDailyWithdrawCap = officerCap;

            int modCap = _editing.ModeratorDailyWithdrawCap;
            y = DrawBufferedIntField(y, "Moderator cap", ref _modCapBuf, ref modCap, -1, int.MaxValue);
            _editing.ModeratorDailyWithdrawCap = modCap;

            int adminCap = _editing.AdminDailyWithdrawCap;
            y = DrawBufferedIntField(y, "Admin cap", ref _adminCapBuf, ref adminCap, -1, int.MaxValue);
            _editing.AdminDailyWithdrawCap = adminCap;

            float btnY = rect.height - 40f;
            if (Widgets.ButtonText(new Rect(0f, btnY, 120f, 32f), "Cancel"))
                Close();
            if (Widgets.ButtonText(new Rect(rect.width - 120f, btnY, 120f, 32f), "Save"))
            {
                PM_GuildHall.UpdateSettings(_editing);
                Close();
            }
        }

        // KMH 26.5.20.1: Wraps DialogLayout.DrawNumericField with a label.
        // The buffer pattern means clearing the field doesn't snap-back to
        // the old value mid-edit, so typing "5" while the field shows "1"
        // doesn't produce "15" or "51" — it just produces "5".
        private float DrawBufferedIntField(float y, string label, ref string buffer, ref int parsedValue, int min, int max)
        {
            Widgets.Label(new Rect(0f, y, 220f, 22f), label + ":");
            DialogLayout.DrawNumericField(new Rect(220f, y, 120f, 24f), ref buffer, ref parsedValue, min, max);
            return y + 30f;
        }
    }
}
