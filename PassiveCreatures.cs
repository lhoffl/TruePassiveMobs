using System;
using System.Collections.Generic;
using HarmonyLib;

namespace TruePassiveMobs
{
    internal static class PassiveCreatures
    {
        public static bool ShouldIgnorePlayers(BaseAI ai)
        {
            if (!ModConfig.PassiveEnabled.Value || ai is not MonsterAI) return false;
            Character character = ai.m_character;
            return character != null && !character.IsTamed() && Creatures.IsPassive(character) && !IsProvoked(character);
        }

        public static bool IsProvoked(Character character) =>
            Provocation.WasHitByPlayerWithin(character, ModConfig.ProvokedDuration.Value);

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
