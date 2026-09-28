using System;
using HarmonyLib;

namespace TruePassiveMobs
{
    [HarmonyPatch]
    internal static class BaseAIReversePatch
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.UpdateAI))]
        public static bool BaseUpdateAI(BaseAI instance, float dt) => throw new NotImplementedException("Harmony reverse patch stub");
    }

    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    internal static class MonsterAI_UpdateAI_Patch
    {
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        {
            ZNetView nview = __instance.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner() || __instance.IsSleeping()) return true;

            Player runFrom;
            if (ModConfig.PassiveEnabled.Value && Creatures.IsPassive(__instance.m_character)) runFrom = PassiveCreatures.Update(__instance, dt);
            else if (ModConfig.FearEnabled.Value && FearOfStrongPlayers.CanBeAfraid(__instance)) runFrom = FearOfStrongPlayers.GetFearedPlayer(__instance);
            else return true;
            
            if (runFrom == null) return true;

            __result = BaseAIReversePatch.BaseUpdateAI(__instance, dt);
            if (__result) RunFrom(__instance, runFrom, dt);
            return false;
        }

        private static void RunFrom(MonsterAI ai, Player player, float dt)
        {
            if (ai.m_targetCreature != null || ai.m_targetStatic != null)
            {
                ai.m_targetCreature = null;
                ai.m_targetStatic = null;
                ai.SetTargetInfo(ZDOID.None);
            }
            
            ai.ChargeStop();
            ai.SetAlerted(true);
            ai.Flee(dt, player.transform.position);
        }
    }
}
