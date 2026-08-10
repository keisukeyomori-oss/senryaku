using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    public enum WeaponId
    {
        Sword,
        Lance,
        Bow,
        Spear,
        Grimoire,
        Staff,
        Daggers,
        Claws
    }

    public enum TacticPolicy
    {
        Balanced,
        Aggressive,
        Defensive
    }

    public enum EnemyDangerLevel
    {
        Standard,
        High,
        Critical
    }

    public enum PreparationReadiness
    {
        Risky,
        Contested,
        Ready
    }

    public enum FormationStatus
    {
        None,
        Weakened,
        Exposed,
        Fortified
    }

    public sealed class WeaponDefinition
    {
        public WeaponId Id { get; }
        public string DisplayName { get; }
        public FormationActionKind AttackKind { get; }
        public int PowerPercent { get; }
        public int SpeedModifier { get; }
        public int Range { get; }
        public string SpecialName { get; }
        public FormationStatus SpecialStatus { get; }
        public bool SpecialTargetsActor { get; }
        public IReadOnlyList<string> CompatibleClasses { get; }

        public WeaponDefinition(
            WeaponId id,
            string displayName,
            FormationActionKind attackKind,
            int powerPercent,
            int speedModifier,
            int range,
            string specialName,
            FormationStatus specialStatus,
            bool specialTargetsActor,
            params string[] compatibleClasses)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            AttackKind = attackKind;
            PowerPercent = Math.Max(1, powerPercent);
            SpeedModifier = speedModifier;
            Range = Math.Max(1, range);
            SpecialName = specialName ?? string.Empty;
            SpecialStatus = specialStatus;
            SpecialTargetsActor = specialTargetsActor;
            CompatibleClasses = compatibleClasses ?? Array.Empty<string>();
        }

        public bool Supports(string className)
        {
            return CompatibleClasses.Any(candidate =>
                string.Equals(candidate, className, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Serializable]
    public sealed class UnitLoadout
    {
        public string unitId;
        public int formationSlot;
        public WeaponId weaponId;
        public ArmorId armorId;
        public TacticPolicy tactic;

        public UnitLoadout Clone()
        {
            return new UnitLoadout
            {
                unitId = unitId,
                formationSlot = formationSlot,
                weaponId = weaponId,
                armorId = armorId,
                tactic = tactic
            };
        }
    }

    [Serializable]
    public sealed class StagePreparationData
    {
        public string stageId;
        public UnitLoadout[] loadouts;
    }

    public sealed class EnemyPreview
    {
        public string UnitId { get; }
        public string DisplayName { get; }
        public string ClassName { get; }
        public int Level { get; }
        public int MaxHp { get; }
        public FormationActionKind AttackKind { get; }
        public EnemyDangerLevel Danger { get; }
        public string CounterHint { get; }

        internal EnemyPreview(
            StageUnitData source,
            FormationActionKind attackKind,
            EnemyDangerLevel danger)
        {
            UnitId = source.id;
            DisplayName = source.displayName;
            ClassName = source.className;
            Level = Math.Max(1, source.level);
            MaxHp = Math.Max(1, source.maxHp);
            AttackKind = attackKind;
            Danger = danger;
            CounterHint = CounterHintFor(attackKind, danger);
        }

        private static string CounterHintFor(
            FormationActionKind attackKind,
            EnemyDangerLevel danger)
        {
            if (danger == EnemyDangerLevel.Critical)
                return "前衛を守勢にして、長射程武器で集中攻撃";
            if (attackKind == FormationActionKind.Magic)
                return "速度の高い武器と攻勢で詠唱役を優先";
            if (attackKind == FormationActionKind.Ranged)
                return "守勢で初撃を耐え、射程2以上で反撃";
            return "耐久力の高い仲間を前衛に置く";
        }
    }

    public sealed class PreparationAssessment
    {
        public int Score { get; }
        public PreparationReadiness Readiness { get; }
        public int MatchedWeapons { get; }
        public int MatchedTactics { get; }
        public bool DurableFrontline { get; }
        public int AttackKindCount { get; }

        internal PreparationAssessment(
            int score,
            int matchedWeapons,
            int matchedTactics,
            bool durableFrontline,
            int attackKindCount)
        {
            Score = Math.Max(0, Math.Min(100, score));
            Readiness = Score >= 75
                ? PreparationReadiness.Ready
                : Score >= 50
                    ? PreparationReadiness.Contested
                    : PreparationReadiness.Risky;
            MatchedWeapons = matchedWeapons;
            MatchedTactics = matchedTactics;
            DurableFrontline = durableFrontline;
            AttackKindCount = attackKindCount;
        }
    }

    public sealed class ExpeditionBattleBonus
    {
        public int SupplyCount { get; }
        public int SupplyHpBonus { get; }
        public int SupplyDamageBonus { get; }
        public FieldSupportType Support { get; }
        public string SupportName { get; }
        public string Description { get; }

        internal ExpeditionBattleBonus(
            int supplyCount,
            FieldSupportType support)
        {
            SupplyCount = Math.Max(0, supplyCount);
            SupplyHpBonus = Math.Min(18, SupplyCount * 3);
            SupplyDamageBonus = Math.Min(3, (SupplyCount + 1) / 2);
            Support = support;
            SupportName = SupportNameFor(support);
            Description = DescriptionFor(support);
        }

        private static string SupportNameFor(FieldSupportType support)
        {
            switch (support)
            {
                case FieldSupportType.Recon: return "偵察支援";
                case FieldSupportType.Medical: return "救護支援";
                case FieldSupportType.Ambush: return "奇襲支援";
                default: return "支援なし";
            }
        }

        private static string DescriptionFor(FieldSupportType support)
        {
            switch (support)
            {
                case FieldSupportType.Recon: return "敵の弱点を把握：味方攻撃力+1";
                case FieldSupportType.Medical: return "応急処置を準備：味方最大HP+15%";
                case FieldSupportType.Ambush: return "先制配置を共有：味方攻撃力+20%";
                default: return "NPC支援は選択されていません";
            }
        }
    }

    public static class ExpeditionBattleBonusPolicy
    {
        public static ExpeditionBattleBonus Create(
            int supplyCount,
            FieldSupportType support)
        {
            return new ExpeditionBattleBonus(supplyCount, support);
        }

        public static void Apply(
            StageUnitData unit,
            ExpeditionBattleBonus bonus)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (bonus == null) throw new ArgumentNullException(nameof(bonus));

            unit.maxHp = Math.Max(1, unit.maxHp) + bonus.SupplyHpBonus;
            unit.damage = Math.Max(1, unit.damage) + bonus.SupplyDamageBonus;
            switch (bonus.Support)
            {
                case FieldSupportType.Recon:
                    unit.damage++;
                    break;
                case FieldSupportType.Medical:
                    unit.maxHp = PercentCeiling(unit.maxHp, 115);
                    break;
                case FieldSupportType.Ambush:
                    unit.damage = PercentCeiling(unit.damage, 120);
                    break;
            }
        }

        private static int PercentCeiling(int value, int percent)
        {
            return Math.Max(1, (value * percent + 99) / 100);
        }
    }

    public static class BattlePreparationCatalog
    {
        private static readonly WeaponDefinition[] Weapons =
        {
            new WeaponDefinition(WeaponId.Sword, "剣", FormationActionKind.Melee, 100, 0, 1, "AEGIS EDGE", FormationStatus.Fortified, true, "knight", "cavalry", "trickster"),
            new WeaponDefinition(WeaponId.Lance, "槍", FormationActionKind.Melee, 110, -5, 2, "PIERCING CHARGE", FormationStatus.Exposed, false, "knight", "cavalry", "flier"),
            new WeaponDefinition(WeaponId.Bow, "弓", FormationActionKind.Ranged, 90, 5, 4, "FALCON VOLLEY", FormationStatus.Weakened, false, "archer", "flier"),
            new WeaponDefinition(WeaponId.Spear, "長槍", FormationActionKind.Melee, 105, 0, 2, "SKYBREAKER", FormationStatus.Exposed, false, "cavalry", "flier"),
            new WeaponDefinition(WeaponId.Grimoire, "魔導書", FormationActionKind.Magic, 115, -5, 4, "ASTRAL RUIN", FormationStatus.Exposed, false, "mage", "cleric"),
            new WeaponDefinition(WeaponId.Staff, "杖", FormationActionKind.Magic, 85, 0, 3, "SANCTUARY PULSE", FormationStatus.Fortified, true, "mage", "cleric"),
            new WeaponDefinition(WeaponId.Daggers, "短剣", FormationActionKind.Melee, 85, 15, 1, "SHADOW BIND", FormationStatus.Weakened, false, "trickster", "archer"),
            new WeaponDefinition(WeaponId.Claws, "爪", FormationActionKind.Melee, 95, 10, 1, "RENDING FANG", FormationStatus.Weakened, false, "trickster")
        };

        public static IReadOnlyList<WeaponDefinition> AllWeapons => Weapons;

        public static IReadOnlyList<WeaponDefinition> GetCompatibleWeapons(string className)
        {
            WeaponDefinition[] compatible = Weapons.Where(weapon => weapon.Supports(className)).ToArray();
            if (compatible.Length == 0)
                throw new ArgumentException($"Unknown preparation class: {className}", nameof(className));
            return compatible;
        }

        public static WeaponDefinition GetWeapon(WeaponId weaponId)
        {
            WeaponDefinition definition = Weapons.FirstOrDefault(weapon => weapon.Id == weaponId);
            if (definition == null)
                throw new ArgumentOutOfRangeException(nameof(weaponId), weaponId, null);
            return definition;
        }

        public static bool IsCompatible(string className, WeaponId weaponId)
        {
            return GetWeapon(weaponId).Supports(className);
        }

        public static WeaponId GetDefaultWeapon(string className)
        {
            switch (className)
            {
                case "knight": return WeaponId.Sword;
                case "cavalry": return WeaponId.Lance;
                case "archer": return WeaponId.Bow;
                case "flier": return WeaponId.Spear;
                case "mage": return WeaponId.Grimoire;
                case "cleric": return WeaponId.Staff;
                case "trickster": return WeaponId.Daggers;
                default:
                    throw new ArgumentException($"Unknown preparation class: {className}", nameof(className));
            }
        }
    }

    public sealed class BattlePreparationState
    {
        public const int MaxDeployedPlayers = 6;
        private readonly StageData _sourceStage;
        private readonly List<UnitLoadout> _loadouts;
        private readonly List<EnemyPreview> _enemies;
        private readonly Dictionary<string, StageUnitData> _playerUnits;

        public string StageId => _sourceStage.id;
        public IReadOnlyList<UnitLoadout> Loadouts => _loadouts;
        public IReadOnlyList<EnemyPreview> Enemies => _enemies;
        public int DeployedCount => Math.Min(MaxDeployedPlayers, _loadouts.Count);

        private BattlePreparationState(StageData stage, StagePreparationData saved)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (stage.units == null) throw new ArgumentException("Stage units are required.", nameof(stage));

            _sourceStage = stage;
            StageUnitData[] players = stage.units
                .Where(unit => !IsEnemy(unit))
                .OrderBy(unit => unit.y)
                .ThenBy(unit => unit.id, StringComparer.Ordinal)
                .ToArray();
            _playerUnits = players.ToDictionary(unit => unit.id, StringComparer.Ordinal);

            Dictionary<string, UnitLoadout> savedById = SavedLoadouts(saved)
                .Where(loadout => loadout != null && !string.IsNullOrWhiteSpace(loadout.unitId))
                .GroupBy(loadout => loadout.unitId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            _loadouts = players
                .Select((unit, originalSlot) => NormalizeLoadout(
                    unit,
                    savedById.TryGetValue(unit.id, out UnitLoadout loadout) ? loadout : null,
                    originalSlot))
                .OrderBy(loadout => loadout.formationSlot)
                .ThenBy(loadout => Array.FindIndex(players, unit => unit.id == loadout.unitId))
                .ToList();
            NormalizeSlots();

            StageUnitData[] enemies = stage.units.Where(IsEnemy).ToArray();
            double averageHp = enemies.Length == 0 ? 0d : enemies.Average(unit => Math.Max(1, unit.maxHp));
            double averageDamage = enemies.Length == 0 ? 0d : enemies.Average(unit => Math.Max(1, unit.damage));
            _enemies = enemies
                .Select(unit => new EnemyPreview(
                    unit,
                    AttackKindForClass(unit.className),
                    DangerFor(unit, stage.recommendedLevel, averageHp, averageDamage)))
                .ToList();
        }

        public static BattlePreparationState Create(StageData stage, StagePreparationData saved = null)
        {
            return new BattlePreparationState(stage, saved);
        }

        public UnitLoadout GetLoadout(string unitId)
        {
            UnitLoadout loadout = _loadouts.FirstOrDefault(candidate => candidate.unitId == unitId);
            if (loadout == null) throw new ArgumentException($"Unknown player unit: {unitId}", nameof(unitId));
            return loadout;
        }

        public bool MoveUnit(string unitId, int direction)
        {
            if (direction != -1 && direction != 1)
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "Direction must be -1 or 1.");
            int index = _loadouts.FindIndex(loadout => loadout.unitId == unitId);
            if (index < 0) throw new ArgumentException($"Unknown player unit: {unitId}", nameof(unitId));
            int target = index + direction;
            if (target < 0 || target >= _loadouts.Count) return false;
            UnitLoadout moving = _loadouts[index];
            _loadouts[index] = _loadouts[target];
            _loadouts[target] = moving;
            NormalizeSlots();
            return true;
        }

        public bool SetWeapon(string unitId, WeaponId weaponId)
        {
            UnitLoadout loadout = GetLoadout(unitId);
            StageUnitData unit = _playerUnits[unitId];
            if (!BattlePreparationCatalog.IsCompatible(unit.className, weaponId)) return false;
            loadout.weaponId = weaponId;
            return true;
        }

        public bool SetArmor(string unitId, ArmorId armorId)
        {
            UnitLoadout loadout = GetLoadout(unitId);
            StageUnitData unit = _playerUnits[unitId];
            if (!ArmorEquipmentCatalog.IsCompatible(unit.className, armorId)) return false;
            loadout.armorId = armorId;
            return true;
        }

        public void SetTactic(string unitId, TacticPolicy tactic)
        {
            if (!Enum.IsDefined(typeof(TacticPolicy), tactic))
                throw new ArgumentOutOfRangeException(nameof(tactic), tactic, null);
            GetLoadout(unitId).tactic = tactic;
        }

        public PreparationAssessment Assess()
        {
            UnitLoadout[] deployed = _loadouts
                .Take(MaxDeployedPlayers)
                .ToArray();
            if (deployed.Length == 0)
                return new PreparationAssessment(0, 0, 0, false, 0);

            bool urgentDefense = _enemies.Any(enemy =>
                enemy.Danger == EnemyDangerLevel.High ||
                enemy.Danger == EnemyDangerLevel.Critical);
            bool pressureMagic = _enemies.Any(enemy =>
                enemy.AttackKind == FormationActionKind.Magic);
            int matchedWeapons = 0;
            int matchedTactics = 0;
            var attackKinds = new HashSet<FormationActionKind>();

            for (int slot = 0; slot < deployed.Length; slot++)
            {
                UnitLoadout loadout = deployed[slot];
                StageUnitData unit = _playerUnits[loadout.unitId];
                WeaponDefinition weapon = BattlePreparationCatalog.GetWeapon(loadout.weaponId);
                attackKinds.Add(weapon.AttackKind);
                if (loadout.weaponId == RecommendedWeaponFor(unit, urgentDefense))
                    matchedWeapons++;
                if (loadout.tactic == RecommendedTacticFor(slot, urgentDefense, pressureMagic))
                    matchedTactics++;
            }

            int maximumHp = deployed
                .Select(loadout => _playerUnits[loadout.unitId])
                .Max(unit => Math.Max(1, unit.maxHp));
            StageUnitData frontline = _playerUnits[deployed[0].unitId];
            bool durableFrontline = Math.Max(1, frontline.maxHp) == maximumHp;
            int score =
                matchedWeapons * 35 / deployed.Length +
                matchedTactics * 25 / deployed.Length +
                (durableFrontline ? 20 : 0) +
                Math.Min(10, attackKinds.Count * 4) +
                (deployed[0].tactic == TacticPolicy.Defensive && urgentDefense ? 10 : 0);
            return new PreparationAssessment(
                score,
                matchedWeapons,
                matchedTactics,
                durableFrontline,
                attackKinds.Count);
        }

        public void ApplyCounterPlan()
        {
            bool urgentDefense = _enemies.Any(enemy =>
                enemy.Danger == EnemyDangerLevel.High ||
                enemy.Danger == EnemyDangerLevel.Critical);
            bool pressureMagic = _enemies.Any(enemy =>
                enemy.AttackKind == FormationActionKind.Magic);

            _loadouts.Sort((left, right) =>
            {
                StageUnitData leftUnit = _playerUnits[left.unitId];
                StageUnitData rightUnit = _playerUnits[right.unitId];
                int hp = Math.Max(1, rightUnit.maxHp).CompareTo(Math.Max(1, leftUnit.maxHp));
                return hp != 0
                    ? hp
                    : string.Compare(left.unitId, right.unitId, StringComparison.Ordinal);
            });
            NormalizeSlots();

            for (int slot = 0; slot < _loadouts.Count; slot++)
            {
                UnitLoadout loadout = _loadouts[slot];
                StageUnitData unit = _playerUnits[loadout.unitId];
                loadout.weaponId = RecommendedWeaponFor(unit, urgentDefense);
                loadout.tactic = RecommendedTacticFor(slot, urgentDefense, pressureMagic);
            }
        }

        public StagePreparationData ToSaveData()
        {
            return new StagePreparationData
            {
                stageId = StageId,
                loadouts = _loadouts.Select(loadout => loadout.Clone()).ToArray()
            };
        }

        public StageData CreateBattleStage(
            int expeditionSupplyCount = 0,
            FieldSupportType support = FieldSupportType.None)
        {
            StageData clone = CloneStage(_sourceStage);
            var deployedIds = new HashSet<string>(
                _loadouts
                    .Take(MaxDeployedPlayers)
                    .Select(loadout => loadout.unitId),
                StringComparer.Ordinal);
            clone.units = clone.units
                .Where(unit => IsEnemy(unit) || deployedIds.Contains(unit.id))
                .ToArray();
            StageUnitData[] players = clone.units
                .Where(unit => !IsEnemy(unit))
                .ToArray();
            if (players.Length == 0) return clone;

            int firstSlotY = players.Min(unit => unit.y);
            Dictionary<string, StageUnitData> byId = players.ToDictionary(unit => unit.id, StringComparer.Ordinal);
            int deployedCount = Math.Min(MaxDeployedPlayers, _loadouts.Count);
            for (int slot = 0; slot < deployedCount; slot++)
            {
                UnitLoadout loadout = _loadouts[slot];
                StageUnitData unit = byId[loadout.unitId];
                unit.y = firstSlotY + slot;
                unit.weaponId = loadout.weaponId;
                unit.armorId = loadout.armorId;
                unit.tactic = loadout.tactic;
            }
            ExpeditionBattleBonus bonus = ExpeditionBattleBonusPolicy.Create(
                expeditionSupplyCount,
                support);
            foreach (StageUnitData unit in players)
                ExpeditionBattleBonusPolicy.Apply(unit, bonus);
            return clone;
        }

        private static IEnumerable<UnitLoadout> SavedLoadouts(StagePreparationData saved)
        {
            return saved?.loadouts ?? Array.Empty<UnitLoadout>();
        }

        private static UnitLoadout NormalizeLoadout(StageUnitData unit, UnitLoadout saved, int fallbackSlot)
        {
            WeaponId defaultWeapon = BattlePreparationCatalog.GetDefaultWeapon(unit.className);
            WeaponId weapon = saved != null &&
                              Enum.IsDefined(typeof(WeaponId), saved.weaponId) &&
                              BattlePreparationCatalog.IsCompatible(unit.className, saved.weaponId)
                ? saved.weaponId
                : defaultWeapon;
            TacticPolicy tactic = saved != null && Enum.IsDefined(typeof(TacticPolicy), saved.tactic)
                ? saved.tactic
                : TacticPolicy.Balanced;
            ArmorId defaultArmor = ArmorEquipmentCatalog.GetDefaultArmor(unit.className);
            ArmorId armor = saved != null &&
                            Enum.IsDefined(typeof(ArmorId), saved.armorId) &&
                            ArmorEquipmentCatalog.IsCompatible(unit.className, saved.armorId)
                ? saved.armorId
                : defaultArmor;
            return new UnitLoadout
            {
                unitId = unit.id,
                formationSlot = saved?.formationSlot ?? fallbackSlot,
                weaponId = weapon,
                armorId = armor,
                tactic = tactic
            };
        }

        private void NormalizeSlots()
        {
            for (int i = 0; i < _loadouts.Count; i++)
                _loadouts[i].formationSlot = i;
        }

        private static FormationActionKind AttackKindForClass(string className)
        {
            if (className == "mage" || className == "cleric") return FormationActionKind.Magic;
            if (className == "archer" || className == "flier") return FormationActionKind.Ranged;
            return FormationActionKind.Melee;
        }

        private static TacticPolicy RecommendedTacticFor(
            int formationSlot,
            bool urgentDefense,
            bool pressureMagic)
        {
            if (formationSlot == 0 && urgentDefense) return TacticPolicy.Defensive;
            if (pressureMagic) return TacticPolicy.Aggressive;
            return TacticPolicy.Balanced;
        }

        private static WeaponId RecommendedWeaponFor(
            StageUnitData unit,
            bool urgentDefense)
        {
            return BattlePreparationCatalog.GetCompatibleWeapons(unit.className)
                .OrderByDescending(weapon =>
                    weapon.PowerPercent +
                    weapon.Range * 20 +
                    weapon.SpeedModifier +
                    (urgentDefense &&
                     weapon.SpecialTargetsActor &&
                     weapon.SpecialStatus == FormationStatus.Fortified
                        ? 25
                        : 0))
                .ThenBy(weapon => weapon.Id)
                .First()
                .Id;
        }

        private static EnemyDangerLevel DangerFor(
            StageUnitData unit,
            int recommendedLevel,
            double averageHp,
            double averageDamage)
        {
            if ((unit.sourceUnitId ?? unit.id ?? string.Empty).IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0)
                return EnemyDangerLevel.Critical;
            if (unit.level > recommendedLevel ||
                unit.maxHp > averageHp * 1.2d ||
                unit.damage > averageDamage * 1.2d)
                return EnemyDangerLevel.High;
            return EnemyDangerLevel.Standard;
        }

        private static bool IsEnemy(StageUnitData unit)
        {
            return string.Equals(unit.team, "enemy", StringComparison.OrdinalIgnoreCase);
        }

        private static StageData CloneStage(StageData source)
        {
            return new StageData
            {
                id = source.id,
                displayName = source.displayName,
                sourceStageId = source.sourceStageId,
                sourceWarmapId = source.sourceWarmapId,
                chapter = source.chapter,
                backgroundId = source.backgroundId,
                learningObjective = source.learningObjective,
                recommendedLevel = source.recommendedLevel,
                difficultyIndex = source.difficultyIndex,
                width = source.width,
                height = source.height,
                units = (source.units ?? Array.Empty<StageUnitData>()).Select(CloneUnit).ToArray()
            };
        }

        private static StageUnitData CloneUnit(StageUnitData source)
        {
            return new StageUnitData
            {
                id = source.id,
                sourceUnitId = source.sourceUnitId,
                displayName = source.displayName,
                className = source.className,
                team = source.team,
                level = source.level,
                x = source.x,
                y = source.y,
                maxHp = source.maxHp,
                moveRange = source.moveRange,
                attackRange = source.attackRange,
                damage = source.damage,
                weaponId = source.weaponId,
                armorId = source.armorId,
                tactic = source.tactic
            };
        }
    }
}
