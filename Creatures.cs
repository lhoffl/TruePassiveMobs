using System.Runtime.CompilerServices;
using UnityEngine;

namespace TruePassiveMobs
{
    internal static class Creatures
    {
        private sealed class Info
        {
            public string PrefabName;
            public int ListVersion = -1;
            public bool Passive;
            public bool FearExcluded;
        }

        private static readonly ConditionalWeakTable<Character, Info> s_info = new ConditionalWeakTable<Character, Info>();

        public static bool IsPassive(Character character) => GetInfo(character).Passive;

        public static bool IsFearExcluded(Character character) => GetInfo(character).FearExcluded;

        public static string GetPrefabName(Character character) => GetInfo(character).PrefabName;

        private static Info GetInfo(Character character)
        {
            Info info = s_info.GetValue(character, _ => new Info());
            if (info.PrefabName == null) info.PrefabName = ResolvePrefabName(character);
            if (info.ListVersion != ModConfig.ListVersion)
            {
                info.ListVersion = ModConfig.ListVersion;
                info.Passive = ModConfig.IsPassiveName(info.PrefabName);
                info.FearExcluded = ModConfig.IsFearExcludedName(info.PrefabName);
            }
            return info;
        }

        private static string ResolvePrefabName(Character character)
        {
            ZDO zdo = character.m_nview != null ? character.m_nview.GetZDO() : null;
            if (zdo != null && ZNetScene.instance != null)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                if (prefab != null) return prefab.name;
            }
            string name = character.gameObject.name;
            int clone = name.IndexOf('(');
            return (clone >= 0 ? name.Substring(0, clone) : name).Trim();
        }
    }
}
