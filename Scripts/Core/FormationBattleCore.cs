using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    public enum FormationActionKind
    {
        Melee,
        Ranged,
        Magic
    }

    public enum FormationCommandKind
    {
        Attack,
        Cooperation,
        Magic,
        Defend,
        Flee
    }

    public sealed class FormationBattleCommand
    {
        public FormationCommandKind Kind { get; }
        public string TargetUnitId { get; }

        public FormationBattleCommand(
            FormationCommandKind kind,
            string targetUnitId = null)
        {
            Kind = kind;
            TargetUnitId = targetUnitId;
        }
    }

    public sealed class FormationCombatant
    {
        public string Id { get; }
        public string SourceUnitId { get; }
        public string DisplayName { get; }
        public string ClassName { get; }
        public BattleTeam Team { get; }
        public int Level { get; }
        public int FormationSlot { get; }
        public int MaxHp { get; }
        public int Hp { get; internal set; }
        public int Damage { get; }
        public WeaponId WeaponId { get; }
        public ArmorId ArmorId { get; }
        public int ArmorDamageReductionPercent { get; }
        public TacticPolicy Tactic { get; }
        public int InitiativeScore { get; }
        public int SpecialCooldown { get; internal set; }
        public FormationStatus Status { get; internal set; }
        public int StatusTurns { get; internal set; }
        public int CooperationCharge { get; internal set; }
        public bool IsAlive => Hp > 0;

        internal FormationCombatant(StageUnitData data, int formationSlot)
        {
            Id = data.id;
            SourceUnitId = string.IsNullOrWhiteSpace(data.sourceUnitId) ? data.id : data.sourceUnitId;
            DisplayName = data.displayName;
            ClassName = data.className ?? string.Empty;
            Team = string.Equals(data.team, "enemy", StringComparison.OrdinalIgnoreCase)
                ? BattleTeam.Enemy
                : BattleTeam.Player;
            Level = Math.Max(1, data.level);
            FormationSlot = formationSlot;
            ArmorId = ArmorEquipmentCatalog.IsCompatible(ClassName, data.armorId)
                ? data.armorId
                : ArmorEquipmentCatalog.GetDefaultArmor(ClassName);
            ArmorDefinition armor = ArmorEquipmentCatalog.GetArmor(ArmorId);
            MaxHp = Math.Max(1, (data.maxHp * armor.MaxHpPercent + 99) / 100);
            Hp = MaxHp;
            Damage = Math.Max(1, data.damage);
            WeaponId = BattlePreparationCatalog.IsCompatible(ClassName, data.weaponId)
                ? data.weaponId
                : BattlePreparationCatalog.GetDefaultWeapon(ClassName);
            ArmorDamageReductionPercent = armor.DamageReductionPercent;
            Tactic = data.tactic;
            InitiativeScore = FormationBattleCore.InitiativeOf(ClassName) +
                              BattlePreparationCatalog.GetWeapon(WeaponId).SpeedModifier +
                              armor.SpeedModifier +
                              FormationBattleCore.TacticInitiativeModifier(Tactic);
            SpecialCooldown =
                BossPresencePolicy.GetSpecialCooldown(Id, SourceUnitId);
            Status = FormationStatus.None;
            StatusTurns = 0;
            CooperationCharge = 0;
        }
    }

    public sealed class FormationAction
    {
        public FormationCombatant Actor { get; }
        public FormationCombatant Target { get; }
        public FormationActionKind Kind { get; }
        public int Damage { get; }
        public bool WasGuarded { get; }
        public bool WasCritical { get; }
        public bool DefeatedTarget { get; }
        public bool IsSpecial { get; }
        public string SpecialName { get; }
        public FormationStatus AppliedStatus { get; }
        public FormationCombatant StatusRecipient { get; }
        public int CooldownRemaining { get; }
        public int FormationDistance { get; }
        public int WeaponRange { get; }
        public bool WasOutOfRange { get; }
        public bool IsCooperation { get; }
        public FormationCombatant Cooperator { get; }
        public int Sequence { get; }
        public FormationCommandKind CommandKind { get; }
        public bool IsDefending => CommandKind == FormationCommandKind.Defend;
        public bool IsEscape => CommandKind == FormationCommandKind.Flee;

        internal FormationAction(
            FormationCombatant actor,
            FormationCombatant target,
            FormationActionKind kind,
            int damage,
            bool wasGuarded,
            bool wasCritical,
            bool defeatedTarget,
            bool isSpecial,
            string specialName,
            FormationStatus appliedStatus,
            FormationCombatant statusRecipient,
            int cooldownRemaining,
            int formationDistance,
            int weaponRange,
            bool wasOutOfRange,
            bool isCooperation,
            FormationCombatant cooperator,
            int sequence,
            FormationCommandKind commandKind = FormationCommandKind.Attack)
        {
            Actor = actor;
            Target = target;
            Kind = kind;
            Damage = damage;
            WasGuarded = wasGuarded;
            WasCritical = wasCritical;
            DefeatedTarget = defeatedTarget;
            IsSpecial = isSpecial;
            SpecialName = specialName ?? string.Empty;
            AppliedStatus = appliedStatus;
            StatusRecipient = statusRecipient;
            CooldownRemaining = cooldownRemaining;
            FormationDistance = formationDistance;
            WeaponRange = weaponRange;
            WasOutOfRange = wasOutOfRange;
            IsCooperation = isCooperation;
            Cooperator = cooperator;
            Sequence = sequence;
            CommandKind = commandKind;
        }
    }

    /// <summary>
    /// Deterministic formation battle. Grid coordinates are used only to preserve the authored
    /// front/back ordering; combat itself has no tile movement or player cursor phase.
    /// </summary>
    public sealed class FormationBattleCore
    {
        private readonly List<FormationCombatant> _units;
        private readonly Queue<FormationCombatant> _initiative = new Queue<FormationCombatant>();
        private int _sequence;

        public IReadOnlyList<FormationCombatant> Units => _units;
        public BattleWinner Winner { get; private set; } = BattleWinner.None;
        public int RoundNumber { get; private set; }
        public int ActionCount => _sequence;

        public IReadOnlyList<FormationCombatant> GetUpcomingUnits(int maximum)
        {
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));

            FormationCombatant[] remaining = _initiative
                .Where(unit => unit.IsAlive)
                .Take(maximum)
                .ToArray();
            if (remaining.Length > 0) return remaining;

            return _units
                .Where(unit => unit.IsAlive)
                .OrderByDescending(unit => unit.InitiativeScore)
                .ThenBy(unit => unit.Team)
                .ThenBy(unit => unit.FormationSlot)
                .ThenBy(unit => unit.Id, StringComparer.Ordinal)
                .Take(maximum)
                .ToArray();
        }

        // 銘器の効果。試練の中では必ず無効になる（RelicEffectPolicy.AppliesTo で判定）。
        private readonly bool _negateGuard;
        private readonly bool _revivesOnceWhenFelled;
        private readonly bool _ignoreBondAdjacency;
        private bool _reviveUsed;

        /// <summary>銘器の効果を持ち込まない通常の戦闘。</summary>
        public FormationBattleCore(StageData stage)
            : this(stage, null)
        {
        }

        /// <summary>
        /// 銘器の所持状況を持ち込む戦闘。
        /// resolvedIds には CampaignSaveData.resolvedStoryEntityIds をそのまま渡してよい。
        /// 試練ステージでは RelicEffectPolicy 側で自動的に無効化されるため、呼び出し側で分岐しなくてよい。
        /// </summary>
        public FormationBattleCore(StageData stage, IEnumerable<string> resolvedIds)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            bool relicsApply = RelicEffectPolicy.AppliesTo(stage);
            _negateGuard = relicsApply && RelicEffectPolicy.NegatesGuard(resolvedIds);
            _revivesOnceWhenFelled = relicsApply && RelicEffectPolicy.RevivesOnceWhenFelled(resolvedIds);
            _ignoreBondAdjacency = relicsApply && RelicEffectPolicy.IgnoresBondAdjacency(resolvedIds);
            if (stage.units == null || stage.units.Length == 0)
                throw new ArgumentException("Stage must contain units.", nameof(stage));
            if (stage.units.Select(unit => unit.id).Distinct().Count() != stage.units.Length)
                throw new ArgumentException("Unit ids must be unique.", nameof(stage));

            _units = new List<FormationCombatant>();
            foreach (IGrouping<BattleTeam, StageUnitData> team in stage.units.GroupBy(TeamOf))
            {
                int slot = 0;
                foreach (StageUnitData data in team.OrderBy(unit => unit.y).ThenBy(unit => unit.id, StringComparer.Ordinal))
                    _units.Add(new FormationCombatant(data, slot++));
            }

            UpdateWinner();
        }

        public FormationCombatant GetUnit(string unitId)
        {
            return _units.FirstOrDefault(unit => unit.Id == unitId);
        }

        public FormationCombatant GetCurrentActor()
        {
            if (Winner != BattleWinner.None) return null;
            EnsureInitiative();
            while (_initiative.Count > 0 && !_initiative.Peek().IsAlive)
                _initiative.Dequeue();
            if (_initiative.Count == 0)
            {
                EnsureInitiative();
                while (_initiative.Count > 0 && !_initiative.Peek().IsAlive)
                    _initiative.Dequeue();
            }
            return _initiative.Count == 0 ? null : _initiative.Peek();
        }

        public FormationAction Advance()
        {
            return Advance(null);
        }

        public FormationAction Advance(FormationBattleCommand command)
        {
            if (Winner != BattleWinner.None) return null;
            EnsureInitiative();

            FormationCombatant actor = null;
            while (_initiative.Count > 0 && (actor == null || !actor.IsAlive))
                actor = _initiative.Dequeue();
            if (actor == null || !actor.IsAlive)
            {
                EnsureInitiative();
                actor = _initiative.Dequeue();
            }

            FormationCommandKind commandKind = actor.Team == BattleTeam.Player && command != null
                ? command.Kind
                : FormationCommandKind.Attack;

            _sequence++;
            TickStatus(actor);
            if (commandKind == FormationCommandKind.Flee)
            {
                Winner = BattleWinner.Escaped;
                return PassiveAction(actor, commandKind);
            }
            if (commandKind == FormationCommandKind.Defend)
            {
                actor.Status = FormationStatus.Fortified;
                actor.StatusTurns = 2;
                actor.CooperationCharge++;
                return PassiveAction(actor, commandKind);
            }

            FormationCombatant requestedTarget = command == null || string.IsNullOrWhiteSpace(command.TargetUnitId)
                ? null
                : GetUnit(command.TargetUnitId);
            FormationCombatant target = requestedTarget != null &&
                                        requestedTarget.IsAlive &&
                                        requestedTarget.Team != actor.Team
                ? requestedTarget
                : SelectTarget(actor);
            if (target == null)
            {
                UpdateWinner();
                return null;
            }

            WeaponDefinition weapon = BattlePreparationCatalog.GetWeapon(actor.WeaponId);
            FormationActionKind kind = commandKind == FormationCommandKind.Magic
                ? FormationActionKind.Magic
                : weapon.AttackKind;
            int formationDistance = FormationDistanceBetween(actor, target);
            bool outOfRange = formationDistance > weapon.Range;
            FormationCombatant cooperator = commandKind == FormationCommandKind.Cooperation
                ? SelectCooperator(actor, true)
                : commandKind == FormationCommandKind.Attack
                    ? SelectCooperator(actor, false)
                    : null;
            bool cooperation = cooperator != null;
            bool critical = IsCritical(actor, _sequence);
            bool special = actor.SpecialCooldown == 0;
            bool guarded = target.Tactic == TacticPolicy.Defensive ||
                           (kind != FormationActionKind.Magic &&
                            string.Equals(target.ClassName, "knight", StringComparison.OrdinalIgnoreCase));
            // 静寂の刃: 味方の攻撃に限り、相手の防御姿勢を無視する。
            if (_negateGuard && actor.Team == BattleTeam.Player) guarded = false;
            int damage = Percent(actor.Damage, weapon.PowerPercent);
            if (commandKind == FormationCommandKind.Magic)
                damage = Percent(damage, 125);
            damage = Percent(damage, OutgoingPowerPercent(actor.Tactic));
            if (actor.Status == FormationStatus.Weakened) damage = Percent(damage, 80);
            if (critical) damage += Math.Max(2, damage / 2);
            if (special)
                damage = Percent(
                    damage,
                    BossPresencePolicy.GetSpecialPowerPercent(actor.Id, actor.SourceUnitId));
            if (cooperation) damage = Percent(damage, 150);
            if (outOfRange) damage = Percent(damage, 50);
            damage = Percent(damage, IncomingPowerPercent(target.Tactic));
            if (target.Status == FormationStatus.Exposed) damage = Percent(damage, 120);
            else if (target.Status == FormationStatus.Fortified) damage = Percent(damage, 80);
            damage = Percent(damage, 100 - target.ArmorDamageReductionPercent);
            if (guarded) damage = Percent(damage, 75);
            damage = Math.Min(target.Hp, Math.Max(1, damage));
            // 帰路の外套: 味方が倒れるはずの一撃を、1戦につき一度だけHP1で耐える。
            if (_revivesOnceWhenFelled &&
                !_reviveUsed &&
                target.Team == BattleTeam.Player &&
                damage >= target.Hp)
            {
                damage = Math.Max(0, target.Hp - 1);
                _reviveUsed = true;
            }
            target.Hp -= damage;
            bool defeated = !target.IsAlive;
            FormationCombatant statusRecipient = null;
            FormationStatus appliedStatus = FormationStatus.None;
            if (special)
            {
                actor.SpecialCooldown =
                    BossPresencePolicy.GetSpecialCooldown(actor.Id, actor.SourceUnitId);
                statusRecipient = weapon.SpecialTargetsActor ? actor : target;
                if (statusRecipient.IsAlive)
                {
                    statusRecipient.Status = weapon.SpecialStatus;
                    statusRecipient.StatusTurns = 2;
                    appliedStatus = weapon.SpecialStatus;
                }
            }
            else
            {
                actor.SpecialCooldown = Math.Max(0, actor.SpecialCooldown - 1);
            }
            if (cooperation)
            {
                actor.CooperationCharge = 0;
                cooperator.CooperationCharge = 0;
            }
            else
            {
                actor.CooperationCharge++;
            }
            UpdateWinner();

            return new FormationAction(
                actor,
                target,
                kind,
                damage,
                guarded,
                critical,
                defeated,
                special,
                special ? BossSpecialNameOr(actor, kind, weapon.SpecialName) : string.Empty,
                appliedStatus,
                statusRecipient,
                actor.SpecialCooldown,
                formationDistance,
                weapon.Range,
                outOfRange,
                cooperation,
                cooperator,
                _sequence,
                commandKind);
        }

        private FormationAction PassiveAction(
            FormationCombatant actor,
            FormationCommandKind commandKind)
        {
            return new FormationAction(
                actor,
                actor,
                FormationActionKind.Melee,
                0,
                false,
                false,
                false,
                false,
                commandKind == FormationCommandKind.Defend ? "DEFEND" : "FLEE",
                commandKind == FormationCommandKind.Defend
                    ? FormationStatus.Fortified
                    : FormationStatus.None,
                commandKind == FormationCommandKind.Defend ? actor : null,
                actor.SpecialCooldown,
                0,
                0,
                false,
                false,
                null,
                _sequence,
                commandKind);
        }

        private void EnsureInitiative()
        {
            if (_initiative.Count > 0) return;
            RoundNumber++;
            foreach (FormationCombatant unit in _units
                         .Where(unit => unit.IsAlive)
                         .OrderByDescending(unit => unit.InitiativeScore)
                         .ThenBy(unit => unit.Team)
                         .ThenBy(unit => unit.FormationSlot)
                         .ThenBy(unit => unit.Id, StringComparer.Ordinal))
            {
                _initiative.Enqueue(unit);
            }
        }

        private FormationCombatant SelectTarget(FormationCombatant actor)
        {
            FormationCombatant[] allOpponents = _units
                .Where(unit => unit.IsAlive && unit.Team != actor.Team)
                .ToArray();
            WeaponDefinition weapon = BattlePreparationCatalog.GetWeapon(actor.WeaponId);
            FormationCombatant[] inRange = allOpponents
                .Where(unit => FormationDistanceBetween(actor, unit) <= weapon.Range)
                .ToArray();
            IEnumerable<FormationCombatant> candidates =
                inRange.Length > 0 ? inRange : allOpponents;
            IOrderedEnumerable<FormationCombatant> opponents = candidates
                .OrderBy(unit => unit.Tactic == TacticPolicy.Defensive ? 1 : 0);
            if (actor.Tactic == TacticPolicy.Aggressive)
                return opponents.OrderBy(unit => unit.Tactic == TacticPolicy.Defensive ? 1 : 0)
                    .ThenBy(unit => unit.Hp)
                    .ThenBy(unit => unit.FormationSlot)
                    .ThenBy(unit => unit.Id, StringComparer.Ordinal)
                    .FirstOrDefault();

            FormationActionKind kind = BattlePreparationCatalog.GetWeapon(actor.WeaponId).AttackKind;
            if (kind == FormationActionKind.Ranged)
                return opponents.ThenBy(unit => unit.Hp).ThenBy(unit => unit.FormationSlot).ThenBy(unit => unit.Id, StringComparer.Ordinal).FirstOrDefault();
            if (kind == FormationActionKind.Magic)
                return opponents.ThenByDescending(unit => unit.ClassName == "knight").ThenBy(unit => unit.Hp).ThenBy(unit => unit.Id, StringComparer.Ordinal).FirstOrDefault();
            return opponents.ThenBy(unit => Math.Abs(unit.FormationSlot - actor.FormationSlot))
                .ThenBy(unit => unit.FormationSlot).ThenBy(unit => unit.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        private FormationCombatant SelectCooperator(FormationCombatant actor, bool requested)
        {
            FormationActionKind actorKind =
                BattlePreparationCatalog.GetWeapon(actor.WeaponId).AttackKind;
            return _units
                .Where(unit =>
                    unit.IsAlive &&
                    unit.Team == actor.Team &&
                    unit != actor &&
                    // 二重奏: 味方に限り、隣接していなくても連携できる。
                    (IsBondAdjacencyIgnoredFor(actor) ||
                     Math.Abs(unit.FormationSlot - actor.FormationSlot) == 1) &&
                    (requested || unit.CooperationCharge > 0) &&
                    (requested || actor.CooperationCharge > 0) &&
                    (requested || unit.CooperationCharge + actor.CooperationCharge >= 3) &&
                    BattlePreparationCatalog.GetWeapon(unit.WeaponId).AttackKind != actorKind)
                .OrderByDescending(unit => unit.CooperationCharge)
                .ThenBy(unit => unit.FormationSlot)
                .ThenBy(unit => unit.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// ボスは武器由来の共通技ではなく専用技を名乗る。
        /// 該当しないユニットは従来どおり武器の技名をそのまま使う。
        /// </summary>
        private static string BossSpecialNameOr(
            FormationCombatant actor,
            FormationActionKind kind,
            string weaponSpecialName)
        {
            string bossName = BossPresencePolicy.GetSpecialName(actor.Id, actor.SourceUnitId, kind);
            return string.IsNullOrEmpty(bossName) ? weaponSpecialName : bossName;
        }

        private bool IsBondAdjacencyIgnoredFor(FormationCombatant actor)
        {
            return _ignoreBondAdjacency && actor.Team == BattleTeam.Player;
        }

        private static int FormationDistanceBetween(
            FormationCombatant actor,
            FormationCombatant target)
        {
            return Math.Abs(actor.FormationSlot - target.FormationSlot) + 1;
        }

        private void UpdateWinner()
        {
            bool playerAlive = _units.Any(unit => unit.Team == BattleTeam.Player && unit.IsAlive);
            bool enemyAlive = _units.Any(unit => unit.Team == BattleTeam.Enemy && unit.IsAlive);
            Winner = playerAlive && enemyAlive
                ? BattleWinner.None
                : playerAlive ? BattleWinner.Player : BattleWinner.Enemy;
        }

        private static BattleTeam TeamOf(StageUnitData data)
        {
            return string.Equals(data.team, "enemy", StringComparison.OrdinalIgnoreCase)
                ? BattleTeam.Enemy
                : BattleTeam.Player;
        }

        private static FormationActionKind KindOf(string className)
        {
            if (className == "mage" || className == "cleric") return FormationActionKind.Magic;
            if (className == "archer" || className == "flier") return FormationActionKind.Ranged;
            return FormationActionKind.Melee;
        }

        internal static int InitiativeOf(string className)
        {
            switch (className)
            {
                case "trickster": return 70;
                case "flier": return 60;
                case "cavalry": return 55;
                case "archer": return 45;
                case "cleric": return 40;
                case "mage": return 35;
                default: return 30;
            }
        }

        internal static int TacticInitiativeModifier(TacticPolicy tactic)
        {
            switch (tactic)
            {
                case TacticPolicy.Aggressive: return 10;
                case TacticPolicy.Defensive: return -10;
                default: return 0;
            }
        }

        private static bool IsCritical(FormationCombatant actor, int sequence)
        {
            if (actor.ClassName == "trickster") return sequence % 3 == 0;
            if (actor.ClassName == "cavalry") return sequence % 5 == 0;
            return false;
        }

        private static int Percent(int value, int percent)
        {
            return Math.Max(1, (value * percent) / 100);
        }

        private static int OutgoingPowerPercent(TacticPolicy tactic)
        {
            switch (tactic)
            {
                case TacticPolicy.Aggressive: return 120;
                case TacticPolicy.Defensive: return 85;
                default: return 100;
            }
        }

        private static int IncomingPowerPercent(TacticPolicy tactic)
        {
            return tactic == TacticPolicy.Aggressive ? 110 : 100;
        }

        private static void TickStatus(FormationCombatant actor)
        {
            if (actor.Status == FormationStatus.None || actor.StatusTurns <= 0) return;
            actor.StatusTurns--;
            if (actor.StatusTurns > 0) return;
            actor.Status = FormationStatus.None;
        }
    }
}
