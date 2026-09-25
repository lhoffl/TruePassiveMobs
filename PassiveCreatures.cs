using System;
using System.Collections.Generic;
using HarmonyLib;

namespace TruePassiveMobs
{
    internal static class PassiveCreatures
    {
        private static readonly int s_provokedUntilKey = "TruePassiveMobs.ProvokedUntil".GetStableHashCode();

        public static bool ShouldIgnorePlayers(BaseAI ai)
        {
            if (!ModConfig.PassiveEnabled.Value || ai is not MonsterAI) return false;
            Character character = ai.m_character;
            return character != null && !character.IsTamed() && Creatures.IsPassive(character) && !IsProvoked(character);
        }

        public static bool IsProvoked(Character character)
        {
            ZDO zdo = character.m_nview != null && character.m_nview.IsValid() ? character.m_nview.GetZDO() : null;
            return zdo != null && zdo.GetLong(s_provokedUntilKey) > NowMs();
        }

        private static void Provoke(Character character, Player attacker)
        {
            long until = NowMs() + (long)(ModConfig.ProvokedDuration.Value * 1000f);
            character.m_nview.GetZDO().Set(s_provokedUntilKey, until);
        }

        private static long NowMs() => (long)(ZNet.instance.GetTimeSeconds() * 1000.0);

        [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
        private static class Character_RPC_Damage_Patch
        {
            private static void Prefix(Character __instance, HitData hit)
            {
                if (!ModConfig.PassiveEnabled.Value || __instance.IsPlayer() || hit == null || !hit.HaveAttacker()) return;
                ZNetView nview = __instance.m_nview;
                if (nview == null || !nview.IsValid() || !nview.IsOwner() || __instance.GetBaseAI() is not MonsterAI) return;
                if (hit.GetAttacker() is Player attacker && Creatures.IsPassive(__instance)) Provoke(__instance, attacker);
            }
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindEnemy))]
        private static class BaseAI_FindEnemy_Patch
        {
            private static void Prefix(BaseAI __instance, out List<Player> __state)
            {
                __state = null;
                if (!ShouldIgnorePlayers(__instance)) return;
                foreach (Player player in Player.GetAllPlayers())
                {
                    if (!player.m_aiSkipTarget)
                    {
                        player.m_aiSkipTarget = true;
                        (__state ??= new List<Player>()).Add(player);
                    }
                }
            }

            private static void Postfix(BaseAI __instance, ref Character __result)
            {
                if (__result is Player && ShouldIgnorePlayers(__instance)) __result = null;
            }

            private static Exception Finalizer(Exception __exception, List<Player> __state)
            {
                if (__state != null)
                {
                    foreach (Player player in __state) if (player != null) player.m_aiSkipTarget = false;
                }
                return __exception;
            }
        }

        public static void ForgetPlayers(MonsterAI ai)
        {
            if (ai.m_targetCreature is Player)
            {
                ai.m_targetCreature = null;
                ai.SetTargetInfo(ZDOID.None);
            }
            ai.m_targetStatic = null;
        }
    }
}
