using HarmonyLib;
using SandBox;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Towns;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate;

namespace TestingMod.patches
{
    /// <summary>
    /// remove the chance for caravan guards to spawn in taverns
    /// </summary>
    [HarmonyPatch(typeof(DefaultTavernMercenaryTroopsModel), nameof(DefaultTavernMercenaryTroopsModel.RegularMercenariesSpawnChance), MethodType.Getter)]
    internal class RegularMercenariesSpawnChancePatch
    {
        [HarmonyPostfix]
        static void Postfix(ref float __result)
        {
            __result = 1f;
        }
    }

    /// <summary>
    /// increases the wage of cavalry and archers
    /// </summary>
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

    /// <summary>
    /// horses in inventory and horse from mounted troops also consum food
    /// </summary>
    [HarmonyPatch(typeof(DefaultMobilePartyFoodConsumptionModel), nameof(DefaultMobilePartyFoodConsumptionModel.CalculateDailyBaseFoodConsumptionf))]
    public class FoodPatch
    {
        [HarmonyPostfix]
        static void Postfix(ref ExplainedNumber __result, MobileParty party, bool includeDescription = false)
        {
            int num = party.Party.NumberOfAllMembers + party.Party.NumberOfMounts + party.Party.NumberOfMenWithHorse + party.Party.NumberOfPackAnimals + party.Party.NumberOfPrisoners / 2;
            num = ((num < 1) ? 1 : num);
            __result = new ExplainedNumber(-(float)num / (float)20f, includeDescription, null);
        }
    }

    /// <summary>
    /// reduce the loyalty loss for a gov of a different culture from -3 to -1
    /// </summary>
    [HarmonyPatch(typeof(DefaultSettlementLoyaltyModel), nameof(DefaultSettlementLoyaltyModel.SettlementOwnerDifferentCultureLoyaltyEffect), MethodType.Getter)]
    internal class LoyaltyPatch
    {
        [HarmonyPostfix]
        static void Postfix(ref float __result)
        {
            __result = -1f;
        }
    }

    /// <summary>
    /// increased the party size at which being disorganised become possible
    /// </summary>
    [HarmonyPatch(typeof(DefaultPartyImpairmentModel), nameof(DefaultPartyImpairmentModel.CanGetDisorganized))]
    internal class CanGetDisorganizedPatch
    {
        [HarmonyPostfix]
        static void Postfix(ref bool __result, PartyBase party)
        {
            __result = (party.IsActive && party.IsMobile && party.MobileParty.MemberRoster.TotalManCount >= 50);
        }
    }

    /// <summary>
    /// reduces the amount of bandit parties
    /// </summary>
    [HarmonyPatch(typeof(DefaultBanditDensityModel), nameof(DefaultBanditDensityModel.NumberOfMaximumBanditPartiesAroundEachHideout), MethodType.Getter)]
    internal class BanditsAroundHideoutsPatch
    {
        [HarmonyPostfix]
        static void Postfix(ref int __result)
        {
            __result = 3;
        }
    }

    /// <summary>
    /// allows for player to be mounted in town
    /// </summary>
    [HarmonyPatch(typeof(TownCenterMissionController), nameof(TownCenterMissionController.AfterStart))]
    internal class TownHorsePatch : TownCenterMissionController
    {
        [HarmonyPrefix]
        static bool AfterStart(ref TownCenterMissionController __instance)
        {
            bool isNight = Campaign.Current.IsNight;
            __instance.Mission.SetMissionMode(MissionMode.StartUp, true);
            __instance.Mission.IsInventoryAccessible = !Campaign.Current.IsMainHeroDisguised;
            __instance.Mission.IsQuestScreenAccessible = true;
            MissionAgentHandler missionBehavior = __instance.Mission.GetMissionBehavior<MissionAgentHandler>();
            SandBoxHelpers.MissionHelper.SpawnPlayer(__instance.Mission.DoesMissionRequireCivilianEquipment, false, false, false, "");
            missionBehavior.SpawnLocationCharacters(null);
            SandBoxHelpers.MissionHelper.SpawnHorses();
            if (!isNight)
            {
                SandBoxHelpers.MissionHelper.SpawnSheeps();
                SandBoxHelpers.MissionHelper.SpawnCows();
                SandBoxHelpers.MissionHelper.SpawnHogs();
                SandBoxHelpers.MissionHelper.SpawnGeese();
                SandBoxHelpers.MissionHelper.SpawnChicken();
            }
            return false;
        }
    }
}
