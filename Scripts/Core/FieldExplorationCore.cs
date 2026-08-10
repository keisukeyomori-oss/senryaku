using System;
using System.Collections.Generic;

namespace BirthdayTactics.Core
{
    public enum FieldExplorationResult
    {
        Idle,
        Moved,
        Blocked,
        Encounter,
        Interacted
    }

    public enum FieldEntityKind
    {
        Enemy,
        Treasure,
        Npc
    }

    public sealed class FieldEntity
    {
        public string Id { get; }
        public string DisplayName { get; }
        public FieldEntityKind Kind { get; }
        public float X { get; }
        public float Y { get; }
        public int Threat { get; }
        public int StageIndex { get; }
        public string Message { get; }
        public bool IsResolved { get; private set; }

        internal FieldEntity(
            string id,
            string displayName,
            FieldEntityKind kind,
            float x,
            float y,
            int threat,
            int stageIndex,
            string message,
            bool isResolved)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            X = x;
            Y = y;
            Threat = threat;
            StageIndex = stageIndex;
            Message = message;
            IsResolved = isResolved;
        }

        internal void Resolve()
        {
            IsResolved = true;
        }
    }

    public sealed class FieldObstacle
    {
        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }

        internal FieldObstacle(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool Contains(float x, float y, float radius)
        {
            return x + radius > MinX &&
                   x - radius < MaxX &&
                   y + radius > MinY &&
                   y - radius < MaxY;
        }
    }

    /// <summary>
    /// Deterministic continuous field traversal used by keyboard, controller and click movement.
    /// Coordinates are normalized so simulation results do not depend on screen resolution.
    /// </summary>
    public sealed class FieldExplorationCore
    {
        private const float MinimumX = 0.055f;
        private const float MaximumX = 0.945f;
        private const float MinimumY = 0.10f;
        private const float MaximumY = 0.90f;

        private readonly FieldObstacle[] _obstacles;
        private readonly FieldEntity[] _entities;

        public float PlayerX { get; private set; }
        public float PlayerY { get; private set; }
        public float EnemyX { get; }
        public float EnemyY { get; }
        public float PlayerRadius => 0.018f;
        public float EncounterRadius => 0.060f;
        public float DistanceTravelled { get; private set; }
        public bool Encountered { get; private set; }
        public IReadOnlyList<FieldObstacle> Obstacles => _obstacles;
        public IReadOnlyList<FieldEntity> Entities => _entities;
        public FieldEntity ActiveEnemy { get; private set; }
        public FieldEntity LastInteractionEntity { get; private set; }

        private FieldExplorationCore(
            int stageIndex,
            float startX,
            float startY,
            IEnumerable<string> resolvedEntityIds)
        {
            int safeStageIndex = Math.Max(0, stageIndex);
            float stageShift = Math.Min(0.018f, safeStageIndex * 0.003f);
            EnemyX = 0.84f - stageShift;
            EnemyY = 0.42f + stageShift * 0.45f;
            _obstacles = new[]
            {
                new FieldObstacle(0.32f, 0.18f, 0.43f, 0.36f),
                new FieldObstacle(0.57f, 0.64f, 0.69f, 0.83f),
                new FieldObstacle(0.72f, 0.18f, 0.80f, 0.30f)
            };
            var resolved = new HashSet<string>(
                resolvedEntityIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            _entities = new[]
            {
                CreateEntity(
                    $"field-{safeStageIndex}-enemy-main",
                    "敵主力",
                    FieldEntityKind.Enemy,
                    EnemyX,
                    EnemyY,
                    3 + safeStageIndex,
                    safeStageIndex,
                    "敵主力と接触した。",
                    resolved),
                CreateEntity(
                    $"field-{safeStageIndex}-enemy-scout",
                    "敵斥候",
                    FieldEntityKind.Enemy,
                    0.61f,
                    0.48f,
                    1 + safeStageIndex,
                    safeStageIndex,
                    "敵斥候と接触した。",
                    resolved),
                CreateEntity(
                    $"field-{safeStageIndex}-treasure",
                    "古い宝箱",
                    FieldEntityKind.Treasure,
                    0.48f,
                    0.82f,
                    0,
                    safeStageIndex,
                    "古い宝箱から遠征物資を獲得した。",
                    resolved),
                CreateEntity(
                    $"field-{safeStageIndex}-npc",
                    "旅の軍師",
                    FieldEntityKind.Npc,
                    0.25f,
                    0.50f,
                    0,
                    safeStageIndex,
                    "旅の軍師「敵斥候は主力より弱い。先に情報を集めるとよい」",
                    resolved)
            };
            PlayerX = Clamp(startX, MinimumX, MaximumX);
            PlayerY = Clamp(startY, MinimumY, MaximumY);
            if (IsBlocked(PlayerX, PlayerY))
            {
                PlayerX = 0.09f;
                PlayerY = 0.72f;
            }
        }

        public static FieldExplorationCore Create(
            int stageIndex,
            float startX = 0.09f,
            float startY = 0.72f,
            IEnumerable<string> resolvedEntityIds = null)
        {
            return new FieldExplorationCore(
                stageIndex,
                startX,
                startY,
                resolvedEntityIds);
        }

        public FieldExplorationResult Move(float directionX, float directionY, float distance)
        {
            if (Encountered) return FieldExplorationResult.Encounter;
            if (distance <= 0f) return FieldExplorationResult.Idle;

            float magnitude = (float)Math.Sqrt(
                directionX * directionX + directionY * directionY);
            if (magnitude <= 0.00001f) return FieldExplorationResult.Idle;

            float stepX = directionX / magnitude * distance;
            float stepY = directionY / magnitude * distance;
            float candidateX = Clamp(PlayerX + stepX, MinimumX, MaximumX);
            float candidateY = Clamp(PlayerY + stepY, MinimumY, MaximumY);
            if (IsBlocked(candidateX, candidateY))
                return FieldExplorationResult.Blocked;

            float travelledX = candidateX - PlayerX;
            float travelledY = candidateY - PlayerY;
            PlayerX = candidateX;
            PlayerY = candidateY;
            DistanceTravelled += (float)Math.Sqrt(
                travelledX * travelledX + travelledY * travelledY);

            return DetectInteraction();
        }

        public FieldExplorationResult MoveToward(float targetX, float targetY, float distance)
        {
            return Move(targetX - PlayerX, targetY - PlayerY, distance);
        }

        public float DistanceToEnemy()
        {
            float x = EnemyX - PlayerX;
            float y = EnemyY - PlayerY;
            return (float)Math.Sqrt(x * x + y * y);
        }

        public float DistanceToNearestEnemy()
        {
            float nearest = float.MaxValue;
            foreach (FieldEntity entity in _entities)
            {
                if (entity.Kind != FieldEntityKind.Enemy || entity.IsResolved) continue;
                nearest = Math.Min(nearest, DistanceTo(entity));
            }
            return nearest;
        }

        public FieldExplorationResult ConfirmCurrentInteraction()
        {
            FieldEntity entity = LastInteractionEntity;
            if (entity == null || entity.IsResolved || entity.Kind == FieldEntityKind.Enemy)
                return FieldExplorationResult.Idle;
            if (DistanceTo(entity) > EncounterRadius)
                return FieldExplorationResult.Idle;
            entity.Resolve();
            return FieldExplorationResult.Interacted;
        }

        private FieldExplorationResult DetectInteraction()
        {
            FieldEntity nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (FieldEntity entity in _entities)
            {
                if (entity.IsResolved) continue;
                float distance = DistanceTo(entity);
                if (distance > EncounterRadius) continue;
                if (nearest == null ||
                    distance < nearestDistance ||
                    (Math.Abs(distance - nearestDistance) < 0.00001f &&
                     string.CompareOrdinal(entity.Id, nearest.Id) < 0))
                {
                    nearest = entity;
                    nearestDistance = distance;
                }
            }

            if (nearest == null) return FieldExplorationResult.Moved;
            if (nearest.Kind == FieldEntityKind.Enemy)
            {
                ActiveEnemy = nearest;
                Encountered = true;
                return FieldExplorationResult.Encounter;
            }

            LastInteractionEntity = nearest;
            return FieldExplorationResult.Interacted;
        }

        private float DistanceTo(FieldEntity entity)
        {
            float x = entity.X - PlayerX;
            float y = entity.Y - PlayerY;
            return (float)Math.Sqrt(x * x + y * y);
        }

        private static FieldEntity CreateEntity(
            string id,
            string displayName,
            FieldEntityKind kind,
            float x,
            float y,
            int threat,
            int stageIndex,
            string message,
            HashSet<string> resolved)
        {
            return new FieldEntity(
                id,
                displayName,
                kind,
                x,
                y,
                threat,
                stageIndex,
                message,
                resolved.Contains(id));
        }

        private bool IsBlocked(float x, float y)
        {
            foreach (FieldObstacle obstacle in _obstacles)
            {
                if (obstacle.Contains(x, y, PlayerRadius)) return true;
            }
            return false;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
