using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    public enum ArmorId
    {
        TravelGear,
        LeatherVest,
        ChainMail,
        KnightPlate,
        MysticRobe,
        WingMantle
    }

    public sealed class ArmorDefinition
    {
        public ArmorId Id { get; }
        public string DisplayName { get; }
        public int MaxHpPercent { get; }
        public int DamageReductionPercent { get; }
        public int SpeedModifier { get; }
        public IReadOnlyList<string> CompatibleClasses { get; }

        public ArmorDefinition(
            ArmorId id,
            string displayName,
            int maxHpPercent,
            int damageReductionPercent,
            int speedModifier,
            params string[] compatibleClasses)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            MaxHpPercent = Math.Max(1, maxHpPercent);
            DamageReductionPercent = Math.Max(0, Math.Min(80, damageReductionPercent));
            SpeedModifier = speedModifier;
            CompatibleClasses = compatibleClasses ?? Array.Empty<string>();
        }

        public bool Supports(string className) => CompatibleClasses.Any(candidate =>
            string.Equals(candidate, className, StringComparison.OrdinalIgnoreCase));
    }

    public static class ArmorEquipmentCatalog
    {
        private static readonly ArmorDefinition[] Armors =
        {
                new ArmorDefinition(ArmorId.TravelGear, "旅装", 100, 0, 0,
                "knight", "cavalry", "archer", "flier", "mage", "cleric", "trickster"),
            new ArmorDefinition(ArmorId.LeatherVest, "革の胴衣", 110, 5, 0,
                "knight", "cavalry", "archer", "trickster"),
            new ArmorDefinition(ArmorId.ChainMail, "鎖帷子", 120, 10, -5,
                "knight", "cavalry", "archer"),
            new ArmorDefinition(ArmorId.KnightPlate, "騎士の胸甲", 132, 16, -10,
                "knight", "cavalry"),
            new ArmorDefinition(ArmorId.MysticRobe, "術師の長衣", 112, 8, 2,
                "mage", "cleric"),
            new ArmorDefinition(ArmorId.WingMantle, "風渡りの外套", 106, 6, 8,
                "flier", "trickster", "mage")
        };

        public static IReadOnlyList<ArmorDefinition> AllArmors => Armors;

        public static ArmorDefinition GetArmor(ArmorId armorId)
        {
            ArmorDefinition armor = Armors.FirstOrDefault(candidate => candidate.Id == armorId);
            if (armor == null) throw new ArgumentOutOfRangeException(nameof(armorId), armorId, null);
            return armor;
        }

        public static IReadOnlyList<ArmorDefinition> GetCompatibleArmors(string className)
        {
            ArmorDefinition[] compatible = Armors.Where(armor => armor.Supports(className)).ToArray();
            if (compatible.Length == 0)
                throw new ArgumentException($"Unknown equipment class: {className}", nameof(className));
            return compatible;
        }

        public static bool IsCompatible(string className, ArmorId armorId) =>
            GetArmor(armorId).Supports(className);

        public static ArmorId GetDefaultArmor(string className)
        {
            switch (className)
            {
                case "knight": return ArmorId.ChainMail;
                case "cavalry": return ArmorId.LeatherVest;
                case "archer": return ArmorId.LeatherVest;
                case "flier": return ArmorId.WingMantle;
                case "mage": return ArmorId.MysticRobe;
                case "cleric": return ArmorId.MysticRobe;
                case "trickster": return ArmorId.WingMantle;
                default: return ArmorId.TravelGear;
            }
        }
    }
}
