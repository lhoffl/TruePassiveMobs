using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace TruePassiveMobs
{
    internal static class PassiveCreatures
    {
        private const float CalmDownRangeFactor = 1.5f;
        private const float GiveUpRangeFactor = 3f;
        private const float ChargeTimeout = 15f;
        private const float MaxAttackDuration = 5f;

        private enum Mode { Calm, Fleeing, Charging, Retreating }

        private sealed class State
        {
            public Mode Mode;
            public Player Player;
            public float CloseTime;
            public float ModeUntil;
            public Attack AttackBefore;
            public float AttackStartedAt = -1f;
        }

        private static readonly ConditionalWeakTable<MonsterAI, State> s_states = new ConditionalWeakTable<MonsterAI, State>();

        public static bool ShouldIgnorePlayers(BaseAI ai)
        {
            if (!ModConfig.PassiveEnabled.Value || ai is not MonsterAI monsterAI) return false;
            Character character = ai.m_character;
            if (character == null || character.IsTamed() || !Creatures.IsPassive(character) || IsProvoked(character)) return false;
            return !(s_states.TryGetValue(monsterAI, out State state) && state.Mode == Mode.Charging);
        }

        public static bool IsProvoked(Character character) => Provocation.WasHitByPlayerWithin(character, ModConfig.ProvokedDuration.Value);

        public static Player Update(MonsterAI ai, float dt)
        {
            Character self = ai.m_character;
            State state = s_states.GetValue(ai, _ => new State());
            if (self.IsTamed() || IsProvoked(self))
            {
                state.Mode = Mode.Calm;
                state.Player = null;
                state.CloseTime = 0f;
                return null;
            }
            switch (Creatures.GetTemperament(self))
            {
                case Temperament.Skittish: return UpdateSkittish(ai, state, dt);
                case Temperament.Territorial: return UpdateTerritorial(ai, state, dt);
                default:
                    state.Mode = Mode.Calm;
                    ForgetPlayers(ai);
                    return null;
            }
        }

        private static Player UpdateSkittish(MonsterAI ai, State state, float dt)
        {
            float range = ModConfig.SkittishRange.Value;
            if (state.Mode == Mode.Fleeing)
            {
                if (!IsGone(state.Player) && Distance(ai, state.Player) <= range * CalmDownRangeFactor) return state.Player;
                BecomeCalm(ai, state);
            }
            
            state.Mode = Mode.Calm;
            ForgetPlayers(ai);
            
            Player spooked = TrackCloseness(ai, state, range, ModConfig.SkittishTime.Value, dt);
            if (spooked == null) return null;
            
            state.Mode = Mode.Fleeing;
            state.Player = spooked;
            return spooked;
        }

        private static Player UpdateTerritorial(MonsterAI ai, State state, float dt)
        {
            float now = Time.time;
            float range = ModConfig.TerritorialRange.Value;
            switch (state.Mode)
            {
                case Mode.Charging:
                    if (IsGone(state.Player) || now > state.ModeUntil || Distance(ai, state.Player) > range * GiveUpRangeFactor)
                    {
                        BecomeCalm(ai, state);
                        break;
                    }
                    
                    if (FinishedAttack(ai, state, now))
                    {
                        state.Mode = Mode.Retreating;
                        state.ModeUntil = now + ModConfig.TerritorialRetreatTime.Value;
                        return state.Player;
                    }
                    
                    Charge(ai, state.Player);
                    return null;
                
                case Mode.Retreating:
                    if (!IsGone(state.Player) && now <= state.ModeUntil) return state.Player;
                    BecomeCalm(ai, state);
                    break;
            }
            
            state.Mode = Mode.Calm;
            ForgetPlayers(ai);
            
            Player intruder = TrackCloseness(ai, state, range, ModConfig.TerritorialTime.Value, dt);
            if (intruder == null) return null;
            
            state.Mode = Mode.Charging;
            state.Player = intruder;
            state.ModeUntil = now + ChargeTimeout;
            state.AttackBefore = (ai.m_character as Humanoid)?.m_currentAttack;
            state.AttackStartedAt = -1f;
            
            Charge(ai, intruder);
            return null;
        }

        private static Player TrackCloseness(MonsterAI ai, State state, float range, float time, float dt)
        {
            Player closest = null;
            float closestDistance = float.MaxValue;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (IsGone(player) || player.InGhostMode() || player.InDebugFlyMode() || player.IsCrouching()) continue;
                float distance = Distance(ai, player);
                if (distance <= range && distance < closestDistance)
                {
                    closest = player;
                    closestDistance = distance;
                }
            }
            
            state.CloseTime = closest != null ? state.CloseTime + dt : Mathf.Max(0f, state.CloseTime - dt);
            return closest != null && state.CloseTime >= time ? closest : null;
        }

        private static bool FinishedAttack(MonsterAI ai, State state, float now)
        {
            if (ai.m_character is not Humanoid humanoid) return false;
            if (state.AttackStartedAt < 0f)
            {
                if (humanoid.m_currentAttack != null && humanoid.m_currentAttack != state.AttackBefore) state.AttackStartedAt = now;
                return false;
            }
            
            float elapsed = now - state.AttackStartedAt;
            return (elapsed > 0.25f && !humanoid.InAttack()) || elapsed > MaxAttackDuration;
        }

        private static void Charge(MonsterAI ai, Player player)
        {
            ai.m_targetCreature = player;
            ai.m_targetStatic = null;
            ai.m_lastKnownTargetPos = player.transform.position;
            ai.m_beenAtLastPos = false;
            ai.SetAlerted(true);
        }

        private static void BecomeCalm(MonsterAI ai, State state)
        {
            state.Mode = Mode.Calm;
            state.Player = null;
            state.CloseTime = 0f;
            ai.SetAlerted(false);
        }

        private static bool IsGone(Player player) => player == null || player.IsDead();

        private static float Distance(MonsterAI ai, Player player) => Mathf.Max(0f, Vector3.Distance(player.transform.position, ai.transform.position) - ai.m_character.GetRadius() - player.GetRadius());

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
