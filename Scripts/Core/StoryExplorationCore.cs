using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    public enum StoryAreaKind
    {
        Town,
        Interior,
        Inn,
        Base,
        Dungeon
    }

    public enum StoryEntityKind
    {
        Dialogue,
        Passage,
        Treasure,
        Recruit
    }

    public enum StoryExplorationResult
    {
        Idle,
        Moved,
        Blocked,
        Locked,
        Dialogue,
        Passage,
        Transfer,
        Treasure,
        Recruit
    }

    public enum StoryTimeOfDay
    {
        Morning,
        Afternoon,
        Evening,
        Night
    }

    public sealed class StoryEntity
    {
        public string Id { get; }
        public string DisplayName { get; }
        public StoryEntityKind Kind { get; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public string Message { get; }
        public bool WasPreviouslyResolved { get; }
        public bool IsResolved { get; private set; }

        internal StoryEntity(
            string id,
            string displayName,
            StoryEntityKind kind,
            float x,
            float y,
            string message,
            bool isResolved,
            bool wasPreviouslyResolved = false)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            X = x;
            Y = y;
            Message = message;
            IsResolved = isResolved;
            WasPreviouslyResolved = wasPreviouslyResolved;
        }

        internal void Resolve()
        {
            IsResolved = true;
        }

        internal void SetPosition(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public sealed class StoryWalkableZone
    {
        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }

        internal StoryWalkableZone(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool Contains(float x, float y, float radius)
        {
            return x - radius >= MinX &&
                   x + radius <= MaxX &&
                   y - radius >= MinY &&
                   y + radius <= MaxY;
        }
    }

    public sealed class StoryObstacle
    {
        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }

        internal StoryObstacle(float minX, float minY, float maxX, float maxY)
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
    /// Deterministic, resolution-independent traversal for the story town and dungeon.
    /// </summary>
    public sealed class StoryExplorationCore
    {
        private const float MinimumX = 0.055f;
        private const float MaximumX = 0.945f;
        private const float MinimumY = 0.10f;
        private const float MaximumY = 0.90f;

        private readonly StoryObstacle[] _obstacles;
        private readonly StoryWalkableZone[] _walkableZones;
        private readonly StoryEntity[] _entities;
        private bool _townGuideHeard;
        private float _clockMinuteAccumulator;

        public StoryAreaKind Area { get; }
        public float PlayerX { get; private set; }
        public float PlayerY { get; private set; }
        public float PlayerRadius => 0.018f;
        public float InteractionRadius => 0.062f;
        public int StoryClockMinutes { get; private set; }
        public StoryTimeOfDay TimeOfDay => TimeFromMinutes(StoryClockMinutes);
        public IReadOnlyList<StoryObstacle> Obstacles => _obstacles;
        public IReadOnlyList<StoryWalkableZone> WalkableZones => _walkableZones;
        public IReadOnlyList<StoryEntity> Entities => _entities;
        public BaseGrowthSnapshot BaseGrowth { get; }
        public StoryEntity LastInteractionEntity { get; private set; }

        private StoryExplorationCore(
            StoryAreaKind area,
            bool townGuideHeard,
            bool memoryArcherJoined,
            bool memoryHealerJoined,
            bool memoryMinstrelJoined,
            IEnumerable<string> resolvedEntityIds,
            int storyClockMinutes)
        {
            Area = area;
            _townGuideHeard = townGuideHeard;
            StoryClockMinutes = NormalizeMinutes(storyClockMinutes);
            var resolved = new HashSet<string>(
                resolvedEntityIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            string[] recruited = new[]
                {
                    memoryArcherJoined ? RecruitmentRosterPolicy.MemoryArcherId : null,
                    memoryHealerJoined ? RecruitmentRosterPolicy.MemoryHealerId : null,
                    memoryMinstrelJoined ? RecruitmentRosterPolicy.MemoryMinstrelId : null
                }
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray();
            BaseGrowth = BaseGrowthPolicy.Create(recruited, resolved);
            if (area == StoryAreaKind.Town)
            {
                PlayerX = 0.22f;
                PlayerY = 0.82f;
                _obstacles = Array.Empty<StoryObstacle>();
                _walkableZones = new[]
                {
                    new StoryWalkableZone(0.07f, 0.58f, 0.88f, 0.90f),
                    new StoryWalkableZone(0.08f, 0.50f, 0.48f, 0.75f),
                    new StoryWalkableZone(0.38f, 0.38f, 0.82f, 0.70f),
                    new StoryWalkableZone(0.66f, 0.42f, 0.90f, 0.84f),
                    new StoryWalkableZone(0.58f, 0.18f, 0.78f, 0.50f)
                };
                _entities = new[]
                {
                    new StoryEntity(
                        "town-guide",
                        "旅の案内人",
                        StoryEntityKind.Dialogue,
                        0.16f,
                        0.74f,
                        "北東の古い礼拝堂で、ふたりの旅人が帰りを待っています。",
                        false,
                        townGuideHeard),
                    new StoryEntity(
                        "town-smith",
                        "水鏡の鍛冶師",
                        StoryEntityKind.Dialogue,
                        0.16f,
                        0.66f,
                        "武器だけでなく、誰をどこに立たせるかで戦いは変わる。",
                        false,
                        resolved.Contains("town-smith")),
                    new StoryEntity(
                        "town-herbalist",
                        "湖風の薬師",
                        StoryEntityKind.Dialogue,
                        0.77f,
                        0.78f,
                        "礼拝堂へ向かうなら、傷薬と帰る道を忘れないで。",
                        false,
                        resolved.Contains("town-herbalist")),
                    new StoryEntity(
                        "town-gate-warden",
                        "北東門の衛兵",
                        StoryEntityKind.Dialogue,
                        0.72f,
                        0.43f,
                        "礼拝堂では残響に惑わされます。足音の重なる道を選んでください。",
                        false,
                        resolved.Contains("town-gate-warden")),
                    new StoryEntity(
                        "town-market-cache",
                        "露店の旅支度箱",
                        StoryEntityKind.Treasure,
                        0.47f,
                        0.84f,
                        "町の人々が旅立つ一行へ託した携帯食と護符。",
                        resolved.Contains("town-market-cache")),
                    new StoryEntity(
                        "town-atelier-door",
                        "思い出工房",
                        StoryEntityKind.Passage,
                        0.78f,
                        0.68f,
                        "町の人々が思い出の品を預ける工房。",
                        false),
                    new StoryEntity(
                        "town-inn-door",
                        "湖畔の宿",
                        StoryEntityKind.Passage,
                        0.82f,
                        0.50f,
                        "旅人と町の人が集う湖畔の宿。",
                        false),
                    new StoryEntity(
                        "town-base-door",
                        "灯の館",
                        StoryEntityKind.Passage,
                        0.55f,
                        0.60f,
                        "迎えた仲間たちが帰ってくる、小さな拠点。",
                        false),
                    new StoryEntity(
                        "town-dungeon-gate",
                        "北東門",
                        StoryEntityKind.Passage,
                        0.68f,
                        0.24f,
                        "古い礼拝堂へ続く門。",
                        false)
                };
            }
            else if (area == StoryAreaKind.Interior)
            {
                PlayerX = 0.50f;
                PlayerY = 0.80f;
                _obstacles = Array.Empty<StoryObstacle>();
                _walkableZones = new[]
                {
                    new StoryWalkableZone(0.18f, 0.42f, 0.82f, 0.90f),
                    new StoryWalkableZone(0.68f, 0.46f, 0.88f, 0.68f)
                };
                _entities = new[]
                {
                    new StoryEntity(
                        "interior-caretaker",
                        "工房の世話人",
                        StoryEntityKind.Dialogue,
                        0.36f,
                        0.55f,
                        "贈り物は、受け取った日の記憶まで大切にしまっておくものです。",
                        false,
                        resolved.Contains("interior-caretaker")),
                    new StoryEntity(
                        "interior-keepsake",
                        "思い出の小箱",
                        StoryEntityKind.Treasure,
                        0.76f,
                        0.58f,
                        "丁寧に包まれた遠征のお守り。",
                        resolved.Contains("interior-keepsake")),
                    new StoryEntity(
                        "interior-exit",
                        "工房の出口",
                        StoryEntityKind.Passage,
                        0.50f,
                        0.87f,
                        "水鏡の町へ戻る。",
                        false)
                };
            }
            else if (area == StoryAreaKind.Inn)
            {
                PlayerX = 0.50f;
                PlayerY = 0.80f;
                _obstacles = Array.Empty<StoryObstacle>();
                _walkableZones = new[]
                {
                    new StoryWalkableZone(0.18f, 0.34f, 0.84f, 0.90f),
                    new StoryWalkableZone(0.42f, 0.26f, 0.76f, 0.58f)
                };
                _entities = new[]
                {
                    new StoryEntity(
                        "inn-host",
                        "湖畔の宿主",
                        StoryEntityKind.Dialogue,
                        0.27f,
                        0.54f,
                        "出発前に仲間の顔を見ておくと、隊列の迷いも少なくなるよ。",
                        false,
                        resolved.Contains("inn-host")),
                    new StoryEntity(
                        "inn-minstrel",
                        memoryMinstrelJoined ? "記憶の吟遊詩人" : "旅の楽師",
                        memoryMinstrelJoined
                            ? StoryEntityKind.Dialogue
                            : townGuideHeard && resolved.Contains("interior-keepsake")
                                ? StoryEntityKind.Recruit
                                : StoryEntityKind.Dialogue,
                        0.58f,
                        0.37f,
                        "勝ち戦より、帰ってきた人の足音を歌に残したいんだ。",
                        false,
                        memoryMinstrelJoined || resolved.Contains("inn-minstrel")),
                    new StoryEntity(
                        "inn-traveler-map",
                        "旅人の古地図",
                        StoryEntityKind.Treasure,
                        0.48f,
                        0.52f,
                        "礼拝堂までの安全な足場が書き込まれた古地図。",
                        resolved.Contains("inn-traveler-map")),
                    new StoryEntity(
                        "inn-exit",
                        "宿の出口",
                        StoryEntityKind.Passage,
                        0.50f,
                        0.88f,
                        "水鏡の町へ戻る。",
                        false)
                };
            }
            else if (area == StoryAreaKind.Base)
            {
                PlayerX = 0.50f;
                PlayerY = 0.82f;
                _obstacles = Array.Empty<StoryObstacle>();
                _walkableZones = new[]
                {
                    new StoryWalkableZone(0.16f, 0.28f, 0.84f, 0.92f)
                };
                var residents = new List<StoryEntity>
                {
                    new StoryEntity(
                        "base-recordkeeper",
                        "灯の名簿を守る記録係",
                        StoryEntityKind.Dialogue,
                        0.25f,
                        0.53f,
                        BaseGrowth.RosterSummary,
                        false,
                        resolved.Contains("base-recordkeeper"))
                };
                if (memoryArcherJoined)
                {
                    residents.Add(new StoryEntity(
                        "base-memory-archer",
                        "記憶の射手",
                        StoryEntityKind.Dialogue,
                        0.45f,
                        0.46f,
                        "拠点から町へ続く道を見張っている。",
                        false,
                        resolved.Contains("base-memory-archer")));
                }
                if (memoryHealerJoined)
                {
                    residents.Add(new StoryEntity(
                        "base-memory-healer",
                        "記憶の癒し手",
                        StoryEntityKind.Dialogue,
                        0.62f,
                        0.51f,
                        "出発する仲間の傷と呼吸を確かめている。",
                        false,
                        resolved.Contains("base-memory-healer")));
                }
                if (memoryMinstrelJoined)
                {
                    residents.Add(new StoryEntity(
                        "base-memory-minstrel",
                        "記憶の吟遊詩人",
                        StoryEntityKind.Dialogue,
                        0.73f,
                        0.37f,
                        "帰還した仲間の足音を、新しい旋律に残している。",
                        false,
                        resolved.Contains("base-memory-minstrel")));
                }
                float[] supportX = { 0.25f, 0.38f, 0.52f, 0.66f, 0.78f, 0.82f };
                float[] supportY = { 0.35f, 0.34f, 0.33f, 0.34f, 0.40f, 0.60f };
                for (int i = 0; i < BaseGrowth.SupportResidents.Count; i++)
                {
                    BaseSupportResident support = BaseGrowth.SupportResidents[i];
                    residents.Add(new StoryEntity(
                        support.BaseEntityId,
                        support.Name,
                        StoryEntityKind.Dialogue,
                        supportX[i],
                        supportY[i],
                        support.Description,
                        false,
                        resolved.Contains(support.BaseEntityId)));
                }
                residents.Add(new StoryEntity(
                    "base-exit",
                    "館の出口",
                    StoryEntityKind.Passage,
                    0.50f,
                    0.89f,
                    "水鏡の町へ戻る。",
                    false));
                _entities = residents.ToArray();
            }
            else
            {
                PlayerX = 0.10f;
                PlayerY = 0.80f;
                _obstacles = Array.Empty<StoryObstacle>();
                _walkableZones = new[]
                {
                    new StoryWalkableZone(0.055f, 0.66f, 0.42f, 0.90f),
                    new StoryWalkableZone(0.18f, 0.38f, 0.74f, 0.76f),
                    new StoryWalkableZone(0.055f, 0.30f, 0.34f, 0.70f),
                    new StoryWalkableZone(0.58f, 0.18f, 0.945f, 0.60f),
                    new StoryWalkableZone(0.56f, 0.48f, 0.945f, 0.78f)
                };
                _entities = new[]
                {
                    new StoryEntity(
                        "dungeon-echo-scholar",
                        "残響を調べる学者",
                        StoryEntityKind.Dialogue,
                        0.17f,
                        0.46f,
                        "礼拝堂の石壁には、帰りを願った人々の声が残っています。",
                        false,
                        resolved.Contains("dungeon-echo-scholar")),
                    new StoryEntity(
                        "dungeon-lost-pilgrim",
                        "道を見失った巡礼者",
                        StoryEntityKind.Dialogue,
                        0.62f,
                        0.52f,
                        "崩れた柱の向こうで、二つの異なる足音を聞きました。",
                        false,
                        resolved.Contains("dungeon-lost-pilgrim")),
                    new StoryEntity(
                        "dungeon-relic-chest",
                        "礼拝堂の宝箱",
                        StoryEntityKind.Treasure,
                        0.78f,
                        0.70f,
                        "古い紋章が刻まれた遠征用の護符。",
                        resolved.Contains("dungeon-relic-chest")),
                    new StoryEntity(
                        "dungeon-memory-archer",
                        "記憶の射手",
                        memoryArcherJoined
                            ? StoryEntityKind.Dialogue
                            : StoryEntityKind.Recruit,
                        0.83f,
                        0.24f,
                        "あの日の約束を胸に、帰る場所を守る射手。",
                        false,
                        memoryArcherJoined),
                    new StoryEntity(
                        "dungeon-memory-healer",
                        "記憶の癒し手",
                        memoryHealerJoined
                            ? StoryEntityKind.Dialogue
                            : StoryEntityKind.Recruit,
                        0.72f,
                        0.42f,
                        "失われかけた記憶を灯し、傷ついた仲間を導く癒し手。",
                        false,
                        memoryHealerJoined)
                };
            }
            ApplyNpcSchedule();
        }

        public static StoryExplorationCore Create(
            StoryAreaKind area,
            bool townGuideHeard = false,
            bool memoryArcherJoined = false,
            IEnumerable<string> resolvedEntityIds = null,
            int storyClockMinutes = 540,
            bool memoryHealerJoined = false,
            bool memoryMinstrelJoined = false)
        {
            return new StoryExplorationCore(
                area,
                townGuideHeard,
                memoryArcherJoined,
                memoryHealerJoined,
                memoryMinstrelJoined,
                resolvedEntityIds,
                storyClockMinutes);
        }

        public StoryExplorationResult Move(
            float directionX,
            float directionY,
            float distance)
        {
            if (distance <= 0f) return StoryExplorationResult.Idle;
            float magnitude = (float)Math.Sqrt(
                directionX * directionX + directionY * directionY);
            if (magnitude <= 0.00001f) return StoryExplorationResult.Idle;

            float candidateX = Clamp(
                PlayerX + directionX / magnitude * distance,
                MinimumX,
                MaximumX);
            float candidateY = Clamp(
                PlayerY + directionY / magnitude * distance,
                MinimumY,
                MaximumY);
            if (IsBlocked(candidateX, candidateY))
                return StoryExplorationResult.Blocked;

            PlayerX = candidateX;
            PlayerY = candidateY;
            return DetectInteraction();
        }

        public StoryExplorationResult MoveToward(
            float targetX,
            float targetY,
            float distance)
        {
            float offsetX = targetX - PlayerX;
            float offsetY = targetY - PlayerY;
            StoryExplorationResult result = Move(offsetX, offsetY, distance);
            if (result != StoryExplorationResult.Blocked) return result;

            if (Math.Abs(offsetX) > 0.00001f)
            {
                result = Move(offsetX, 0f, distance);
                if (result != StoryExplorationResult.Blocked) return result;
            }
            if (Math.Abs(offsetY) > 0.00001f)
                return Move(0f, offsetY, distance);
            return StoryExplorationResult.Blocked;
        }

        public void AdvanceClock(float realSeconds)
        {
            if (realSeconds <= 0f) return;
            _clockMinuteAccumulator += realSeconds * 2f;
            int elapsedMinutes = (int)_clockMinuteAccumulator;
            if (elapsedMinutes <= 0) return;
            _clockMinuteAccumulator -= elapsedMinutes;
            StoryClockMinutes = NormalizeMinutes(StoryClockMinutes + elapsedMinutes);
            ApplyNpcSchedule();
        }

        public void WaitMinutes(int minutes)
        {
            if (minutes <= 0) return;
            StoryClockMinutes = NormalizeMinutes(StoryClockMinutes + minutes);
            ApplyNpcSchedule();
        }

        public bool IsWalkable(float x, float y)
        {
            return IsWalkable(x, y, PlayerRadius);
        }

        public StoryEntity FindEntity(string entityId)
        {
            return _entities.FirstOrDefault(entity =>
                string.Equals(entity.Id, entityId, StringComparison.Ordinal));
        }

        public void ResolveEntity(string entityId)
        {
            StoryEntity entity = FindEntity(entityId);
            entity?.Resolve();
            if (entity != null &&
                entity.Kind == StoryEntityKind.Dialogue &&
                string.Equals(entity.Id, "town-guide", StringComparison.Ordinal))
            {
                _townGuideHeard = true;
            }
        }

        public StoryExplorationResult ConfirmCurrentPassage()
        {
            StoryEntity passage = LastInteractionEntity;
            if (passage == null || passage.Kind != StoryEntityKind.Passage)
                return StoryExplorationResult.Idle;

            float offsetX = passage.X - PlayerX;
            float offsetY = passage.Y - PlayerY;
            float distance = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
            if (distance > InteractionRadius)
                return StoryExplorationResult.Idle;

            if (string.Equals(
                    passage.Id,
                    "town-dungeon-gate",
                    StringComparison.Ordinal) &&
                !_townGuideHeard)
                return StoryExplorationResult.Locked;
            return StoryExplorationResult.Transfer;
        }

        public StoryExplorationResult ConfirmCurrentInteraction()
        {
            StoryEntity entity = LastInteractionEntity;
            if (entity == null || entity.IsResolved)
                return StoryExplorationResult.Idle;
            float offsetX = entity.X - PlayerX;
            float offsetY = entity.Y - PlayerY;
            if (Math.Sqrt(offsetX * offsetX + offsetY * offsetY) > InteractionRadius)
                return StoryExplorationResult.Idle;
            switch (entity.Kind)
            {
                case StoryEntityKind.Dialogue: return StoryExplorationResult.Dialogue;
                case StoryEntityKind.Recruit: return StoryExplorationResult.Recruit;
                case StoryEntityKind.Treasure: return StoryExplorationResult.Treasure;
                default: return ConfirmCurrentPassage();
            }
        }

        private StoryExplorationResult DetectInteraction()
        {
            StoryEntity nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (StoryEntity entity in _entities)
            {
                if (entity.IsResolved) continue;
                float offsetX = entity.X - PlayerX;
                float offsetY = entity.Y - PlayerY;
                float distance = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                if (distance > InteractionRadius) continue;
                if (nearest == null ||
                    distance < nearestDistance ||
                    (Math.Abs(distance - nearestDistance) < 0.00001f &&
                     string.CompareOrdinal(entity.Id, nearest.Id) < 0))
                {
                    nearest = entity;
                    nearestDistance = distance;
                }
            }

            if (nearest == null)
            {
                LastInteractionEntity = null;
                return StoryExplorationResult.Moved;
            }
            LastInteractionEntity = nearest;
            if (nearest.Kind == StoryEntityKind.Dialogue)
                return StoryExplorationResult.Dialogue;
            if (nearest.Kind == StoryEntityKind.Recruit)
                return StoryExplorationResult.Recruit;
            if (nearest.Kind == StoryEntityKind.Treasure)
                return StoryExplorationResult.Treasure;
            if (string.Equals(
                    nearest.Id,
                    "town-dungeon-gate",
                    StringComparison.Ordinal) &&
                !_townGuideHeard)
                return StoryExplorationResult.Locked;
            return StoryExplorationResult.Passage;
        }

        private bool IsBlocked(float x, float y)
        {
            if (!IsWalkable(x, y, PlayerRadius)) return true;
            foreach (StoryObstacle obstacle in _obstacles)
            {
                if (obstacle.Contains(x, y, PlayerRadius)) return true;
            }
            return false;
        }

        private bool IsWalkable(float x, float y, float radius)
        {
            foreach (StoryWalkableZone zone in _walkableZones)
            {
                if (zone.Contains(x, y, radius)) return true;
            }
            return false;
        }

        private void ApplyNpcSchedule()
        {
            float patrol = TriangleWave((StoryClockMinutes % 30) / 30f);
            foreach (StoryEntity entity in _entities)
            {
                switch (entity.Id)
                {
                    case "town-guide":
                        SetScheduledPosition(
                            entity,
                            0.16f,
                            0.74f,
                            0.04f,
                            0f,
                            patrol);
                        break;
                    case "town-smith":
                        SetScheduledPosition(
                            entity,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.21f : 0.16f,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.62f : 0.66f,
                            0.035f,
                            0f,
                            patrol);
                        break;
                    case "town-herbalist":
                        SetScheduledPosition(
                            entity,
                            TimeOfDay == StoryTimeOfDay.Morning ? 0.77f :
                            TimeOfDay == StoryTimeOfDay.Afternoon ? 0.74f :
                            TimeOfDay == StoryTimeOfDay.Evening ? 0.68f : 0.80f,
                            TimeOfDay == StoryTimeOfDay.Morning ? 0.78f :
                            TimeOfDay == StoryTimeOfDay.Afternoon ? 0.72f :
                            TimeOfDay == StoryTimeOfDay.Evening ? 0.68f : 0.76f,
                            0.045f,
                            0f,
                            patrol);
                        break;
                    case "town-gate-warden":
                        SetScheduledPosition(
                            entity,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.68f : 0.72f,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.36f : 0.43f,
                            0.025f,
                            0f,
                            patrol);
                        break;
                    case "interior-caretaker":
                        SetScheduledPosition(
                            entity,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.42f : 0.30f,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.62f : 0.55f,
                            0.04f,
                            0f,
                            patrol);
                        break;
                    case "inn-host":
                        SetScheduledPosition(
                            entity,
                            TimeOfDay == StoryTimeOfDay.Morning ? 0.31f : 0.27f,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.48f : 0.54f,
                            0.04f,
                            0.015f,
                            patrol);
                        break;
                    case "inn-minstrel":
                        SetScheduledPosition(
                            entity,
                            TimeOfDay == StoryTimeOfDay.Evening ||
                            TimeOfDay == StoryTimeOfDay.Night
                                ? 0.54f
                                : 0.58f,
                            TimeOfDay == StoryTimeOfDay.Evening ||
                            TimeOfDay == StoryTimeOfDay.Night
                                ? 0.36f
                                : 0.37f,
                            0.05f,
                            0f,
                            patrol);
                        break;
                    case "dungeon-echo-scholar":
                        SetScheduledPosition(
                            entity,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.22f : 0.17f,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.52f : 0.46f,
                            0.03f,
                            0.025f,
                            patrol);
                        break;
                    case "dungeon-lost-pilgrim":
                        SetScheduledPosition(
                            entity,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.66f : 0.62f,
                            TimeOfDay == StoryTimeOfDay.Night ? 0.48f : 0.52f,
                            0.025f,
                            0f,
                            patrol);
                        break;
                }
            }
        }

        private void SetScheduledPosition(
            StoryEntity entity,
            float baseX,
            float baseY,
            float patrolX,
            float patrolY,
            float patrol)
        {
            float x = baseX + patrolX * patrol;
            float y = baseY + patrolY * patrol;
            if (IsWalkable(x, y, PlayerRadius))
                entity.SetPosition(x, y);
        }

        private static float TriangleWave(float normalized)
        {
            float wrapped = normalized - (float)Math.Floor(normalized);
            return wrapped < 0.5f
                ? wrapped * 2f
                : (1f - wrapped) * 2f;
        }

        private static StoryTimeOfDay TimeFromMinutes(int minutes)
        {
            int hour = NormalizeMinutes(minutes) / 60;
            if (hour >= 6 && hour < 12) return StoryTimeOfDay.Morning;
            if (hour >= 12 && hour < 18) return StoryTimeOfDay.Afternoon;
            if (hour >= 18 && hour < 22) return StoryTimeOfDay.Evening;
            return StoryTimeOfDay.Night;
        }

        private static int NormalizeMinutes(int minutes)
        {
            int normalized = minutes % 1440;
            return normalized < 0 ? normalized + 1440 : normalized;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }

    public static class StoryDialogueCatalog
    {
        public static string[] GetLines(StoryEntity entity) =>
            GetLines(entity, StoryTimeOfDay.Afternoon);

        public static string[] GetLines(
            StoryEntity entity,
            StoryTimeOfDay timeOfDay)
        {
            if (entity == null) return Array.Empty<string>();
            if (string.Equals(entity.Id, "inn-minstrel", StringComparison.Ordinal) &&
                entity.Kind == StoryEntityKind.Recruit)
            {
                return new[]
                {
                    "旅の楽師「その小箱の旋律……町で途切れた帰還の歌だ。君たちが音をつないでくれたんだね」",
                    "ケイハン「礼拝堂へ行く。帰りを待つ人の声を、今度こそ戦場まで届けたい」",
                    "旅の楽師「なら僕の弦も連れていって。傷ついた記憶を音に変え、みんなの歩調を支えるよ」",
                    "旅の楽師「僕はもう旅の楽師じゃない。君たちと歩く、記憶の吟遊詩人だ」"
                };
            }
            string timeLine = TimeSpecificLine(entity.Id, timeOfDay);
            BaseSupportResident baseSupport = BaseGrowthPolicy.FindByBaseEntityId(entity.Id);
            if (baseSupport != null)
            {
                string[] supportLines = entity.WasPreviouslyResolved
                    ? new[] { $"{baseSupport.Name}「ここで待っています。次の帰還も、同じ灯りの下で迎えましょう」" }
                    : new[] { baseSupport.Quote, baseSupport.Description };
                return Prepend(timeLine, supportLines);
            }
            if (entity.WasPreviouslyResolved)
                return Prepend(timeLine, RepeatLines(entity.Id));

            string[] lines;
            switch (entity.Id)
            {
                case "dungeon-memory-archer":
                    lines = new[]
                    {
                        "記憶の射手「ここまで来たんだね。あの日の約束、まだ覚えてる？」",
                        "みんも「忘れるわけないよ。一緒に帰ろう」",
                        "記憶の射手「弓を引くたび怖かった。でも、呼んでくれる声が道標になった」",
                        "記憶の射手「うん。今度は私も、みんなの帰る場所を守る」"
                    };
                    break;
                case "dungeon-memory-healer":
                    lines = new[]
                    {
                        "記憶の癒し手「ここに残った灯りを、ずっと消さずに待っていました」",
                        "ケイハン「その灯りごと連れて帰ろう。今度は同じ隊列で歩ける」",
                        "記憶の癒し手「治せない痛みもあります。それでも、隣で支えることはできます」",
                        "記憶の癒し手「はい。傷ついた記憶も、皆さんとなら癒していけます」"
                    };
                    break;
                case "town-smith":
                    lines = new[]
                    {
                        "水鏡の鍛冶師「敵の姿が分かるなら、武器だけでなく隊列も組み直すんだ」",
                        "ケイハン「前衛と後衛、それぞれが力を出せる位置を選ぶということだね」"
                    };
                    break;
                case "town-herbalist":
                    lines = new[]
                    {
                        "湖風の薬師「礼拝堂の空気は冷たいよ。この薬草を香りだけでも覚えておいて」",
                        "みんも「帰り道で同じ香りを見つけたら、町が近い合図ですね」"
                    };
                    break;
                case "town-gate-warden":
                    lines = new[]
                    {
                        "北東門の衛兵「礼拝堂には、離れて聞こえるのに同じ方角へ進む二つの足音があります」",
                        "ケイハン「一人だけではないんだ。二人とも見つけて町へ戻ります」"
                    };
                    break;
                case "interior-caretaker":
                    lines = new[]
                    {
                        "工房の世話人「ここには、町のみんなが大切にしている品を預かっています」",
                        "工房の世話人「奥の小箱は旅立つ人へ。忘れずに持っていってください」"
                    };
                    break;
                case "inn-host":
                    lines = new[]
                    {
                        "湖畔の宿主「出発前に仲間の顔を見ておくと、隊列の迷いも少なくなるよ」",
                        "ケイハン「戦う順番だけでなく、互いの調子を見る場所でもあるんだね」"
                    };
                    break;
                case "inn-minstrel":
                    lines = new[]
                    {
                        "旅の楽師「勝ち戦より、帰ってきた人の足音を歌に残したいんだ」",
                        "みんも「それなら、全員分の足音を持ち帰ります」"
                    };
                    break;
                case "dungeon-echo-scholar":
                    lines = new[]
                    {
                        "残響を調べる学者「石壁に触れると、帰りを願う声が聞こえるでしょう」",
                        "みんも「待っている人がいるなら、必ず一緒に帰ります」"
                    };
                    break;
                case "dungeon-lost-pilgrim":
                    lines = new[]
                    {
                        "道を見失った巡礼者「崩れた柱の先で、弓弦の音と祈りの声を聞きました」",
                        "みんも「二つの気配を順番にたどれば、帰り道も見失わずに済みそうです」"
                    };
                    break;
                case "base-recordkeeper":
                    lines = new[]
                    {
                        "記録係「迎えた仲間の名は、戦力ではなく帰る灯としてここに記します」",
                        entity.Message
                    };
                    break;
                case "base-memory-archer":
                    lines = new[]
                    {
                        "記憶の射手「ここなら遠くまで見える。みんなが帰る道を、先に見つけておくね」",
                        "みんも「ただいまって言える場所が増えたね」"
                    };
                    break;
                case "base-memory-healer":
                    lines = new[]
                    {
                        "記憶の癒し手「出発前の呼吸も、帰ってきたあとの傷も、ここでならゆっくり確かめられます」",
                        "ケイハン「次の戦いだけじゃなく、帰った後まで頼りにしてる」"
                    };
                    break;
                case "base-memory-minstrel":
                    lines = new[]
                    {
                        "記憶の吟遊詩人「この館に足音が増えるたび、帰還の歌も長くなるんだ」",
                        "みんも「次の一節も、みんなで持ち帰ろう」"
                    };
                    break;
                default:
                    lines = new[]
                    {
                        "旅の案内人「北東の古い礼拝堂で、ふたりの旅人が帰りを待っています」",
                        "ケイハン「二人とも迎えに行こう。戦うためだけじゃない、同じ道を歩く仲間として」"
                    };
                    break;
            }
            return Prepend(timeLine, lines);
        }

        private static string TimeSpecificLine(
            string entityId,
            StoryTimeOfDay timeOfDay)
        {
            if (string.Equals(
                    entityId,
                    "dungeon-memory-archer",
                    StringComparison.Ordinal) ||
                string.Equals(
                    entityId,
                    "dungeon-memory-healer",
                    StringComparison.Ordinal))
                return null;

            switch (timeOfDay)
            {
                case StoryTimeOfDay.Morning:
                    return "朝の空気の中、町と旅支度が静かに動き始めている。";
                case StoryTimeOfDay.Evening:
                    return "夕暮れの灯りがともり、帰りを待つ人々の声が近くなる。";
                case StoryTimeOfDay.Night:
                    return "夜の見回りと明日の支度を続けながら、話を聞かせてくれた。";
                default:
                    return null;
            }
        }

        private static string[] Prepend(string line, string[] lines)
        {
            if (string.IsNullOrWhiteSpace(line)) return lines;
            return new[] { line }.Concat(lines ?? Array.Empty<string>()).ToArray();
        }

        private static string[] RepeatLines(string entityId)
        {
            switch (entityId)
            {
                case "town-guide":
                    return new[]
                    {
                        "旅の案内人「北東門はもう開いています。みんなで帰ってきてください」"
                    };
                case "town-smith":
                    return new[]
                    {
                        "水鏡の鍛冶師「迷ったら敵の射程を見直しな。隊列の答えは戦場ごとに違う」"
                    };
                case "town-herbalist":
                    return new[]
                    {
                        "湖風の薬師「薬草の香りを忘れないで。帰る方角を教えてくれるから」"
                    };
                case "town-gate-warden":
                    return new[]
                    {
                        "北東門の衛兵「二人の足音を確かめるまで、門は開けて待っています」"
                    };
                case "interior-caretaker":
                    return new[]
                    {
                        "工房の世話人「小箱も思い出も、受け取る人を静かに待っています」"
                    };
                case "inn-host":
                    return new[]
                    {
                        "湖畔の宿主「席は空けておくよ。仲間を連れて、また顔を見せておくれ」"
                    };
                case "inn-minstrel":
                    return new[]
                    {
                        "旅の楽師「次は帰還の歌を聞かせるよ。全員そろって聴きにおいで」"
                    };
                case "dungeon-echo-scholar":
                    return new[]
                    {
                        "残響を調べる学者「先ほどより声が穏やかです。あなたたちが来たからでしょう」"
                    };
                case "dungeon-lost-pilgrim":
                    return new[]
                    {
                        "道を見失った巡礼者「あなたたちの足音を追えば、私も出口へ戻れそうです」"
                    };
                case "dungeon-memory-archer":
                    return new[]
                    {
                        "記憶の射手「隊列の後ろは任せて。今度は帰り道まで見失わないから」",
                        "みんも「うん。戦いが終わったら、町の橋を一緒に渡ろう」"
                    };
                case "dungeon-memory-healer":
                    return new[]
                    {
                        "記憶の癒し手「皆さんの呼吸が聞こえます。急がず、同じ歩調で進みましょう」",
                        "ケイハン「帰るまでが遠征だ。灯りを頼りにしてる」"
                    };
                case "base-recordkeeper":
                    return new[] { "記録係「名簿の灯は消えません。帰る場所がある限り」" };
                case "base-memory-archer":
                    return new[] { "記憶の射手「次の帰還も、ここから見届けるね」" };
                case "base-memory-healer":
                    return new[] { "記憶の癒し手「館にいる間は、どうか急がず休んでください」" };
                case "base-memory-minstrel":
                    return new[] { "記憶の吟遊詩人「おかえり。今日の足音も歌に足しておくよ」" };
                default:
                    return new[] { "さっきの話を覚えていてください。" };
            }
        }
    }

    public sealed class ChapterStoryBeat
    {
        public string Id { get; }
        public int StageIndex { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string BackgroundId { get; }
        public IReadOnlyList<string> Lines { get; }

        internal ChapterStoryBeat(
            string id,
            int stageIndex,
            string title,
            string subtitle,
            string backgroundId,
            params string[] lines)
        {
            Id = id;
            StageIndex = stageIndex;
            Title = title;
            Subtitle = subtitle;
            BackgroundId = backgroundId;
            Lines = lines ?? Array.Empty<string>();
        }
    }

    public static class ChapterStoryPolicy
    {
        private static readonly ChapterStoryBeat[] Beats =
        {
            new ChapterStoryBeat(
                "chapter-story-s2",
                2,
                "第三章　空からの脅威",
                "勝利の先で、空の道が影に覆われる",
                "forest_ruins",
                "街道を抜けた一行の前で、森の鳥たちが一斉に鳴きやんだ。",
                "記憶の射手「上です。雲の切れ間を、翼のある部隊が城門へ向かっています」",
                "ケイハン「追いつく。地上の隊列を崩さず、空への備えを準備しよう」",
                "みんも「帰る場所へ続く道なら、空からだって奪わせません」"),
            new ChapterStoryBeat(
                "chapter-story-s3",
                3,
                "第四章　城門",
                "閉ざされた門の向こうで、決戦の鐘が鳴る",
                "castle",
                "空の追手を退けても、城門は重い鎖に閉ざされたままだった。",
                "記憶の癒し手「門の内側にも負傷者がいます。まだ戦いは終わっていません」",
                "ケイハン「正面を支え、遠距離と魔法で守備隊を崩す。全員で門を開く」",
                "みんも「ここまで重ねた足音を、城の中まで届けましょう」"),
            new ChapterStoryBeat(
                "chapter-story-s4",
                4,
                "第五章　挫折",
                "崩れた隊列を、もう一度つなぎ直す",
                "night",
                "城門を越えた先で待っていた反撃は、一行の歩調を初めて乱した。",
                "記憶の吟遊詩人「音が途切れても、歌そのものが消えたわけじゃない」",
                "みんも「負けた記憶も置いていきません。次の一歩に変えて連れていきます」",
                "ケイハン「編成を見直そう。誰か一人ではなく、全員で立て直す」"),
            new ChapterStoryBeat(
                "chapter-story-s5",
                5,
                "第六章　決戦",
                "すべての記憶を連れて、玉座へ",
                "throne",
                "夜明け前、最後の扉の向こうから奪われた記憶の気配があふれ出した。",
                "ケイハン「ここから先は帰り道まで含めて決戦だ。誰も置いていかない」",
                "みんも「みんなの武器も声も覚えています。だから、きっと帰れます」",
                "仲間たちの呼吸が重なり、一行は最後の隊列を組んだ。")
        };

        public static ChapterStoryBeat GetPending(
            int stageIndex,
            IEnumerable<string> resolvedStoryEntityIds)
        {
            ChapterStoryBeat beat = Beats.FirstOrDefault(candidate =>
                candidate.StageIndex == stageIndex);
            if (beat == null) return null;
            var resolved = new HashSet<string>(
                resolvedStoryEntityIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            return resolved.Contains(beat.Id) ? null : beat;
        }
    }

    public static class RecruitmentRosterPolicy
    {
        public const string MemoryArcherId = "memory1";
        public const string MemoryHealerId = "memory2";
        public const string MemoryMinstrelId = "memory3";
        public static readonly string[] PrologueRecruitIds =
        {
            MemoryArcherId,
            MemoryHealerId
        };
        private static readonly string[] RosterRecruitIds =
        {
            MemoryArcherId,
            MemoryHealerId,
            MemoryMinstrelId
        };

        public static IReadOnlyList<string> KnownRecruitIds => RosterRecruitIds;

        public static StageData CreateStage(
            StageData source,
            IReadOnlyList<StageData> catalogStages,
            IEnumerable<string> recruitedUnitIds)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var units = (source.units ?? Array.Empty<StageUnitData>())
                .Select(CloneUnit)
                .ToList();
            var recruited = new HashSet<string>(
                recruitedUnitIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            foreach (string recruitId in RosterRecruitIds)
            {
                if (!recruited.Contains(recruitId) ||
                    units.Any(unit =>
                        string.Equals(unit.sourceUnitId, recruitId, StringComparison.Ordinal)))
                    continue;

                StageUnitData template = (catalogStages ?? Array.Empty<StageData>())
                    .Where(stage => stage != null)
                    .SelectMany(stage => stage.units ?? Array.Empty<StageUnitData>())
                    .FirstOrDefault(unit =>
                        string.Equals(unit.sourceUnitId, recruitId, StringComparison.Ordinal));
                if (template == null &&
                    string.Equals(recruitId, MemoryMinstrelId, StringComparison.Ordinal))
                {
                    template = new StageUnitData
                    {
                        id = MemoryMinstrelId,
                        sourceUnitId = MemoryMinstrelId,
                        displayName = "記憶の吟遊詩人",
                        className = "mage",
                        team = "player",
                        level = 1,
                        maxHp = 30,
                        moveRange = 2,
                        attackRange = 2,
                        damage = 8,
                        weaponId = WeaponId.Grimoire,
                        tactic = TacticPolicy.Balanced
                    };
                }
                if (template != null)
                {
                    StageUnitData recruit = CloneUnit(template);
                    recruit.id = recruitId;
                    recruit.level = Math.Max(1, source.recommendedLevel);
                    recruit.x = 1;
                    recruit.y = FirstOpenRow(units, source.height);
                    units.Add(recruit);
                }
            }

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
                units = units.ToArray()
            };
        }

        private static int FirstOpenRow(
            IEnumerable<StageUnitData> units,
            int stageHeight)
        {
            var occupied = new HashSet<int>(
                units.Where(unit =>
                        string.Equals(unit.team, "player", StringComparison.OrdinalIgnoreCase) &&
                        unit.x == 1)
                    .Select(unit => unit.y));
            int lastRow = Math.Max(1, stageHeight - 2);
            for (int row = 1; row <= lastRow; row++)
            {
                if (!occupied.Contains(row)) return row;
            }
            return lastRow;
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
