using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    public sealed class BattleCore
    {
        private readonly List<UnitState> _units;

        public int Width { get; }
        public int Height { get; }
        public string StageId { get; }
        public string StageName { get; }
        public string Chapter { get; }
        public string LearningObjective { get; }
        public int DifficultyIndex { get; }
        public IReadOnlyList<UnitState> Units => _units;
        public BattleTeam ActiveTeam { get; private set; } = BattleTeam.Player;
        public BattleWinner Winner { get; private set; } = BattleWinner.None;
        public string SelectedUnitId { get; private set; }
        public int TurnNumber { get; private set; } = 1;
        public int ReadyPlayerCount => _units.Count(unit => unit.Team == BattleTeam.Player && unit.IsAlive && !unit.HasActed);

        public BattleCore(StageData stage)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (stage.units == null || stage.units.Length == 0)
                throw new ArgumentException("Stage must contain units.", nameof(stage));

            Width = Math.Max(1, stage.width);
            Height = Math.Max(1, stage.height);
            StageId = stage.id;
            StageName = string.IsNullOrWhiteSpace(stage.displayName) ? stage.id : stage.displayName;
            Chapter = stage.chapter ?? string.Empty;
            LearningObjective = stage.learningObjective ?? string.Empty;
            DifficultyIndex = Math.Max(1, stage.difficultyIndex);
            _units = stage.units.Select(data => new UnitState(data)).ToList();

            if (_units.Select(unit => unit.Id).Distinct().Count() != _units.Count)
                throw new ArgumentException("Unit ids must be unique.", nameof(stage));
            if (_units.Any(unit => !IsInside(unit.Position)))
                throw new ArgumentException("Every unit must start inside the grid.", nameof(stage));
            if (_units.GroupBy(unit => unit.Position).Any(group => group.Count() > 1))
                throw new ArgumentException("Units cannot share a starting cell.", nameof(stage));

            UpdateWinner();
        }

        public UnitState GetUnit(string unitId)
        {
            return _units.FirstOrDefault(unit => unit.Id == unitId);
        }

        public UnitState GetUnitAt(GridPoint point)
        {
            return _units.FirstOrDefault(unit => unit.IsAlive && unit.Position.Equals(point));
        }

        public bool SelectUnit(string unitId)
        {
            if (Winner != BattleWinner.None || ActiveTeam != BattleTeam.Player) return false;
            UnitState unit = GetUnit(unitId);
            if (unit == null || !unit.IsAlive || unit.Team != BattleTeam.Player || unit.HasActed) return false;
            SelectedUnitId = unitId;
            return true;
        }

        public IReadOnlyList<GridPoint> GetMoveRange(string unitId)
        {
            UnitState unit = GetUnit(unitId);
            if (unit == null || !unit.IsAlive || unit.HasMoved || unit.HasActed)
                return Array.Empty<GridPoint>();

            var points = new List<GridPoint>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var point = new GridPoint(x, y);
                    if (unit.Position.DistanceTo(point) <= unit.MoveRange &&
                        !point.Equals(unit.Position) &&
                        GetUnitAt(point) == null)
                    {
                        points.Add(point);
                    }
                }
            }

            return points;
        }

        public IReadOnlyList<UnitState> GetAttackableEnemies(string unitId)
        {
            UnitState unit = GetUnit(unitId);
            if (unit == null || !unit.IsAlive || unit.HasActed)
                return Array.Empty<UnitState>();

            return _units
                .Where(target => target.IsAlive && target.Team != unit.Team &&
                                 unit.Position.DistanceTo(target.Position) <= unit.AttackRange)
                .OrderBy(target => target.Hp)
                .ThenBy(target => target.Id, StringComparer.Ordinal)
                .ToArray();
        }

        public bool TryMoveSelected(GridPoint destination, out BattleEvent battleEvent)
        {
            battleEvent = null;
            UnitState unit = GetUnit(SelectedUnitId);
            if (unit == null || unit.Team != ActiveTeam || !GetMoveRange(unit.Id).Contains(destination))
                return false;

            unit.Position = destination;
            unit.HasMoved = true;
            battleEvent = new BattleEvent("move", unit.Id, null, 0, destination);
            return true;
        }

        public bool TryAttackSelected(string targetId, out BattleEvent battleEvent)
        {
            battleEvent = null;
            UnitState attacker = GetUnit(SelectedUnitId);
            UnitState target = GetUnit(targetId);
            if (attacker == null || target == null || attacker.Team != ActiveTeam ||
                !GetAttackableEnemies(attacker.Id).Contains(target))
            {
                return false;
            }

            battleEvent = ApplyAttack(attacker, target);
            attacker.HasActed = true;
            SelectedUnitId = null;
            UpdateWinner();
            if (Winner == BattleWinner.None)
                ActiveTeam = ReadyPlayerCount > 0 ? BattleTeam.Player : BattleTeam.Enemy;
            return true;
        }

        public void EndPlayerTurn()
        {
            if (Winner == BattleWinner.None && ActiveTeam == BattleTeam.Player)
            {
                SelectedUnitId = null;
                ActiveTeam = BattleTeam.Enemy;
            }
        }

        public IReadOnlyList<BattleEvent> RunEnemyTurn()
        {
            var events = new List<BattleEvent>();
            if (Winner != BattleWinner.None || ActiveTeam != BattleTeam.Enemy) return events;

            foreach (UnitState enemy in _units.Where(unit => unit.Team == BattleTeam.Enemy && unit.IsAlive)
                                               .OrderBy(unit => unit.Id, StringComparer.Ordinal))
            {
                UnitState target = _units.Where(unit => unit.Team == BattleTeam.Player && unit.IsAlive)
                    .OrderBy(unit => enemy.Position.DistanceTo(unit.Position))
                    .ThenBy(unit => unit.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (target == null) break;

                if (enemy.Position.DistanceTo(target.Position) > enemy.AttackRange)
                {
                    GridPoint destination = FindEnemyDestination(enemy, target);
                    if (!destination.Equals(enemy.Position))
                    {
                        enemy.Position = destination;
                        events.Add(new BattleEvent("move", enemy.Id, null, 0, destination));
                    }
                }

                if (enemy.Position.DistanceTo(target.Position) <= enemy.AttackRange)
                {
                    events.Add(ApplyAttack(enemy, target));
                    UpdateWinner();
                    if (Winner != BattleWinner.None) break;
                }
            }

            if (Winner == BattleWinner.None)
            {
                ActiveTeam = BattleTeam.Player;
                TurnNumber++;
                foreach (UnitState player in _units.Where(unit => unit.Team == BattleTeam.Player))
                {
                    player.HasMoved = false;
                    player.HasActed = false;
                }
            }

            return events;
        }

        private GridPoint FindEnemyDestination(UnitState enemy, UnitState target)
        {
            IEnumerable<GridPoint> candidates = GetMoveRange(enemy.Id).Concat(new[] { enemy.Position });
            return candidates
                .OrderBy(point => point.DistanceTo(target.Position))
                .ThenBy(point => point.X)
                .ThenBy(point => point.Y)
                .First();
        }

        private BattleEvent ApplyAttack(UnitState attacker, UnitState target)
        {
            int appliedDamage = Math.Min(target.Hp, attacker.Damage);
            target.Hp -= appliedDamage;
            return new BattleEvent("attack", attacker.Id, target.Id, appliedDamage, target.Position);
        }

        private void UpdateWinner()
        {
            bool playerAlive = _units.Any(unit => unit.Team == BattleTeam.Player && unit.IsAlive);
            bool enemyAlive = _units.Any(unit => unit.Team == BattleTeam.Enemy && unit.IsAlive);
            Winner = playerAlive && enemyAlive
                ? BattleWinner.None
                : playerAlive ? BattleWinner.Player : BattleWinner.Enemy;
        }

        private bool IsInside(GridPoint point)
        {
            return point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;
        }
    }
}
