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
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, new Action(this.OnCharacterCreationIsOverEvent));
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
        private void OnCharacterCreationIsOverEvent()
        {
            Hero.MainHero.ClanBanner.ChangeBackgroundColor(Hero.MainHero.Culture.Color, Hero.MainHero.Culture.BackgroundColor2);
            if (Clan.PlayerClan.Tier > 0 && Hero.MainHero.Culture.StringId != "empire")
            {
                Hero ruler = Hero.FindAll(hero => hero.Culture == Hero.MainHero.Culture && hero.IsAlive && hero.IsFactionLeader && !hero.MapFaction.IsMinorFaction).GetRandomElementInefficiently();
                ChangeKingdomAction.ApplyByJoinToKingdom(Hero.MainHero.Clan, ruler.Clan.Kingdom, default, false);
            }
        }
    }
}
