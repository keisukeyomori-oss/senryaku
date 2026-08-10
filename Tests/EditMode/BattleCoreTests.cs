using System.Linq;
using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    public sealed class BattleCoreTests
    {
        [Test]
        public void MoveRange_StaysInsideGridAndExcludesOccupiedCells()
        {
            BattleCore battle = CreateBattle();

            Assert.That(battle.SelectUnit("hero"), Is.True);
            GridPoint[] range = battle.GetMoveRange("hero").ToArray();

            Assert.That(range, Does.Contain(new GridPoint(2, 1)));
            Assert.That(range.Contains(new GridPoint(3, 1)), Is.False);
            Assert.That(range.All(point => point.X >= 0 && point.X < 5 && point.Y >= 0 && point.Y < 3), Is.True);
        }

        [Test]
        public void PlayerAttack_AppliesDamageAndHandsTurnToEnemy()
        {
            BattleCore battle = CreateAdjacentBattle(enemyHp: 20);

            battle.SelectUnit("hero");
            Assert.That(battle.TryAttackSelected("enemy", out BattleEvent battleEvent), Is.True);

            Assert.That(battleEvent.Type, Is.EqualTo("attack"));
            Assert.That(battle.GetUnit("enemy").Hp, Is.EqualTo(12));
            Assert.That(battle.ActiveTeam, Is.EqualTo(BattleTeam.Enemy));
        }

        [Test]
        public void MultiplePlayers_CanActBeforeEnemyTurn()
        {
            BattleCore battle = new BattleCore(new StageData
            {
                id = "multi-player",
                displayName = "Multi Player",
                width = 5,
                height = 4,
                units = new[]
                {
                    Unit("hero", "player", 1, 1, 24, 1, 1, 8),
                    Unit("partner", "player", 1, 2, 24, 1, 1, 8),
                    Unit("enemy-a", "enemy", 2, 1, 20, 1, 1, 5),
                    Unit("enemy-b", "enemy", 2, 2, 20, 1, 1, 5)
                }
            });

            battle.SelectUnit("hero");
            battle.TryAttackSelected("enemy-a", out _);

            Assert.That(battle.ActiveTeam, Is.EqualTo(BattleTeam.Player));
            Assert.That(battle.ReadyPlayerCount, Is.EqualTo(1));

            battle.SelectUnit("partner");
            battle.TryAttackSelected("enemy-b", out _);

            Assert.That(battle.ActiveTeam, Is.EqualTo(BattleTeam.Enemy));
            Assert.That(battle.ReadyPlayerCount, Is.EqualTo(0));
        }

        [Test]
        public void EnemyTurn_IsDeterministicAndReturnsControlToPlayer()
        {
            BattleCore battle = CreateBattle();
            battle.EndPlayerTurn();

            BattleEvent[] events = battle.RunEnemyTurn().ToArray();

            Assert.That(events.First().Type, Is.EqualTo("move"));
            Assert.That(battle.GetUnit("enemy").Position, Is.EqualTo(new GridPoint(2, 1)));
            Assert.That(battle.ActiveTeam, Is.EqualTo(BattleTeam.Player));
            Assert.That(battle.TurnNumber, Is.EqualTo(2));
        }

        [Test]
        public void DefeatingLastEnemy_SetsPlayerWinner()
        {
            BattleCore battle = CreateAdjacentBattle(enemyHp: 8);

            battle.SelectUnit("hero");
            battle.TryAttackSelected("enemy", out _);

            Assert.That(battle.Winner, Is.EqualTo(BattleWinner.Player));
        }

        [Test]
        public void EnemyAttackDefeatsLastPlayer_SetsEnemyWinner()
        {
            BattleCore battle = new BattleCore(new StageData
            {
                id = "test-defeat",
                displayName = "Test Defeat",
                width = 4,
                height = 3,
                units = new[]
                {
                    Unit("hero", "player", 1, 1, 5, 1, 1, 8),
                    Unit("enemy", "enemy", 2, 1, 20, 1, 1, 5)
                }
            });

            battle.EndPlayerTurn();
            battle.RunEnemyTurn();

            Assert.That(battle.Winner, Is.EqualTo(BattleWinner.Enemy));
        }

        private static BattleCore CreateBattle()
        {
            return new BattleCore(new StageData
            {
                id = "test",
                displayName = "Test",
                width = 5,
                height = 3,
                units = new[]
                {
                    Unit("hero", "player", 1, 1, 24, 1, 1, 8),
                    Unit("enemy", "enemy", 3, 1, 20, 1, 1, 5)
                }
            });
        }

        private static BattleCore CreateAdjacentBattle(int enemyHp)
        {
            return new BattleCore(new StageData
            {
                id = "test-adjacent",
                displayName = "Test Adjacent",
                width = 4,
                height = 3,
                units = new[]
                {
                    Unit("hero", "player", 1, 1, 24, 1, 1, 8),
                    Unit("enemy", "enemy", 2, 1, enemyHp, 1, 1, 5)
                }
            });
        }

        internal static StageUnitData Unit(string id, string team, int x, int y, int hp, int move, int range, int damage)
        {
            return new StageUnitData
            {
                id = id,
                sourceUnitId = id,
                displayName = id,
                className = "test",
                team = team,
                level = 1,
                x = x,
                y = y,
                maxHp = hp,
                moveRange = move,
                attackRange = range,
                damage = damage
            };
        }
    }
}
