using System;
using System.Linq;
using BirthdayTactics.Core;

internal static class Program
{
    private static int _passed;

    private static int Main()
    {
        try
        {
            TownGateRequiresDialogue();
            TownGateTransfersAfterDialogue();
            DungeonRecruitsMemoryArcher();
            DungeonRecruitsMemoryHealer();
            RecruitmentRoundTrips();
            PrologueRequiresBothRecruits();
            SchemaSixProgressDoesNotRewind();
            SchemaSixStageZeroDoesNotForceReplay();
            RecruitAppearsInFirstPreparation();
            RecruitedPreparationRoundTrips();
            ExpandedExplorationHasInteriorAndTreasure();
            ExpandedStoryTreasureCountReachesFour();
            DialogueHistoryChangesAfterReload();
            ExpandedExplorationIncludesInn();
            ExpandedDialogueRosterIsPresent();
            BackgroundRoutesRemainReachable();
            NpcScheduleAndClockRoundTrip();
            Console.WriteLine($"STORY_ACCEPTANCE PASSED {_passed}/17");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"STORY_ACCEPTANCE FAILED after {_passed}/17");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void TownGateRequiresDialogue()
    {
        StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);
        Equal(
            StoryExplorationResult.Locked,
            WalkTo(town, town.FindEntity("town-dungeon-gate")),
            "Town gate must remain locked before the guide dialogue.");
        Pass();
    }

    private static void TownGateTransfersAfterDialogue()
    {
        StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);
        StoryEntity guide = town.FindEntity("town-guide");
        Equal(
            StoryExplorationResult.Dialogue,
            WalkTo(town, guide),
            "Town guide must trigger dialogue.");
        town.ResolveEntity(guide.Id);
        Equal(
            StoryExplorationResult.Passage,
            WalkTo(town, town.FindEntity("town-dungeon-gate")),
            "Town gate contact must wait for explicit confirmation.");
        Equal(
            StoryExplorationResult.Transfer,
            town.ConfirmCurrentPassage(),
            "Town gate must transfer only after confirmation.");
        Pass();
    }

    private static void DungeonRecruitsMemoryArcher()
    {
        StoryExplorationCore dungeon = StoryExplorationCore.Create(StoryAreaKind.Dungeon);
        Equal(
            StoryExplorationResult.Recruit,
            WalkTo(dungeon, dungeon.FindEntity("dungeon-memory-archer")),
            "Dungeon objective must trigger recruitment.");
        Pass();
    }

    private static void DungeonRecruitsMemoryHealer()
    {
        StoryExplorationCore dungeon = StoryExplorationCore.Create(StoryAreaKind.Dungeon);
        foreach (StoryEntity entity in dungeon.Entities)
        {
            if (entity.Id != "dungeon-memory-healer") dungeon.ResolveEntity(entity.Id);
        }
        Equal(
            StoryExplorationResult.Recruit,
            WalkTo(dungeon, dungeon.FindEntity("dungeon-memory-healer")),
            "Dungeon must contain a second recruitable companion.");
        Pass();
    }

    private static void RecruitmentRoundTrips()
    {
        StageData[] stages = Stages();
        CampaignSaveData save = CampaignSavePolicy.NewSave(stages.Length);
        save = CampaignSavePolicy.StoreTownGuideHeard(save, stages);
        save = CampaignSavePolicy.StoreRecruitment(save, "memory1", stages);
        save = CampaignSavePolicy.StoreRecruitment(save, "memory1", stages);
        save = CampaignSavePolicy.StoreRecruitment(save, "memory2", stages);
        save = CampaignSavePolicy.StoreRecruitment(save, "memory2", stages);
        save = CampaignSavePolicy.CompleteStoryPrologue(save, stages);
        CampaignSaveData restored = CampaignSavePolicy.Normalize(save, stages);

        Equal(CampaignSavePolicy.CurrentSchemaVersion, restored.schemaVersion, "Save schema must be current.");
        True(restored.storyPrologueCompleted, "Story completion must persist.");
        Equal(2, restored.recruitedUnitIds.Length, "Recruitment must be idempotent.");
        True(
            CampaignSavePolicy.HasRecruited(restored, "memory1"),
            "Memory archer recruitment must persist.");
        True(
            CampaignSavePolicy.HasRecruited(restored, "memory2"),
            "Memory healer recruitment must persist.");
        Pass();
    }

    private static void PrologueRequiresBothRecruits()
    {
        StageData[] stages = Stages();
        CampaignSaveData archerOnly = CampaignSavePolicy.StoreRecruitment(
            CampaignSavePolicy.NewSave(stages.Length),
            "memory1",
            stages);
        archerOnly = CampaignSavePolicy.CompleteStoryPrologue(archerOnly, stages);
        True(
            !archerOnly.storyPrologueCompleted,
            "One recruit must not complete the prologue.");

        CampaignSaveData both = CampaignSavePolicy.StoreRecruitment(
            archerOnly,
            "memory2",
            stages);
        both = CampaignSavePolicy.CompleteStoryPrologue(both, stages);
        True(
            both.storyPrologueCompleted,
            "Both companions must complete the prologue.");
        Pass();
    }

    private static void SchemaSixProgressDoesNotRewind()
    {
        CampaignSaveData restored = CampaignSavePolicy.Normalize(
            new CampaignSaveData
            {
                schemaVersion = 6,
                stageIndex = 1,
                maxUnlocked = 1
            },
            Stages());
        Equal(1, restored.stageIndex, "Migrated stage progress must not rewind.");
        True(restored.storyPrologueCompleted, "Progressed saves must skip the prologue.");
        True(
            CampaignSavePolicy.HasRecruited(restored, "memory1"),
            "Progressed saves must infer the existing archer.");
        True(
            CampaignSavePolicy.HasRecruited(restored, "memory2"),
            "Progressed saves must infer the existing healer.");
        Pass();
    }

    private static void SchemaSixStageZeroDoesNotForceReplay()
    {
        CampaignSaveData restored = CampaignSavePolicy.Normalize(
            new CampaignSaveData
            {
                schemaVersion = 6,
                stageIndex = 0,
                maxUnlocked = 0
            },
            Stages());
        Equal(0, restored.stageIndex, "Migrated stage zero must stay selected.");
        True(restored.storyPrologueCompleted, "Legacy saves must not be forced into the new prologue.");
        True(
            CampaignSavePolicy.HasRecruited(restored, "memory1"),
            "Legacy saves must infer the existing archer.");
        True(
            CampaignSavePolicy.HasRecruited(restored, "memory2"),
            "Legacy saves must infer the existing healer.");
        Pass();
    }

    private static void RecruitAppearsInFirstPreparation()
    {
        StageData[] stages = Stages();
        StageData prepared = RecruitmentRosterPolicy.CreateStage(
            stages[0],
            stages,
            new[] { "memory1", "memory2" });
        Equal(
            1,
            prepared.units.Count(unit => unit.sourceUnitId == "memory1"),
            "Recruited archer must appear exactly once.");
        Equal(
            1,
            prepared.units.Count(unit => unit.sourceUnitId == "memory2"),
            "Recruited healer must appear exactly once.");
        True(
            stages[0].units.All(unit => unit.sourceUnitId != "memory1"),
            "Authored stage data must remain unchanged.");
        Pass();
    }

    private static void RecruitedPreparationRoundTrips()
    {
        StageData[] stages = Stages();
        CampaignSaveData save = CampaignSavePolicy.StoreRecruitment(
            CampaignSavePolicy.NewSave(stages.Length),
            "memory1",
            stages);
        StageData roster = RecruitmentRosterPolicy.CreateStage(
            stages[0],
            stages,
            save.recruitedUnitIds);
        BattlePreparationState preparation = BattlePreparationState.Create(roster);
        True(
            preparation.SetWeapon("memory1", WeaponId.Daggers),
            "Recruited archer must accept daggers.");
        preparation.SetTactic("memory1", TacticPolicy.Defensive);

        save = CampaignSavePolicy.StorePreparation(
            save,
            stages[0],
            preparation.ToSaveData(),
            stages);
        CampaignSaveData restoredSave = CampaignSavePolicy.Normalize(save, stages);
        BattlePreparationState restored = BattlePreparationState.Create(
            RecruitmentRosterPolicy.CreateStage(
                stages[0],
                stages,
                restoredSave.recruitedUnitIds),
            CampaignSavePolicy.FindPreparation(restoredSave, stages[0].id));

        Equal(
            WeaponId.Daggers,
            restored.GetLoadout("memory1").weaponId,
            "Recruited weapon must survive normalization.");
        Equal(
            TacticPolicy.Defensive,
            restored.GetLoadout("memory1").tactic,
            "Recruited tactic must survive normalization.");
        Pass();
    }

    private static void ExpandedExplorationHasInteriorAndTreasure()
    {
        StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);
        True(town.FindEntity("town-smith") != null, "Town side NPC is required.");
        Equal(
            StoryExplorationResult.Passage,
            WalkTo(town, town.FindEntity("town-atelier-door")),
            "Town atelier entrance must wait for confirmation.");

        StoryExplorationCore interior = StoryExplorationCore.Create(StoryAreaKind.Interior);
        Equal(
            StoryExplorationResult.Treasure,
            WalkTo(interior, interior.FindEntity("interior-keepsake")),
            "Interior treasure must be reachable.");

        CampaignSaveData save = CampaignSavePolicy.StoreStoryEntityResolution(
            CampaignSavePolicy.NewSave(Stages().Length),
            "interior-keepsake",
            true,
            Stages());
        save = CampaignSavePolicy.StoreStoryEntityResolution(
            save,
            "interior-keepsake",
            true,
            Stages());
        Equal(1, save.storyTreasureCount, "Story treasure must be idempotent.");
        Pass();
    }

    private static void ExpandedStoryTreasureCountReachesFour()
    {
        StageData[] stages = Stages();
        string[] treasureIds =
        {
            "town-market-cache",
            "interior-keepsake",
            "inn-traveler-map",
            "dungeon-relic-chest"
        };
        CampaignSaveData save = CampaignSavePolicy.NewSave(stages.Length);
        foreach (string treasureId in treasureIds)
        {
            save = CampaignSavePolicy.StoreStoryEntityResolution(
                save,
                treasureId,
                true,
                stages);
        }
        Equal(4, save.storyTreasureCount, "All four story treasures must persist.");
        Equal(
            4,
            save.resolvedStoryEntityIds.Length,
            "Every story treasure must retain a unique resolution id.");
        Pass();
    }

    private static void DialogueHistoryChangesAfterReload()
    {
        StageData[] stages = Stages();
        StoryExplorationCore firstVisit = StoryExplorationCore.Create(StoryAreaKind.Town);
        StoryEntity firstSmith = firstVisit.FindEntity("town-smith");
        string firstLine = StoryDialogueCatalog.GetLines(firstSmith)[0];

        CampaignSaveData save = CampaignSavePolicy.StoreStoryEntityResolution(
            CampaignSavePolicy.NewSave(stages.Length),
            firstSmith.Id,
            false,
            stages);
        StoryExplorationCore revisit = StoryExplorationCore.Create(
            StoryAreaKind.Town,
            resolvedEntityIds: save.resolvedStoryEntityIds);
        StoryEntity returningSmith = revisit.FindEntity("town-smith");

        True(returningSmith.WasPreviouslyResolved, "Dialogue history must survive reload.");
        True(!returningSmith.IsResolved, "Returning NPC must remain interactable.");
        True(
            StoryDialogueCatalog.GetLines(returningSmith)[0] != firstLine,
            "Returning NPC dialogue must change.");
        Equal(
            StoryExplorationResult.Dialogue,
            WalkTo(revisit, returningSmith),
            "Returning NPC must still trigger dialogue.");
        Equal(0, save.storyTreasureCount, "Dialogue must not increment treasure.");
        Pass();
    }

    private static void ExpandedDialogueRosterIsPresent()
    {
        StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);
        StoryExplorationCore dungeon = StoryExplorationCore.Create(StoryAreaKind.Dungeon);
        Equal(
            StoryEntityKind.Dialogue,
            town.FindEntity("town-gate-warden").Kind,
            "Town gate warden dialogue must be authored.");
        Equal(
            StoryEntityKind.Dialogue,
            dungeon.FindEntity("dungeon-lost-pilgrim").Kind,
            "Dungeon pilgrim dialogue must be authored.");
        Equal(
            StoryEntityKind.Recruit,
            dungeon.FindEntity("dungeon-memory-healer").Kind,
            "Dungeon healer must be recruitable.");
        Pass();
    }

    private static void ExpandedExplorationIncludesInn()
    {
        StoryExplorationCore town = StoryExplorationCore.Create(StoryAreaKind.Town);
        StoryExplorationCore inn = StoryExplorationCore.Create(StoryAreaKind.Inn);

        True(town.FindEntity("town-herbalist") != null, "Town requires an additional resident.");
        True(town.FindEntity("town-inn-door") != null, "Town requires an additional building.");
        True(inn.FindEntity("inn-host") != null, "Inn requires a host.");
        True(inn.FindEntity("inn-minstrel") != null, "Inn requires a second resident.");
        Equal(
            StoryExplorationResult.Passage,
            WalkTo(inn, inn.FindEntity("inn-exit")),
            "Inn exit must wait for confirmation.");
        Pass();
    }

    private static void BackgroundRoutesRemainReachable()
    {
        foreach (StoryAreaKind areaKind in new[]
                 {
                     StoryAreaKind.Town,
                     StoryAreaKind.Interior,
                     StoryAreaKind.Inn,
                     StoryAreaKind.Dungeon
                 })
        {
            StoryExplorationCore area = StoryExplorationCore.Create(
                areaKind,
                townGuideHeard: true);
            True(
                area.IsWalkable(area.PlayerX, area.PlayerY),
                $"{areaKind} player start must be on an authored route.");

            foreach (StoryEntity entity in area.Entities)
            {
                True(
                    area.IsWalkable(entity.X, entity.Y),
                    $"{areaKind}/{entity.Id} must be on an authored route.");

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
                Equal(
                    expected,
                    WalkTo(route, route.FindEntity(entity.Id)),
                    $"{areaKind}/{entity.Id} must be reachable.");
                Equal(
                    entity.Id,
                    route.LastInteractionEntity.Id,
                    $"{areaKind}/{entity.Id} must be the reached passage.");
                if (entity.Kind == StoryEntityKind.Passage)
                {
                    Equal(
                        StoryExplorationResult.Transfer,
                        route.ConfirmCurrentPassage(),
                        $"{areaKind}/{entity.Id} must transfer after confirmation.");
                }
            }
        }
        Pass();
    }

    private static void NpcScheduleAndClockRoundTrip()
    {
        StoryExplorationCore first = StoryExplorationCore.Create(
            StoryAreaKind.Town,
            storyClockMinutes: 555);
        StoryExplorationCore second = StoryExplorationCore.Create(
            StoryAreaKind.Town,
            storyClockMinutes: 555);
        StoryEntity firstGuide = first.FindEntity("town-guide");
        StoryEntity secondGuide = second.FindEntity("town-guide");
        Equal(firstGuide.X, secondGuide.X, "NPC patrol must be deterministic.");
        Equal(firstGuide.Y, secondGuide.Y, "NPC patrol must be deterministic.");

        StoryEntity morningHerbalist = first.FindEntity("town-herbalist");
        StoryExplorationCore evening = StoryExplorationCore.Create(
            StoryAreaKind.Town,
            storyClockMinutes: 1140);
        StoryEntity eveningHerbalist = evening.FindEntity("town-herbalist");
        True(
            morningHerbalist.X != eveningHerbalist.X,
            "Time of day must change the NPC location.");
        True(
            StoryDialogueCatalog.GetLines(
                morningHerbalist,
                StoryTimeOfDay.Morning)[0] !=
            StoryDialogueCatalog.GetLines(
                eveningHerbalist,
                StoryTimeOfDay.Evening)[0],
            "Time of day must change situational dialogue.");

        StageData[] stages = Stages();
        CampaignSaveData migrated = CampaignSavePolicy.Normalize(
            new CampaignSaveData
            {
                schemaVersion = 8,
                storyClockMinutes = 1200
            },
            stages);
        Equal(540, migrated.storyClockMinutes, "Schema eight must migrate to 09:00.");
        CampaignSaveData restored = CampaignSavePolicy.Normalize(
            CampaignSavePolicy.StoreStoryClock(migrated, 1140, stages),
            stages);
        Equal(1140, restored.storyClockMinutes, "Story clock must round-trip.");
        Pass();
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

    private static StageData[] Stages()
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

    private static void Pass()
    {
        _passed++;
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message} Expected={expected}, Actual={actual}");
        }
    }
}
