using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    public enum BaseFacility
    {
        Entrance,
        Roster,
        Kitchen,
        Forge,
        Bath,
        Archive,
        GatheringHall
    }

    public sealed class BaseSupportResident
    {
        public string SourceEntityId { get; }
        public string BaseEntityId { get; }
        public string Name { get; }
        public string Role { get; }
        public string Quote { get; }
        public string Description { get; }
        public string ResourcePath { get; }

        internal BaseSupportResident(
            string sourceEntityId,
            string baseEntityId,
            string name,
            string role,
            string quote,
            string description,
            string resourcePath)
        {
            SourceEntityId = sourceEntityId;
            BaseEntityId = baseEntityId;
            Name = name;
            Role = role;
            Quote = quote;
            Description = description;
            ResourcePath = resourcePath;
        }
    }

    public sealed class BaseGrowthSnapshot
    {
        public int LightCount { get; }
        public int TargetLightCount => BaseGrowthPolicy.TargetLightCount;
        public int Level { get; }
        public IReadOnlyList<BaseFacility> Facilities { get; }
        public IReadOnlyList<BaseSupportResident> SupportResidents { get; }
        public string RosterSummary { get; }

        internal BaseGrowthSnapshot(
            int lightCount,
            int level,
            IReadOnlyList<BaseFacility> facilities,
            IReadOnlyList<BaseSupportResident> supportResidents,
            string rosterSummary)
        {
            LightCount = lightCount;
            Level = level;
            Facilities = facilities;
            SupportResidents = supportResidents;
            RosterSummary = rosterSummary;
        }
    }

    /// <summary>
    /// 戦闘員だけでなく、会話で縁を結んだ住民も「灯」として数える拠点成長契約。
    /// セーブ値から毎回同じ段階を再構成し、取り逃しや重複で段階が壊れないようにする。
    /// </summary>
    public static class BaseGrowthPolicy
    {
        public const int TargetLightCount = 26;

        private static readonly BaseSupportResident[] SupportCatalog =
        {
            Resident("town-smith", "base-smith", "水鏡の鍛冶師", "館の鍛冶場を預かる装備職人", "「武器も帰る場所も、手入れを怠れば鈍る。ここは任せな」", "鍛冶場を整え、出発前の装備と隊列を見守る。", "Art/Story/NPCs/town_smith"),
            Resident("town-herbalist", "base-herbalist", "湖風の薬師", "台所と薬棚を守る調薬師", "「帰ってきた時に同じ香りがするよう、薬草を絶やさないよ」", "台所を開き、遠征帰りの仲間を温かく迎える。", "Art/Story/NPCs/town_herbalist"),
            Resident("interior-caretaker", "base-caretaker", "工房の世話人", "思い出の品を修復する管理人", "「預かった品にも、帰る棚が必要ですから」", "玄関と収蔵棚を整え、館の記憶を守る。", "Art/Story/NPCs/interior_caretaker"),
            Resident("inn-host", "base-inn-host", "湖畔の宿主", "食卓を囲む世話役", "「空いた椅子はそのままにしないよ。次の灯を迎える席さ」", "台所と食卓を賑わせ、仲間の会話を増やす。", "Art/Story/NPCs/inn_host"),
            Resident("dungeon-echo-scholar", "base-scholar", "残響を調べる学者", "戦いの声を記す研究者", "「勝敗だけでなく、誰が誰を支えたかまで残しましょう」", "記録室を開き、これまでの遠征と会戦を整理する。", "Art/Story/NPCs/dungeon_scholar"),
            Resident("dungeon-lost-pilgrim", "base-pilgrim", "帰路を見つけた巡礼者", "縁側と灯籠を守る案内役", "「今度は私が、帰ってくる人の足元を照らします」", "湯屋へ続く縁側を整え、帰還者の道を照らす。", "Art/Story/NPCs/dungeon_lost_pilgrim")
        };

        public static IReadOnlyList<BaseSupportResident> AllSupportResidents => SupportCatalog;

        public static BaseGrowthSnapshot Create(
            IEnumerable<string> recruitedUnitIds,
            IEnumerable<string> resolvedStoryEntityIds)
        {
            var recruited = new HashSet<string>(
                recruitedUnitIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var resolved = new HashSet<string>(
                resolvedStoryEntityIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            BaseSupportResident[] support = SupportCatalog
                .Where(candidate => resolved.Contains(candidate.SourceEntityId))
                .ToArray();
            int combatLights = RecruitmentRosterPolicy.KnownRecruitIds
                .Count(recruited.Contains);
            int lightCount = 1 + combatLights + support.Length;
            int level = LevelForLightCount(lightCount);
            BaseFacility[] facilities = Enum.GetValues(typeof(BaseFacility))
                .Cast<BaseFacility>()
                .Where(facility => RequiredLevel(facility) <= level)
                .ToArray();
            string open = string.Join("・", facilities.Select(FacilityName));
            string next = level >= 6
                ? "すべての区画に灯がともっている。"
                : $"次の段階まであと{NextThreshold(level) - lightCount}灯。";
            string summary = $"灯の名簿 {lightCount}/{TargetLightCount}　館 第{level}段　開放：{open}。{next}";
            return new BaseGrowthSnapshot(lightCount, level, facilities, support, summary);
        }

        public static BaseSupportResident FindBySourceEntityId(string entityId) =>
            SupportCatalog.FirstOrDefault(candidate =>
                string.Equals(candidate.SourceEntityId, entityId, StringComparison.Ordinal));

        public static BaseSupportResident FindByBaseEntityId(string entityId) =>
            SupportCatalog.FirstOrDefault(candidate =>
                string.Equals(candidate.BaseEntityId, entityId, StringComparison.Ordinal));

        public static int LevelForLightCount(int lightCount)
        {
            if (lightCount >= 12) return 6;
            if (lightCount >= 9) return 5;
            if (lightCount >= 7) return 4;
            if (lightCount >= 5) return 3;
            if (lightCount >= 3) return 2;
            return 1;
        }

        private static int NextThreshold(int level)
        {
            switch (level)
            {
                case 1: return 3;
                case 2: return 5;
                case 3: return 7;
                case 4: return 9;
                default: return 12;
            }
        }

        private static int RequiredLevel(BaseFacility facility)
        {
            switch (facility)
            {
                case BaseFacility.Kitchen: return 2;
                case BaseFacility.Forge: return 3;
                case BaseFacility.Bath: return 4;
                case BaseFacility.Archive: return 5;
                case BaseFacility.GatheringHall: return 6;
                default: return 1;
            }
        }

        public static string FacilityName(BaseFacility facility)
        {
            switch (facility)
            {
                case BaseFacility.Entrance: return "玄関";
                case BaseFacility.Roster: return "灯の名簿";
                case BaseFacility.Kitchen: return "台所";
                case BaseFacility.Forge: return "鍛冶場";
                case BaseFacility.Bath: return "湯屋と縁側";
                case BaseFacility.Archive: return "記録室";
                default: return "全員の広間";
            }
        }

        private static BaseSupportResident Resident(
            string sourceEntityId,
            string baseEntityId,
            string name,
            string role,
            string quote,
            string description,
            string resourcePath) =>
            new BaseSupportResident(
                sourceEntityId,
                baseEntityId,
                name,
                role,
                quote,
                description,
                resourcePath);
    }
}
