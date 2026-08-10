using System.Linq;
using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    public sealed class FormationBattleCoreTests
    {
        [Test]
        public void FormationBattle_IsDeterministicAndAlwaysFinishes()
        {
            StageData stage = CreateStage();
            string first = Resolve(stage);
            string second = Resolve(stage);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.StartWith("Player:"));
        }

        [Test]
        public void FormationBattle_UsesSlotsWithoutGridMovement()
        {
            var battle = new FormationBattleCore(CreateStage());

            Assert.That(battle.Units.Select(unit => unit.FormationSlot), Is.EquivalentTo(new[] { 0, 1, 0, 1 }));
            FormationAction action = battle.Advance();
            Assert.That(action, Is.Not.Null);
            Assert.That(action.Actor.Team, Is.Not.EqualTo(action.Target.Team));
        }

        [Test]
        public void BossSpecialsRepeatEverySecondOwnAction_AndNormalSpecialsEveryThird()
        {
            StageData bossStage = Duel(
                Unit("boss", "e_boss", "mage", "player", 1, 0, 10000, 1, WeaponId.Grimoire),
                Unit("target", "e_knight", "knight", "enemy", 7, 0, 10000, 1, WeaponId.Sword));
            StageData normalStage = Duel(
                Unit("mage", "e_mage", "mage", "player", 1, 0, 10000, 1, WeaponId.Grimoire),
                Unit("target", "e_knight", "knight", "enemy", 7, 0, 10000, 1, WeaponId.Sword));

            bool[] bossPattern = NextActionsBy(new FormationBattleCore(bossStage), "boss", 4)
                .Select(action => action.IsSpecial)
                .ToArray();
            bool[] normalPattern = NextActionsBy(new FormationBattleCore(normalStage), "mage", 4)
                .Select(action => action.IsSpecial)
                .ToArray();

            Assert.That(bossPattern, Is.EqualTo(new[] { false, true, false, true }));
            Assert.That(normalPattern, Is.EqualTo(new[] { false, false, true, false }));
        }

        [Test]
        public void WeaponProfile_ChangesAttackKindAndPower()
        {
            StageData bowStage = Duel(
                Unit("player", "archer", "archer", "player", 1, 0, 100, 20, WeaponId.Bow),
                Unit("enemy", "mage", "mage", "enemy", 7, 0, 100, 1, WeaponId.Grimoire));
            StageData daggerStage = Duel(
                Unit("player", "archer", "archer", "player", 1, 0, 100, 20, WeaponId.Daggers),
                Unit("enemy", "mage", "mage", "enemy", 7, 0, 100, 1, WeaponId.Grimoire));

            FormationAction bow = new FormationBattleCore(bowStage).Advance();
            FormationAction daggers = new FormationBattleCore(daggerStage).Advance();

            Assert.That(bow.Kind, Is.EqualTo(FormationActionKind.Ranged));
            Assert.That(bow.Damage, Is.EqualTo(18));
            Assert.That(daggers.Kind, Is.EqualTo(FormationActionKind.Melee));
            Assert.That(daggers.Damage, Is.EqualTo(17));
        }

        [Test]
        public void Tactics_ModifyDamageAndIncomingGuard()
        {
            FormationAction balanced = FirstPlayerAction(TacticPolicy.Balanced, TacticPolicy.Balanced);
            FormationAction aggressive = FirstPlayerAction(TacticPolicy.Aggressive, TacticPolicy.Balanced);
            FormationAction defensive = FirstPlayerAction(TacticPolicy.Defensive, TacticPolicy.Balanced);
            FormationAction againstAggressive = FirstPlayerAction(TacticPolicy.Balanced, TacticPolicy.Aggressive);
            FormationAction againstDefensive = FirstPlayerAction(TacticPolicy.Balanced, TacticPolicy.Defensive);

            Assert.That(balanced.Damage, Is.EqualTo(18));
            Assert.That(aggressive.Damage, Is.EqualTo(21));
            Assert.That(defensive.Damage, Is.EqualTo(15));
            Assert.That(againstAggressive.Damage, Is.EqualTo(19));
            Assert.That(againstDefensive.Damage, Is.EqualTo(13));
            Assert.That(againstDefensive.WasGuarded, Is.True);
        }

        [Test]
        public void AggressiveTargetsWeakEnemy_ButDefensiveEnemyIsDeprioritized()
        {
            StageData stage = new StageData
            {
                id = "target-policy",
                width = 9,
                height = 7,
                units = new[]
                {
                    Unit("player", "hero", "cavalry", "player", 1, 0, 100, 10, WeaponId.Lance, TacticPolicy.Aggressive),
                    Unit("enemy-front", "e_mage", "mage", "enemy", 7, 0, 100, 1, WeaponId.Grimoire),
                    Unit("enemy-weak", "e_archer", "archer", "enemy", 7, 1, 5, 1, WeaponId.Bow)
                }
            };

            FormationAction weakTarget = new FormationBattleCore(stage).Advance();
            stage.units[2].tactic = TacticPolicy.Defensive;
            FormationAction protectedTarget = new FormationBattleCore(stage).Advance();

            Assert.That(weakTarget.Target.Id, Is.EqualTo("enemy-weak"));
            Assert.That(protectedTarget.Target.Id, Is.EqualTo("enemy-front"));
        }

        [Test]
        public void Initiative_UsesClassWeaponAndTacticSpeed()
        {
            StageData stage = Duel(
                Unit("player", "archer", "archer", "player", 1, 0, 100, 10, WeaponId.Bow, TacticPolicy.Defensive),
                Unit("enemy", "archer", "archer", "enemy", 7, 0, 100, 10, WeaponId.Bow, TacticPolicy.Balanced));
            var battle = new FormationBattleCore(stage);

            FormationCombatant player = battle.GetUnit("player");
            FormationCombatant enemy = battle.GetUnit("enemy");
            FormationAction action = battle.Advance();

            Assert.That(player.InitiativeScore, Is.EqualTo(40));
            Assert.That(enemy.InitiativeScore, Is.EqualTo(50));
            Assert.That(action.Actor.Id, Is.EqualTo("enemy"));
        }

        [Test]
        public void InitiativePreview_ShowsNextLivingActorsWithoutAdvancingBattle()
        {
            var battle = new FormationBattleCore(CreateStage());
            string firstPreview = battle.GetUpcomingUnits(4).First().Id;

            FormationAction first = battle.Advance();
            string nextPreview = battle.GetUpcomingUnits(4).First().Id;

            Assert.That(first.Actor.Id, Is.EqualTo(firstPreview));
            Assert.That(nextPreview, Is.Not.EqualTo(first.Actor.Id));
            Assert.That(battle.ActionCount, Is.EqualTo(1));
            Assert.That(() => battle.GetUpcomingUnits(0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void Special_TriggersAfterCooldownAndResetsDeterministically()
        {
            var battle = new FormationBattleCore(Duel(
                Unit("player", "archer", "archer", "player", 1, 0, 500, 20, WeaponId.Bow),
                Unit("enemy", "mage", "mage", "enemy", 7, 0, 500, 1, WeaponId.Grimoire)));

            FormationAction[] playerActions = NextActionsBy(battle, "player", 3);

            Assert.That(playerActions.Select(action => action.IsSpecial), Is.EqualTo(new[] { false, false, true }));
            Assert.That(playerActions.Select(action => action.CooldownRemaining), Is.EqualTo(new[] { 1, 0, 2 }));
            Assert.That(playerActions[2].Damage, Is.GreaterThan(playerActions[1].Damage));
            Assert.That(playerActions[2].SpecialName, Is.EqualTo("FALCON VOLLEY"));
        }

        [Test]
        public void SpecialStatus_ChangesFollowingActionAndExpiresByTurns()
        {
            var battle = new FormationBattleCore(Duel(
                Unit("player", "archer", "archer", "player", 1, 0, 500, 20, WeaponId.Bow),
                Unit("enemy", "mage", "mage", "enemy", 7, 0, 500, 20, WeaponId.Grimoire)));

            FormationAction special = NextActionsBy(battle, "player", 3)[2];
            FormationCombatant enemy = battle.GetUnit("enemy");
            FormationAction weakenedSpecial = battle.Advance();

            Assert.That(special.AppliedStatus, Is.EqualTo(FormationStatus.Weakened));
            Assert.That(special.StatusRecipient, Is.SameAs(enemy));
            Assert.That(weakenedSpecial.Actor, Is.SameAs(enemy));
            Assert.That(weakenedSpecial.IsSpecial, Is.True);
            Assert.That(weakenedSpecial.Damage, Is.EqualTo(24));
            Assert.That(enemy.Status, Is.EqualTo(FormationStatus.Weakened));
            Assert.That(enemy.StatusTurns, Is.EqualTo(1));
        }

        [Test]
        public void WeaponRange_FiltersTargetsByFormationDistance()
        {
            StageData bowStage = RangeStage(WeaponId.Bow);
            StageData daggerStage = RangeStage(WeaponId.Daggers);

            FormationAction bow = new FormationBattleCore(bowStage).Advance();
            FormationAction daggers = new FormationBattleCore(daggerStage).Advance();

            Assert.That(bow.Target.Id, Is.EqualTo("enemy-weak"));
            Assert.That(bow.FormationDistance, Is.EqualTo(2));
            Assert.That(bow.WeaponRange, Is.EqualTo(4));
            Assert.That(bow.WasOutOfRange, Is.False);
            Assert.That(daggers.Target.Id, Is.EqualTo("enemy-front"));
            Assert.That(daggers.FormationDistance, Is.EqualTo(1));
            Assert.That(daggers.WeaponRange, Is.EqualTo(1));
        }

        [Test]
        public void Cooperation_ChargesAndUsesAdjacentDifferentWeaponPartner()
        {
            StageData stage = new StageData
            {
                id = "cooperation",
                width = 9,
                height = 7,
                units = new[]
                {
                    Unit("player-bow", "archer", "archer", "player", 1, 0, 500, 20, WeaponId.Bow),
                    Unit("player-blade", "archer", "archer", "player", 1, 1, 500, 20, WeaponId.Daggers),
                    Unit("enemy", "mage", "mage", "enemy", 7, 0, 1000, 1, WeaponId.Grimoire)
                }
            };
            var battle = new FormationBattleCore(stage);

            FormationAction cooperation = null;
            for (int i = 0; i < 12 && cooperation == null; i++)
            {
                FormationAction action = battle.Advance();
                if (action.IsCooperation) cooperation = action;
            }

            Assert.That(cooperation, Is.Not.Null);
            Assert.That(cooperation.Actor.Id, Is.EqualTo("player-bow"));
            Assert.That(cooperation.Cooperator.Id, Is.EqualTo("player-blade"));
            Assert.That(cooperation.Damage, Is.EqualTo(27));
            Assert.That(cooperation.Actor.CooperationCharge, Is.EqualTo(0));
            Assert.That(cooperation.Cooperator.CooperationCharge, Is.EqualTo(0));
        }

        private static string Resolve(StageData stage)
        {
            var battle = new FormationBattleCore(stage);
            var transcript = new System.Text.StringBuilder();
            while (battle.Winner == BattleWinner.None && battle.ActionCount < 100)
            {
                FormationAction action = battle.Advance();
                transcript.Append(action.Actor.Id).Append('>').Append(action.Target.Id).Append(':').Append(action.Damage).Append('|');
            }

            Assert.That(battle.Winner, Is.Not.EqualTo(BattleWinner.None));
            Assert.That(battle.ActionCount, Is.LessThan(100));
            return battle.Winner + ":" + transcript;
        }

        [Test]
        public void PlayerCommands_ControlActionKindDefenceAndEscape()
        {
            var cooperationBattle = new FormationBattleCore(CreateStage());
            FormationAction cooperation = cooperationBattle.Advance(
                new FormationBattleCommand(FormationCommandKind.Cooperation));
            Assert.That(cooperation.IsCooperation, Is.True);
            Assert.That(cooperation.CommandKind, Is.EqualTo(FormationCommandKind.Cooperation));

            var magicBattle = new FormationBattleCore(CreateStage());
            FormationAction magic = magicBattle.Advance(
                new FormationBattleCommand(FormationCommandKind.Magic));
            Assert.That(magic.Kind, Is.EqualTo(FormationActionKind.Magic));

            var defenceBattle = new FormationBattleCore(CreateStage());
            FormationAction defence = defenceBattle.Advance(
                new FormationBattleCommand(FormationCommandKind.Defend));
            Assert.That(defence.IsDefending, Is.True);
            Assert.That(defence.Actor.Status, Is.EqualTo(FormationStatus.Fortified));
            Assert.That(defence.Damage, Is.Zero);

            var escapeBattle = new FormationBattleCore(CreateStage());
            FormationAction escape = escapeBattle.Advance(
                new FormationBattleCommand(FormationCommandKind.Flee));
            Assert.That(escape.IsEscape, Is.True);
            Assert.That(escapeBattle.Winner, Is.EqualTo(BattleWinner.Escaped));
        }

        private static StageData CreateStage()
        {
            return new StageData
            {
                id = "formation-test",
                width = 9,
                height = 7,
                units = new[]
                {
                    Unit("hero", "hero", "knight", "player", 1, 1, 36, 10),
                    Unit("partner", "partner", "mage", "player", 1, 4, 28, 12),
                    Unit("enemy-a", "e_knight", "knight", "enemy", 7, 1, 28, 8),
                    Unit("enemy-b", "e_archer", "archer", "enemy", 7, 4, 24, 7)
                }
            };
        }

        private static StageData Duel(StageUnitData player, StageUnitData enemy)
        {
            return new StageData
            {
                id = "duel",
                width = 9,
                height = 7,
                units = new[] { player, enemy }
            };
        }

        private static StageData RangeStage(WeaponId weaponId)
        {
            return new StageData
            {
                id = "range",
                width = 9,
                height = 7,
                units = new[]
                {
                    Unit("player", "archer", "archer", "player", 1, 0, 100, 20, weaponId, TacticPolicy.Aggressive),
                    Unit("enemy-front", "mage", "mage", "enemy", 7, 0, 100, 1, WeaponId.Grimoire),
                    Unit("enemy-weak", "mage", "mage", "enemy", 7, 1, 5, 1, WeaponId.Grimoire)
                }
            };
        }

        private static FormationAction FirstPlayerAction(TacticPolicy actorTactic, TacticPolicy targetTactic)
        {
            StageData stage = Duel(
                Unit("player", "archer", "archer", "player", 1, 0, 100, 20, WeaponId.Bow, actorTactic),
                Unit("enemy", "mage", "mage", "enemy", 7, 0, 100, 1, WeaponId.Grimoire, targetTactic));
            return new FormationBattleCore(stage).Advance();
        }

        private static FormationAction[] NextActionsBy(
            FormationBattleCore battle,
            string actorId,
            int count)
        {
            var actions = new System.Collections.Generic.List<FormationAction>();
            while (actions.Count < count)
            {
                FormationAction action = battle.Advance();
                Assert.That(action, Is.Not.Null);
                if (action.Actor.Id == actorId) actions.Add(action);
            }
            return actions.ToArray();
        }

        private static StageUnitData Unit(
            string id,
            string source,
            string className,
            string team,
            int x,
            int y,
            int hp,
            int damage,
            WeaponId? weaponId = null,
            TacticPolicy tactic = TacticPolicy.Balanced)
        {
            return new StageUnitData
            {
                id = id,
                sourceUnitId = source,
                displayName = id,
                className = className,
                team = team,
                level = 1,
                x = x,
                y = y,
                maxHp = hp,
                damage = damage,
                moveRange = 1,
                attackRange = 1,
                weaponId = weaponId ?? BattlePreparationCatalog.GetDefaultWeapon(className),
                tactic = tactic
            };
        }
    }
}
