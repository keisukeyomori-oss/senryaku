using System;
using System.Collections.Generic;
using System.Linq;

using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    public sealed class FormationPresentationProfileTests
    {
        [Test]
        public void RegisteredRoster_ContainsExactlyEighteenUniqueUnits()
        {
            Assert.That(FormationPresentationProfile.RegisteredUnitIds.Count, Is.EqualTo(18));
            Assert.That(FormationPresentationProfile.RegisteredUnitIds.Distinct().Count(), Is.EqualTo(18));
        }

        [Test]
        public void EveryRegisteredUnit_HasStableIdsForAllBattlePoses()
        {
            foreach (string unitId in FormationPresentationProfile.RegisteredUnitIds)
            {
                Assert.That(FormationPresentationProfile.GetPoseAssetId(unitId, BattlePose.Idle), Is.EqualTo(unitId));
                Assert.That(FormationPresentationProfile.GetPoseAssetId(unitId, BattlePose.Attack), Is.EqualTo(unitId + "_attack"));
                Assert.That(FormationPresentationProfile.GetPoseAssetId(unitId, BattlePose.Hit), Is.EqualTo(unitId + "_hit"));
                Assert.That(FormationPresentationProfile.GetPoseAssetId(unitId, BattlePose.Victory), Is.EqualTo(unitId + "_victory"));
                Assert.That(
                    FormationPresentationProfile.GetPoseAssetId(unitId, BattlePose.Incapacitated),
                    Is.EqualTo(unitId + "_defeat"));
            }

            Assert.That(
                () => FormationPresentationProfile.GetPoseAssetId("not-registered", BattlePose.Attack),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void EveryRegisteredPose_HasValidSpriteMetrics()
        {
            foreach (string unitId in FormationPresentationProfile.RegisteredUnitIds)
            {
                foreach (BattlePose pose in Enum.GetValues(typeof(BattlePose)))
                {
                    string assetId = FormationPresentationProfile.GetPoseAssetId(unitId, pose);
                    BattleSpriteMetrics metrics = FormationPresentationProfile.GetSpriteMetrics(assetId);
                    Assert.That(metrics.PivotX, Is.InRange(0.25f, 0.75f), assetId);
                    Assert.That(metrics.PivotY, Is.InRange(0f, 0.25f), assetId);
                    Assert.That(metrics.VisibleHeight, Is.InRange(0.45f, 1f), assetId);
                }
            }

            Assert.That(
                () => FormationPresentationProfile.GetSpriteMetrics("not-registered"),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void FormationAnchors_UseCompactDiagonalFrontAndRearRows()
        {
            for (int slot = 0; slot < 5; slot++)
            {
                FormationAnchor player = FormationPresentationProfile.GetAnchor(BattleTeam.Player, slot);
                FormationAnchor enemy = FormationPresentationProfile.GetAnchor(BattleTeam.Enemy, slot);

                Assert.That(player.X, Is.GreaterThan(0f));
                Assert.That(player.Y, Is.LessThan(0f));
                Assert.That(enemy.X, Is.LessThan(0f));
                Assert.That(enemy.Y, Is.GreaterThan(0f));
                Assert.That(player.Height, Is.InRange(1.68f, 2.16f));
                Assert.That(enemy.Height, Is.InRange(1.48f, 1.94f));
                Assert.That(player.ShadowWidth, Is.LessThan(0.9f));
            }

            Assert.That(FormationPresentationProfile.GetFormationRow(0), Is.EqualTo(FormationRow.Front));
            Assert.That(FormationPresentationProfile.GetFormationRow(2), Is.EqualTo(FormationRow.Front));
            Assert.That(FormationPresentationProfile.GetFormationRow(3), Is.EqualTo(FormationRow.Rear));
            Assert.That(FormationPresentationProfile.GetFormationRow(4), Is.EqualTo(FormationRow.Rear));
            Assert.That(
                FormationPresentationProfile.GetAnchor(BattleTeam.Player, 3).Y,
                Is.GreaterThan(FormationPresentationProfile.GetAnchor(BattleTeam.Player, 0).Y));
            Assert.That(
                FormationPresentationProfile.GetAnchor(BattleTeam.Enemy, 3).Y,
                Is.GreaterThan(FormationPresentationProfile.GetAnchor(BattleTeam.Enemy, 0).Y));
        }

        [Test]
        public void FormationLayers_AreUniqueAcrossBothTeamsAndAllSlots()
        {
            var orders = new HashSet<int>();
            foreach (BattleTeam team in new[] { BattleTeam.Player, BattleTeam.Enemy })
            {
                for (int slot = 0; slot < 5; slot++)
                {
                    FormationAnchor anchor = FormationPresentationProfile.GetAnchor(team, slot);
                    foreach (FormationRenderLayer layer in Enum.GetValues(typeof(FormationRenderLayer)))
                    {
                        int order = FormationPresentationProfile.GetSortingOrder(team, anchor.Y, layer);
                        Assert.That(orders.Add(order), Is.True, $"{team} slot {slot} {layer} collided at {order}.");
                        Assert.That(order, Is.LessThan(150), $"{team} slot {slot} {layer} overlaps combat effects.");
                    }
                }
            }

            Assert.That(orders.Count, Is.EqualTo(30));
        }

        [Test]
        public void IncapacitatedAnchors_MoveOutwardIntoVisibleForegroundLane()
        {
            foreach (BattleTeam team in new[] { BattleTeam.Player, BattleTeam.Enemy })
            {
                for (int slot = 0; slot < 5; slot++)
                {
                    FormationAnchor home = FormationPresentationProfile.GetAnchor(team, slot);
                    FormationAnchor defeated =
                        FormationPresentationProfile.GetIncapacitatedAnchor(team, slot);
                    Assert.That(Math.Abs(defeated.X), Is.GreaterThan(Math.Abs(home.X)));
                    Assert.That(defeated.Y, Is.LessThan(home.Y));
                    Assert.That(Math.Sign(defeated.X), Is.EqualTo(Math.Sign(home.X)));
                    Assert.That(defeated.ShadowWidth, Is.GreaterThan(home.ShadowWidth));
                }
            }
        }

        [Test]
        public void IncapacitatedAnchors_UseDistinctStaggeredLanes()
        {
            FormationAnchor[] anchors = Enumerable.Range(0, 5)
                .Select(slot =>
                    FormationPresentationProfile.GetIncapacitatedAnchor(BattleTeam.Player, slot))
                .ToArray();

            Assert.That(anchors.Select(anchor => anchor.X).Distinct().Count(), Is.EqualTo(5));
            Assert.That(anchors.Select(anchor => anchor.Y).Distinct().Count(), Is.GreaterThanOrEqualTo(3));
            Assert.That(
                Math.Abs(anchors[4].X - FormationPresentationProfile.GetAnchor(BattleTeam.Player, 4).X),
                Is.GreaterThan(
                    Math.Abs(anchors[0].X - FormationPresentationProfile.GetAnchor(BattleTeam.Player, 0).X)));
        }

        [Test]
        public void IncapacitatedMotion_IsLongEnoughForReadablePoseChanges()
        {
            foreach (string className in new[]
                     {
                         "knight", "cavalry", "trickster", "flier", "archer", "mage", "cleric"
                     })
            {
                float transition =
                    FormationPresentationProfile.GetIncapacitatedTransitionDuration(className);
                float settle =
                    FormationPresentationProfile.GetIncapacitatedSettleDuration(className);
                Assert.That(transition, Is.InRange(0.26f, 0.30f), className);
                Assert.That(settle, Is.InRange(0.40f, 0.43f), className);
                Assert.That(settle, Is.GreaterThan(transition), className);
            }
        }

        [Test]
        public void SafeBattleCamera_AddsSpaceForNarrowAndLowResolutionViews()
        {
            float fullHd = FormationPresentationProfile.GetSafeBattleCameraSize(1920, 1080);
            float hd = FormationPresentationProfile.GetSafeBattleCameraSize(1280, 720);
            float narrow = FormationPresentationProfile.GetSafeBattleCameraSize(1024, 768);
            float low = FormationPresentationProfile.GetSafeBattleCameraSize(960, 540);

            Assert.That(fullHd, Is.EqualTo(5.4f).Within(0.001f));
            Assert.That(hd, Is.EqualTo(5.4f).Within(0.001f));
            Assert.That(narrow, Is.GreaterThan(fullHd));
            Assert.That(low, Is.GreaterThan(fullHd));
            Assert.That(
                () => FormationPresentationProfile.GetSafeBattleCameraSize(0, 720),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void IncapacitatedLayers_StayVisibleBelowCombatEffects()
        {
            var orders = new HashSet<int>();
            foreach (BattleTeam team in new[] { BattleTeam.Player, BattleTeam.Enemy })
            {
                foreach (FormationRenderLayer layer in Enum.GetValues(typeof(FormationRenderLayer)))
                {
                    int order =
                        FormationPresentationProfile.GetIncapacitatedSortingOrder(team, layer);
                    Assert.That(orders.Add(order), Is.True);
                    Assert.That(order, Is.LessThan(150));
                }
            }

            Assert.That(
                FormationPresentationProfile.GetIncapacitatedSortingOrder(
                    BattleTeam.Player,
                    FormationRenderLayer.Body),
                Is.GreaterThan(
                    FormationPresentationProfile.GetSortingOrder(
                        BattleTeam.Player,
                        FormationPresentationProfile.GetAnchor(BattleTeam.Player, 1).Y,
                        FormationRenderLayer.Body)));
        }

        [Test]
        public void FacingContract_UsesTeamDirectionAndSourceOrientation()
        {
            Assert.That(FormationPresentationProfile.GetFlipX(BattleTeam.Player, "hero_attack"), Is.True);
            Assert.That(FormationPresentationProfile.GetFlipX(BattleTeam.Enemy, "e_mage_attack"), Is.False);

            Assert.That(FormationPresentationProfile.GetFlipX(BattleTeam.Player, "e_archer_attack"), Is.False);
            Assert.That(FormationPresentationProfile.GetFlipX(BattleTeam.Enemy, "e_archer_attack"), Is.True);
            Assert.That(FormationPresentationProfile.GetFlipX(BattleTeam.Enemy, "e_cavalry_attack"), Is.True);
            Assert.That(FormationPresentationProfile.GetFlipX(BattleTeam.Enemy, "e_flier_attack"), Is.True);
        }

        [Test]
        public void PoseNormalization_UsesEachPoseOwnVisibleHeight()
        {
            const float targetHeight = 3.72f;
            const int textureHeight = 1024;
            const float pixelsPerUnit = 100f;

            float attack = FormationPresentationProfile.GetNormalizedPoseScale(
                "hero_attack", targetHeight, textureHeight, pixelsPerUnit);
            float victory = FormationPresentationProfile.GetNormalizedPoseScale(
                "hero_victory", targetHeight, textureHeight, pixelsPerUnit);
            float defeat = FormationPresentationProfile.GetNormalizedPoseScale(
                "hero_defeat", targetHeight, textureHeight, pixelsPerUnit);

            Assert.That(attack, Is.Not.EqualTo(victory).Within(0.0001f));
            Assert.That(victory, Is.Not.EqualTo(defeat).Within(0.0001f));
            Assert.That(
                attack * 0.709961f * textureHeight / pixelsPerUnit,
                Is.EqualTo(targetHeight).Within(0.001f));
            Assert.That(
                victory * 0.937500f * textureHeight / pixelsPerUnit,
                Is.EqualTo(targetHeight).Within(0.001f));
            Assert.That(
                defeat * 0.536133f * textureHeight / pixelsPerUnit,
                Is.EqualTo(targetHeight).Within(0.001f));
        }

        [Test]
        public void MotionProfiles_AreClassSpecificAndWithinPlayableRanges()
        {
            string[] classIds = { "knight", "cavalry", "trickster", "flier", "archer", "mage", "cleric" };
            BattleMotionProfile[] profiles = classIds
                .Select(FormationPresentationProfile.GetMotionProfile)
                .ToArray();

            Assert.That(profiles.Select(profile => profile.StopDistance).Distinct().Count(), Is.GreaterThan(3));
            foreach (BattleMotionProfile profile in profiles)
            {
                Assert.That(profile.WindupDistance, Is.InRange(0.18f, 0.65f));
                Assert.That(profile.StopDistance, Is.InRange(0.8f, 1.6f));
                Assert.That(profile.ApproachDuration, Is.InRange(0.12f, 0.21f));
                Assert.That(profile.TravelArc, Is.InRange(0.08f, 0.3f));
                Assert.That(profile.ReturnDuration, Is.InRange(0.23f, 0.35f));
                Assert.That(profile.HitRecoil, Is.InRange(0.25f, 0.45f));
            }

            Assert.That(
                () => FormationPresentationProfile.GetMotionProfile("unknown"),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void BondTechnique_IsRestrictedToHeroAndPartner()
        {
            Assert.That(FormationPresentationProfile.SupportsBondTechnique("hero"), Is.True);
            Assert.That(FormationPresentationProfile.SupportsBondTechnique("partner"), Is.True);
            Assert.That(FormationPresentationProfile.SupportsBondTechnique("azuki"), Is.False);
            Assert.That(FormationPresentationProfile.IsBondTechniquePair("hero", "partner"), Is.True);
            Assert.That(FormationPresentationProfile.IsBondTechniquePair("partner", "hero"), Is.True);
            Assert.That(FormationPresentationProfile.IsBondTechniquePair("hero", "azuki"), Is.False);
        }
    }
}
