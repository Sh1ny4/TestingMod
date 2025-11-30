using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TestingMod.patches.Villages;

namespace TestingMod.patches.Villages
{
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.OwnerClan), MethodType.Getter)]
    internal class VillageOwnerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref Settlement __instance, ref Clan __result)
        {
            if (__instance.Village != null)
            {
                __result = __instance.Village.OwnerClan();
            }
            if (__instance.Town != null)
            {
                __result = __instance.Town.OwnerClan;
            }
            if (__instance.IsHideout)
            {
                __result = __instance.Hideout.MapFaction as Clan;
            }
            __result = null;
        }
    }
}
