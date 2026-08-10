using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    public enum FieldSupportType
    {
        None,
        Recon,
        Medical,
        Ambush
    }

    [Serializable]
    public sealed class FieldSupportData
    {
        public int stageIndex;
        public FieldSupportType support;
        public string npcEntityId;
    }

    [Serializable]
    public sealed class CampaignSaveData
    {
        public int schemaVersion = CampaignSavePolicy.CurrentSchemaVersion;
        public int stageIndex;
        public int maxUnlocked;
        public bool campaignComplete;
        public float volume = 0.8f;
        public bool muted;
        public StagePreparationData[] preparations = Array.Empty<StagePreparationData>();
        public int fieldNodeIndex;
        public bool hasFieldPosition;
        public float fieldX;
        public float fieldY;
        public string[] clearedWarmapIds = Array.Empty<string>();
        public string[] resolvedFieldEntityIds = Array.Empty<string>();
        public int fieldTreasureCount;
        public FieldSupportData[] fieldSupports = Array.Empty<FieldSupportData>();
        public bool townGuideHeard;
        public bool storyPrologueCompleted;
        public string[] recruitedUnitIds = Array.Empty<string>();
        public string[] resolvedStoryEntityIds = Array.Empty<string>();
        public int storyTreasureCount;
        public int storyClockMinutes = 540;
        public int baseLevel = 1;
    }

    public static class CampaignSavePolicy
    {
        public const int CurrentSchemaVersion = 11;
        private static readonly string[] PrologueRecruitIds =
        {
            RecruitmentRosterPolicy.MemoryArcherId,
            RecruitmentRosterPolicy.MemoryHealerId
        };

        public static CampaignSaveData NewSave(int stageCount)
        {
            return Normalize(new CampaignSaveData(), stageCount);
        }

        public static CampaignSaveData Normalize(CampaignSaveData source, int stageCount)
        {
            int lastStage = Math.Max(0, stageCount - 1);
            if (source == null || source.schemaVersion < 1 || source.schemaVersion > CurrentSchemaVersion)
                source = new CampaignSaveData();
            int sourceSchemaVersion = source.schemaVersion;

            int stageIndex = Clamp(source.stageIndex, 0, lastStage);
            int maxUnlocked = Clamp(Math.Max(source.maxUnlocked, stageIndex), 0, lastStage);
            StagePreparationData[] preparations = source.schemaVersion >= 2
                ? NormalizePreparationArray(source.preparations)
                : Array.Empty<StagePreparationData>();
            int fieldNodeIndex = source.schemaVersion >= 3
                ? Clamp(source.fieldNodeIndex, 0, 7)
                : 0;
            bool hasFieldPosition = source.schemaVersion >= 4 && source.hasFieldPosition;
            float fieldX = hasFieldPosition ? Clamp(source.fieldX, 0.055f, 0.945f) : 0f;
            float fieldY = hasFieldPosition ? Clamp(source.fieldY, 0.10f, 0.90f) : 0f;
            string[] clearedWarmapIds = source.schemaVersion >= 3
                ? NormalizeStringArray(source.clearedWarmapIds)
                : Array.Empty<string>();
            string[] resolvedFieldEntityIds = source.schemaVersion >= 5
                ? NormalizeStringArray(source.resolvedFieldEntityIds)
                : Array.Empty<string>();
            int fieldTreasureCount = source.schemaVersion >= 5
                ? Math.Max(0, source.fieldTreasureCount)
                : 0;
            FieldSupportData[] fieldSupports = source.schemaVersion >= 6
                ? NormalizeFieldSupports(source.fieldSupports, lastStage)
                : Array.Empty<FieldSupportData>();
            bool migratedLegacySave = sourceSchemaVersion >= 1 &&
                                      sourceSchemaVersion < 7;
            bool townGuideHeard = sourceSchemaVersion >= 7
                ? source.townGuideHeard
                : migratedLegacySave;
            bool storyPrologueCompleted = sourceSchemaVersion >= 7
                ? source.storyPrologueCompleted
                : migratedLegacySave;
            string[] recruitedUnitIds = sourceSchemaVersion >= 7
                ? NormalizeStringArray(source.recruitedUnitIds)
                : migratedLegacySave
                    ? PrologueRecruitIds.ToArray()
                    : Array.Empty<string>();
            if (sourceSchemaVersion < 10 && storyPrologueCompleted)
            {
                recruitedUnitIds = recruitedUnitIds
                    .Concat(PrologueRecruitIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
            }
            string[] resolvedStoryEntityIds = sourceSchemaVersion >= 8
                ? NormalizeStringArray(source.resolvedStoryEntityIds)
                : Array.Empty<string>();
            int storyTreasureCount = sourceSchemaVersion >= 8
                ? Math.Max(0, source.storyTreasureCount)
                : 0;
            int storyClockMinutes = sourceSchemaVersion >= 9
                ? Clamp(source.storyClockMinutes, 0, 1439)
                : 540;
            int baseLevel = BaseGrowthPolicy.Create(
                    recruitedUnitIds,
                    resolvedStoryEntityIds)
                .Level;
            return new CampaignSaveData
            {
                schemaVersion = CurrentSchemaVersion,
                stageIndex = stageIndex,
                maxUnlocked = maxUnlocked,
                campaignComplete = source.campaignComplete,
                volume = Clamp01(source.volume),
                muted = source.muted,
                preparations = preparations,
                fieldNodeIndex = fieldNodeIndex,
                hasFieldPosition = hasFieldPosition,
                fieldX = fieldX,
                fieldY = fieldY,
                clearedWarmapIds = clearedWarmapIds,
                resolvedFieldEntityIds = resolvedFieldEntityIds,
                fieldTreasureCount = fieldTreasureCount,
                fieldSupports = fieldSupports,
                townGuideHeard = townGuideHeard,
                storyPrologueCompleted = storyPrologueCompleted,
                recruitedUnitIds = recruitedUnitIds,
                resolvedStoryEntityIds = resolvedStoryEntityIds,
                storyTreasureCount = storyTreasureCount,
                storyClockMinutes = storyClockMinutes,
                baseLevel = baseLevel
            };
        }

        public static CampaignSaveData Normalize(
            CampaignSaveData source,
            IReadOnlyList<StageData> stages)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            CampaignSaveData normalized = Normalize(source, stages.Count);
            Dictionary<string, StageData> byId = stages
                .Where(stage => stage != null && !string.IsNullOrWhiteSpace(stage.id))
                .GroupBy(stage => stage.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            normalized.preparations = normalized.preparations
                .Where(preparation => preparation != null &&
                                      !string.IsNullOrWhiteSpace(preparation.stageId) &&
                                      byId.ContainsKey(preparation.stageId))
                .GroupBy(preparation => preparation.stageId, StringComparer.Ordinal)
                .Select(group =>
                {
                    StageData rosterStage = RecruitmentRosterPolicy.CreateStage(
                        byId[group.Key],
                        stages,
                        normalized.recruitedUnitIds);
                    return BattlePreparationState
                        .Create(rosterStage, group.First())
                        .ToSaveData();
                })
                .ToArray();
            return normalized;
        }

        public static CampaignSaveData SelectStage(CampaignSaveData source, int stageIndex, int stageCount)
        {
            CampaignSaveData save = Normalize(source, stageCount);
            save.stageIndex = Clamp(stageIndex, 0, Math.Max(0, stageCount - 1));
            save.maxUnlocked = Math.Max(save.maxUnlocked, save.stageIndex);
            return save;
        }

        public static CampaignSaveData SelectStage(
            CampaignSaveData source,
            int stageIndex,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            save.stageIndex = Clamp(stageIndex, 0, Math.Max(0, stages.Count - 1));
            save.maxUnlocked = Math.Max(save.maxUnlocked, save.stageIndex);
            return save;
        }

        public static CampaignSaveData CompleteStage(CampaignSaveData source, int stageIndex, int stageCount)
        {
            CampaignSaveData save = SelectStage(source, stageIndex, stageCount);
            int lastStage = Math.Max(0, stageCount - 1);
            if (save.stageIndex >= lastStage)
            {
                save.campaignComplete = true;
                save.maxUnlocked = lastStage;
            }
            else
            {
                save.stageIndex++;
                save.maxUnlocked = Math.Max(save.maxUnlocked, save.stageIndex);
            }
            save.fieldNodeIndex = 0;
            save.hasFieldPosition = false;
            save.fieldX = 0f;
            save.fieldY = 0f;
            return save;
        }

        public static CampaignSaveData CompleteStage(
            CampaignSaveData source,
            int stageIndex,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = SelectStage(source, stageIndex, stages);
            int lastStage = Math.Max(0, stages.Count - 1);
            if (save.stageIndex >= lastStage)
            {
                save.campaignComplete = true;
                save.maxUnlocked = lastStage;
            }
            else
            {
                save.stageIndex++;
                save.maxUnlocked = Math.Max(save.maxUnlocked, save.stageIndex);
            }
            save.fieldNodeIndex = 0;
            save.hasFieldPosition = false;
            save.fieldX = 0f;
            save.fieldY = 0f;
            return save;
        }

        public static CampaignSaveData StoreFieldNode(
            CampaignSaveData source,
            int nodeIndex,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            save.fieldNodeIndex = Clamp(nodeIndex, 0, 7);
            return save;
        }

        public static CampaignSaveData StoreFieldPosition(
            CampaignSaveData source,
            float fieldX,
            float fieldY,
            int nearestNodeIndex,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            save.hasFieldPosition = true;
            save.fieldX = Clamp(fieldX, 0.055f, 0.945f);
            save.fieldY = Clamp(fieldY, 0.10f, 0.90f);
            save.fieldNodeIndex = Clamp(nearestNodeIndex, 0, 7);
            return save;
        }

        public static CampaignSaveData CompleteWarmap(
            CampaignSaveData source,
            string warmapId,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            if (string.IsNullOrWhiteSpace(warmapId)) return save;
            save.clearedWarmapIds = save.clearedWarmapIds
                .Concat(new[] { warmapId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            return save;
        }

        public static CampaignSaveData StoreFieldEntityResolution(
            CampaignSaveData source,
            string entityId,
            bool grantsTreasure,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            if (string.IsNullOrWhiteSpace(entityId)) return save;
            bool alreadyResolved = save.resolvedFieldEntityIds.Contains(
                entityId,
                StringComparer.Ordinal);
            save.resolvedFieldEntityIds = save.resolvedFieldEntityIds
                .Concat(new[] { entityId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (grantsTreasure && !alreadyResolved)
                save.fieldTreasureCount++;
            return save;
        }

        public static CampaignSaveData ResolveFieldEnemyVictory(
            CampaignSaveData source,
            string enemyEntityId,
            int stageIndex,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = StoreFieldEntityResolution(
                source,
                enemyEntityId,
                false,
                stages);
            if (IsScoutEnemy(enemyEntityId)) return save;
            return CompleteStage(save, stageIndex, stages);
        }

        public static bool IsScoutEnemy(string enemyEntityId)
        {
            return !string.IsNullOrWhiteSpace(enemyEntityId) &&
                   enemyEntityId.EndsWith(
                       "-enemy-scout",
                       StringComparison.Ordinal);
        }

        public static CampaignSaveData StoreFieldNpcSupport(
            CampaignSaveData source,
            string npcEntityId,
            int stageIndex,
            FieldSupportType support,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            if (string.IsNullOrWhiteSpace(npcEntityId) ||
                support == FieldSupportType.None ||
                !Enum.IsDefined(typeof(FieldSupportType), support))
                return save;

            int normalizedStageIndex = Clamp(
                stageIndex,
                0,
                Math.Max(0, stages.Count - 1));
            save = StoreFieldEntityResolution(
                save,
                npcEntityId,
                false,
                stages);
            var stored = save.fieldSupports
                .Where(candidate => candidate.stageIndex != normalizedStageIndex)
                .ToList();
            stored.Add(new FieldSupportData
            {
                stageIndex = normalizedStageIndex,
                support = support,
                npcEntityId = npcEntityId
            });
            save.fieldSupports = stored
                .OrderBy(candidate => candidate.stageIndex)
                .ToArray();
            return save;
        }

        public static FieldSupportType FindFieldSupport(
            CampaignSaveData source,
            int stageIndex)
        {
            FieldSupportData stored = (source?.fieldSupports ?? Array.Empty<FieldSupportData>())
                .FirstOrDefault(candidate => candidate != null &&
                                             candidate.stageIndex == stageIndex);
            return stored == null ? FieldSupportType.None : stored.support;
        }

        public static bool HasResolvedFieldEntity(
            CampaignSaveData source,
            string entityId)
        {
            return !string.IsNullOrWhiteSpace(entityId) &&
                   (source?.resolvedFieldEntityIds ?? Array.Empty<string>())
                   .Contains(entityId, StringComparer.Ordinal);
        }

        public static CampaignSaveData StoreTownGuideHeard(
            CampaignSaveData source,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            save.townGuideHeard = true;
            return save;
        }

        public static CampaignSaveData StoreRecruitment(
            CampaignSaveData source,
            string unitId,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            if (string.IsNullOrWhiteSpace(unitId)) return save;
            save.recruitedUnitIds = save.recruitedUnitIds
                .Concat(new[] { unitId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            return save;
        }

        public static CampaignSaveData CompleteStoryPrologue(
            CampaignSaveData source,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            if (PrologueRecruitIds.Any(unitId => !HasRecruited(save, unitId)))
                return save;
            save.townGuideHeard = true;
            save.storyPrologueCompleted = true;
            return save;
        }

        public static bool HasRecruited(CampaignSaveData source, string unitId)
        {
            return !string.IsNullOrWhiteSpace(unitId) &&
                   (source?.recruitedUnitIds ?? Array.Empty<string>())
                   .Contains(unitId, StringComparer.Ordinal);
        }

        public static CampaignSaveData StoreStoryEntityResolution(
            CampaignSaveData source,
            string entityId,
            bool grantsTreasure,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            if (string.IsNullOrWhiteSpace(entityId)) return save;
            bool alreadyResolved = HasResolvedStoryEntity(save, entityId);
            save.resolvedStoryEntityIds = save.resolvedStoryEntityIds
                .Concat(new[] { entityId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (grantsTreasure && !alreadyResolved)
                save.storyTreasureCount++;
            return save;
        }

        public static bool HasResolvedStoryEntity(
            CampaignSaveData source,
            string entityId)
        {
            return !string.IsNullOrWhiteSpace(entityId) &&
                   (source?.resolvedStoryEntityIds ?? Array.Empty<string>())
                   .Contains(entityId, StringComparer.Ordinal);
        }

        public static CampaignSaveData StoreStoryClock(
            CampaignSaveData source,
            int storyClockMinutes,
            IReadOnlyList<StageData> stages)
        {
            CampaignSaveData save = Normalize(source, stages);
            save.storyClockMinutes = Clamp(storyClockMinutes, 0, 1439);
            return save;
        }

        public static bool HasCompletedWarmap(CampaignSaveData source, string warmapId)
        {
            return !string.IsNullOrWhiteSpace(warmapId) &&
                   (source?.clearedWarmapIds ?? Array.Empty<string>())
                   .Contains(warmapId, StringComparer.Ordinal);
        }

        public static bool IsGiftUnlocked(CampaignSaveData source, int stageCount)
        {
            CampaignSaveData save = Normalize(source, stageCount);
            int lastStage = Math.Max(0, stageCount - 1);
            return save.campaignComplete &&
                   save.stageIndex == lastStage &&
                   save.maxUnlocked == lastStage;
        }

        public static CampaignSaveData StorePreparation(
            CampaignSaveData source,
            StageData stage,
            StagePreparationData preparation,
            IReadOnlyList<StageData> stages)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            CampaignSaveData save = Normalize(source, stages);
            StageData rosterStage = RecruitmentRosterPolicy.CreateStage(
                stage,
                stages,
                save.recruitedUnitIds);
            StagePreparationData canonical = BattlePreparationState
                .Create(rosterStage, preparation)
                .ToSaveData();
            var stored = save.preparations
                .Where(candidate => !string.Equals(candidate.stageId, stage.id, StringComparison.Ordinal))
                .ToList();
            stored.Add(canonical);
            save.preparations = stored.ToArray();
            return save;
        }

        public static StagePreparationData FindPreparation(CampaignSaveData source, string stageId)
        {
            if (source?.preparations == null || string.IsNullOrWhiteSpace(stageId)) return null;
            return source.preparations.FirstOrDefault(preparation =>
                preparation != null &&
                string.Equals(preparation.stageId, stageId, StringComparison.Ordinal));
        }

        private static StagePreparationData[] NormalizePreparationArray(StagePreparationData[] source)
        {
            return (source ?? Array.Empty<StagePreparationData>())
                .Where(preparation => preparation != null && !string.IsNullOrWhiteSpace(preparation.stageId))
                .GroupBy(preparation => preparation.stageId, StringComparer.Ordinal)
                .Select(group => new StagePreparationData
                {
                    stageId = group.Key,
                    loadouts = (group.First().loadouts ?? Array.Empty<UnitLoadout>())
                        .Where(loadout => loadout != null && !string.IsNullOrWhiteSpace(loadout.unitId))
                        .GroupBy(loadout => loadout.unitId, StringComparer.Ordinal)
                        .Select(unitGroup => unitGroup.First().Clone())
                        .ToArray()
                })
                .ToArray();
        }

        private static string[] NormalizeStringArray(string[] source)
        {
            return (source ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static FieldSupportData[] NormalizeFieldSupports(
            FieldSupportData[] source,
            int lastStage)
        {
            return (source ?? Array.Empty<FieldSupportData>())
                .Where(candidate => candidate != null &&
                                    candidate.stageIndex >= 0 &&
                                    candidate.stageIndex <= lastStage &&
                                    candidate.support != FieldSupportType.None &&
                                    Enum.IsDefined(typeof(FieldSupportType), candidate.support))
                .GroupBy(candidate => candidate.stageIndex)
                .Select(group =>
                {
                    FieldSupportData selected = group.Last();
                    return new FieldSupportData
                    {
                        stageIndex = selected.stageIndex,
                        support = selected.support,
                        npcEntityId = selected.npcEntityId ?? string.Empty
                    };
                })
                .OrderBy(candidate => candidate.stageIndex)
                .ToArray();
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return min;
            return value < min ? min : value > max ? max : value;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0.8f;
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }

    public static class GiftSequenceData
    {
        public const string Heading = "みんもへ";
        public const string Message = "いつもありがとう。ずっと大好きだよ。\n……３人、ずっと一緒だよ。";
        public const string AzukiLine = "あずき『にゃ〜ん』";
    }
}
