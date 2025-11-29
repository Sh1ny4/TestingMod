using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TestingMod.patches
{
    [HarmonyPatch(typeof(DefaultPartyWageModel), nameof(DefaultPartyWageModel.GetCharacterWage))]
    internal class GetCharacterWagePatch
    {
        [HarmonyPostfix]
        static void Postfix(ref int __result, CharacterObject character)
        {
            if (character.IsMounted) { __result = (int)((float)__result * 1.5f); }
            if (character.IsRanged) { __result = (int)((float)__result * 1.1f); }
        }
    }
}
