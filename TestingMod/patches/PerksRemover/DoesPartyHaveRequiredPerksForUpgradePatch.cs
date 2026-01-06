using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TestingMod.patches.PerksRemover
{
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
