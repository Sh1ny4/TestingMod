using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace TestingMod.patches.DelayedBanner
{
    [HarmonyPatch(typeof(CharacterCreationCampaignBehavior), nameof(CharacterCreationCampaignBehavior.InitializeCharacterCreationStages))]
    internal class InitializeCharacterCreationStagesPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instruction = new List<CodeInstruction>(instructions);
            if (instruction[9].opcode == OpCodes.Ldarg_1 && instruction[10].opcode == OpCodes.Newobj && instruction[11].opcode == OpCodes.Callvirt)
            {
                instruction[9].opcode = OpCodes.Nop;
                instruction[10].opcode = OpCodes.Nop;
                instruction[11].opcode = OpCodes.Nop;
            }
            return instruction;
        }
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.CanChooseBanner), MethodType.Getter)]
    internal class CanChooseBannerPatch
    {
        [HarmonyPostfix]
        static void Postfix(ref bool __result)
        {
            __result = false;
        }
    }
    [HarmonyPatch(typeof(DefaultClanTierModel), nameof(DefaultClanTierModel.BannerEligibleTier), MethodType.Getter)]
    internal class BannerEligibleTierPatch
    {
        [HarmonyPostfix]
        static void Postfix(ref int __result)
        {
            __result = 2;
        }
    }
}
