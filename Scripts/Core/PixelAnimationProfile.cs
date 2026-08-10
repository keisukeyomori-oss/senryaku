using System;
using System.Collections.Generic;

namespace BirthdayTactics.Core
{
    public enum PixelFacing
    {
        Down,
        Up,
        Right,
        Left
    }

    public enum PixelBattlePose
    {
        Idle,
        Attack,
        Hit,
        Victory,
        Defeat
    }

    /// <summary>
    /// 4x4ドットアトラスの配置契約。探索は上3行、戦闘は最下行を使う。
    /// ドットのコマ送りとTransform補間を分離し、移動自体は60fpsを維持する。
    /// </summary>
    public static class PixelAnimationProfile
    {
        public const int Columns = 4;
        public const int Rows = 4;
        public const int CellPixels = 128;
        public const float SourceFramesPerSecond = 12f;
        public const float FramesPerSecond = 60f;
        public const bool UseMorphMotion = false;
        public const string QuadrupedUnitId = "azuki";
        public const int QuadrupedColumns = 6;
        public const int QuadrupedRows = 4;
        public const int MotionColumns = 15;
        public const int MotionRunFrames = 20;
        public const int MotionPoseFrames = 60;
        public const int MotionFieldFrames = 240;
        public const int MotionBattleAFrames = 180;
        public const int MotionBattleBFrames = 120;

        private static readonly string[] UnitIds =
        {
            "hero",
            "partner",
            "azuki",
            "memory1",
            "memory2",
            "memory3",
            "c_lancer",
            "c_skywarden",
            "c_cleric",
            "c_guard",
            "c_archer",
            "c_mage",
            "e_knight",
            "e_cavalry",
            "e_archer",
            "e_flier",
            "e_mage",
            "e_cleric",
            "e_boss"
        };

        public static IReadOnlyList<string> SupportedUnitIds => UnitIds;

        public static bool IsSupported(string sourceUnitId) =>
            Array.IndexOf(UnitIds, sourceUnitId) >= 0;

        public static bool UsesQuadrupedAtlas(string sourceUnitId) =>
            string.Equals(sourceUnitId, QuadrupedUnitId, StringComparison.Ordinal);

        public static int GetQuadrupedFieldFrameCount(PixelFacing facing)
        {
            switch (facing)
            {
                case PixelFacing.Down: return 5;
                case PixelFacing.Up: return 6;
                case PixelFacing.Right:
                case PixelFacing.Left: return 5;
                default: throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
            }
        }

        public static int GetQuadrupedFieldFrameIndex(
            PixelFacing facing,
            bool moving,
            float elapsedSeconds)
        {
            int row;
            switch (facing)
            {
                case PixelFacing.Down: row = 0; break;
                case PixelFacing.Up: row = 1; break;
                case PixelFacing.Right:
                case PixelFacing.Left: row = 2; break;
                default: throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
            }

            int count = GetQuadrupedFieldFrameCount(facing);
            int frame = moving
                ? Math.Max(0, (int)Math.Floor(elapsedSeconds * SourceFramesPerSecond)) % count
                : 0;
            return row * QuadrupedColumns + frame;
        }

        public static int GetFieldFrameIndex(
            PixelFacing facing,
            bool moving,
            float elapsedSeconds)
        {
            int row;
            switch (facing)
            {
                case PixelFacing.Down: row = 0; break;
                case PixelFacing.Up: row = 1; break;
                case PixelFacing.Left:
                case PixelFacing.Right: row = 2; break;
                default: throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
            }

            int frame = moving
                ? Math.Max(0, (int)Math.Floor(elapsedSeconds * SourceFramesPerSecond)) % Columns
                : 0;
            return row * Columns + frame;
        }

        public static int GetMotionFieldFrameIndex(
            PixelFacing facing,
            bool moving,
            float elapsedSeconds)
        {
            int direction;
            switch (facing)
            {
                case PixelFacing.Down: direction = 0; break;
                case PixelFacing.Up: direction = 1; break;
                case PixelFacing.Left:
                case PixelFacing.Right: direction = 2; break;
                default: throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
            }

            int sequenceLength = moving ? MotionRunFrames : MotionPoseFrames;
            int sequenceStart = moving
                ? direction * MotionRunFrames
                : 3 * MotionRunFrames + direction * MotionPoseFrames;
            int frame = Math.Max(0, (int)Math.Floor(elapsedSeconds * FramesPerSecond)) %
                        sequenceLength;
            return sequenceStart + frame;
        }

        public static bool ShouldFlipField(PixelFacing facing) =>
            facing == PixelFacing.Left;

        public static int GetBattleFrameIndex(PixelBattlePose pose)
        {
            switch (pose)
            {
                case PixelBattlePose.Idle: return 12;
                case PixelBattlePose.Attack: return 13;
                case PixelBattlePose.Hit: return 14;
                case PixelBattlePose.Victory: return 15;
                // 試作アトラスでは被弾フレームから倒れ込みを補間する。
                case PixelBattlePose.Defeat: return 14;
                default: throw new ArgumentOutOfRangeException(nameof(pose), pose, null);
            }
        }

        public static int GetMotionBattleAtlasIndex(PixelBattlePose pose)
        {
            switch (pose)
            {
                case PixelBattlePose.Idle:
                case PixelBattlePose.Attack:
                case PixelBattlePose.Hit:
                    return 0;
                case PixelBattlePose.Victory:
                case PixelBattlePose.Defeat:
                    return 1;
                default: throw new ArgumentOutOfRangeException(nameof(pose), pose, null);
            }
        }

        public static int GetMotionBattleSequenceStart(PixelBattlePose pose)
        {
            switch (pose)
            {
                case PixelBattlePose.Idle: return 0;
                case PixelBattlePose.Attack: return MotionPoseFrames;
                case PixelBattlePose.Hit: return MotionPoseFrames * 2;
                case PixelBattlePose.Victory: return 0;
                case PixelBattlePose.Defeat: return MotionPoseFrames;
                default: throw new ArgumentOutOfRangeException(nameof(pose), pose, null);
            }
        }

        public static int GetMotionBattleFrameIndex(PixelBattlePose pose, float normalizedTime)
        {
            int local = Math.Min(
                MotionPoseFrames - 1,
                Math.Max(0, (int)Math.Floor(normalizedTime * MotionPoseFrames)));
            return GetMotionBattleSequenceStart(pose) + local;
        }

        public static int GetMotionRows(int frameCount) =>
            (frameCount + MotionColumns - 1) / MotionColumns;
    }
}
