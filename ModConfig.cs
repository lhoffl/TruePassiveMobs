using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace TruePassiveMobs
{
    internal static class ModConfig
    {
        private const string PassiveSection = "1 - Passive creatures";
        private const string FearSection = "2 - Enemies fear strong players";

        public static ConfigEntry<bool> PassiveEnabled;
        public static ConfigEntry<string> PassiveCreatures;
        public static ConfigEntry<float> ProvokedDuration;

        public static ConfigEntry<bool> FearEnabled;
        public static ConfigEntry<bool> FightBackWhenAttacked;
        public static ConfigEntry<float> FightBackDuration;
        public static ConfigEntry<bool> RaidersNeverFlee;
        public static ConfigEntry<bool> UseEnemyDamageCheck;
        public static ConfigEntry<float> MaxEnemyHitPercent;
        public static ConfigEntry<bool> UsePlayerDamageCheck;
        public static ConfigEntry<int> MaxHitsToKill;
        public static ConfigEntry<bool> RequireBothChecks;
        public static ConfigEntry<float> FearRange;
        public static ConfigEntry<string> FearExcludedCreatures;

        public static int ListVersion { get; private set; }

        private static HashSet<string> s_passiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> s_fearExcludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Bind(ConfigFile config)
        {
            PassiveEnabled = Bind(config, PassiveSection, "Enabled", true, "Creatures below never seek or attack players unless damaged first:");
            PassiveCreatures = Bind(config, PassiveSection, "Creatures", "Lox, Asksvin, Asksvin_hatchling, Moose, Boar, Hen, Neck, Bjorn, Bjorn_sleeping", "");
            ProvokedDuration = Bind(config, PassiveSection, "ProvokedDuration", 60f, "Seconds a passive creature stays hostile after being damaged", new AcceptableValueRange<float>(1f, 3600f));
            FearEnabled = Bind(config, FearSection, "Enabled", true, "All enemies run away from players instead of attacking if the player's gear is good enough.");
            FightBackWhenAttacked = Bind(config, FearSection, "FightBackWhenAttacked", true, "An enemy a player damages stops running and fights back until FightBackDuration seconds after the last hit.");
            FightBackDuration = Bind(config, FearSection, "FightBackDuration", 60f, "Seconds an attacked enemy keeps fighting back after being damaged.", new AcceptableValueRange<float>(1f, 3600f));
            RaidersNeverFlee = Bind(config, FearSection, "RaidersNeverFlee", true, "Creatures spawned by a raid never run away.");
            UseEnemyDamageCheck = Bind(config, FearSection, "UseEnemyDamageCheck", true, "Checks that the enemy's strongest attack, after your armor and resistances, deals MaxEnemyHitPercent of your max health.");
            MaxEnemyHitPercent = Bind(config, FearSection, "MaxEnemyHitPercent", 10f, "Largest hit (as % of your max health) an enemy may be able to deal and still be afraid of you.", new AcceptableValueRange<float>(0f, 100f));
            UsePlayerDamageCheck = Bind(config, FearSection, "UsePlayerDamageCheck", true, "Check that your equipped weapon (with skill, buffs, and the enemy's resistances) is able to kill the enemy in at most MaxHitsToKill hits.");
            MaxHitsToKill = Bind(config, FearSection, "MaxHitsToKill", 3, "Number of hits it may take you to kill an enemy for it to still be afraid of you.", new AcceptableValueRange<int>(1, 100));
            RequireBothChecks = Bind(config, FearSection, "RequireBothChecks", true, "true = enemies flee only if both checks pass, false = enemies flee if either passes.");
            FearRange = Bind(config, FearSection, "FearRange", 30f, "Enemies within this many meters will evalutate fear.", new AcceptableValueRange<float>(5f, 100f));
            FearExcludedCreatures = Bind(config, FearSection, "ExcludedCreatures", "", "Comma-separated enemies that should never run away.");

            PassiveCreatures.SettingChanged += (_, _) => RebuildLists();
            FearExcludedCreatures.SettingChanged += (_, _) => RebuildLists();
            RebuildLists();
        }

        private static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T value, string description, AcceptableValueBase range = null) =>
            config.Bind(section, key, value, new ConfigDescription(description, range, new ConfigurationManagerAttributes { IsAdminOnly = true }));

        public static bool IsPassiveName(string prefabName) => s_passiveNames.Contains(prefabName);

        public static bool IsFearExcludedName(string prefabName) => s_fearExcludedNames.Contains(prefabName);

        private static void RebuildLists()
        {
            s_passiveNames = ParseList(PassiveCreatures.Value);
            s_fearExcludedNames = ParseList(FearExcludedCreatures.Value);
            ListVersion++;
        }

        private static HashSet<string> ParseList(string value)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in (value ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();
                if (name.Length > 0) names.Add(name);
            }
            return names;
        }
    }
}
