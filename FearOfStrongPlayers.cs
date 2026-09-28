using System.Runtime.CompilerServices;
using UnityEngine;

namespace TruePassiveMobs
{
    internal static class FearOfStrongPlayers
    {
        private const float CheckInterval = 0.5f;
        private const float FearLinger = 3f;

        private sealed class State
        {
            public float NextCheck;
            public Player Feared;
            public float FearUntil;
            public Character LastTarget;
        }

        private static readonly ConditionalWeakTable<MonsterAI, State> s_states = new ConditionalWeakTable<MonsterAI, State>();

        public static bool CanBeAfraid(MonsterAI ai)
        {
            Character character = ai.m_character;
            if (character == null || character.IsTamed() || character.IsBoss() || character.GetFaction() == Character.Faction.Boss || Creatures.IsFearExcluded(character)) return false;
            if (ModConfig.RaidersNeverFlee.Value && ai.IsEventCreature()) return false;
            if (ModConfig.FightBackWhenAttacked.Value && Provocation.WasHitByPlayerWithin(character, ModConfig.FightBackDuration.Value)) return false;
            return true;
        }

        public static Player GetFearedPlayer(MonsterAI ai)
        {
            State state = s_states.GetValue(ai, _ => new State { NextCheck = Time.time + UnityEngine.Random.Range(0f, CheckInterval) });
            float now = Time.time;
            float range = ModConfig.FearRange.Value;

            bool newTarget = ai.m_targetCreature != state.LastTarget;
            state.LastTarget = ai.m_targetCreature;
            if (now >= state.NextCheck || (newTarget && ai.m_targetCreature is Player))
            {
                state.NextCheck = now + CheckInterval;
                Player scariest = FindOutclassingPlayer(ai, state.Feared, range);
                if (scariest != null)
                {
                    state.Feared = scariest;
                    state.FearUntil = now + FearLinger;
                }
            }

            Player feared = state.Feared;
            if ((object)feared != null)
            {
                bool gone = feared == null || feared.IsDead() || now > state.FearUntil || Vector3.Distance(feared.transform.position, ai.transform.position) > range;
                if (gone) state.Feared = null;
            }
            return state.Feared;
        }

        private static Player FindOutclassingPlayer(MonsterAI ai, Player currentlyFeared, float range)
        {
            Character self = ai.m_character;
            Vector3 position = ai.transform.position;
            Player best = null;
            float bestDistance = float.MaxValue;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null || player.IsDead() || player.InGhostMode() || player.InDebugFlyMode()) continue;
                
                float distance = Vector3.Distance(player.transform.position, position);
                if (distance > range || distance >= bestDistance || !BaseAI.IsEnemy(self, player)) continue;
                
                bool aware = player == currentlyFeared || ai.m_targetCreature == player || ai.CanSenseTarget(player);
                if (aware && CombatMath.Evaluate(self, player).Outclassed)
                {
                    best = player;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
