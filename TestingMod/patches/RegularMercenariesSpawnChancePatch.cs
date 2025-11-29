using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TestingMod.patches
{
    [HarmonyPatch(typeof(DefaultTavernMercenaryTroopsModel), nameof(DefaultTavernMercenaryTroopsModel.RegularMercenariesSpawnChance), MethodType.Getter)]
    internal class RegularMercenariesSpawnChancePatch
    {
        [HarmonyPostfix]
        static void Postfix(ref float __result)
        {
            //remove the chance for caravan guards to spawn in taverns
            __result = 1f;
        }
    }
}
