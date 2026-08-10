using System.Linq;
using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    public sealed class BaseGrowthCoreTests
    {
        [Test]
        public void EmptyRoster_StartsWithRecordkeeperAndCoreFacilities()
        {
            BaseGrowthSnapshot growth = BaseGrowthPolicy.Create(null, null);

            Assert.That(growth.LightCount, Is.EqualTo(1));
            Assert.That(growth.TargetLightCount, Is.EqualTo(26));
            Assert.That(growth.Level, Is.EqualTo(1));
            Assert.That(growth.Facilities, Is.EqualTo(new[]
            {
                BaseFacility.Entrance,
                BaseFacility.Roster
            }));
            Assert.That(growth.RosterSummary, Does.Contain("1/26"));
        }

        [Test]
        public void CombatAndSupportLightsUnlockFacilitiesDeterministically()
        {
            string[] recruits = RecruitmentRosterPolicy.KnownRecruitIds.ToArray();
            string[] residents = BaseGrowthPolicy.AllSupportResidents
                .Select(candidate => candidate.SourceEntityId)
                .ToArray();

            BaseGrowthSnapshot first = BaseGrowthPolicy.Create(recruits, residents);
            BaseGrowthSnapshot second = BaseGrowthPolicy.Create(
                recruits.Reverse(),
                residents.Reverse());

            Assert.That(first.LightCount, Is.EqualTo(10));
            Assert.That(first.Level, Is.EqualTo(5));
            Assert.That(first.Facilities, Has.Member(BaseFacility.Archive));
            Assert.That(first.Facilities, Has.No.Member(BaseFacility.GatheringHall));
            Assert.That(first.RosterSummary, Is.EqualTo(second.RosterSummary));
            Assert.That(first.SupportResidents.Select(item => item.BaseEntityId),
                Is.EqualTo(second.SupportResidents.Select(item => item.BaseEntityId)));
        }

        [Test]
        public void SupportResidentLookupKeepsSourceAndBaseIdentitySeparate()
        {
            BaseSupportResident smith = BaseGrowthPolicy.FindBySourceEntityId("town-smith");

            Assert.That(smith, Is.Not.Null);
            Assert.That(smith.BaseEntityId, Is.EqualTo("base-smith"));
            Assert.That(BaseGrowthPolicy.FindByBaseEntityId("base-smith").SourceEntityId,
                Is.EqualTo("town-smith"));
            Assert.That(BaseGrowthPolicy.FindBySourceEntityId("unknown"), Is.Null);
        }
    }
}
