using System;
using System.Collections.Generic;

namespace BirthdayTactics.Core
{
    public static class StoryNpcArtPolicy
    {
        private static readonly Dictionary<string, string> ResourcePaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["town-guide"] = "Art/Story/NPCs/town_guide",
                ["town-smith"] = "Art/Story/NPCs/town_smith",
                ["town-herbalist"] = "Art/Story/NPCs/town_herbalist",
                ["town-gate-warden"] = "Art/Story/NPCs/town_gate_warden",
                ["interior-caretaker"] = "Art/Story/NPCs/interior_caretaker",
                ["inn-host"] = "Art/Story/NPCs/inn_host",
                ["inn-minstrel"] = "Art/Battle/Units/memory3",
                ["dungeon-echo-scholar"] = "Art/Story/NPCs/dungeon_scholar",
                ["dungeon-lost-pilgrim"] = "Art/Story/NPCs/dungeon_lost_pilgrim",
                ["dungeon-memory-archer"] = "Art/Battle/Units/memory1",
                ["dungeon-memory-healer"] = "Art/Battle/Units/memory2",
                ["base-recordkeeper"] = "Art/Story/NPCs/interior_caretaker",
                ["base-memory-archer"] = "Art/Battle/Units/memory1",
                ["base-memory-healer"] = "Art/Battle/Units/memory2",
                ["base-memory-minstrel"] = "Art/Battle/Units/memory3",
                ["base-smith"] = "Art/Story/NPCs/town_smith",
                ["base-herbalist"] = "Art/Story/NPCs/town_herbalist",
                ["base-caretaker"] = "Art/Story/NPCs/interior_caretaker",
                ["base-inn-host"] = "Art/Story/NPCs/inn_host",
                ["base-scholar"] = "Art/Story/NPCs/dungeon_scholar",
                ["base-pilgrim"] = "Art/Story/NPCs/dungeon_lost_pilgrim"
            };

        public static IReadOnlyDictionary<string, string> Mappings => ResourcePaths;

        public static string ResourcePathForEntity(string entityId)
        {
            return !string.IsNullOrWhiteSpace(entityId) &&
                   ResourcePaths.TryGetValue(entityId, out string path)
                ? path
                : null;
        }
    }
}
