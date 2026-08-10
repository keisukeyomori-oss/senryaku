using System.Linq;
using BirthdayTactics.Core;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    public sealed class StoryExplorationCoreTests
    {
        [TestCase("town_guide")]
        [TestCase("town_smith")]
        [TestCase("town_herbalist")]
        [TestCase("town_gate_warden")]
        [TestCase("dungeon_scholar")]
        [TestCase("dungeon_lost_pilgrim")]
        [TestCase("interior_caretaker")]
        [TestCase("inn_host")]
        public void StoryNpcArt_LoadsDedicatedTexture(string assetId)
        {
            Texture2D texture = Resources.Load<Texture2D>(
                $"Art/Story/NPCs/{assetId}");

            Assert.That(texture, Is.Not.Null, assetId);
            Assert.That(texture.width, Is.GreaterThanOrEqualTo(900), assetId);
            Assert.That(texture.height, Is.GreaterThanOrEqualTo(1500), assetId);
        }

        [Test]
        public void StoryNpcArtPolicy_MapsEveryVisibleCharacterToLoadableArt()
        {
            foreach (StoryAreaKind areaKind in new[]
                     {
                         StoryAreaKind.Town,
                         StoryAreaKind.Interior,
                         StoryAreaKind.Inn,
                         StoryAreaKind.Base,
                         StoryAreaKind.Dungeon
                     })
            {
                StoryExplorationCore area = StoryExplorationCore.Create(
                    areaKind,
                    townGuideHeard: true,
                    memoryArcherJoined: true,
                    resolvedEntityIds: new[] { "interior-keepsake" },
                    memoryHealerJoined: true,
                    memoryMinstrelJoined: true);
                foreach (StoryEntity entity in area.Entities.Where(entity =>
                             entity.Kind == StoryEntityKind.Dialogue ||
                             entity.Kind == StoryEntityKind.Recruit))
                {
                    string resourcePath =
                        StoryNpcArtPolicy.ResourcePathForEntity(entity.Id);
                    Assert.That(resourcePath, Is.Not.Null.And.Not.Empty, entity.Id);
                    Assert.That(
                        Resources.Load<Texture2D>(resourcePath),
                        Is.Not.Null,
                        $"{entity.Id} -> {resourcePath}");
                }
            }
        }

        [Test]
        public void MemoryMinstrelArt_LoadsAllBattlePoses()
        {
            string[] assetIds = System.Enum
                .GetValues(typeof(BattlePose))
                .Cast<BattlePose>()
                .Select(pose => FormationPresentationProfile.GetPoseAssetId(
                    RecruitmentRosterPolicy.MemoryMinstrelId,
                    pose))
                .ToArray();

            foreach (string assetId in assetIds)
            {
                Texture2D texture = Resources.Load<Texture2D>(
                    $"Art/Battle/Units/{assetId}");

                Assert.That(texture, Is.Not.Null, assetId);
                Assert.That(
                    FormationPresentationProfile.GetSpriteMetrics(assetId).VisibleHeight,
                    Is.InRange(0.55f, 1f),
                    assetId);
            }

            Assert.That(assetIds.Distinct().Count(), Is.EqualTo(5));
        }

        [Test]
        public void Town_RequiresGuideDialogueBeforeDungeonTransfer()
        {
            StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);
            StoryEntity gate = town.FindEntity("town-dungeon-gate");
            StoryExplorationResult result = WalkTo(town, gate);

            Assert.That(result, Is.EqualTo(StoryExplorationResult.Locked));
            Assert.That(town.LastInteractionEntity.Id, Is.EqualTo("town-dungeon-gate"));
        }

        [Test]
        public void Town_GuideUnlocksDeterministicDungeonTransfer()
        {
            StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);
            StoryEntity guide = town.FindEntity("town-guide");
            StoryEntity gate = town.FindEntity("town-dungeon-gate");

            Assert.That(WalkTo(town, guide), Is.EqualTo(StoryExplorationResult.Dialogue));
            town.ResolveEntity(guide.Id);
            Assert.That(WalkTo(town, gate), Is.EqualTo(StoryExplorationResult.Passage));
            Assert.That(town.ConfirmCurrentPassage(), Is.EqualTo(StoryExplorationResult.Transfer));
        }

        [Test]
        public void Dungeon_ReachesVisibleRecruitSymbol()
        {
            StoryExplorationCore dungeon = StoryExplorationCore.Create(StoryAreaKind.Dungeon);
            StoryEntity recruit = dungeon.FindEntity("dungeon-memory-archer");

            Assert.That(WalkTo(dungeon, recruit), Is.EqualTo(StoryExplorationResult.Recruit));
            Assert.That(dungeon.LastInteractionEntity.DisplayName, Is.EqualTo("記憶の射手"));
        }

        [Test]
        public void Dungeon_ReachesSecondVisibleRecruitSymbol()
        {
            StoryExplorationCore dungeon = StoryExplorationCore.Create(StoryAreaKind.Dungeon);
            foreach (StoryEntity entity in dungeon.Entities)
            {
                if (entity.Id != "dungeon-memory-healer") dungeon.ResolveEntity(entity.Id);
            }
            StoryEntity recruit = dungeon.FindEntity("dungeon-memory-healer");

            Assert.That(WalkTo(dungeon, recruit), Is.EqualTo(StoryExplorationResult.Recruit));
            Assert.That(dungeon.LastInteractionEntity.DisplayName, Is.EqualTo("記憶の癒し手"));
        }

        [Test]
        public void Town_ContainsSideDialogueAndInteriorPassage()
        {
            StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);

            Assert.That(
                town.FindEntity("town-smith").Kind,
                Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(
                WalkTo(town, town.FindEntity("town-atelier-door")),
                Is.EqualTo(StoryExplorationResult.Passage));
        }

        [Test]
        public void Interior_ContainsDialogueTreasureAndExit()
        {
            StoryExplorationCore interior = StoryExplorationCore.Create(StoryAreaKind.Interior);

            Assert.That(
                interior.FindEntity("interior-caretaker").Kind,
                Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(
                WalkTo(interior, interior.FindEntity("interior-keepsake")),
                Is.EqualTo(StoryExplorationResult.Treasure));
            interior.ResolveEntity("interior-keepsake");
            Assert.That(
                WalkTo(interior, interior.FindEntity("interior-exit")),
                Is.EqualTo(StoryExplorationResult.Passage));
            Assert.That(
                interior.ConfirmCurrentPassage(),
                Is.EqualTo(StoryExplorationResult.Transfer));
        }

        [Test]
        public void Dungeon_ContainsOptionalDialogueAndTreasure()
        {
            StoryExplorationCore dungeon = StoryExplorationCore.Create(StoryAreaKind.Dungeon);

            Assert.That(
                dungeon.FindEntity("dungeon-echo-scholar").Kind,
                Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(
                dungeon.FindEntity("dungeon-relic-chest").Kind,
                Is.EqualTo(StoryEntityKind.Treasure));
        }

        [Test]
        public void DialogueHistory_ReopensNpcWithChangedLinesAfterReload()
        {
            StoryExplorationCore firstVisit = StoryExplorationCore.Create(StoryAreaKind.Town);
            StoryEntity firstSmith = firstVisit.FindEntity("town-smith");
            string firstLine = StoryDialogueCatalog.GetLines(firstSmith)[0];

            StoryExplorationCore revisit = StoryExplorationCore.Create(
                StoryAreaKind.Town,
                resolvedEntityIds: new[] { "town-smith" });
            StoryEntity returningSmith = revisit.FindEntity("town-smith");

            Assert.That(returningSmith.WasPreviouslyResolved, Is.True);
            Assert.That(returningSmith.IsResolved, Is.False);
            Assert.That(
                StoryDialogueCatalog.GetLines(returningSmith)[0],
                Is.Not.EqualTo(firstLine));
            Assert.That(
                WalkTo(revisit, returningSmith),
                Is.EqualTo(StoryExplorationResult.Dialogue));
        }

        [Test]
        public void Town_ContainsAdditionalResidentAndEnterableInn()
        {
            StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);
            StoryExplorationCore inn = StoryExplorationCore.Create(StoryAreaKind.Inn);

            Assert.That(town.FindEntity("town-herbalist").Kind, Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(town.FindEntity("town-inn-door").Kind, Is.EqualTo(StoryEntityKind.Passage));
            Assert.That(inn.FindEntity("inn-host").Kind, Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(inn.FindEntity("inn-minstrel").Kind, Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(inn.FindEntity("inn-exit").Kind, Is.EqualTo(StoryEntityKind.Passage));
        }

        [Test]
        public void TownBase_ShowsRosterAndOnlyJoinedCompanionsWithFollowUpDialogue()
        {
            StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);
            StoryExplorationCore emptyBase = StoryExplorationCore.Create(StoryAreaKind.Base);
            StoryExplorationCore joinedBase = StoryExplorationCore.Create(
                StoryAreaKind.Base,
                memoryArcherJoined: true,
                memoryHealerJoined: true,
                memoryMinstrelJoined: true);

            Assert.That(town.FindEntity("town-base-door").Kind, Is.EqualTo(StoryEntityKind.Passage));
            Assert.That(emptyBase.FindEntity("base-recordkeeper").Message, Does.Contain("1/26"));
            Assert.That(emptyBase.FindEntity("base-memory-archer"), Is.Null);
            Assert.That(emptyBase.FindEntity("base-memory-healer"), Is.Null);
            Assert.That(emptyBase.FindEntity("base-memory-minstrel"), Is.Null);
            Assert.That(joinedBase.FindEntity("base-recordkeeper").Message, Does.Contain("4/26"));
            Assert.That(joinedBase.FindEntity("base-memory-archer").Kind, Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(joinedBase.FindEntity("base-memory-healer").Kind, Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(joinedBase.FindEntity("base-memory-minstrel").Kind, Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(joinedBase.FindEntity("base-exit").Kind, Is.EqualTo(StoryEntityKind.Passage));
            Assert.That(
                StoryDialogueCatalog.GetLines(joinedBase.FindEntity("base-memory-minstrel")).Length,
                Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void TownRelationshipsPopulateBaseAndUnlockFacilities()
        {
            string[] relationships =
            {
                "town-smith",
                "town-herbalist",
                "interior-caretaker",
                "inn-host",
                "dungeon-echo-scholar",
                "dungeon-lost-pilgrim"
            };
            StoryExplorationCore growth = StoryExplorationCore.Create(
                StoryAreaKind.Base,
                memoryArcherJoined: true,
                memoryHealerJoined: true,
                memoryMinstrelJoined: true,
                resolvedEntityIds: relationships);

            Assert.That(growth.BaseGrowth.LightCount, Is.EqualTo(10));
            Assert.That(growth.BaseGrowth.Level, Is.EqualTo(5));
            Assert.That(growth.BaseGrowth.Facilities, Has.Member(BaseFacility.Archive));
            foreach (BaseSupportResident resident in BaseGrowthPolicy.AllSupportResidents)
            {
                Assert.That(growth.FindEntity(resident.BaseEntityId), Is.Not.Null);
                Assert.That(
                    StoryNpcArtPolicy.ResourcePathForEntity(resident.BaseEntityId),
                    Is.EqualTo(resident.ResourcePath));
            }
        }

        [Test]
        public void Inn_FirstMoveTowardMinstrel_DoesNotTransferToExit()
        {
            StoryExplorationCore inn = StoryExplorationCore.Create(StoryAreaKind.Inn);
            StoryEntity minstrel = inn.FindEntity("inn-minstrel");

            StoryExplorationResult result = inn.MoveToward(
                minstrel.X,
                minstrel.Y,
                0.012f);

            Assert.That(result, Is.EqualTo(StoryExplorationResult.Moved));
            Assert.That(inn.LastInteractionEntity, Is.Null);
        }

        [Test]
        public void CrossAreaQuest_TurnsMinstrelIntoRecruitAfterKeepsake()
        {
            StoryExplorationCore beforeQuest = StoryExplorationCore.Create(
                StoryAreaKind.Inn,
                townGuideHeard: true);
            StoryExplorationCore ready = StoryExplorationCore.Create(
                StoryAreaKind.Inn,
                townGuideHeard: true,
                resolvedEntityIds: new[] { "interior-keepsake" });

            Assert.That(
                beforeQuest.FindEntity("inn-minstrel").Kind,
                Is.EqualTo(StoryEntityKind.Dialogue));
            Assert.That(
                ready.FindEntity("inn-minstrel").Kind,
                Is.EqualTo(StoryEntityKind.Recruit));
            Assert.That(
                WalkTo(ready, ready.FindEntity("inn-minstrel")),
                Is.EqualTo(StoryExplorationResult.Recruit));
            Assert.That(
                StoryDialogueCatalog.GetLines(ready.FindEntity("inn-minstrel")).Length,
                Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void JoinedCompanions_RemainAvailableForFollowUpDialogue()
        {
            StoryExplorationCore dungeon = StoryExplorationCore.Create(
                StoryAreaKind.Dungeon,
                memoryArcherJoined: true,
                memoryHealerJoined: true);
            StoryExplorationCore inn = StoryExplorationCore.Create(
                StoryAreaKind.Inn,
                townGuideHeard: true,
                resolvedEntityIds: new[] { "interior-keepsake" },
                memoryMinstrelJoined: true);

            foreach (StoryEntity companion in new[]
                     {
                         dungeon.FindEntity("dungeon-memory-archer"),
                         dungeon.FindEntity("dungeon-memory-healer"),
                         inn.FindEntity("inn-minstrel")
                     })
            {
                Assert.That(companion.Kind, Is.EqualTo(StoryEntityKind.Dialogue));
                Assert.That(companion.WasPreviouslyResolved, Is.True);
                Assert.That(
                    StoryDialogueCatalog.GetLines(companion).Length,
                    Is.GreaterThanOrEqualTo(1),
                    companion.Id);
            }
        }

        [Test]
        public void StoryProgress_RoundTripsAndRecruitmentIsIdempotent()
        {
            StageData[] stages = CreateStages();
            CampaignSaveData save = CampaignSavePolicy.NewSave(stages.Length);

            save = CampaignSavePolicy.StoreTownGuideHeard(save, stages);
            save = CampaignSavePolicy.StoreRecruitment(save, "memory1", stages);
            save = CampaignSavePolicy.StoreRecruitment(save, "memory1", stages);
            save = CampaignSavePolicy.StoreRecruitment(save, "memory2", stages);
            save = CampaignSavePolicy.StoreRecruitment(save, "memory2", stages);
            save = CampaignSavePolicy.CompleteStoryPrologue(save, stages);
            CampaignSaveData restored = CampaignSavePolicy.Normalize(save, stages);

            Assert.That(restored.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(restored.townGuideHeard, Is.True);
            Assert.That(restored.storyPrologueCompleted, Is.True);
            Assert.That(restored.recruitedUnitIds, Is.EqualTo(new[] { "memory1", "memory2" }));
            Assert.That(CampaignSavePolicy.HasRecruited(restored, "memory1"), Is.True);
            Assert.That(CampaignSavePolicy.HasRecruited(restored, "memory2"), Is.True);
        }

        [Test]
        public void StoryProgress_RequiresBothRecruitsBeforeCompletion()
        {
            StageData[] stages = CreateStages();
            CampaignSaveData save = CampaignSavePolicy.StoreRecruitment(
                CampaignSavePolicy.NewSave(stages.Length),
                "memory1",
                stages);

            save = CampaignSavePolicy.CompleteStoryPrologue(save, stages);
            Assert.That(save.storyPrologueCompleted, Is.False);

            save = CampaignSavePolicy.StoreRecruitment(save, "memory2", stages);
            save = CampaignSavePolicy.CompleteStoryPrologue(save, stages);
            Assert.That(save.storyPrologueCompleted, Is.True);
        }

        [Test]
        public void SchemaSixProgress_MigratesWithoutForcingReplay()
        {
            CampaignSaveData restored = CampaignSavePolicy.Normalize(
                new CampaignSaveData
                {
                    schemaVersion = 6,
                    stageIndex = 1,
                    maxUnlocked = 1
                },
                CreateStages());

            Assert.That(restored.stageIndex, Is.EqualTo(1));
            Assert.That(restored.storyPrologueCompleted, Is.True);
            Assert.That(restored.recruitedUnitIds, Does.Contain("memory1"));
            Assert.That(restored.recruitedUnitIds, Does.Contain("memory2"));
        }

        [Test]
        public void SchemaSixStageZero_MigratesWithoutForcingReplay()
        {
            CampaignSaveData restored = CampaignSavePolicy.Normalize(
                new CampaignSaveData
                {
                    schemaVersion = 6,
                    stageIndex = 0,
                    maxUnlocked = 0
                },
                CreateStages());

            Assert.That(restored.stageIndex, Is.Zero);
            Assert.That(restored.storyPrologueCompleted, Is.True);
            Assert.That(restored.recruitedUnitIds, Does.Contain("memory1"));
            Assert.That(restored.recruitedUnitIds, Does.Contain("memory2"));
        }

        [Test]
        public void RecruitedCompanions_AreAddedToFirstBattlePreparation()
        {
            StageData[] stages = CreateStages();
            StageData prepared = RecruitmentRosterPolicy.CreateStage(
                stages[0],
                stages,
                new[] { "memory1", "memory2", "memory3" });

            Assert.That(
                prepared.units.Count(unit => unit.sourceUnitId == "memory1"),
                Is.EqualTo(1));
            Assert.That(
                prepared.units.Count(unit => unit.sourceUnitId == "memory2"),
                Is.EqualTo(1));
            Assert.That(
                prepared.units.Count(unit => unit.sourceUnitId == "memory3"),
                Is.EqualTo(1));
            Assert.That(prepared.units.Select(unit => unit.id), Is.Unique);
            Assert.That(
                stages[0].units.Any(unit => unit.sourceUnitId == "memory1"),
                Is.False,
                "The authored catalog must remain immutable.");
        }

        [Test]
        public void SixMemberRoster_DeploysChosenFiveAndKeepsOneInReserve()
        {
            var stage = new StageData
            {
                id = "six-member-stage",
                displayName = "Six",
                recommendedLevel = 1,
                width = 9,
                height = 8,
                units = new[]
                {
                    Unit("hero", "hero", "knight", 1, 1),
                    Unit("azuki", "azuki", "trickster", 1, 2),
                    Unit("partner", "partner", "cleric", 1, 3),
                    Unit("memory1", "memory1", "archer", 1, 4),
                    Unit("memory2", "memory2", "cleric", 1, 5),
                    Unit("memory3", "memory3", "mage", 1, 6),
                    Unit("enemy", "e_knight", "knight", 7, 2, "enemy")
                }
            };
            BattlePreparationState preparation = BattlePreparationState.Create(stage);

            Assert.That(preparation.Loadouts.Count, Is.EqualTo(6));
            Assert.That(preparation.DeployedCount, Is.EqualTo(6));
            Assert.That(preparation.MoveUnit("memory3", -1), Is.True);
            StageData battle = preparation.CreateBattleStage();

            Assert.That(
                battle.units.Count(unit => unit.team == "player"),
                Is.EqualTo(6));
            Assert.That(
                battle.units.Any(unit => unit.sourceUnitId == "memory3"),
                Is.True);
        }

        [Test]
        public void BackgroundRoutes_KeepEveryEntityOnWalkableGroundAndReachable()
        {
            foreach (StoryAreaKind areaKind in new[]
                     {
                         StoryAreaKind.Town,
                         StoryAreaKind.Interior,
                         StoryAreaKind.Inn,
                         StoryAreaKind.Base,
                         StoryAreaKind.Dungeon
                     })
            {
                StoryExplorationCore area = StoryExplorationCore.Create(
                    areaKind,
                    townGuideHeard: true);
                Assert.That(
                    area.IsWalkable(area.PlayerX, area.PlayerY),
                    Is.True,
                    $"{areaKind} player start");

                foreach (StoryEntity entity in area.Entities)
                {
                    Assert.That(
                        area.IsWalkable(entity.X, entity.Y),
                        Is.True,
                        $"{areaKind}/{entity.Id}");
                    StoryExplorationCore route = StoryExplorationCore.Create(
                        areaKind,
                        townGuideHeard: true);
                    foreach (StoryEntity other in route.Entities)
                    {
                        if (other.Id != entity.Id) route.ResolveEntity(other.Id);
                    }

                    StoryExplorationResult expected =
                        entity.Kind == StoryEntityKind.Passage
                            ? StoryExplorationResult.Passage
                            : entity.Kind == StoryEntityKind.Dialogue
                                ? StoryExplorationResult.Dialogue
                                : entity.Kind == StoryEntityKind.Treasure
                                    ? StoryExplorationResult.Treasure
                                    : StoryExplorationResult.Recruit;
                    Assert.That(
                        WalkTo(route, route.FindEntity(entity.Id)),
                        Is.EqualTo(expected),
                        $"{areaKind}/{entity.Id}");
                    Assert.That(
                        route.LastInteractionEntity.Id,
                        Is.EqualTo(entity.Id),
                        $"{areaKind}/{entity.Id}");
                }
            }
        }

        [Test]
        public void NpcSchedule_IsDeterministicAndChangesPositionAndDialogueByTime()
        {
            StoryExplorationCore first = StoryExplorationCore.Create(
                StoryAreaKind.Town,
                storyClockMinutes: 555);
            StoryExplorationCore second = StoryExplorationCore.Create(
                StoryAreaKind.Town,
                storyClockMinutes: 555);

            Assert.That(first.TimeOfDay, Is.EqualTo(StoryTimeOfDay.Morning));
            Assert.That(
                first.FindEntity("town-guide").X,
                Is.EqualTo(second.FindEntity("town-guide").X));
            Assert.That(
                first.FindEntity("town-guide").Y,
                Is.EqualTo(second.FindEntity("town-guide").Y));

            StoryEntity morningHerbalist = first.FindEntity("town-herbalist");
            StoryExplorationCore evening = StoryExplorationCore.Create(
                StoryAreaKind.Town,
                storyClockMinutes: 1140);
            StoryEntity eveningHerbalist = evening.FindEntity("town-herbalist");

            Assert.That(evening.TimeOfDay, Is.EqualTo(StoryTimeOfDay.Evening));
            Assert.That(eveningHerbalist.X, Is.Not.EqualTo(morningHerbalist.X));
            Assert.That(
                StoryDialogueCatalog.GetLines(
                    morningHerbalist,
                    StoryTimeOfDay.Morning)[0],
                Is.Not.EqualTo(
                    StoryDialogueCatalog.GetLines(
                        eveningHerbalist,
                        StoryTimeOfDay.Evening)[0]));

            float guideXBeforeWait = first.FindEntity("town-guide").X;
            first.WaitMinutes(10);
            Assert.That(first.StoryClockMinutes, Is.EqualTo(565));
            Assert.That(
                first.FindEntity("town-guide").X,
                Is.Not.EqualTo(guideXBeforeWait));
        }

        [Test]
        public void ChapterStory_AfterSecondBattleAppearsOnceAndCoversRemainingChapters()
        {
            Assert.That(
                ChapterStoryPolicy.GetPending(1, new string[0]),
                Is.Null);

            ChapterStoryBeat thirdChapter = ChapterStoryPolicy.GetPending(
                2,
                new string[0]);

            Assert.That(thirdChapter, Is.Not.Null);
            Assert.That(thirdChapter.Id, Is.EqualTo("chapter-story-s2"));
            Assert.That(thirdChapter.Title, Does.Contain("第三章"));
            Assert.That(thirdChapter.Lines.Count, Is.EqualTo(4));
            Assert.That(
                ChapterStoryPolicy.GetPending(2, new[] { thirdChapter.Id }),
                Is.Null);
            Assert.That(
                Enumerable.Range(2, 4)
                    .Select(index => ChapterStoryPolicy.GetPending(index, new string[0]))
                    .All(beat => beat != null && beat.Lines.Count >= 4),
                Is.True);
        }

        private static StoryExplorationResult WalkTo(
            StoryExplorationCore area,
            StoryEntity entity)
        {
            StoryExplorationResult result = StoryExplorationResult.Idle;
            for (int step = 0; step < 400; step++)
            {
                result = area.MoveToward(entity.X, entity.Y, 0.012f);
                if (result != StoryExplorationResult.Moved) break;
            }
            return result;
        }

        private static StageData[] CreateStages()
        {
            return new[]
            {
                new StageData
                {
                    id = "s0",
                    displayName = "First",
                    recommendedLevel = 1,
                    width = 9,
                    height = 7,
                    units = new[]
                    {
                        Unit("hero", "hero", "knight", 1, 1),
                        Unit("enemy", "e_knight", "knight", 7, 2, "enemy")
                    }
                },
                new StageData
                {
                    id = "s1",
                    displayName = "Second",
                    recommendedLevel = 2,
                    width = 9,
                    height = 7,
                    units = new[]
                    {
                        Unit("hero", "hero", "knight", 1, 1),
                        Unit("memory1", "memory1", "archer", 1, 5),
                        Unit("memory2", "memory2", "cleric", 1, 4),
                        Unit("enemy-2", "e_archer", "archer", 7, 2, "enemy")
                    }
                }
            };
        }

        private static StageUnitData Unit(
            string id,
            string sourceUnitId,
            string className,
            int x,
            int y,
            string team = "player")
        {
            return new StageUnitData
            {
                id = id,
                sourceUnitId = sourceUnitId,
                displayName = id,
                className = className,
                team = team,
                level = 1,
                x = x,
                y = y,
                maxHp = 30,
                moveRange = 2,
                attackRange = className == "archer" ? 2 : 1,
                damage = 8
            };
        }
    }
}
