using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TestingMod.patches.PerksRemover
{
    [HarmonyPatch(typeof(DefaultPerks), "InitializeAll")]
    internal class InitializeAllPatch
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            return false;
        }
    }
    [HarmonyPatch(typeof(DefaultPartyTroopUpgradeModel), "DoesPartyHaveRequiredPerksForUpgrade")]
    internal class DoesPartyHaveRequiredPerksForUpgradePatch
    {
        [HarmonyPrefix]
        static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}
