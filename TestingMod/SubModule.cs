using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TestingMod
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            new Harmony("TestingMod.patches").PatchAll();
        }

        public override void OnGameInitializationFinished(Game game)
        {
            if (!(game.GameType is Campaign)) return;
            Campaign.Current.CampaignBehaviorManager.RemoveBehavior<BackstoryCampaignBehavior>();
        }
    }
}
