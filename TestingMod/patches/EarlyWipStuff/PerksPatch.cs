using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace TestingMod.patches.EarlyWipStuff
{
    [HarmonyPatch(typeof(DefaultPerks), "InitializeAll")]
    internal class PerksPatch
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            return false;
        }
    }
}
