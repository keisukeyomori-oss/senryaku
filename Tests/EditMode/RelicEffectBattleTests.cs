using System;
using System.Collections.Generic;
using System.Linq;

using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    /// <summary>
    /// 銘器の効果が実際に戦闘へ反映されることを検証する。
    ///
    /// 所持していないときに効果が出ないこと、試練の中では無効になることの2点を
    /// 特に固定する。前者が壊れると全員が銘器持ちになり、後者が壊れると
    /// 3つ目の試練がただの消化になる。
    /// </summary>
    public sealed class RelicEffectBattleTests
    {
        private static string[] Owned(params string[] relicIds)
        {
            return relicIds.Select(StoryChoicePolicy.BuildRelicRecordId).ToArray();
        }

        /// <summary>防御姿勢の敵1体に、味方1体がぶつかるだけの最小構成。</summary>
        private static StageData BuildGuardedDuel(string stageId)
        {
            return new StageData
            {
                id = stageId,
                displayName = "test",
                backgroundId = "forest_ruins",
                width = 8,
                height = 8,
                units = new[]
                {
                    new StageUnitData
                    {
                        id = "p1", sourceUnitId = "hero", displayName = "味方",
                        className = "knight", team = "player", level = 5,
                        x = 1, y = 1, maxHp = 200, moveRange = 3, attackRange = 1,
                        damage = 40, weaponId = WeaponId.Sword, tactic = TacticPolicy.Balanced
                    },
                    new StageUnitData
                    {
                        id = "e1", sourceUnitId = "e_knight", displayName = "敵",
                        className = "knight", team = "enemy", level = 5,
                        x = 6, y = 1, maxHp = 4000, moveRange = 3, attackRange = 1,
                        damage = 1, weaponId = WeaponId.Sword, tactic = TacticPolicy.Defensive
                    }
                }
            };
        }

        private static int FirstPlayerHit(StageData stage, IEnumerable<string> resolvedIds)
        {
            var battle = new FormationBattleCore(stage, resolvedIds);
            for (int guard = 0; guard < 40; guard++)
            {
                FormationAction action = battle.Advance();
                if (action == null) break;
                if (action.Actor.Team == BattleTeam.Player) return action.Damage;
            }
            throw new InvalidOperationException("味方の攻撃が発生しませんでした。");
        }

        [Test]
        public void HushEdge_IgnoresTheGuardStanceOnlyWhenOwned()
        {
            StageData stage = BuildGuardedDuel("stage-guard");

            int withoutRelic = FirstPlayerHit(stage, Array.Empty<string>());
            int withRelic = FirstPlayerHit(stage, Owned(RelicEffectPolicy.HushEdgeId));

            Assert.That(
                withRelic,
                Is.GreaterThan(withoutRelic),
                "静寂の刃を持っていても防御姿勢を貫けていません。");
        }

        [Test]
        public void HushEdge_IsInertInsideAnOrdeal()
        {
            // ステージIDが ordeal- で始まると RelicEffectPolicy.AppliesTo が false になる。
            StageData ordealStage = BuildGuardedDuel("ordeal-stage-guard");

            int withoutRelic = FirstPlayerHit(ordealStage, Array.Empty<string>());
            int withRelic = FirstPlayerHit(ordealStage, Owned(RelicEffectPolicy.HushEdgeId));

            Assert.That(
                withRelic,
                Is.EqualTo(withoutRelic),
                "試練の中で銘器が効いてしまっています。");
        }

        /// <summary>味方が確実に倒される構成。外套を持つと1度だけHP1で耐える。</summary>
        private static StageData BuildLethalStage(string stageId)
        {
            return new StageData
            {
                id = stageId,
                displayName = "test",
                backgroundId = "forest_ruins",
                width = 8,
                height = 8,
                units = new[]
                {
                    new StageUnitData
                    {
                        id = "p1", sourceUnitId = "hero", displayName = "味方",
                        className = "archer", team = "player", level = 1,
                        x = 1, y = 1, maxHp = 10, moveRange = 3, attackRange = 1,
                        damage = 1, weaponId = WeaponId.Sword, tactic = TacticPolicy.Balanced
                    },
                    new StageUnitData
                    {
                        id = "e1", sourceUnitId = "e_knight", displayName = "敵",
                        className = "cavalry", team = "enemy", level = 20,
                        x = 6, y = 1, maxHp = 9000, moveRange = 3, attackRange = 1,
                        damage = 900, weaponId = WeaponId.Sword, tactic = TacticPolicy.Aggressive
                    }
                }
            };
        }

        private static (int survivedHits, bool everRevivedToOne) RunUntilPlayerFalls(
            StageData stage,
            IEnumerable<string> resolvedIds)
        {
            var battle = new FormationBattleCore(stage, resolvedIds);
            int hitsTaken = 0;
            bool sawOneHp = false;

            for (int guard = 0; guard < 60; guard++)
            {
                FormationAction action = battle.Advance();
                if (action == null) break;
                if (action.Actor.Team != BattleTeam.Enemy) continue;

                hitsTaken++;
                FormationCombatant player = battle.Units.First(unit => unit.Team == BattleTeam.Player);
                if (player.IsAlive && player.Hp == 1) sawOneHp = true;
                if (!player.IsAlive) break;
            }

            return (hitsTaken, sawOneHp);
        }

        [Test]
        public void ReturningCoat_SurvivesOneLethalBlowThenNoMore()
        {
            StageData stage = BuildLethalStage("stage-lethal");

            (int baseHits, bool baseRevive) = RunUntilPlayerFalls(stage, Array.Empty<string>());
            (int coatHits, bool coatRevive) =
                RunUntilPlayerFalls(stage, Owned(RelicEffectPolicy.ReturningCoatId));

            Assert.That(baseRevive, Is.False, "外套なしでHP1に踏みとどまっています。");
            Assert.That(coatRevive, Is.True, "外套を持っていても倒れるはずの一撃を耐えていません。");
            Assert.That(
                coatHits,
                Is.GreaterThan(baseHits),
                "外套があるのに耐えた回数が増えていません。");
        }

        [Test]
        public void ReturningCoat_IsInertInsideAnOrdeal()
        {
            StageData ordealStage = BuildLethalStage("ordeal-stage-lethal");

            (_, bool revived) =
                RunUntilPlayerFalls(ordealStage, Owned(RelicEffectPolicy.ReturningCoatId));

            Assert.That(revived, Is.False, "試練の中で外套が効いてしまっています。");
        }

        /// <summary>
        /// 連携できる2人を、わざと離れたスロットに置く。
        /// 二重奏がなければ隣接条件で弾かれ、あれば連携が成立する。
        /// </summary>
        private static StageData BuildSplitPartyStage(string stageId)
        {
            var units = new List<StageUnitData>
            {
                new StageUnitData
                {
                    id = "p1", sourceUnitId = "hero", displayName = "前衛",
                    className = "knight", team = "player", level = 5,
                    x = 1, y = 0, maxHp = 400, moveRange = 3, attackRange = 1,
                    damage = 30, weaponId = WeaponId.Sword, tactic = TacticPolicy.Balanced
                },
                // 間に挟まる味方。これで p1 と p3 は隣接しなくなる。
                new StageUnitData
                {
                    id = "p2", sourceUnitId = "azuki", displayName = "中衛",
                    className = "knight", team = "player", level = 5,
                    x = 1, y = 1, maxHp = 400, moveRange = 3, attackRange = 1,
                    damage = 30, weaponId = WeaponId.Sword, tactic = TacticPolicy.Balanced
                },
                // 攻撃種別が違うこと（連携の条件）。
                new StageUnitData
                {
                    id = "p3", sourceUnitId = "partner", displayName = "後衛",
                    className = "mage", team = "player", level = 5,
                    x = 1, y = 2, maxHp = 400, moveRange = 3, attackRange = 3,
                    damage = 30, weaponId = WeaponId.Grimoire, tactic = TacticPolicy.Balanced
                },
                new StageUnitData
                {
                    id = "e1", sourceUnitId = "e_knight", displayName = "敵",
                    className = "knight", team = "enemy", level = 5,
                    x = 6, y = 1, maxHp = 20000, moveRange = 3, attackRange = 1,
                    damage = 1, weaponId = WeaponId.Sword, tactic = TacticPolicy.Balanced
                }
            };

            return new StageData
            {
                id = stageId,
                displayName = "test",
                backgroundId = "forest_ruins",
                width = 8,
                height = 8,
                units = units.ToArray()
            };
        }

        /// <summary>
        /// 観測できた「味方どうしの連携」のうち、最も離れたスロット距離を返す。
        /// 隣接だけなら1、離れた相手と組めていれば2以上になる。
        /// 連携が一度も起きなければ0。
        /// </summary>
        private static int WidestCooperationSpan(StageData stage, IEnumerable<string> resolvedIds)
        {
            var battle = new FormationBattleCore(stage, resolvedIds);
            int widest = 0;

            for (int guard = 0; guard < 120; guard++)
            {
                FormationAction action = battle.Advance();
                if (action == null) break;
                if (!action.IsCooperation || action.Cooperator == null) continue;
                if (action.Actor.Team != BattleTeam.Player) continue;

                int span = Math.Abs(action.Actor.FormationSlot - action.Cooperator.FormationSlot);
                if (span > widest) widest = span;
            }

            return widest;
        }

        [Test]
        public void DuetUnison_LetsNonAdjacentAlliesCooperate()
        {
            StageData stage = BuildSplitPartyStage("stage-split");

            int withoutRelic = WidestCooperationSpan(stage, Array.Empty<string>());
            int withRelic = WidestCooperationSpan(stage, Owned(RelicEffectPolicy.DuetUnisonId));

            // 銘器なしでも隣接どうしの連携は起きるので、0ではなく「1どまり」を確認する。
            Assert.That(withoutRelic, Is.EqualTo(1), "銘器なしで隣接以外の連携が起きています。");
            Assert.That(
                withRelic,
                Is.GreaterThanOrEqualTo(2),
                "二重奏を持っていても離れた味方と連携できていません。");
        }

        [Test]
        public void DuetUnison_IsInertInsideAnOrdeal()
        {
            StageData ordeal = BuildSplitPartyStage("ordeal-stage-split");
            string[] owned = Owned(RelicEffectPolicy.DuetUnisonId);

            Assert.That(
                WidestCooperationSpan(ordeal, owned),
                Is.EqualTo(1),
                "試練の中で二重奏が効いてしまっています。");
        }

        /// <summary>
        /// 銘器は味方だけのもの。敵にまで効くと、鏡像の試練が理不尽になる。
        /// </summary>
        [Test]
        public void Relics_NeverBenefitTheEnemyTeam()
        {
            StageData stage = BuildGuardedDuel("stage-enemy-check");
            string[] all = Owned(
                RelicEffectPolicy.HushEdgeId,
                RelicEffectPolicy.ReturningCoatId,
                RelicEffectPolicy.DuetUnisonId);

            var battle = new FormationBattleCore(stage, all);
            FormationCombatant enemy = battle.Units.First(unit => unit.Team == BattleTeam.Enemy);
            int enemyStartHp = enemy.Hp;

            for (int guard = 0; guard < 20; guard++)
            {
                if (battle.Advance() == null) break;
            }

            Assert.That(enemy.Hp, Is.LessThan(enemyStartHp), "敵に一切ダメージが入っていません。");
        }

        [Test]
        public void BattleWithoutRelicArgument_BehavesExactlyLikeNoRelics()
        {
            StageData stage = BuildGuardedDuel("stage-parity");

            int legacy = FirstPlayerHit(stage, null);
            int explicitNone = FirstPlayerHit(stage, Array.Empty<string>());

            Assert.That(legacy, Is.EqualTo(explicitNone));
        }
    }
}
