using System;

namespace BirthdayTactics.Core
{
    public enum BattleTeam
    {
        Player,
        Enemy
    }

    public enum BattleWinner
    {
        None,
        Player,
        Enemy,
        Escaped
    }

    [Serializable]
    public sealed class ContentCatalogData
    {
        public int schemaVersion;
        public ClassCatalogData[] classes;
        public UnitPrototypeData[] unitPrototypes;
        public StageData[] stages;
        public WarmapCatalogData[] warmaps;
    }

    [Serializable]
    public sealed class ClassCatalogData
    {
        public string id;
        public string displayName;
        public string role;
        public int hpGrowth;
        public int attackGrowth;
        public int defenseGrowth;
        public int resistanceGrowth;
        public int speedGrowth;
        public int moveRange;
        public int attackRange;
        public string[] traits;
    }

    [Serializable]
    public sealed class UnitPrototypeData
    {
        public string id;
        public string displayName;
        public string className;
        public string team;
        public int baseLevel;
    }

    [Serializable]
    public sealed class WarmapCatalogData
    {
        public string id;
        public string displayName;
        public string difficulty;
        public string backgroundId;
        public int laneCount;
        public int nodeCount;
        public int enemySquadCount;
    }

    [Serializable]
    public sealed class StageData
    {
        public string id;
        public string displayName;
        public string sourceStageId;
        public string sourceWarmapId;
        public string chapter;
        public string backgroundId;
        public string learningObjective;
        public int recommendedLevel;
        public int difficultyIndex;
        public int width;
        public int height;
        public StageUnitData[] units;
    }

    [Serializable]
    public sealed class StageUnitData
    {
        public string id;
        public string sourceUnitId;
        public string displayName;
        public string className;
        public string team;
        public int level;
        public int x;
        public int y;
        public int maxHp;
        public int moveRange;
        public int attackRange;
        public int damage;
        public WeaponId weaponId;
        public ArmorId armorId;
        public TacticPolicy tactic;
    }

    public sealed class UnitState
    {
        public string Id { get; }
        public string SourceUnitId { get; }
        public string DisplayName { get; }
        public string ClassName { get; }
        public BattleTeam Team { get; }
        public int Level { get; }
        public GridPoint Position { get; internal set; }
        public int MaxHp { get; }
        public int Hp { get; internal set; }
        public int MoveRange { get; }
        public int AttackRange { get; }
        public int Damage { get; }
        public bool HasMoved { get; internal set; }
        public bool HasActed { get; internal set; }
        public bool IsAlive => Hp > 0;

        internal UnitState(StageUnitData data)
        {
            Id = data.id;
            SourceUnitId = string.IsNullOrWhiteSpace(data.sourceUnitId) ? data.id : data.sourceUnitId;
            DisplayName = data.displayName;
            ClassName = data.className;
            Team = string.Equals(data.team, "enemy", StringComparison.OrdinalIgnoreCase)
                ? BattleTeam.Enemy
                : BattleTeam.Player;
            Level = Math.Max(1, data.level);
            Position = new GridPoint(data.x, data.y);
            MaxHp = Math.Max(1, data.maxHp);
            Hp = MaxHp;
            MoveRange = Math.Max(1, data.moveRange);
            AttackRange = Math.Max(1, data.attackRange);
            Damage = Math.Max(1, data.damage);
        }
    }

    public sealed class BattleEvent
    {
        public string Type { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public int Amount { get; }
        public GridPoint Position { get; }

        public BattleEvent(string type, string actorId, string targetId, int amount, GridPoint position)
        {
            Type = type;
            ActorId = actorId;
            TargetId = targetId;
            Amount = amount;
            Position = position;
        }
    }
}
