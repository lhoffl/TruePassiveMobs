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
            PassiveEnabled = config.Bind(PassiveSection, "Enabled", true, "Creatures below never seek or attack players unless damaged first:");
            PassiveCreatures = config.Bind(PassiveSection, "Creatures", "Lox, Asksvin, Asksvin_hatchling, Moose, Boar, Hen, Neck, Bjorn, Bjorn_sleeping");
            ProvokedDuration = config.Bind(PassiveSection, "ProvokedDuration", 60f, new ConfigDescription("Seconds a passive creature stays hostile after being damaged", new AcceptableValueRange<float>(1f, 3600f)));
            FearEnabled = config.Bind(FearSection, "Enabled", true, "All enemies run away from players instead of attacking if the player's gear is good enough.");
            UseEnemyDamageCheck = config.Bind(FearSection, "UseEnemyDamageCheck", true, "Checks that the enemy's strongest attack, after your armor and resistances, deals MaxEnemyHitPercent of your max health.");
            MaxEnemyHitPercent = config.Bind(FearSection, "MaxEnemyHitPercent", 10f, new ConfigDescription("Largest hit (as % of your max health) an enemy may be able to deal and still be afraid of you.", new AcceptableValueRange<float>(0f, 100f)));
            UsePlayerDamageCheck = config.Bind(FearSection, "UsePlayerDamageCheck", true, "Check that your equipped weapon (with skill, buffs, and the enemy's resistances) is able to kill the enemy in at most MaxHitsToKill hits.");
            MaxHitsToKill = config.Bind(FearSection, "MaxHitsToKill", 3, new ConfigDescription("Number of hits it may take you to kill an enemy for it to still be afraid of you.", new AcceptableValueRange<int>(1, 100)));
            RequireBothChecks = config.Bind(FearSection, "RequireBothChecks", true, "true = enemies flee only if both checks pass, false = enemies flee if either passes.");
            FearRange = config.Bind(FearSection, "FearRange", 30f, new ConfigDescription("Enemies within this many meters will evalutate fear.", new AcceptableValueRange<float>(5f, 100f)));
            FearExcludedCreatures = config.Bind(FearSection, "ExcludedCreatures", "", "Comma-separated enemies that should never run away.");

            PassiveCreatures.SettingChanged += (_, _) => RebuildLists();
            FearExcludedCreatures.SettingChanged += (_, _) => RebuildLists();
            RebuildLists();
        }

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
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
            return names;
        }
    }
}
