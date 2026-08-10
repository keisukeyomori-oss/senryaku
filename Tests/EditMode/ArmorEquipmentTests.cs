using System.Linq;
using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    public sealed class ArmorEquipmentTests
    {
        [Test]
        public void EveryClassHasACompatibleDefaultArmor()
        {
            string[] classes = { "knight", "cavalry", "archer", "flier", "mage", "cleric", "trickster" };
            foreach (string className in classes)
            {
                ArmorId armor = ArmorEquipmentCatalog.GetDefaultArmor(className);
                Assert.That(ArmorEquipmentCatalog.IsCompatible(className, armor), Is.True, className);
                Assert.That(ArmorEquipmentCatalog.GetCompatibleArmors(className), Is.Not.Empty, className);
            }
        }

        [Test]
        public void PreparationRejectsIncompatibleArmorAndCarriesValidArmorIntoBattle()
        {
            StageData stage = Duel(ArmorId.TravelGear);
            BattlePreparationState preparation = BattlePreparationState.Create(stage);

            Assert.That(preparation.SetArmor("player", ArmorId.MysticRobe), Is.False);
            Assert.That(preparation.SetArmor("player", ArmorId.KnightPlate), Is.True);

            FormationCombatant player = new FormationBattleCore(preparation.CreateBattleStage())
                .Units.Single(unit => unit.Id == "player");
            Assert.That(player.ArmorId, Is.EqualTo(ArmorId.KnightPlate));
            Assert.That(player.MaxHp, Is.EqualTo(132));
            Assert.That(player.ArmorDamageReductionPercent, Is.EqualTo(16));
        }

        [Test]
        public void HeavyArmorReducesIncomingDamageDeterministically()
        {
            FormationAction unarmored = new FormationBattleCore(Duel(ArmorId.TravelGear)).Advance();
            FormationAction plated = new FormationBattleCore(Duel(ArmorId.KnightPlate)).Advance();

            Assert.That(unarmored.Actor.Id, Is.EqualTo("player"));
            Assert.That(plated.Actor.Id, Is.EqualTo("player"));
            Assert.That(plated.Damage, Is.LessThan(unarmored.Damage));
            Assert.That(plated.Target.ArmorDamageReductionPercent, Is.EqualTo(16));
        }

        private static StageData Duel(ArmorId enemyArmor)
        {
            return new StageData
            {
                id = "armor-duel",
                width = 9,
                height = 7,
                units = new[]
                {
                    Unit("player", "player", 0, ArmorId.TravelGear),
                    Unit("enemy", "enemy", 1, enemyArmor)
                }
            };
        }

        private static StageUnitData Unit(string id, string team, int x, ArmorId armor)
        {
            return new StageUnitData
            {
                id = id,
                sourceUnitId = id == "player" ? "hero" : "e_knight",
                displayName = id,
                className = "knight",
                team = team,
                level = 1,
                x = x,
                y = 0,
                maxHp = 100,
                damage = id == "player" ? 20 : 1,
                moveRange = 1,
                attackRange = 1,
                weaponId = WeaponId.Sword,
                armorId = armor,
                tactic = TacticPolicy.Balanced
            };
        }
    }
}
