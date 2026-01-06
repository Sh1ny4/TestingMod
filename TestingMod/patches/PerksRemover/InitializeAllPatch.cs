using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

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
}
