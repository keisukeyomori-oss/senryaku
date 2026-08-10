using System;
using System.Linq;
using BirthdayTactics.Core;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    public sealed class BattlePreparationTests
    {
        [Test]
        public void EnemyPreview_MatchesEveryCurrentStageEnemy()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/mu2_content");
            Assert.That(json, Is.Not.Null);
            ContentCatalogData catalog = JsonUtility.FromJson<ContentCatalogData>(json.text);

            foreach (StageData stage in catalog.stages)
            {
                BattlePreparationState preparation = BattlePreparationState.Create(stage);
                StageUnitData[] enemies = stage.units
                    .Where(unit => string.Equals(unit.team, "enemy", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                Assert.That(preparation.Enemies.Count, Is.EqualTo(enemies.Length), stage.id);
                foreach (StageUnitData source in enemies)
                {
                    EnemyPreview preview = preparation.Enemies.Single(enemy => enemy.UnitId == source.id);
                    Assert.That(preview.DisplayName, Is.EqualTo(source.displayName), source.id);
                    Assert.That(preview.ClassName, Is.EqualTo(source.className), source.id);
                    Assert.That(preview.Level, Is.EqualTo(Math.Max(1, source.level)), source.id);
                    Assert.That(preview.MaxHp, Is.EqualTo(Math.Max(1, source.maxHp)), source.id);
                    Assert.That(
                        preview.AttackKind,
                        Is.EqualTo(ExpectedAttackKind(source.className)),
                        source.id);
                }
            }
        }

        [Test]
        public void FormationSwap_IsDeterministicAndProducesContiguousSlots()
        {
            BattlePreparationState preparation = BattlePreparationState.Create(CreateStage());

            Assert.That(preparation.MoveUnit("p_mage", -1), Is.True);
            Assert.That(
                preparation.Loadouts.Select(loadout => loadout.unitId),
                Is.EqualTo(new[] { "p_mage", "p_knight", "p_archer" }));
            Assert.That(
                preparation.Loadouts.Select(loadout => loadout.formationSlot),
                Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(preparation.MoveUnit("p_mage", -1), Is.False);

            StageData battleStage = preparation.CreateBattleStage();
            FormationBattleCore battle = new FormationBattleCore(battleStage);
            Assert.That(
                battle.Units
                    .Where(unit => unit.Team == BattleTeam.Player)
                    .OrderBy(unit => unit.FormationSlot)
                    .Select(unit => unit.Id),
                Is.EqualTo(new[] { "p_mage", "p_knight", "p_archer" }));
        }

        [Test]
        public void EveryClassDefaultWeapon_IsCompatible()
        {
            string[] classes = { "knight", "cavalry", "archer", "flier", "mage", "cleric", "trickster" };
            foreach (string className in classes)
            {
                WeaponId weapon = BattlePreparationCatalog.GetDefaultWeapon(className);
                Assert.That(BattlePreparationCatalog.IsCompatible(className, weapon), Is.True, className);
                Assert.That(BattlePreparationCatalog.GetCompatibleWeapons(className), Is.Not.Empty, className);
            }
        }

        [Test]
        public void IncompatibleWeapon_IsRejectedWithoutChangingLoadout()
        {
            BattlePreparationState preparation = BattlePreparationState.Create(CreateStage());
            WeaponId before = preparation.GetLoadout("p_knight").weaponId;

            Assert.That(preparation.SetWeapon("p_knight", WeaponId.Bow), Is.False);
            Assert.That(preparation.GetLoadout("p_knight").weaponId, Is.EqualTo(before));
            Assert.That(preparation.SetWeapon("p_knight", WeaponId.Lance), Is.True);
            Assert.That(preparation.GetLoadout("p_knight").weaponId, Is.EqualTo(WeaponId.Lance));
        }

        [Test]
        public void TacticSelection_IsStoredPerUnit()
        {
            BattlePreparationState preparation = BattlePreparationState.Create(CreateStage());

            preparation.SetTactic("p_archer", TacticPolicy.Defensive);
            preparation.SetTactic("p_mage", TacticPolicy.Aggressive);

            Assert.That(preparation.GetLoadout("p_archer").tactic, Is.EqualTo(TacticPolicy.Defensive));
            Assert.That(preparation.GetLoadout("p_mage").tactic, Is.EqualTo(TacticPolicy.Aggressive));
            Assert.That(preparation.GetLoadout("p_knight").tactic, Is.EqualTo(TacticPolicy.Balanced));
        }

        [Test]
        public void SavedPreparation_NormalizesDuplicatesAndInvalidSelections()
        {
            StageData stage = CreateStage();
            var saved = new StagePreparationData
            {
                stageId = stage.id,
                loadouts = new[]
                {
                    new UnitLoadout
                    {
                        unitId = "p_knight",
                        formationSlot = 9,
                        weaponId = WeaponId.Bow,
                        tactic = (TacticPolicy)99
                    },
                    new UnitLoadout
                    {
                        unitId = "p_knight",
                        formationSlot = 0,
                        weaponId = WeaponId.Lance,
                        tactic = TacticPolicy.Aggressive
                    },
                    new UnitLoadout
                    {
                        unitId = "missing",
                        formationSlot = -3,
                        weaponId = WeaponId.Bow,
                        tactic = TacticPolicy.Defensive
                    }
                }
            };

            BattlePreparationState preparation = BattlePreparationState.Create(stage, saved);

            Assert.That(preparation.Loadouts.Count, Is.EqualTo(3));
            Assert.That(preparation.Loadouts.Select(loadout => loadout.unitId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(preparation.Loadouts.Select(loadout => loadout.formationSlot), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(preparation.GetLoadout("p_knight").weaponId, Is.EqualTo(WeaponId.Sword));
            Assert.That(preparation.GetLoadout("p_knight").tactic, Is.EqualTo(TacticPolicy.Balanced));
        }

        [Test]
        public void CreatingBattleStage_DoesNotMutateAuthoredStage()
        {
            StageData stage = CreateStage();
            int[] originalY = stage.units.Select(unit => unit.y).ToArray();
            StageUnitData[] originalUnits = stage.units;
            BattlePreparationState preparation = BattlePreparationState.Create(stage);
            preparation.MoveUnit("p_archer", -1);
            preparation.MoveUnit("p_archer", -1);

            StageData battleStage = preparation.CreateBattleStage();

            Assert.That(stage.units, Is.SameAs(originalUnits));
            Assert.That(stage.units.Select(unit => unit.y), Is.EqualTo(originalY));
            Assert.That(battleStage, Is.Not.SameAs(stage));
            Assert.That(battleStage.units, Is.Not.SameAs(stage.units));
            Assert.That(
                battleStage.units.Single(unit => unit.id == "e_mage").y,
                Is.EqualTo(stage.units.Single(unit => unit.id == "e_mage").y));
        }

        [Test]
        public void PreparedWeaponAndTactic_AreCarriedIntoBattleWithoutChangingDamage()
        {
            StageData stage = CreateStage();
            int authoredDamage = stage.units.Single(unit => unit.id == "p_knight").damage;
            BattlePreparationState preparation = BattlePreparationState.Create(stage);
            preparation.SetWeapon("p_knight", WeaponId.Lance);
            preparation.SetTactic("p_knight", TacticPolicy.Defensive);

            FormationBattleCore battle = new FormationBattleCore(preparation.CreateBattleStage());
            FormationCombatant knight = battle.Units.Single(unit => unit.Id == "p_knight");

            Assert.That(knight.WeaponId, Is.EqualTo(WeaponId.Lance));
            Assert.That(knight.Tactic, Is.EqualTo(TacticPolicy.Defensive));
            Assert.That(knight.Damage, Is.EqualTo(authoredDamage));
            Assert.That(stage.units.Single(unit => unit.id == "p_knight").weaponId, Is.EqualTo(default(WeaponId)));
            Assert.That(stage.units.Single(unit => unit.id == "p_knight").tactic, Is.EqualTo(default(TacticPolicy)));
        }

        [Test]
        public void CounterPlan_ImprovesAssessmentAndCarriesRecommendationsIntoBattle()
        {
            BattlePreparationState preparation = BattlePreparationState.Create(CreateStage());
            PreparationAssessment before = preparation.Assess();

            preparation.ApplyCounterPlan();
            PreparationAssessment after = preparation.Assess();
            StageData preparedStage = preparation.CreateBattleStage();
            FormationBattleCore battle = new FormationBattleCore(preparedStage);

            Assert.That(after.Score, Is.GreaterThan(before.Score));
            Assert.That(after.Readiness, Is.EqualTo(PreparationReadiness.Ready));
            Assert.That(after.MatchedWeapons, Is.EqualTo(preparation.Loadouts.Count));
            Assert.That(after.MatchedTactics, Is.EqualTo(preparation.Loadouts.Count));
            Assert.That(after.DurableFrontline, Is.True);
            Assert.That(preparation.Loadouts[0].unitId, Is.EqualTo("p_knight"));
            Assert.That(preparation.Loadouts[0].tactic, Is.EqualTo(TacticPolicy.Defensive));
            Assert.That(
                battle.Units.Single(unit => unit.Id == "p_knight").Tactic,
                Is.EqualTo(TacticPolicy.Defensive));
            Assert.That(
                battle.Units.Single(unit => unit.Id == "p_mage").Tactic,
                Is.EqualTo(TacticPolicy.Aggressive));
        }

        [Test]
        public void EnemyPreview_AlwaysProvidesAnActionableCounterHint()
        {
            BattlePreparationState preparation = BattlePreparationState.Create(CreateStage());

            Assert.That(
                preparation.Enemies.All(enemy => !string.IsNullOrWhiteSpace(enemy.CounterHint)),
                Is.True);
            Assert.That(
                preparation.Enemies.Single(enemy => enemy.UnitId == "e_boss").CounterHint,
                Does.Contain("守勢"));
        }

        [Test]
        public void ExpeditionRewards_ApplyToPlayersWithoutMutatingAuthoredStage()
        {
            StageData stage = CreateStage();
            BattlePreparationState preparation = BattlePreparationState.Create(stage);

            StageData medicalStage = preparation.CreateBattleStage(
                2,
                FieldSupportType.Medical);
            StageUnitData medicalKnight = medicalStage.units.Single(
                unit => unit.id == "p_knight");
            StageUnitData enemyKnight = medicalStage.units.Single(
                unit => unit.id == "e_knight");

            Assert.That(medicalKnight.maxHp, Is.EqualTo(214));
            Assert.That(medicalKnight.damage, Is.EqualTo(21));
            Assert.That(enemyKnight.maxHp, Is.EqualTo(190));
            Assert.That(enemyKnight.damage, Is.EqualTo(22));
            Assert.That(
                stage.units.Single(unit => unit.id == "p_knight").maxHp,
                Is.EqualTo(180));
            Assert.That(
                stage.units.Single(unit => unit.id == "p_knight").damage,
                Is.EqualTo(20));

            StageUnitData reconKnight = preparation
                .CreateBattleStage(1, FieldSupportType.Recon)
                .units.Single(unit => unit.id == "p_knight");
            Assert.That(reconKnight.maxHp, Is.EqualTo(183));
            Assert.That(reconKnight.damage, Is.EqualTo(22));

            StageUnitData ambushKnight = preparation
                .CreateBattleStage(2, FieldSupportType.Ambush)
                .units.Single(unit => unit.id == "p_knight");
            Assert.That(ambushKnight.maxHp, Is.EqualTo(186));
            Assert.That(ambushKnight.damage, Is.EqualTo(26));
        }

        private static FormationActionKind ExpectedAttackKind(string className)
        {
            if (className == "mage" || className == "cleric") return FormationActionKind.Magic;
            if (className == "archer" || className == "flier") return FormationActionKind.Ranged;
            return FormationActionKind.Melee;
        }

        private static StageData CreateStage()
        {
            return new StageData
            {
                id = "prep-stage",
                displayName = "Preparation Test",
                recommendedLevel = 3,
                difficultyIndex = 2,
                width = 8,
                height = 6,
                units = new[]
                {
                    Unit("p_knight", "Hero", "knight", "player", 3, 0, 180, 20),
                    Unit("p_mage", "Mage", "mage", "player", 3, 1, 120, 28),
                    Unit("p_archer", "Archer", "archer", "player", 3, 2, 130, 24),
                    Unit("e_knight", "Enemy Knight", "knight", "enemy", 3, 0, 190, 22),
                    Unit("e_mage", "Enemy Mage", "mage", "enemy", 4, 1, 125, 31),
                    Unit("e_boss", "Enemy Boss", "cavalry", "enemy", 5, 2, 260, 38)
                }
            };
        }

        private static StageUnitData Unit(
            string id,
            string displayName,
            string className,
            string team,
            int level,
            int y,
            int hp,
            int damage)
        {
            return new StageUnitData
            {
                id = id,
                sourceUnitId = id,
                displayName = displayName,
                className = className,
                team = team,
                level = level,
                x = team == "enemy" ? 6 : 1,
                y = y,
                maxHp = hp,
                moveRange = 2,
                attackRange = className == "mage" || className == "archer" ? 2 : 1,
                damage = damage
            };
        }
    }
}
