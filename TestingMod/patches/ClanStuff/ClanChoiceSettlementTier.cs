using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace TestingMod.patches.ClanStuff
{
    [HarmonyPatch(typeof(SettlementClaimantDecision), nameof(SettlementClaimantDecision.DetermineInitialCandidates))]
    internal class ClanChoiceSettlementTier
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
}
