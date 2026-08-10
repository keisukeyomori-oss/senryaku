using System;
using System.Linq;

using BirthdayTactics.Core;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    public sealed class PixelAnimationProfileTests
    {
        [Test]
        public void CompleteRoster_UsesTheCrispKeyFramePipeline()
        {
            Assert.That(
                PixelAnimationProfile.SupportedUnitIds,
                Is.EqualTo(new[]
                {
                    "hero", "partner", "azuki", "memory1", "memory2", "memory3",
                    "c_lancer", "c_skywarden", "c_cleric", "c_guard", "c_archer", "c_mage",
                    "e_knight", "e_cavalry", "e_archer", "e_flier", "e_mage", "e_cleric", "e_boss"
                }));
            Assert.That(PixelAnimationProfile.SupportedUnitIds.Distinct().Count(), Is.EqualTo(19));
            Assert.That(PixelAnimationProfile.IsSupported("hero"), Is.True);
            Assert.That(PixelAnimationProfile.IsSupported("e_mage"), Is.True);
            Assert.That(PixelAnimationProfile.UseMorphMotion, Is.False);
        }

        [Test]
        public void FieldRowsAndHorizontalFlip_FollowTheAtlasContract()
        {
            Assert.That(PixelAnimationProfile.GetFieldFrameIndex(PixelFacing.Down, false, 10f), Is.EqualTo(0));
            Assert.That(PixelAnimationProfile.GetFieldFrameIndex(PixelFacing.Up, false, 10f), Is.EqualTo(4));
            Assert.That(PixelAnimationProfile.GetFieldFrameIndex(PixelFacing.Right, false, 10f), Is.EqualTo(8));
            Assert.That(PixelAnimationProfile.GetFieldFrameIndex(PixelFacing.Left, false, 10f), Is.EqualTo(8));
            Assert.That(PixelAnimationProfile.ShouldFlipField(PixelFacing.Left), Is.True);
            Assert.That(PixelAnimationProfile.ShouldFlipField(PixelFacing.Right), Is.False);
        }

        [Test]
        public void SourceRunningFrames_AdvanceAtTwelveFramesPerSecond()
        {
            int[] frames = Enumerable.Range(0, 4)
                .Select(index => PixelAnimationProfile.GetFieldFrameIndex(
                    PixelFacing.Down,
                    true,
                    index / PixelAnimationProfile.SourceFramesPerSecond))
                .ToArray();

            Assert.That(frames, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(
                PixelAnimationProfile.GetFieldFrameIndex(PixelFacing.Down, true, 4f / 12f),
                Is.EqualTo(0));
        }

        [Test]
        public void AzukiQuadruped_UsesEveryAuthoredDirectionalFrame()
        {
            Assert.That(PixelAnimationProfile.UsesQuadrupedAtlas("azuki"), Is.True);
            Assert.That(PixelAnimationProfile.UsesQuadrupedAtlas("hero"), Is.False);
            Assert.That(PixelAnimationProfile.GetQuadrupedFieldFrameCount(PixelFacing.Down), Is.EqualTo(5));
            Assert.That(PixelAnimationProfile.GetQuadrupedFieldFrameCount(PixelFacing.Up), Is.EqualTo(6));
            Assert.That(PixelAnimationProfile.GetQuadrupedFieldFrameCount(PixelFacing.Right), Is.EqualTo(5));
            Assert.That(
                PixelAnimationProfile.GetQuadrupedFieldFrameIndex(PixelFacing.Up, true, 5f / 12f),
                Is.EqualTo(11));
            Assert.That(
                PixelAnimationProfile.GetQuadrupedFieldFrameIndex(PixelFacing.Right, true, 4f / 12f),
                Is.EqualTo(16));
        }

        [Test]
        public void MotionFrames_AdvanceAtSixtyFramesPerSecond()
        {
            Assert.That(PixelAnimationProfile.FramesPerSecond, Is.EqualTo(60f));
            int[] frames = Enumerable.Range(0, 4)
                .Select(index => PixelAnimationProfile.GetMotionFieldFrameIndex(
                    PixelFacing.Down,
                    true,
                    index / PixelAnimationProfile.FramesPerSecond))
                .ToArray();
            Assert.That(frames, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(
                PixelAnimationProfile.GetMotionFieldFrameIndex(
                    PixelFacing.Down,
                    true,
                    PixelAnimationProfile.MotionRunFrames / PixelAnimationProfile.FramesPerSecond),
                Is.EqualTo(0));
            Assert.That(
                PixelAnimationProfile.GetMotionFieldFrameIndex(PixelFacing.Down, false, 0f),
                Is.EqualTo(60));
            Assert.That(
                PixelAnimationProfile.GetMotionFieldFrameIndex(PixelFacing.Up, false, 0f),
                Is.EqualTo(120));
            Assert.That(
                PixelAnimationProfile.GetMotionFieldFrameIndex(PixelFacing.Right, false, 0f),
                Is.EqualTo(180));
        }

        [Test]
        public void BattlePoses_UseTheFinalAtlasRow()
        {
            Assert.That(PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Idle), Is.EqualTo(12));
            Assert.That(PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Attack), Is.EqualTo(13));
            Assert.That(PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Hit), Is.EqualTo(14));
            Assert.That(PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Victory), Is.EqualTo(15));
            Assert.That(PixelAnimationProfile.GetBattleFrameIndex(PixelBattlePose.Defeat), Is.EqualTo(14));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PixelAnimationProfile.GetBattleFrameIndex((PixelBattlePose)99));
        }

        [Test]
        public void MotionBattleSequences_HaveStableAtlasAndOffsets()
        {
            Assert.That(PixelAnimationProfile.GetMotionBattleAtlasIndex(PixelBattlePose.Idle), Is.EqualTo(0));
            Assert.That(PixelAnimationProfile.GetMotionBattleAtlasIndex(PixelBattlePose.Attack), Is.EqualTo(0));
            Assert.That(PixelAnimationProfile.GetMotionBattleAtlasIndex(PixelBattlePose.Hit), Is.EqualTo(0));
            Assert.That(PixelAnimationProfile.GetMotionBattleAtlasIndex(PixelBattlePose.Victory), Is.EqualTo(1));
            Assert.That(PixelAnimationProfile.GetMotionBattleAtlasIndex(PixelBattlePose.Defeat), Is.EqualTo(1));
            Assert.That(PixelAnimationProfile.GetMotionBattleFrameIndex(PixelBattlePose.Attack, 0f), Is.EqualTo(60));
            Assert.That(PixelAnimationProfile.GetMotionBattleFrameIndex(PixelBattlePose.Attack, 1f), Is.EqualTo(119));
            Assert.That(PixelAnimationProfile.GetMotionBattleFrameIndex(PixelBattlePose.Defeat, 0f), Is.EqualTo(60));
            Assert.That(PixelAnimationProfile.GetMotionBattleFrameIndex(PixelBattlePose.Defeat, 1f), Is.EqualTo(119));
        }

        [Test]
        public void PilotAtlases_ArePresentAtTheFixedFourByFourResolution()
        {
            foreach (string unitId in PixelAnimationProfile.SupportedUnitIds)
            {
                Texture2D atlas = Resources.Load<Texture2D>(
                    $"Art/Pixel/Characters/{unitId}_atlas");
                Assert.That(atlas, Is.Not.Null, unitId);
                Assert.That(
                    atlas.width,
                    Is.EqualTo(PixelAnimationProfile.Columns * PixelAnimationProfile.CellPixels),
                    unitId);
                Assert.That(
                    atlas.height,
                    Is.EqualTo(PixelAnimationProfile.Rows * PixelAnimationProfile.CellPixels),
                    unitId);
                Texture2D defeat = Resources.Load<Texture2D>(
                    $"Art/Pixel/Characters/Defeat/{unitId}_defeat");
                Assert.That(defeat, Is.Not.Null, unitId + " defeat");
                Assert.That(defeat.width, Is.EqualTo(PixelAnimationProfile.CellPixels), unitId);
                Assert.That(defeat.height, Is.EqualTo(PixelAnimationProfile.CellPixels), unitId);
            }

            Texture2D azukiQuadruped = Resources.Load<Texture2D>(
                "Art/Pixel/Characters/azuki_quadruped");
            Assert.That(azukiQuadruped, Is.Not.Null);
            Assert.That(
                azukiQuadruped.width,
                Is.EqualTo(PixelAnimationProfile.QuadrupedColumns * PixelAnimationProfile.CellPixels));
            Assert.That(
                azukiQuadruped.height,
                Is.EqualTo(PixelAnimationProfile.QuadrupedRows * PixelAnimationProfile.CellPixels));
        }

        [Test]
        public void LegacyMorphAtlases_AreNotRequiredByTheProductionRoster()
        {
            Assert.That(PixelAnimationProfile.UseMorphMotion, Is.False);
        }

        private static void AssertMotionAtlas(string unitId, string suffix, int frameCount)
        {
            Texture2D atlas = Resources.Load<Texture2D>(
                $"Art/Pixel/Characters/Motion60/{unitId}_{suffix}");
            Assert.That(atlas, Is.Not.Null, $"{unitId}_{suffix}");
            Assert.That(
                atlas.width,
                Is.EqualTo(PixelAnimationProfile.MotionColumns * PixelAnimationProfile.CellPixels),
                $"{unitId}_{suffix}");
            Assert.That(
                atlas.height,
                Is.EqualTo(
                    PixelAnimationProfile.GetMotionRows(frameCount) * PixelAnimationProfile.CellPixels),
                $"{unitId}_{suffix}");
        }
    }
}
