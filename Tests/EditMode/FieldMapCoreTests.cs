using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    public sealed class FieldMapCoreTests
    {
        [Test]
        public void Map_HasVisibleEnemyAndBranchingRoutes()
        {
            FieldMapCore map = FieldMapCore.Create(3);

            Assert.That(map.Nodes.Count, Is.EqualTo(8));
            Assert.That(map.EncounterNode.StageIndex, Is.EqualTo(3));
            Assert.That(map.EncounterNode.Threat, Is.EqualTo(4));
            Assert.That(map.CurrentNode.Kind, Is.EqualTo(FieldNodeKind.Camp));
            Assert.That(map.CanMoveTo(1), Is.True);
            Assert.That(map.CanMoveTo(2), Is.True);
            Assert.That(map.CanMoveTo(6), Is.False);
        }

        [Test]
        public void AdvanceTowardEncounter_IsDeterministicAndReachesSymbol()
        {
            FieldMapCore first = FieldMapCore.Create(2);
            FieldMapCore second = FieldMapCore.Create(2);
            FieldMoveResult result = FieldMoveResult.Moved;

            while (result != FieldMoveResult.Encounter)
            {
                result = first.AdvanceTowardEncounter();
                second.AdvanceTowardEncounter();
                Assert.That(first.PlayerNodeIndex, Is.EqualTo(second.PlayerNodeIndex));
                Assert.That(first.MoveCount, Is.LessThanOrEqualTo(8));
            }

            Assert.That(first.CurrentNode.Kind, Is.EqualTo(FieldNodeKind.Enemy));
            Assert.That(first.MoveCount, Is.EqualTo(4));
            Assert.That(first.AdvanceTowardEncounter(), Is.EqualTo(FieldMoveResult.Encounter));
            Assert.That(first.MoveCount, Is.EqualTo(4));
        }

        [Test]
        public void MoveTo_BlocksNonAdjacentDestination()
        {
            FieldMapCore map = FieldMapCore.Create(0);

            Assert.That(map.MoveTo(5), Is.EqualTo(FieldMoveResult.Blocked));
            Assert.That(map.PlayerNodeIndex, Is.EqualTo(0));
            Assert.That(map.MoveCount, Is.EqualTo(0));
        }

        [Test]
        public void SavedRoadAdjacentToEnemy_AdvanceEntersEncounter()
        {
            FieldMapCore map = FieldMapCore.Create(5, 5);

            Assert.That(map.CurrentNode.Kind, Is.EqualTo(FieldNodeKind.Road));
            Assert.That(map.AdvanceTowardEncounter(), Is.EqualTo(FieldMoveResult.Encounter));
            Assert.That(map.PlayerNodeIndex, Is.EqualTo(map.EncounterNode.Index));
            Assert.That(map.MoveCount, Is.EqualTo(1));
        }
    }
}
