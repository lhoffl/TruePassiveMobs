using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TruePassiveMobs
{
    internal sealed class PlayerCombatProfile
    {
        private const byte FormatVersion = 1;

        public float MaxHealth;
        public float Armor;
        public float DamageTakenRate;
        public HitData.DamageModifiers Resistances;
        public HitData.DamageTypes WeaponHit;

        public static PlayerCombatProfile Compute(Player player)
        {
            return new PlayerCombatProfile
            {
                MaxHealth = player.GetMaxHealth(),
                Armor = player.GetBodyArmor(),
                DamageTakenRate = Game.m_localDamgeTakenRate,
                Resistances = player.GetDamageModifiers(),
                WeaponHit = EstimateWeaponHit(player),
            };
        }

        private static HitData.DamageTypes EstimateWeaponHit(Player player)
        {
            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            if (weapon == null) return default;
            
            var hit = new HitData { m_damage = weapon.GetDamage() };
            if (!string.IsNullOrEmpty(weapon.m_shared.m_ammoType))
            {
                ItemDrop.ItemData ammo = Attack.FindAmmo(player, weapon);
                if (ammo == null) return default;
                hit.m_damage.Add(ammo.GetDamage());
            }
            
            Attack attack = weapon.m_shared.m_attack;
            if (attack != null) hit.m_damage.Modify(attack.m_damageMultiplier);
            
            float center = Mathf.Lerp(0.4f, 1f, player.GetSkillFactor(weapon.m_shared.m_skillType));
            hit.m_damage.Modify((Mathf.Clamp01(center - 0.15f) + Mathf.Clamp01(center + 0.15f)) * 0.5f);
            player.GetSEMan().ModifyAttack(weapon.m_shared.m_skillType, ref hit);
            return hit.m_damage;
        }

        public byte[] Serialize()
        {
            var pkg = new ZPackage();
            pkg.Write(FormatVersion);
            pkg.Write(MaxHealth);
            pkg.Write(Armor);
            pkg.Write(DamageTakenRate);
            HitData.DamageModifiers r = Resistances;
            
            foreach (HitData.DamageModifier mod in new[] { r.m_blunt, r.m_slash, r.m_pierce, r.m_chop, r.m_pickaxe, r.m_fire, r.m_frost, r.m_lightning, r.m_poison, r.m_spirit, r.m_nonPlayer })
            {
                pkg.Write((byte)mod);
            }
            
            HitData.DamageTypes d = WeaponHit;
            foreach (float value in new[] { d.m_damage, d.m_blunt, d.m_slash, d.m_pierce, d.m_chop, d.m_pickaxe, d.m_fire, d.m_frost, d.m_lightning, d.m_poison, d.m_spirit, d.m_nonPlayer })
            {
                pkg.Write(value);
            }
            
            return pkg.GetArray();
        }

        public static PlayerCombatProfile Deserialize(byte[] data)
        {
            try
            {
                var pkg = new ZPackage(data);
                if (pkg.ReadByte() != FormatVersion) return null;

                var profile = new PlayerCombatProfile
                {
                    MaxHealth = pkg.ReadSingle(),
                    Armor = pkg.ReadSingle(),
                    DamageTakenRate = pkg.ReadSingle(),
                };

                HitData.DamageModifier Mod() => (HitData.DamageModifier)pkg.ReadByte();
                profile.Resistances = new HitData.DamageModifiers
                {
                    m_blunt = Mod(), m_slash = Mod(), m_pierce = Mod(), m_chop = Mod(), m_pickaxe = Mod(), m_fire = Mod(),
                    m_frost = Mod(), m_lightning = Mod(), m_poison = Mod(), m_spirit = Mod(), m_nonPlayer = Mod(),
                };

                profile.WeaponHit = new HitData.DamageTypes
                {
                    m_damage = pkg.ReadSingle(), m_blunt = pkg.ReadSingle(), m_slash = pkg.ReadSingle(), m_pierce = pkg.ReadSingle(),
                    m_chop = pkg.ReadSingle(), m_pickaxe = pkg.ReadSingle(), m_fire = pkg.ReadSingle(), m_frost = pkg.ReadSingle(),
                    m_lightning = pkg.ReadSingle(), m_poison = pkg.ReadSingle(), m_spirit = pkg.ReadSingle(), m_nonPlayer = pkg.ReadSingle(),
                };

                return profile;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    internal static class ProfileSync
    {
        private static readonly int s_zdoKey = "TruePassiveMobs.CombatProfile".GetStableHashCode();
        private const float LocalRefreshInterval = 0.5f;

        private sealed class RemoteCache
        {
            public ZDO Zdo;
            public uint Revision;
            public PlayerCombatProfile Profile;
        }

        private static readonly ConditionalWeakTable<Player, RemoteCache> s_remote = new ConditionalWeakTable<Player, RemoteCache>();

        private static Player s_localPlayer;
        private static PlayerCombatProfile s_localProfile;
        private static float s_nextLocalRefresh;
        private static byte[] s_lastPublished;

        public static void Update()
        {
            Player player = Player.m_localPlayer;
            if (player != s_localPlayer)
            {
                s_localPlayer = player;
                s_localProfile = null;
                s_lastPublished = null;
                s_nextLocalRefresh = 0f;
            }
            
            if (player == null || Time.time < s_nextLocalRefresh) return;
            
            s_nextLocalRefresh = Time.time + LocalRefreshInterval;
            s_localProfile = PlayerCombatProfile.Compute(player);

            ZNetView nview = player.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            
            byte[] data = s_localProfile.Serialize();
            if (s_lastPublished == null || !BytesEqual(data, s_lastPublished))
            {
                nview.GetZDO().Set(s_zdoKey, data);
                s_lastPublished = data;
            }
        }

        public static PlayerCombatProfile Get(Player player)
        {
            if (player == Player.m_localPlayer) return s_localProfile ??= PlayerCombatProfile.Compute(player);
            
            ZDO zdo = player.m_nview != null && player.m_nview.IsValid() ? player.m_nview.GetZDO() : null;
            if (zdo == null) return null;
            
            RemoteCache cache = s_remote.GetValue(player, _ => new RemoteCache());
            if (cache.Zdo != zdo || cache.Revision != zdo.DataRevision)
            {
                cache.Zdo = zdo;
                cache.Revision = zdo.DataRevision;
                byte[] data = zdo.GetByteArray(s_zdoKey);
                cache.Profile = data != null ? PlayerCombatProfile.Deserialize(data) : null;
            }
            
            return cache.Profile;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }

    internal struct Matchup
    {
        public bool HasProfile;
        public bool EnemyHitKnown;
        public float EnemyHit;
        public float EnemyHitPercent;
        public float PlayerHit;
        public int HitsToKill;
        public bool EnemyCheckPassed;
        public bool PlayerCheckPassed;
        public bool Outclassed;
    }

    internal static class CombatMath
    {
        private struct EnemyAttack
        {
            public HitData.DamageTypes Damage;
            public Skills.SkillType Skill;
        }

        private sealed class EnemyAttackCache
        {
            public float Expires;
            public readonly List<EnemyAttack> Attacks = new List<EnemyAttack>();
        }

        private const float EnemyAttackCacheSeconds = 10f;
        private const float EmptyEnemyAttackCacheSeconds = 1f;
        private static readonly ConditionalWeakTable<Character, EnemyAttackCache> s_enemyAttacks = new ConditionalWeakTable<Character, EnemyAttackCache>();

        public static Matchup Evaluate(Character enemy, Player player)
        {
            var result = new Matchup { HitsToKill = int.MaxValue };
            PlayerCombatProfile profile = ProfileSync.Get(player);
            if (profile == null || profile.MaxHealth <= 0f) return result;
            result.HasProfile = true;

            result.EnemyHitKnown = TryEstimateEnemyBestHit(enemy, profile, player.transform.position, out result.EnemyHit);
            if (result.EnemyHitKnown)
            {
                result.EnemyHitPercent = result.EnemyHit / profile.MaxHealth * 100f;
                result.EnemyCheckPassed = result.EnemyHitPercent <= ModConfig.MaxEnemyHitPercent.Value;
            }

            result.PlayerHit = EstimatePlayerHit(enemy, profile);
            if (result.PlayerHit > 0.1f)
            {
                result.HitsToKill = Mathf.CeilToInt(enemy.GetMaxHealth() / result.PlayerHit);
                result.PlayerCheckPassed = result.HitsToKill <= ModConfig.MaxHitsToKill.Value;
            }

            bool useEnemy = ModConfig.UseEnemyDamageCheck.Value;
            bool usePlayer = ModConfig.UsePlayerDamageCheck.Value;
            if (useEnemy && usePlayer)
            {
                result.Outclassed = ModConfig.RequireBothChecks.Value ? result.EnemyCheckPassed && result.PlayerCheckPassed : result.EnemyCheckPassed || result.PlayerCheckPassed;
            }
            else
            {
                result.Outclassed = (useEnemy && result.EnemyCheckPassed) || (usePlayer && result.PlayerCheckPassed);
            }
            return result;
        }

        private static bool TryEstimateEnemyBestHit(Character enemy, PlayerCombatProfile target, Vector3 targetPos, out float bestHit)
        {
            bestHit = 0f;
            EnemyAttackCache attacks = GetEnemyAttacks(enemy);
            if (attacks.Attacks.Count == 0) return false;
            
            float levelFactor = 1f + Mathf.Max(0, enemy.GetLevel() - 1) * 0.5f;
            ZDO zdo = enemy.m_nview.GetZDO();
            float skillFactor = zdo != null ? zdo.GetFloat(ZDOVars.s_randomSkillFactor, 1f) : 1f;
            float difficultyScale = Game.instance.GetDifficultyDamageScalePlayer(targetPos);

            foreach (EnemyAttack attack in attacks.Attacks)
            {
                var hit = new HitData { m_damage = attack.Damage };
                hit.m_damage.Modify(levelFactor * skillFactor);
                enemy.GetSEMan().ModifyAttack(attack.Skill, ref hit);
                hit.SetAttacker(enemy);
                hit.ApplyModifier(difficultyScale);
                hit.ApplyModifier(Game.m_enemyDamageRate);
                hit.ApplyResistance(target.Resistances, out _);
                hit.ApplyArmor(target.Armor);
                hit.ApplyModifier(target.DamageTakenRate);
                bestHit = Mathf.Max(bestHit, hit.GetTotalDamage());
            }
            
            return true;
        }

        private static float EstimatePlayerHit(Character enemy, PlayerCombatProfile attacker)
        {
            var hit = new HitData { m_damage = attacker.WeaponHit };
            hit.ApplyResistance(enemy.GetDamageModifiers(), out _);
            if (Game.m_worldLevel > 0) hit.ApplyArmor(Game.m_worldLevel * Game.instance.m_worldLevelEnemyBaseAC);
            
            hit.ApplyModifier(Game.instance.GetDifficultyDamageScaleEnemy(enemy.transform.position));
            hit.ApplyModifier(Game.m_playerDamageRate);
            return hit.GetTotalDamage();
        }

        private static EnemyAttackCache GetEnemyAttacks(Character enemy)
        {
            EnemyAttackCache cache = s_enemyAttacks.GetValue(enemy, _ => new EnemyAttackCache());
            if (Time.time < cache.Expires) return cache;
            cache.Attacks.Clear();
            
            Inventory inventory = (enemy as Humanoid)?.GetInventory();
            if (inventory != null) CollectAttacks(inventory, cache.Attacks);
            
            cache.Expires = Time.time + (cache.Attacks.Count > 0 ? EnemyAttackCacheSeconds : EmptyEnemyAttackCacheSeconds);
            return cache;
        }

        private static void CollectAttacks(Inventory inventory, List<EnemyAttack> attacks)
        {
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (!item.IsWeapon() || item.m_shared.m_aiTargetType != ItemDrop.ItemData.AiTarget.Enemy) continue;
                
                Skills.SkillType skill = item.m_shared.m_skillType;
                Attack attack = item.m_shared.m_attack;

                HitData.DamageTypes weaponDamage = item.GetDamage();
                if (attack != null) weaponDamage.Modify(attack.m_damageMultiplier);
                
                AddIfDamaging(attacks, weaponDamage, skill);

                if (attack != null)
                {
                    AddAreaEffects(attacks, attack.m_attackProjectile, skill);
                    AddAreaEffects(attacks, attack.m_spawnOnTrigger, skill);
                    Projectile projectile = attack.m_attackProjectile != null ? attack.m_attackProjectile.GetComponent<Projectile>() : null;
                    
                    if (projectile != null) AddAreaEffects(attacks, projectile.m_spawnOnHit, skill);
                }
            }
        }

        private static void AddAreaEffects(List<EnemyAttack> attacks, GameObject prefab, Skills.SkillType skill)
        {
            if (prefab == null) return;
            foreach (Aoe aoe in prefab.GetComponentsInChildren<Aoe>(true))
            {
                if (!aoe.m_useAttackSettings) AddIfDamaging(attacks, aoe.m_damage, skill);
            }
        }

        private static void AddIfDamaging(List<EnemyAttack> attacks, HitData.DamageTypes damage, Skills.SkillType skill)
        {
            if (damage.GetTotalDamage() > 0f) attacks.Add(new EnemyAttack { Damage = damage, Skill = skill });
        }
    }
}
