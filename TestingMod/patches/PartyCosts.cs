using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

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
    [HarmonyPatch(typeof(DefaultMobilePartyFoodConsumptionModel), nameof(DefaultMobilePartyFoodConsumptionModel.CalculateDailyBaseFoodConsumptionf))]
    public class FoodPatch
    {
        [HarmonyPostfix]
        static void Postfix(ref ExplainedNumber __result, MobileParty party, bool includeDescription = false)
        {
            // makes it so that horse in inventory and horse from mounted troops also consum food
            int num = party.Party.NumberOfAllMembers + party.Party.NumberOfMounts + party.Party.NumberOfMenWithHorse + party.Party.NumberOfPackAnimals + party.Party.NumberOfPrisoners / 2;
            num = ((num < 1) ? 1 : num);
            __result = new ExplainedNumber(-(float)num / (float)20f, includeDescription, null);
        }
    }
}