using System;
using System.Collections.Generic;
using System.Linq;

using BirthdayTactics.Core;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    /// <summary>
    /// 試練ステージの生成と銘器効果の契約を固定する。
    ///
    /// 特に重要なのは「試練が本当に理不尽な強さになっていること」と
    /// 「隊列の5枠を超えないこと」の2点。後者を破ると6体目以降が
    /// 5体目に完全に重なって見えなくなる。
    /// </summary>
    public sealed class OrdealStageCoreTests
    {
        private ContentCatalogData _catalog;
        private StageData _finalStage;

        [SetUp]
        public void LoadCatalog()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/mu2_content");
            Assert.That(json, Is.Not.Null, "M-U2 content catalog is missing.");
            _catalog = JsonUtility.FromJson<ContentCatalogData>(json.text);
            _finalStage = _catalog.stages.Last();
        }

        [Test]
        public void EveryOrdeal_BuildsAPlayableStageWithinTheFormationBudget()
        {
            foreach (OrdealEncounter ordeal in StoryChoicePolicy.AllOrdeals)
            {
                StageData stage = OrdealStagePolicy.BuildStage(_finalStage, ordeal);

                Assert.That(stage.units, Is.Not.Empty, ordeal.Id);
                Assert.That(
                    stage.units.Select(unit => unit.id).Distinct().Count(),
                    Is.EqualTo(stage.units.Length),
                    $"{ordeal.Id} でユニットIDが重複しています。");

                foreach (string team in new[] { OrdealStagePolicy.PlayerTeam, OrdealStagePolicy.EnemyTeam })
                {
                    int count = stage.units.Count(unit =>
                        string.Equals(unit.team, team, StringComparison.OrdinalIgnoreCase));
                    Assert.That(count, Is.InRange(1, OrdealStagePolicy.MaxUnitsPerTeam),
                        $"{ordeal.Id} の {team} が {count} 体で、隊列の5枠に収まりません。");
                }

                // FormationBattleCore がそのまま受け取れること。
                Assert.That(() => new FormationBattleCore(stage), Throws.Nothing, ordeal.Id);
            }
        }

        [Test]
        public void EveryOrdeal_IsOverwhelminglyStrongerThanTheFinalStage()
        {
            int finalEnemyHp = _finalStage.units
                .Where(unit => string.Equals(unit.team, "enemy", StringComparison.OrdinalIgnoreCase))
                .Sum(unit => unit.maxHp);

            foreach (OrdealEncounter ordeal in StoryChoicePolicy.AllOrdeals)
            {
                StageData stage = OrdealStagePolicy.BuildStage(_finalStage, ordeal);
                int ordealEnemyHp = stage.units
                    .Where(unit => string.Equals(unit.team, "enemy", StringComparison.OrdinalIgnoreCase))
                    .Sum(unit => unit.maxHp);

                Assert.That(
                    ordealEnemyHp,
                    Is.GreaterThan(finalEnemyHp),
                    $"{ordeal.Id} が最終戦より弱くなっています。");

                // 倍率が実際に効いていること（切り上げのため下限で判定）。
                Assert.That(
                    ordealEnemyHp,
                    Is.GreaterThanOrEqualTo((int)(finalEnemyHp * (ordeal.PowerMultiplier - 1f))),
                    ordeal.Id);
            }
        }

        [Test]
        public void SingleFoe_CarriesTheWholeEnemyArmyHitPoints()
        {
            OrdealEncounter ordeal = StoryChoicePolicy.AllOrdeals
                .First(candidate => candidate.FoeKind == OrdealFoeKind.SingleFoe);
            StageData stage = OrdealStagePolicy.BuildStage(_finalStage, ordeal);

            StageUnitData[] foes = stage.units
                .Where(unit => string.Equals(unit.team, "enemy", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.That(foes.Length, Is.EqualTo(1), "単体の試練が1体になっていません。");
            Assert.That(foes[0].sourceUnitId, Is.EqualTo(ordeal.FoeSourceUnitId));
            Assert.That(foes[0].displayName, Is.EqualTo(ordeal.Name));

            int finalEnemyHp = _finalStage.units
                .Where(unit => string.Equals(unit.team, "enemy", StringComparison.OrdinalIgnoreCase))
                .Sum(unit => unit.maxHp);
            Assert.That(
                foes[0].maxHp,
                Is.EqualTo((int)Math.Ceiling(finalEnemyHp * (double)ordeal.PowerMultiplier)));
        }

        [Test]
        public void SingleFoe_UsesTheAuthoredVisualArchetypeForCombatProperties()
        {
            OrdealEncounter ordeal = StoryChoicePolicy.AllOrdeals
                .Single(candidate => candidate.Id == "ordeal-encore");
            StageData stage = OrdealStagePolicy.BuildStage(_finalStage, ordeal);
            StageUnitData foe = stage.units.Single(unit =>
                string.Equals(unit.team, "enemy", StringComparison.OrdinalIgnoreCase));
            StageUnitData authoredArchetype = _finalStage.units.Single(unit =>
                string.Equals(
                    unit.sourceUnitId,
                    ordeal.FoeSourceUnitId,
                    StringComparison.Ordinal));

            Assert.That(foe.sourceUnitId, Is.EqualTo("e_cleric"));
            Assert.That(foe.className, Is.EqualTo(authoredArchetype.className));
            Assert.That(foe.weaponId, Is.EqualTo(authoredArchetype.weaponId));
            Assert.That(foe.moveRange, Is.EqualTo(authoredArchetype.moveRange));
            Assert.That(foe.attackRange, Is.EqualTo(authoredArchetype.attackRange));
            Assert.That(foe.tactic, Is.EqualTo(authoredArchetype.tactic));
        }

        [Test]
        public void MirrorOfParty_CopiesOurOwnCompositionExactly()
        {
            OrdealEncounter ordeal = StoryChoicePolicy.AllOrdeals
                .First(candidate => candidate.FoeKind == OrdealFoeKind.MirrorOfParty);
            StageData stage = OrdealStagePolicy.BuildStage(_finalStage, ordeal);

            StageUnitData[] players = stage.units
                .Where(unit => string.Equals(unit.team, "player", StringComparison.OrdinalIgnoreCase))
                .OrderBy(unit => unit.id, StringComparer.Ordinal)
                .ToArray();
            StageUnitData[] mirrors = stage.units
                .Where(unit => string.Equals(unit.team, "enemy", StringComparison.OrdinalIgnoreCase))
                .OrderBy(unit => unit.id, StringComparer.Ordinal)
                .ToArray();

            Assert.That(mirrors.Length, Is.EqualTo(players.Length), "鏡像の人数がこちらと違います。");

            foreach (StageUnitData player in players)
            {
                StageUnitData mirror = mirrors.Single(candidate =>
                    string.Equals(candidate.id, "ordeal-mirror-" + player.id, StringComparison.Ordinal));

                Assert.That(mirror.className, Is.EqualTo(player.className));
                Assert.That(mirror.sourceUnitId, Is.EqualTo(player.sourceUnitId));
                Assert.That(mirror.weaponId, Is.EqualTo(player.weaponId));
                Assert.That(mirror.maxHp, Is.GreaterThan(player.maxHp), "鏡像がこちらより弱いです。");
                Assert.That(mirror.damage, Is.GreaterThan(player.damage));
            }
        }

        [Test]
        public void BuildStage_IsDeterministicAndRejectsBrokenInput()
        {
            OrdealEncounter ordeal = StoryChoicePolicy.AllOrdeals[0];

            StageData first = OrdealStagePolicy.BuildStage(_finalStage, ordeal);
            StageData second = OrdealStagePolicy.BuildStage(_finalStage, ordeal);

            Assert.That(
                first.units.Select(unit => $"{unit.id}:{unit.maxHp}:{unit.damage}"),
                Is.EqualTo(second.units.Select(unit => $"{unit.id}:{unit.maxHp}:{unit.damage}")),
                "同じ入力から違うステージが生成されました。");

            Assert.That(() => OrdealStagePolicy.BuildStage(null, ordeal), Throws.ArgumentNullException);
            Assert.That(() => OrdealStagePolicy.BuildStage(_finalStage, null), Throws.ArgumentNullException);
            Assert.That(
                () => OrdealStagePolicy.BuildStage(
                    new StageData { id = "empty", units = Array.Empty<StageUnitData>() }, ordeal),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void OrdealStages_SortAfterEveryNormalStage()
        {
            int hardestNormal = _catalog.stages.Max(stage => stage.difficultyIndex);

            foreach (OrdealEncounter ordeal in StoryChoicePolicy.AllOrdeals)
            {
                StageData stage = OrdealStagePolicy.BuildStage(_finalStage, ordeal);
                Assert.That(stage.difficultyIndex, Is.GreaterThan(hardestNormal), ordeal.Id);
                Assert.That(stage.id, Does.StartWith("ordeal-"), ordeal.Id);
            }
        }

        [Test]
        public void RelicEffects_ActivateOnlyWhenTheRelicIsOwned()
        {
            var none = Array.Empty<string>();
            Assert.That(RelicEffectPolicy.NegatesGuard(none), Is.False);
            Assert.That(RelicEffectPolicy.RevivesOnceWhenFelled(none), Is.False);
            Assert.That(RelicEffectPolicy.IgnoresBondAdjacency(none), Is.False);

            var owned = new List<string>
            {
                StoryChoicePolicy.BuildRelicRecordId(RelicEffectPolicy.HushEdgeId),
                StoryChoicePolicy.BuildRelicRecordId(RelicEffectPolicy.ReturningCoatId),
                StoryChoicePolicy.BuildRelicRecordId(RelicEffectPolicy.DuetUnisonId)
            };
            Assert.That(RelicEffectPolicy.NegatesGuard(owned), Is.True);
            Assert.That(RelicEffectPolicy.RevivesOnceWhenFelled(owned), Is.True);
            Assert.That(RelicEffectPolicy.IgnoresBondAdjacency(owned), Is.True);

            // 3つの銘器IDが実在すること（綴り間違いで永久に無効化されるのを防ぐ）。
            foreach (string relicId in new[]
                     {
                         RelicEffectPolicy.HushEdgeId,
                         RelicEffectPolicy.ReturningCoatId,
                         RelicEffectPolicy.DuetUnisonId
                     })
            {
                Assert.That(StoryChoicePolicy.FindRelic(relicId), Is.Not.Null, relicId);
            }
        }

        /// <summary>
        /// 試練で得た銘器を次の試練に持ち込めると、3つ目がただの消化になる。
        /// 本編でだけ効くことを固定する。
        /// </summary>
        [Test]
        public void RelicEffects_AreDisabledInsideOrdeals()
        {
            StageData normal = _finalStage;
            StageData ordealStage = OrdealStagePolicy.BuildStage(
                _finalStage, StoryChoicePolicy.AllOrdeals[0]);

            Assert.That(RelicEffectPolicy.AppliesTo(normal), Is.True);
            Assert.That(RelicEffectPolicy.AppliesTo(ordealStage), Is.False);
            Assert.That(RelicEffectPolicy.AppliesTo(null), Is.False);
        }
    }
}
