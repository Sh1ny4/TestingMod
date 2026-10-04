using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace TestingMod.patches.DelayedBanner
{
    internal class DelayedBannerCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, new Action<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool>(this.OnClanChangedKingdom));
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
        {
            if (clan == Clan.PlayerClan && Campaign.Current.IsBannerEditorEnabled)
            {
                Game.Current.GameStateManager.PushState(Game.Current.GameStateManager.CreateState<BannerEditorState>(), 0);
            }
            if (newKingdom.Leader == Hero.MainHero)
            {
                newKingdom.Banner = Clan.PlayerClan.Banner;
            }
        }
    }
}
