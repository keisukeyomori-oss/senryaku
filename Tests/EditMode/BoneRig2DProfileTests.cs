using BirthdayTactics.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    public sealed class BoneRig2DProfileTests
    {
        [Test]
        public void BattleFormation_HasTwelveArticulatedPartsPerUniqueUnit()
        {
            string[] expected =
            {
                "hero", "azuki", "partner", "memory1", "memory2", "e_knight", "e_archer"
            };

            Assert.That(BoneRig2DProfile.PartCount, Is.EqualTo(12));
            Assert.That(BoneRig2DProfile.SupportedUnitIds, Is.EqualTo(expected));
            foreach (string unitId in expected)
                Assert.That(BoneRig2DProfile.Supports(unitId), Is.True, unitId);
            Assert.That(BoneRig2DProfile.Supports("e_boss"), Is.False);
        }

        [Test]
        public void ProductionBattle_UsesAuthoredFullBodySpritesInsteadOfCutoutRigs()
        {
            foreach (string unitId in BoneRig2DProfile.SupportedUnitIds)
                Assert.That(BoneRig2DProfile.ShouldUseInBattle(unitId), Is.False, unitId);
        }

        [Test]
        public void IdleSample_IsDeterministicAndBreathes()
        {
            BoneRigPoseSample2D first = BoneRig2DProfile.Sample(BoneRigPose2D.Idle, 0.25f, 0.1f);
            BoneRigPoseSample2D repeated = BoneRig2DProfile.Sample(BoneRigPose2D.Idle, 0.25f, 0.1f);
            BoneRigPoseSample2D later = BoneRig2DProfile.Sample(BoneRigPose2D.Idle, 0.50f, 0.1f);

            Assert.That(repeated.RootY, Is.EqualTo(first.RootY));
            Assert.That(repeated.TorsoRotation, Is.EqualTo(first.TorsoRotation));
            Assert.That(later.RootY, Is.Not.EqualTo(first.RootY));
            Assert.That(later.HeadRotation, Is.Not.EqualTo(first.HeadRotation));
        }

        [Test]
        public void StrikeAndDefeat_HaveDistinctReadableSilhouettes()
        {
            BoneRigPoseSample2D idle = BoneRig2DProfile.Sample(BoneRigPose2D.Idle, 0f);
            BoneRigPoseSample2D strike = BoneRig2DProfile.Sample(BoneRigPose2D.Strike, 1f);
            BoneRigPoseSample2D defeat = BoneRig2DProfile.Sample(BoneRigPose2D.Defeat, 1f);

            Assert.That(
                Mathf.Abs(strike.WeaponRotation - idle.WeaponRotation),
                Is.GreaterThan(60f));
            Assert.That(
                Mathf.Abs(strike.UpperArmLeftRotation - idle.UpperArmLeftRotation),
                Is.GreaterThan(45f));
            Assert.That(defeat.RootY, Is.LessThan(-0.5f));
            Assert.That(Mathf.Abs(defeat.TorsoRotation), Is.GreaterThan(15f));
        }

        [Test]
        public void BattleFormation_CanBuildAllRuntimeParts()
        {
            var parent = new GameObject("rig-test-parent");
            try
            {
                foreach (string unitId in BoneRig2DProfile.SupportedUnitIds)
                {
                    var unitParent = new GameObject(unitId);
                    unitParent.transform.SetParent(parent.transform);
                    BoneRig2DView rig = BoneRig2DView.TryCreate(
                        unitParent.transform,
                        unitId,
                        3.48f,
                        1f,
                        unitId.StartsWith("e_"));

                    Assert.That(rig, Is.Not.Null, unitId);
                    Assert.That(
                        rig.GetComponentsInChildren<SpriteRenderer>(true).Length,
                        Is.EqualTo(BoneRig2DProfile.PartCount),
                        unitId);
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
