using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace TestingMod.patches.ProgressionTweaks
{
    [HarmonyPatch(typeof(SettlementClaimantDecision), nameof(SettlementClaimantDecision.DetermineInitialCandidates))]
    internal class DetermineInitialCandidatesPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref SettlementClaimantDecision __instance, ref IEnumerable<DecisionOutcome> __result)
        {
            Kingdom kingdom = (Kingdom)__instance.Settlement.MapFaction;
            List<SettlementClaimantDecision.ClanAsDecisionOutcome> list = new List<SettlementClaimantDecision.ClanAsDecisionOutcome>();
            foreach (Clan clan in kingdom.Clans)
            {
                if ((clan != __instance.ClanToExclude && !clan.IsUnderMercenaryService && !clan.IsEliminated && !clan.Leader.IsDead) && ((__instance.Settlement.IsVillage && clan.Tier > 1) || (__instance.Settlement.IsCastle && clan.Tier > 2) || (__instance.Settlement.IsTown && clan.Tier > 3)))
                {
                    list.Add(new SettlementClaimantDecision.ClanAsDecisionOutcome(clan));
                }
            }
            __result = list;
        }
    }

    [HarmonyPatch(typeof(DefaultPartySizeLimitModel), "GetClanTierPartySizeEffectForHero")]
    internal class GetClanTierPartySizeEffectForHeroPatch : DefaultPartySizeLimitModel
    {
        [HarmonyPostfix]
        static void Postfix(ref int __result, Hero hero)
        {
            __result = 0;
        }
    }

    [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.CreateArmy))]
    internal class CreateArmyPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Hero armyLeader, Settlement targetSettlement, Army.ArmyTypes selectedArmyType)
        {
            if (armyLeader.Clan.Tier >= 4)
            {
                return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), nameof(DefaultArmyManagementCalculationModel.CanPlayerCreateArmy))]
    internal class CanPlayerCreateArmyPatch : DefaultArmyManagementCalculationModel
    {
        [HarmonyPrefix]
        static bool Prefix(ref DefaultArmyManagementCalculationModel __instance, ref bool __result, out TextObject disabledReason)
        {
            if (Clan.PlayerClan.Tier < 4)
            {
                disabledReason = new TextObject("{=wld_clan_tier_too_low}You need a higher clan Tier to create an army", null);
                __result = false;
                return false;
            }
            disabledReason = TextObject.GetEmpty();
            __result = true;
            return true;
        }
    }
}
