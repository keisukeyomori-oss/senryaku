using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    public sealed class WarCampaignCoreTests
    {
        [Test]
        public void EnemyIntent_IsHiddenUntilOneLaneIsScouted()
        {
            var war = new WarCampaignCore(Warmap("normal"));

            Assert.That(war.ScoutsRemaining, Is.EqualTo(1));
            Assert.That(war.Lanes[0].RevealedEnemyIntent, Is.Null);
            Assert.Throws<System.InvalidOperationException>(() => war.CounterOrder(0));

            Assert.That(war.RevealIntent(0), Is.True);
            Assert.That(war.ScoutsRemaining, Is.Zero);
            Assert.That(war.Lanes[0].RevealedEnemyIntent, Is.Not.Null);
            Assert.That(war.RevealIntent(1), Is.False);

            WarOrder revealed = war.Lanes[0].RevealedEnemyIntent.Value;
            WarOrder counter = war.CounterOrder(0);
            Assert.That(counter, Is.Not.EqualTo(revealed));
            war.SetOrder(0, counter);

            WarRoundReport report = war.AdvanceRound();
            Assert.That(report.Round, Is.EqualTo(1));
            Assert.That(war.ScoutsRemaining, Is.EqualTo(1));
            Assert.That(war.Lanes[0].RevealedEnemyIntent, Is.Null);
        }

        [Test]
        public void CounterStrategy_IsDeterministicAndAlwaysFinishes()
        {
            WarCampaignCore first = ResolveWithCounters("hard");
            WarCampaignCore second = ResolveWithCounters("hard");

            Assert.That(first.Winner, Is.EqualTo(WarWinner.Player));
            Assert.That(first.Round, Is.LessThanOrEqualTo(first.MaxRounds));
            Assert.That(first.Round, Is.EqualTo(second.Round));
            for (int i = 0; i < first.Lanes.Count; i++)
            {
                Assert.That(first.Lanes[i].PlayerStrength, Is.EqualTo(second.Lanes[i].PlayerStrength));
                Assert.That(first.Lanes[i].EnemyStrength, Is.EqualTo(second.Lanes[i].EnemyStrength));
                Assert.That(first.Lanes[i].Control, Is.EqualTo(second.Lanes[i].Control));
            }
        }

        [Test]
        public void CycleOrder_TraversesAllThreeCommands()
        {
            var war = new WarCampaignCore(Warmap("easy"));

            Assert.That(war.Lanes[0].PlayerOrder, Is.EqualTo(WarOrder.Assault));
            Assert.That(war.CycleOrder(0), Is.EqualTo(WarOrder.Hold));
            Assert.That(war.CycleOrder(0), Is.EqualTo(WarOrder.Support));
            Assert.That(war.CycleOrder(0), Is.EqualTo(WarOrder.Assault));
        }

        private static WarCampaignCore ResolveWithCounters(string difficulty)
        {
            var war = new WarCampaignCore(Warmap(difficulty), 3);
            while (war.Winner == WarWinner.None)
            {
                for (int i = 0; i < war.Lanes.Count; i++)
                {
                    Assert.That(war.RevealIntent(i), Is.True);
                    war.SetOrder(i, war.CounterOrder(i));
                }
                war.AdvanceRound();
            }
            return war;
        }

        private static WarmapCatalogData Warmap(string difficulty)
        {
            return new WarmapCatalogData
            {
                id = "test-war",
                displayName = "Test War",
                difficulty = difficulty,
                laneCount = 3,
                nodeCount = 12,
                enemySquadCount = 8
            };
        }
    }
}
