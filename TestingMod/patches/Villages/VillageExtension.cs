using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace TestingMod.patches.Villages
{
    internal static class VillageExtension
    {
        public static Clan OwnerClan(this Village village)
        {
            return village.Settlement.OwnerClan;
        }
    }
}
