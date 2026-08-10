using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    public sealed class CampaignSaveTests
    {
        [Test]
        public void NewSave_StartsAtFirstStageWithSafeAudioDefaults()
        {
            CampaignSaveData save = CampaignSavePolicy.NewSave(6);
            Assert.That(save.stageIndex, Is.EqualTo(0));
            Assert.That(save.maxUnlocked, Is.EqualTo(0));
            Assert.That(save.volume, Is.EqualTo(0.8f));
            Assert.That(save.muted, Is.False);
        }

        [Test]
        public void Normalize_ClampsBrokenOneSlotData()
        {
            CampaignSaveData save = CampaignSavePolicy.Normalize(new CampaignSaveData
            {
                stageIndex = 99,
                maxUnlocked = -4,
                volume = 4f
            }, 6);

            Assert.That(save.stageIndex, Is.EqualTo(5));
            Assert.That(save.maxUnlocked, Is.EqualTo(5));
            Assert.That(save.volume, Is.EqualTo(1f));
        }

        [Test]
        public void CompleteStage_AdvancesAndMarksFinalVictory()
        {
            CampaignSaveData save = CampaignSavePolicy.CompleteStage(CampaignSavePolicy.NewSave(6), 2, 6);
            Assert.That(save.stageIndex, Is.EqualTo(3));
            Assert.That(save.campaignComplete, Is.False);

            save = CampaignSavePolicy.CompleteStage(save, 5, 6);
            Assert.That(save.stageIndex, Is.EqualTo(5));
            Assert.That(save.campaignComplete, Is.True);
        }

        [Test]
        public void CompletingEveryStageSequentially_UnlocksGiftAndResetsFieldPosition()
        {
            CampaignSaveData save = CampaignSavePolicy.NewSave(6);
            for (int stage = 0; stage < 6; stage++)
            {
                save = CampaignSavePolicy.StoreFieldPosition(
                    save,
                    0.7f,
                    0.4f,
                    5,
                    CreateStages(6));
                save = CampaignSavePolicy.CompleteStage(save, stage, 6);
                Assert.That(save.hasFieldPosition, Is.False, $"stage {stage}");
            }

            Assert.That(save.stageIndex, Is.EqualTo(5));
            Assert.That(save.maxUnlocked, Is.EqualTo(5));
            Assert.That(save.campaignComplete, Is.True);
            Assert.That(CampaignSavePolicy.IsGiftUnlocked(save, 6), Is.True);
        }

        [Test]
        public void Normalize_MigratesV1WithoutLosingProgressOrAudio()
        {
            CampaignSaveData migrated = CampaignSavePolicy.Normalize(new CampaignSaveData
            {
                schemaVersion = 1,
                stageIndex = 3,
                maxUnlocked = 4,
                campaignComplete = false,
                volume = 0.37f,
                muted = true,
                preparations = null
            }, 6);

            Assert.That(migrated.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(migrated.stageIndex, Is.EqualTo(3));
            Assert.That(migrated.maxUnlocked, Is.EqualTo(4));
            Assert.That(migrated.volume, Is.EqualTo(0.37f));
            Assert.That(migrated.muted, Is.True);
            Assert.That(migrated.preparations, Is.Empty);
        }

        [Test]
        public void StorePreparation_RoundTripsCanonicalStageSelection()
        {
            StageData stage = new StageData
            {
                id = "save-stage",
                units = new[]
                {
                    new StageUnitData
                    {
                        id = "hero",
                        sourceUnitId = "hero",
                        displayName = "Hero",
                        className = "knight",
                        team = "player",
                        level = 1,
                        maxHp = 100,
                        damage = 10
                    },
                    new StageUnitData
                    {
                        id = "enemy",
                        sourceUnitId = "e_knight",
                        displayName = "Enemy",
                        className = "knight",
                        team = "enemy",
                        level = 1,
                        maxHp = 100,
                        damage = 10
                    }
                }
            };
            StageData[] stages = { stage };
            BattlePreparationState preparation = BattlePreparationState.Create(stage);
            preparation.SetWeapon("hero", WeaponId.Lance);
            preparation.SetArmor("hero", ArmorId.KnightPlate);
            preparation.SetTactic("hero", TacticPolicy.Defensive);

            CampaignSaveData save = CampaignSavePolicy.StorePreparation(
                CampaignSavePolicy.NewSave(stages.Length),
                stage,
                preparation.ToSaveData(),
                stages);
            CampaignSaveData normalized = CampaignSavePolicy.Normalize(save, stages);
            StagePreparationData restored = CampaignSavePolicy.FindPreparation(normalized, stage.id);
            BattlePreparationState restoredState = BattlePreparationState.Create(stage, restored);

            Assert.That(normalized.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(normalized.preparations.Length, Is.EqualTo(1));
            Assert.That(restoredState.GetLoadout("hero").weaponId, Is.EqualTo(WeaponId.Lance));
            Assert.That(restoredState.GetLoadout("hero").armorId, Is.EqualTo(ArmorId.KnightPlate));
            Assert.That(restoredState.GetLoadout("hero").tactic, Is.EqualTo(TacticPolicy.Defensive));
        }

        [Test]
        public void RecruitedUnitPreparation_RoundTripsAgainstAuthoredCatalog()
        {
            StageData authored = new StageData
            {
                id = "recruit-save-stage",
                recommendedLevel = 1,
                width = 9,
                height = 7,
                units = new[]
                {
                    Player("hero", "hero", "knight")
                }
            };
            StageData template = new StageData
            {
                id = "recruit-template",
                units = new[]
                {
                    Player("memory1", "memory1", "archer")
                }
            };
            StageData[] stages = { authored, template };
            CampaignSaveData save = CampaignSavePolicy.StoreRecruitment(
                CampaignSavePolicy.NewSave(stages.Length),
                "memory1",
                stages);
            StageData rosterStage = RecruitmentRosterPolicy.CreateStage(
                authored,
                stages,
                save.recruitedUnitIds);
            BattlePreparationState preparation = BattlePreparationState.Create(rosterStage);
            Assert.That(preparation.SetWeapon("memory1", WeaponId.Daggers), Is.True);
            preparation.SetTactic("memory1", TacticPolicy.Defensive);

            save = CampaignSavePolicy.StorePreparation(
                save,
                authored,
                preparation.ToSaveData(),
                stages);
            CampaignSaveData restoredSave = CampaignSavePolicy.Normalize(save, stages);
            StageData restoredStage = RecruitmentRosterPolicy.CreateStage(
                authored,
                stages,
                restoredSave.recruitedUnitIds);
            BattlePreparationState restored = BattlePreparationState.Create(
                restoredStage,
                CampaignSavePolicy.FindPreparation(restoredSave, authored.id));

            Assert.That(restored.GetLoadout("memory1").weaponId, Is.EqualTo(WeaponId.Daggers));
            Assert.That(restored.GetLoadout("memory1").tactic, Is.EqualTo(TacticPolicy.Defensive));
        }

        [Test]
        public void FieldAndWarmapProgress_RoundTripsInSchemaThree()
        {
            StageData[] stages =
            {
                new StageData { id = "s0", units = new StageUnitData[0] }
            };
            CampaignSaveData save = CampaignSavePolicy.NewSave(stages.Length);

            save = CampaignSavePolicy.StoreFieldNode(save, 5, stages);
            save = CampaignSavePolicy.CompleteWarmap(save, "w1", stages);
            CampaignSaveData normalized = CampaignSavePolicy.Normalize(save, stages);

            Assert.That(normalized.fieldNodeIndex, Is.EqualTo(5));
            Assert.That(CampaignSavePolicy.HasCompletedWarmap(normalized, "w1"), Is.True);
        }

        [Test]
        public void FieldPosition_RoundTripsAndClampsInSchemaFour()
        {
            StageData[] stages =
            {
                new StageData { id = "s0", units = new StageUnitData[0] }
            };

            CampaignSaveData save = CampaignSavePolicy.StoreFieldPosition(
                CampaignSavePolicy.NewSave(stages.Length),
                2f,
                -1f,
                6,
                stages);
            CampaignSaveData normalized = CampaignSavePolicy.Normalize(save, stages);

            Assert.That(normalized.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(normalized.hasFieldPosition, Is.True);
            Assert.That(normalized.fieldX, Is.EqualTo(0.945f));
            Assert.That(normalized.fieldY, Is.EqualTo(0.10f));
            Assert.That(normalized.fieldNodeIndex, Is.EqualTo(6));
        }

        [Test]
        public void Normalize_MigratesSchemaThreeWithoutInventingContinuousPosition()
        {
            CampaignSaveData migrated = CampaignSavePolicy.Normalize(new CampaignSaveData
            {
                schemaVersion = 3,
                stageIndex = 2,
                fieldNodeIndex = 5,
                fieldX = 0.7f,
                fieldY = 0.4f,
                hasFieldPosition = true
            }, 6);

            Assert.That(migrated.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(migrated.fieldNodeIndex, Is.EqualTo(5));
            Assert.That(migrated.hasFieldPosition, Is.False);
            Assert.That(migrated.fieldX, Is.EqualTo(0f));
            Assert.That(migrated.fieldY, Is.EqualTo(0f));
        }

        [Test]
        public void FieldEntityResolution_RoundTripsAndTreasureIsIdempotent()
        {
            StageData[] stages =
            {
                new StageData { id = "s0", units = new StageUnitData[0] }
            };
            CampaignSaveData save = CampaignSavePolicy.NewSave(stages.Length);

            save = CampaignSavePolicy.StoreFieldEntityResolution(
                save,
                "field-0-treasure",
                true,
                stages);
            save = CampaignSavePolicy.StoreFieldEntityResolution(
                save,
                "field-0-treasure",
                true,
                stages);
            CampaignSaveData normalized = CampaignSavePolicy.Normalize(save, stages);

            Assert.That(normalized.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(normalized.resolvedFieldEntityIds, Is.EqualTo(new[] { "field-0-treasure" }));
            Assert.That(normalized.fieldTreasureCount, Is.EqualTo(1));
        }

        [Test]
        public void StoryTreasure_RoundTripsAndIsIdempotent()
        {
            StageData[] stages =
            {
                new StageData { id = "s0", units = new StageUnitData[0] }
            };
            CampaignSaveData save = CampaignSavePolicy.NewSave(stages.Length);

            save = CampaignSavePolicy.StoreStoryEntityResolution(
                save,
                "interior-keepsake",
                true,
                stages);
            save = CampaignSavePolicy.StoreStoryEntityResolution(
                save,
                "interior-keepsake",
                true,
                stages);
            CampaignSaveData normalized = CampaignSavePolicy.Normalize(save, stages);

            Assert.That(normalized.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(normalized.resolvedStoryEntityIds, Is.EqualTo(new[] { "interior-keepsake" }));
            Assert.That(normalized.storyTreasureCount, Is.EqualTo(1));
            Assert.That(
                CampaignSavePolicy.HasResolvedStoryEntity(normalized, "interior-keepsake"),
                Is.True);
        }

        [Test]
        public void StoryDialogue_RoundTripsWithoutIncreasingTreasure()
        {
            StageData[] stages =
            {
                new StageData { id = "s0", units = new StageUnitData[0] }
            };
            CampaignSaveData save = CampaignSavePolicy.NewSave(stages.Length);

            save = CampaignSavePolicy.StoreStoryEntityResolution(
                save,
                "town-smith",
                false,
                stages);
            save = CampaignSavePolicy.StoreStoryEntityResolution(
                save,
                "town-smith",
                false,
                stages);
            CampaignSaveData normalized = CampaignSavePolicy.Normalize(save, stages);

            Assert.That(normalized.resolvedStoryEntityIds, Is.EqualTo(new[] { "town-smith" }));
            Assert.That(normalized.storyTreasureCount, Is.Zero);
        }

        [Test]
        public void StoryClock_RoundTripsAndSchemaEightMigratesToMorning()
        {
            StageData[] stages =
            {
                new StageData { id = "s0", units = new StageUnitData[0] }
            };
            CampaignSaveData migrated = CampaignSavePolicy.Normalize(
                new CampaignSaveData
                {
                    schemaVersion = 8,
                    storyClockMinutes = 1200
                },
                stages);
            Assert.That(migrated.storyClockMinutes, Is.EqualTo(540));

            CampaignSaveData stored = CampaignSavePolicy.StoreStoryClock(
                migrated,
                1140,
                stages);
            CampaignSaveData restored = CampaignSavePolicy.Normalize(stored, stages);

            Assert.That(restored.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(restored.storyClockMinutes, Is.EqualTo(1140));
        }

        [Test]
        public void Normalize_MigratesCompletedSchemaNineWithBothPrologueRecruits()
        {
            CampaignSaveData migrated = CampaignSavePolicy.Normalize(
                new CampaignSaveData
                {
                    schemaVersion = 9,
                    storyPrologueCompleted = true,
                    townGuideHeard = true,
                    recruitedUnitIds = new[] { "memory1" },
                    storyClockMinutes = 780
                },
                CreateStages(3));

            Assert.That(migrated.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(migrated.storyPrologueCompleted, Is.True);
            Assert.That(migrated.storyClockMinutes, Is.EqualTo(780));
            Assert.That(migrated.recruitedUnitIds, Does.Contain("memory1"));
            Assert.That(migrated.recruitedUnitIds, Does.Contain("memory2"));
        }

        [Test]
        public void EnemyVictory_ScoutPersistsButOnlyMainAdvancesChapter()
        {
            StageData[] stages = CreateStages(3);
            CampaignSaveData save = CampaignSavePolicy.NewSave(stages.Length);

            CampaignSaveData afterScout = CampaignSavePolicy.ResolveFieldEnemyVictory(
                save,
                "field-0-enemy-scout",
                0,
                stages);

            Assert.That(afterScout.stageIndex, Is.EqualTo(0));
            Assert.That(afterScout.maxUnlocked, Is.EqualTo(0));
            Assert.That(
                CampaignSavePolicy.HasResolvedFieldEntity(
                    afterScout,
                    "field-0-enemy-scout"),
                Is.True);

            CampaignSaveData afterMain = CampaignSavePolicy.ResolveFieldEnemyVictory(
                afterScout,
                "field-0-enemy-main",
                0,
                stages);

            Assert.That(afterMain.stageIndex, Is.EqualTo(1));
            Assert.That(afterMain.maxUnlocked, Is.EqualTo(1));
            Assert.That(
                CampaignSavePolicy.HasResolvedFieldEntity(
                    afterMain,
                    "field-0-enemy-main"),
                Is.True);
        }

        [Test]
        public void NpcSupport_ResolvesNpcAndRoundTripsPerStage()
        {
            StageData[] stages = CreateStages(3);
            CampaignSaveData save = CampaignSavePolicy.StoreFieldNpcSupport(
                CampaignSavePolicy.NewSave(stages.Length),
                "field-1-npc",
                1,
                FieldSupportType.Medical,
                stages);
            CampaignSaveData normalized = CampaignSavePolicy.Normalize(save, stages);

            Assert.That(normalized.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(
                CampaignSavePolicy.HasResolvedFieldEntity(
                    normalized,
                    "field-1-npc"),
                Is.True);
            Assert.That(
                CampaignSavePolicy.FindFieldSupport(normalized, 1),
                Is.EqualTo(FieldSupportType.Medical));
            Assert.That(
                CampaignSavePolicy.FindFieldSupport(normalized, 0),
                Is.EqualTo(FieldSupportType.None));
        }

        [Test]
        public void Normalize_MigratesSchemaFiveWithoutInventingNpcSupport()
        {
            CampaignSaveData migrated = CampaignSavePolicy.Normalize(
                new CampaignSaveData
                {
                    schemaVersion = 5,
                    stageIndex = 1,
                    fieldTreasureCount = 2,
                    resolvedFieldEntityIds = new[] { "field-1-treasure" },
                    fieldSupports = new[]
                    {
                        new FieldSupportData
                        {
                            stageIndex = 1,
                            support = FieldSupportType.Ambush,
                            npcEntityId = "field-1-npc"
                        }
                    }
                },
                3);

            Assert.That(migrated.schemaVersion, Is.EqualTo(CampaignSavePolicy.CurrentSchemaVersion));
            Assert.That(migrated.fieldTreasureCount, Is.EqualTo(2));
            Assert.That(
                migrated.resolvedFieldEntityIds,
                Is.EqualTo(new[] { "field-1-treasure" }));
            Assert.That(migrated.fieldSupports, Is.Empty);
        }

        [Test]
        public void GiftMessage_IsTheApprovedPersonalMessage()
        {
            StringAssert.Contains("いつもありがとう", GiftSequenceData.Message);
            StringAssert.Contains("３人、ずっと一緒だよ", GiftSequenceData.Message);
            StringAssert.Contains("あずき", GiftSequenceData.AzukiLine);
        }

        private static StageData[] CreateStages(int count)
        {
            var stages = new StageData[count];
            for (int i = 0; i < count; i++)
            {
                stages[i] = new StageData
                {
                    id = $"s{i}",
                    units = new StageUnitData[0]
                };
            }
            return stages;
        }

        private static StageUnitData Player(
            string id,
            string sourceUnitId,
            string className)
        {
            return new StageUnitData
            {
                id = id,
                sourceUnitId = sourceUnitId,
                displayName = id,
                className = className,
                team = "player",
                level = 1,
                maxHp = 30,
                moveRange = 2,
                attackRange = className == "archer" ? 2 : 1,
                damage = 8
            };
        }
    }
}
