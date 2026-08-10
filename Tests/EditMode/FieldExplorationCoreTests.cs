using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    public sealed class FieldExplorationCoreTests
    {
        [Test]
        public void Create_UsesVisibleDeterministicEnemyAndSafePlayerStart()
        {
            FieldExplorationCore first = FieldExplorationCore.Create(4);
            FieldExplorationCore second = FieldExplorationCore.Create(4);

            Assert.That(first.PlayerX, Is.EqualTo(0.09f));
            Assert.That(first.PlayerY, Is.EqualTo(0.72f));
            Assert.That(first.EnemyX, Is.EqualTo(second.EnemyX));
            Assert.That(first.EnemyY, Is.EqualTo(second.EnemyY));
            Assert.That(first.DistanceToEnemy(), Is.GreaterThan(first.EncounterRadius));
            Assert.That(first.Obstacles.Count, Is.EqualTo(3));
            Assert.That(first.Entities.Count, Is.EqualTo(4));
            Assert.That(first.Entities[0].Id, Is.EqualTo("field-4-enemy-main"));
            Assert.That(first.Entities[1].Kind, Is.EqualTo(FieldEntityKind.Enemy));
            Assert.That(first.Entities[2].Kind, Is.EqualTo(FieldEntityKind.Treasure));
            Assert.That(first.Entities[3].Kind, Is.EqualTo(FieldEntityKind.Npc));
        }

        [Test]
        public void Move_UsesContinuousNormalizedCoordinates()
        {
            FieldExplorationCore field = FieldExplorationCore.Create(0);

            FieldExplorationResult result = field.Move(1f, 0f, 0.05f);

            Assert.That(result, Is.EqualTo(FieldExplorationResult.Moved));
            Assert.That(field.PlayerX, Is.EqualTo(0.14f).Within(0.0001f));
            Assert.That(field.PlayerY, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(field.DistanceTravelled, Is.EqualTo(0.05f).Within(0.0001f));
        }

        [Test]
        public void Move_BlocksSceneryCollision()
        {
            FieldExplorationCore field = FieldExplorationCore.Create(0, 0.30f, 0.27f);

            FieldExplorationResult result = field.Move(1f, 0f, 0.03f);

            Assert.That(result, Is.EqualTo(FieldExplorationResult.Blocked));
            Assert.That(field.PlayerX, Is.EqualTo(0.30f));
            Assert.That(field.PlayerY, Is.EqualTo(0.27f));
        }

        [Test]
        public void MoveTowardEnemy_AlwaysReachesVisibleSymbol()
        {
            FieldExplorationCore first = FieldExplorationCore.Create(5);
            FieldExplorationCore second = FieldExplorationCore.Create(5);
            FieldExplorationResult result = FieldExplorationResult.Idle;
            int steps = 0;

            while (result != FieldExplorationResult.Encounter && steps < 200)
            {
                result = first.MoveToward(first.EnemyX, first.EnemyY, 0.012f);
                FieldExplorationResult secondResult =
                    second.MoveToward(second.EnemyX, second.EnemyY, 0.012f);
                Assert.That(secondResult, Is.EqualTo(result));
                Assert.That(second.PlayerX, Is.EqualTo(first.PlayerX));
                Assert.That(second.PlayerY, Is.EqualTo(first.PlayerY));
                steps++;
            }

            Assert.That(result, Is.EqualTo(FieldExplorationResult.Encounter));
            Assert.That(first.Encountered, Is.True);
            Assert.That(steps, Is.LessThan(200));
            Assert.That(first.Move(1f, 0f, 1f), Is.EqualTo(FieldExplorationResult.Encounter));
        }

        [Test]
        public void Create_RejectsSavedPositionInsideScenery()
        {
            FieldExplorationCore field = FieldExplorationCore.Create(0, 0.37f, 0.25f);

            Assert.That(field.PlayerX, Is.EqualTo(0.09f));
            Assert.That(field.PlayerY, Is.EqualTo(0.72f));
        }

        [Test]
        public void MoveTowardTreasure_RequiresConfirmAndCanRestoreResolution()
        {
            FieldExplorationCore field = FieldExplorationCore.Create(2);
            FieldEntity treasure = field.Entities[2];
            FieldExplorationResult result = FieldExplorationResult.Idle;

            for (int step = 0; step < 200 && result != FieldExplorationResult.Interacted; step++)
                result = field.MoveToward(treasure.X, treasure.Y, 0.01f);

            Assert.That(result, Is.EqualTo(FieldExplorationResult.Interacted));
            Assert.That(field.LastInteractionEntity.Id, Is.EqualTo(treasure.Id));
            Assert.That(treasure.IsResolved, Is.False);
            Assert.That(
                field.ConfirmCurrentInteraction(),
                Is.EqualTo(FieldExplorationResult.Interacted));
            Assert.That(treasure.IsResolved, Is.True);
            Assert.That(
                field.MoveToward(treasure.X, treasure.Y, 0.01f),
                Is.EqualTo(FieldExplorationResult.Moved));

            FieldExplorationCore restored = FieldExplorationCore.Create(
                2,
                resolvedEntityIds: new[] { treasure.Id });
            Assert.That(restored.Entities[2].IsResolved, Is.True);
        }

        [Test]
        public void MoveTowardScout_StartsEncounterWithThatVisibleEnemy()
        {
            FieldExplorationCore field = FieldExplorationCore.Create(3, 0.50f, 0.48f);
            FieldEntity scout = field.Entities[1];
            FieldExplorationResult result = FieldExplorationResult.Idle;

            for (int step = 0; step < 50 && result != FieldExplorationResult.Encounter; step++)
                result = field.MoveToward(scout.X, scout.Y, 0.01f);

            Assert.That(result, Is.EqualTo(FieldExplorationResult.Encounter));
            Assert.That(field.ActiveEnemy.Id, Is.EqualTo("field-3-enemy-scout"));
            Assert.That(field.ActiveEnemy.StageIndex, Is.EqualTo(3));
            Assert.That(field.DistanceToNearestEnemy(), Is.LessThanOrEqualTo(field.EncounterRadius));
        }
    }
}
