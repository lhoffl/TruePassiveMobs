using HarmonyLib;

namespace TruePassiveMobs
{
    internal static class Provocation
    {
        private static readonly int s_lastPlayerHitKey = "TruePassiveMobs.LastPlayerHit".GetStableHashCode();

        public static bool WasHitByPlayerWithin(Character character, float seconds)
        {
            ZDO zdo = character.m_nview != null && character.m_nview.IsValid() ? character.m_nview.GetZDO() : null;
            if (zdo == null) return false;
            long lastHit = zdo.GetLong(s_lastPlayerHitKey);
            return lastHit > 0 && NowMs() - lastHit < (long)(seconds * 1000f);
        }

        private static long NowMs() => (long)(ZNet.instance.GetTimeSeconds() * 1000.0);

        [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
        private static class Character_RPC_Damage_Patch
        {
            private static void Prefix(Character __instance, HitData hit)
            {
                if (__instance.IsPlayer() || hit == null || !hit.HaveAttacker()) return;
                ZNetView nview = __instance.m_nview;
                if (nview == null || !nview.IsValid() || !nview.IsOwner() || __instance.GetBaseAI() is not MonsterAI) return;
                if (hit.GetAttacker() is Player) nview.GetZDO().Set(s_lastPlayerHitKey, NowMs());
            }
        }
    }
}
