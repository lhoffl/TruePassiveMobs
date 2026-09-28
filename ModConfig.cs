using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TruePassiveMobs
{
    internal static class ModConfig
    {
        private const string PassiveSection = "1 - Passive creatures";
        private const string FearSection = "2 - Enemies fear strong players";

        public static ConfigEntry<bool> PassiveEnabled;
        public static ConfigEntry<string> PassiveCreatures;
        public static ConfigEntry<string> SkittishCreatures;
        public static ConfigEntry<float> SkittishRange;
        public static ConfigEntry<float> SkittishTime;
        public static ConfigEntry<string> TerritorialCreatures;
        public static ConfigEntry<float> TerritorialRange;
        public static ConfigEntry<float> TerritorialTime;
        public static ConfigEntry<float> TerritorialRetreatTime;
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
        private static HashSet<string> s_skittishNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> s_territorialNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> s_fearExcludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Bind(ConfigFile config)
        {
            PassiveEnabled = Bind(config, PassiveSection, "Enabled", true, "Creatures in the lists below never seek out players, and fight back normally once damaged. A creature in several lists uses Territorial over Skittish over Creatures.");
            PassiveCreatures = Bind(config, PassiveSection, "Creatures", "Lox, Asksvin, Asksvin_hatchling, Moose, Boar, Hen, Neck, Bjorn, Bjorn_sleeping", "Fully passive: ignore players entirely.");
            SkittishCreatures = Bind(config, PassiveSection, "SkittishCreatures", "Neck, Lox", "Run away from a player who stays within SkittishRange for SkittishTime seconds.");
            SkittishRange = Bind(config, PassiveSection, "SkittishRange", 5f, "Meters within which a player makes skittish creatures nervous.", new AcceptableValueRange<float>(1f, 50f));
            SkittishTime = Bind(config, PassiveSection, "SkittishTime", 3f, "Seconds a player must stay within SkittishRange before skittish creatures run away.", new AcceptableValueRange<float>(0f, 120f));
            TerritorialCreatures = Bind(config, PassiveSection, "TerritorialCreatures", "Asksvin, Moose, Bjorn, Boar", "Attack a player who stays within TerritorialRange for TerritorialTime seconds, then run away for TerritorialRetreatTime seconds.");
            TerritorialRange = Bind(config, PassiveSection, "TerritorialRange", 3f, "Meters within which a player is intruding on territorial creatures.", new AcceptableValueRange<float>(1f, 50f));
            TerritorialTime = Bind(config, PassiveSection, "TerritorialTime", 5f, "Seconds a player must stay within TerritorialRange before territorial creatures attack.", new AcceptableValueRange<float>(0f, 120f));
            TerritorialRetreatTime = Bind(config, PassiveSection, "TerritorialRetreatTime", 10f, "Seconds a territorial creature runs away after attacking.", new AcceptableValueRange<float>(0f, 120f));
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
            FearRange = Bind(config, FearSection, "FearRange", 30f, "Enemies within this many meters will evaluate fear.", new AcceptableValueRange<float>(5f, 100f));
            FearExcludedCreatures = Bind(config, FearSection, "ExcludedCreatures", "", "Comma-separated enemies that should never run away.");

            PassiveCreatures.SettingChanged += (_, _) => RebuildLists();
            SkittishCreatures.SettingChanged += (_, _) => RebuildLists();
            TerritorialCreatures.SettingChanged += (_, _) => RebuildLists();
            FearExcludedCreatures.SettingChanged += (_, _) => RebuildLists();
            RebuildLists();
        }

        private static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T value, string description, AcceptableValueBase range = null) =>
            config.Bind(section, key, value, new ConfigDescription(description, range, new ConfigurationManagerAttributes { IsAdminOnly = true }));

        public static Temperament GetTemperament(string prefabName)
        {
            if (s_territorialNames.Contains(prefabName)) return Temperament.Territorial;
            if (s_skittishNames.Contains(prefabName)) return Temperament.Skittish;
            if (s_passiveNames.Contains(prefabName)) return Temperament.Passive;
            return Temperament.Hostile;
        }

        public static bool IsFearExcludedName(string prefabName) => s_fearExcludedNames.Contains(prefabName);

        private static int s_checkedListVersion = -1;
        private static ZNetScene s_checkedScene;

        public static void WarnAboutUnknownNames()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || (scene == s_checkedScene && ListVersion == s_checkedListVersion)) return;
            s_checkedScene = scene;
            s_checkedListVersion = ListVersion;

            var creatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GameObject prefab in scene.m_prefabs)
            {
                if (prefab != null && prefab.GetComponent<Character>() != null) creatures.Add(prefab.name);
            }
            WarnAboutUnknownNames(PassiveCreatures, s_passiveNames, creatures);
            WarnAboutUnknownNames(SkittishCreatures, s_skittishNames, creatures);
            WarnAboutUnknownNames(TerritorialCreatures, s_territorialNames, creatures);
            WarnAboutUnknownNames(FearExcludedCreatures, s_fearExcludedNames, creatures);
        }

        private static void WarnAboutUnknownNames(ConfigEntry<string> entry, HashSet<string> names, HashSet<string> creatures)
        {
            foreach (string name in names)
            {
                if (!creatures.Contains(name)) Plugin.Log.LogWarning($"{entry.Definition.Key}: '{name}' isn't a known creature, check the spelling.");
            }
        }

        private static void RebuildLists()
        {
            s_passiveNames = ParseList(PassiveCreatures.Value);
            s_skittishNames = ParseList(SkittishCreatures.Value);
            s_territorialNames = ParseList(TerritorialCreatures.Value);
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
