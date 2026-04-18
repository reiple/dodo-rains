using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoWAutoBattler
{
    public enum AbilityType
    {
        None,
        ArcaneBurst,
        ChainLightning,
        HolyLight,
        Whirlwind,
        ShadowStrike,
        FrostNova,
        Starfall,
        ShieldSlam
    }

    [Serializable]
    public sealed class TraitDefinition
    {
        public string Id;
        public string DisplayName;
        public int[] Thresholds;
        public string Description;

        public TraitDefinition(string id, string displayName, int[] thresholds, string description)
        {
            Id = id;
            DisplayName = displayName;
            Thresholds = thresholds;
            Description = description;
        }
    }

    [Serializable]
    public sealed class UnitDefinition
    {
        public string Id;
        public string DisplayName;
        public int Cost;
        public float MaxHealth;
        public float AttackDamage;
        public float AttackSpeed;
        public float AttackRange;
        public float MoveSpeed;
        public float MaxMana;
        public float AbilityPower;
        public AbilityType AbilityType;
        public string[] Traits;
        public Color PrimaryColor;

        public UnitDefinition(string id, string displayName, int cost, float maxHealth, float attackDamage, float attackSpeed, float attackRange, float moveSpeed, float maxMana, float abilityPower, AbilityType abilityType, Color primaryColor, params string[] traits)
        {
            Id = id;
            DisplayName = displayName;
            Cost = cost;
            MaxHealth = maxHealth;
            AttackDamage = attackDamage;
            AttackSpeed = attackSpeed;
            AttackRange = attackRange;
            MoveSpeed = moveSpeed;
            MaxMana = maxMana;
            AbilityPower = abilityPower;
            AbilityType = abilityType;
            PrimaryColor = primaryColor;
            Traits = traits;
        }
    }

    [Serializable]
    public sealed class EnemyWaveEntry
    {
        public string UnitId;
        public int Count;

        public EnemyWaveEntry(string unitId, int count)
        {
            UnitId = unitId;
            Count = count;
        }
    }

    public static class DemoUnitLibrary
    {
        public static List<TraitDefinition> BuildTraits()
        {
            return new List<TraitDefinition>
            {
                new TraitDefinition("alliance", "Alliance", new[] { 2, 4 }, "Start-of-combat team shield"),
                new TraitDefinition("horde", "Horde", new[] { 2, 4 }, "Bonus attack damage"),
                new TraitDefinition("scourge", "Scourge", new[] { 2, 4 }, "Lifesteal"),
                new TraitDefinition("cenarion", "Cenarion", new[] { 2, 4 }, "Health regeneration"),
                new TraitDefinition("titanforged", "Titanforged", new[] { 2, 4 }, "Bonus max health"),
                new TraitDefinition("warrior", "Warrior", new[] { 2, 4 }, "Flat damage reduction"),
                new TraitDefinition("mage", "Mage", new[] { 2, 4 }, "Ability power and starting mana"),
                new TraitDefinition("shaman", "Shaman", new[] { 2, 4 }, "Mana on attack"),
                new TraitDefinition("rogue", "Rogue", new[] { 2, 4 }, "Critical strike chance"),
                new TraitDefinition("paladin", "Paladin", new[] { 2, 4 }, "Personal shield"),
                new TraitDefinition("hunter", "Hunter", new[] { 2, 4 }, "Attack range"),
                new TraitDefinition("druid", "Druid", new[] { 2, 4 }, "Cast sustain"),
            };
        }

        public static List<UnitDefinition> BuildUnits()
        {
            return new List<UnitDefinition>
            {
                new UnitDefinition("stormblade_commander", "Stormblade Commander", 1, 165f, 22f, 0.85f, 1.35f, 2.8f, 100f, 24f, AbilityType.ShieldSlam, new Color(0.29f, 0.52f, 0.92f), "alliance", "warrior"),
                new UnitDefinition("frost_archmage", "Frost Archmage", 2, 120f, 18f, 0.78f, 3.6f, 2.4f, 80f, 38f, AbilityType.FrostNova, new Color(0.40f, 0.86f, 1f), "alliance", "mage"),
                new UnitDefinition("gryphon_ranger", "Gryphon Ranger", 2, 128f, 24f, 0.95f, 4.2f, 2.5f, 90f, 22f, AbilityType.None, new Color(0.70f, 0.83f, 0.95f), "alliance", "hunter"),
                new UnitDefinition("warsong_blademaster", "Warsong Blademaster", 1, 160f, 25f, 0.92f, 1.4f, 2.95f, 100f, 26f, AbilityType.Whirlwind, new Color(0.77f, 0.23f, 0.23f), "horde", "warrior"),
                new UnitDefinition("spirit_caller", "Spirit Caller", 2, 132f, 20f, 0.8f, 3.5f, 2.45f, 75f, 34f, AbilityType.ChainLightning, new Color(0.16f, 0.70f, 0.56f), "horde", "shaman"),
                new UnitDefinition("shadow_hunter", "Shadow Hunter", 3, 118f, 30f, 1.05f, 1.5f, 3.25f, 70f, 36f, AbilityType.ShadowStrike, new Color(0.44f, 0.27f, 0.15f), "horde", "rogue"),
                new UnitDefinition("deathbound_lich", "Deathbound Lich", 3, 130f, 20f, 0.8f, 3.8f, 2.3f, 70f, 45f, AbilityType.ArcaneBurst, new Color(0.56f, 0.90f, 0.92f), "scourge", "mage"),
                new UnitDefinition("boneguard", "Boneguard", 1, 175f, 21f, 0.82f, 1.3f, 2.6f, 100f, 20f, AbilityType.None, new Color(0.82f, 0.82f, 0.76f), "scourge", "warrior"),
                new UnitDefinition("crypt_stalker", "Crypt Stalker", 2, 122f, 28f, 1.02f, 1.45f, 3.2f, 75f, 32f, AbilityType.ShadowStrike, new Color(0.40f, 0.78f, 0.45f), "scourge", "rogue"),
                new UnitDefinition("emerald_sage", "Emerald Sage", 2, 135f, 17f, 0.82f, 3.7f, 2.35f, 75f, 34f, AbilityType.HolyLight, new Color(0.28f, 0.82f, 0.33f), "cenarion", "druid"),
                new UnitDefinition("moonclaw_sentinel", "Moonclaw Sentinel", 3, 142f, 26f, 0.92f, 4.0f, 2.7f, 85f, 28f, AbilityType.Starfall, new Color(0.49f, 0.76f, 0.60f), "cenarion", "hunter"),
                new UnitDefinition("rune_warden", "Rune Warden", 3, 188f, 23f, 0.84f, 1.4f, 2.7f, 90f, 30f, AbilityType.HolyLight, new Color(0.89f, 0.76f, 0.36f), "titanforged", "paladin"),
                new UnitDefinition("forge_speaker", "Forge Speaker", 4, 150f, 22f, 0.85f, 3.9f, 2.4f, 65f, 48f, AbilityType.ChainLightning, new Color(0.94f, 0.62f, 0.20f), "titanforged", "shaman"),
            };
        }

        public static List<EnemyWaveEntry> BuildWave(int round)
        {
            if (round <= 1) return new List<EnemyWaveEntry> { new EnemyWaveEntry("boneguard", 2) };
            if (round == 2) return new List<EnemyWaveEntry> { new EnemyWaveEntry("boneguard", 2), new EnemyWaveEntry("crypt_stalker", 1) };
            if (round == 3) return new List<EnemyWaveEntry> { new EnemyWaveEntry("boneguard", 2), new EnemyWaveEntry("deathbound_lich", 1) };
            if (round == 4) return new List<EnemyWaveEntry> { new EnemyWaveEntry("boneguard", 2), new EnemyWaveEntry("deathbound_lich", 1), new EnemyWaveEntry("crypt_stalker", 1) };
            if (round == 5) return new List<EnemyWaveEntry> { new EnemyWaveEntry("boneguard", 2), new EnemyWaveEntry("deathbound_lich", 1), new EnemyWaveEntry("spirit_caller", 1), new EnemyWaveEntry("shadow_hunter", 1) };

            return new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry("boneguard", 2 + Mathf.Min(2, round / 4)),
                new EnemyWaveEntry("deathbound_lich", 1 + round / 5),
                new EnemyWaveEntry("spirit_caller", 1),
                new EnemyWaveEntry("shadow_hunter", 1),
            };
        }
    }
}
