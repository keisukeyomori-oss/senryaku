using System.Collections.Generic;
using UnityEngine;

namespace BirthdayTactics.Presentation
{
    public enum BoneRigPose2D
    {
        Idle,
        Entrance,
        Run,
        Windup,
        Cast,
        Strike,
        Return,
        Hit,
        Guard,
        Victory,
        Defeat
    }

    public enum BoneAttachment2D
    {
        Hips,
        Torso,
        LeftForearm,
        RightForearm
    }

    public sealed class BoneRigLayout2D
    {
        public float ReferenceHeight;
        public Vector2 Hips;
        public Vector2 Head;
        public Vector2 UpperArmLeft;
        public Vector2 UpperArmRight;
        public Vector2 ForearmLeft;
        public Vector2 ForearmRight;
        public Vector2 ThighLeft;
        public Vector2 ThighRight;
        public Vector2 ShinLeft;
        public Vector2 ShinRight;
        public Vector2 Cape;
        public Vector2 Weapon;
        public Vector2 TorsoPivot = new Vector2(0.5f, 0.5f);
        public Vector2 HeadPivot = new Vector2(0.5f, 0.05f);
        public Vector2 UpperArmLeftPivot = new Vector2(0.5f, 0.91f);
        public Vector2 UpperArmRightPivot = new Vector2(0.5f, 0.91f);
        public Vector2 WeaponPivot = new Vector2(0.5f, 0.89f);
        public Vector2 CapePivot = new Vector2(0.5f, 0.94f);
        public BoneAttachment2D WeaponAttachment = BoneAttachment2D.LeftForearm;
        public BoneAttachment2D CapeAttachment = BoneAttachment2D.Hips;
    }

    public readonly struct BoneRigPoseSample2D
    {
        public BoneRigPoseSample2D(
            float rootX,
            float rootY,
            float rootRotation,
            float torsoRotation,
            float headRotation,
            float upperArmLeftRotation,
            float forearmLeftRotation,
            float upperArmRightRotation,
            float forearmRightRotation,
            float thighLeftRotation,
            float shinLeftRotation,
            float thighRightRotation,
            float shinRightRotation,
            float capeRotation,
            float weaponRotation)
        {
            RootX = rootX;
            RootY = rootY;
            RootRotation = rootRotation;
            TorsoRotation = torsoRotation;
            HeadRotation = headRotation;
            UpperArmLeftRotation = upperArmLeftRotation;
            ForearmLeftRotation = forearmLeftRotation;
            UpperArmRightRotation = upperArmRightRotation;
            ForearmRightRotation = forearmRightRotation;
            ThighLeftRotation = thighLeftRotation;
            ShinLeftRotation = shinLeftRotation;
            ThighRightRotation = thighRightRotation;
            ShinRightRotation = shinRightRotation;
            CapeRotation = capeRotation;
            WeaponRotation = weaponRotation;
        }

        public float RootX { get; }
        public float RootY { get; }
        public float RootRotation { get; }
        public float TorsoRotation { get; }
        public float HeadRotation { get; }
        public float UpperArmLeftRotation { get; }
        public float ForearmLeftRotation { get; }
        public float UpperArmRightRotation { get; }
        public float ForearmRightRotation { get; }
        public float ThighLeftRotation { get; }
        public float ShinLeftRotation { get; }
        public float ThighRightRotation { get; }
        public float ShinRightRotation { get; }
        public float CapeRotation { get; }
        public float WeaponRotation { get; }
    }

    public static class BoneRig2DProfile
    {
        public const int PartCount = 12;
        private static readonly string[] UnitIds =
        {
            "hero", "azuki", "partner", "memory1", "memory2", "e_knight", "e_archer"
        };

        private static readonly Dictionary<string, BoneRigLayout2D> Layouts =
            new Dictionary<string, BoneRigLayout2D>
            {
                ["hero"] = Layout(
                    10.95f, 4.52f, 2.55f, 1.04f, 2.30f, 1.58f,
                    0.48f, 1.65f, new Vector2(0f, 2.35f), new Vector2(0f, -1.02f),
                    new Vector2(0.5f, 0.89f), BoneAttachment2D.LeftForearm,
                    BoneAttachment2D.Hips, new Vector2(0.72f, 0.91f),
                    new Vector2(0.25f, 0.91f)),
                ["azuki"] = Layout(
                    9.75f, 3.50f, 2.00f, 0.78f, 1.80f, 1.55f,
                    0.43f, 1.65f, new Vector2(0f, 1.72f), new Vector2(-0.72f, -0.12f),
                    new Vector2(0.90f, 0.10f), BoneAttachment2D.Hips,
                    BoneAttachment2D.Torso),
                ["partner"] = Layout(
                    8.95f, 4.20f, 1.80f, 1.18f, 1.62f, 1.62f,
                    0.42f, 1.58f, new Vector2(0f, 1.58f), new Vector2(0f, -0.95f),
                    new Vector2(0.5f, 0.70f), BoneAttachment2D.LeftForearm,
                    BoneAttachment2D.Hips),
                ["memory1"] = Layout(
                    9.30f, 3.35f, 1.65f, 0.86f, 1.48f, 1.18f,
                    0.39f, 1.08f, new Vector2(0f, 1.42f), new Vector2(0f, -0.72f),
                    new Vector2(0.5f, 0.50f), BoneAttachment2D.LeftForearm,
                    BoneAttachment2D.Hips),
                ["memory2"] = Layout(
                    8.35f, 3.40f, 2.08f, 1.08f, 1.82f, 1.78f,
                    0.38f, 1.48f, new Vector2(0f, 1.78f), new Vector2(0f, -0.78f),
                    new Vector2(0.5f, 0.66f), BoneAttachment2D.RightForearm,
                    BoneAttachment2D.Hips),
                ["e_knight"] = Layout(
                    7.95f, 3.90f, 2.02f, 1.22f, 1.62f, 1.75f,
                    0.48f, 1.34f, new Vector2(0f, -0.92f), new Vector2(0f, -0.98f),
                    new Vector2(0.5f, 0.93f), BoneAttachment2D.LeftForearm,
                    BoneAttachment2D.RightForearm,
                    capePivot: new Vector2(0.5f, 0.66f)),
                ["e_archer"] = Layout(
                    8.60f, 3.65f, 1.72f, 1.02f, 1.52f, 1.62f,
                    0.44f, 1.38f, new Vector2(0f, 1.52f), new Vector2(0f, -0.76f),
                    new Vector2(0.5f, 0.50f), BoneAttachment2D.RightForearm,
                    BoneAttachment2D.Torso)
            };

        public static IReadOnlyList<string> SupportedUnitIds => UnitIds;

        public static bool Supports(string sourceUnitId)
        {
            return sourceUnitId != null && Layouts.ContainsKey(sourceUnitId);
        }

        /// <summary>
        /// 切り抜きパーツのボーンリグを戦闘で使うかどうか。
        ///
        /// 【判定済み: 使わない】2026-08-06 に hero だけ有効化して実機確認したところ、
        /// 自動分割したパーツが関節でつながらず、人型として破綻していた。
        /// 描き起こしたポーズ絵のほうが明確に品質が高いため、本番では使わない。
        ///
        /// リグ自体（BoneRig2DProfile / BoneRig2DView とそのテスト）は残してある。
        /// 将来パーツを手作業で整えるなら、ここを Supports(sourceUnitId) に変えれば復活する。
        /// その際は VerticalSliceController 側で本体スプライトを隠す処理
        /// （boneRig != null のとき renderer.enabled = false）も必要になる。
        /// </summary>
        public static bool ShouldUseInBattle(string sourceUnitId)
        {
            return false;
        }

        public static BoneRigLayout2D GetLayout(string sourceUnitId)
        {
            if (!Supports(sourceUnitId))
                throw new KeyNotFoundException($"No 2D bone layout for {sourceUnitId}");
            return Layouts[sourceUnitId];
        }

        public static BoneRigPoseSample2D Sample(
            BoneRigPose2D pose,
            float normalizedTime,
            float phase = 0f)
        {
            float t = Mathf.Clamp01(normalizedTime);
            float wave = Mathf.Sin((t + phase) * Mathf.PI * 2f);
            switch (pose)
            {
                case BoneRigPose2D.Entrance:
                    float entranceStride = Mathf.Sin(t * Mathf.PI * 5f) * (1f - t);
                    float entranceLanding = Mathf.Sin(t * Mathf.PI);
                    return new BoneRigPoseSample2D(
                        Mathf.Lerp(-0.28f, 0f, Smooth(t)),
                        entranceLanding * 0.16f,
                        Mathf.Lerp(7f, 0f, Smooth(t)),
                        Mathf.Lerp(-5f, 0f, Smooth(t)) + entranceStride * 2f,
                        Mathf.Lerp(4f, 0f, Smooth(t)) - entranceStride * 1.5f,
                        -8f - entranceStride * 15f, 9f + entranceStride * 8f,
                        10f + entranceStride * 15f, -8f - entranceStride * 8f,
                        -5f + entranceStride * 18f, 8f + Mathf.Max(0f, -entranceStride) * 12f,
                        6f - entranceStride * 18f, -8f + Mathf.Max(0f, entranceStride) * 12f,
                        Mathf.Lerp(9f, 0f, t),
                        Mathf.Lerp(-12f, 0f, t));
                case BoneRigPose2D.Run:
                    return new BoneRigPoseSample2D(
                        0f, Mathf.Abs(wave) * 0.035f,
                        wave * 1.8f, -wave * 3.2f, wave * 2.1f,
                        -wave * 22f, wave * 13f,
                        wave * 22f, -wave * 13f,
                        wave * 24f, Mathf.Max(0f, -wave) * 16f,
                        -wave * 24f, Mathf.Max(0f, wave) * 16f,
                        -wave * 15f, -wave * 20f);
                case BoneRigPose2D.Windup:
                    return new BoneRigPoseSample2D(
                        -0.08f, -0.04f, 4.5f, -5f, 2.5f,
                        24f, -34f, -18f, 20f,
                        -8f, 14f, 5f, -8f, 10f, -48f);
                case BoneRigPose2D.Cast:
                    float castPulse = Mathf.Sin(t * Mathf.PI);
                    float castWave = Mathf.Sin(t * Mathf.PI * 4f);
                    return new BoneRigPoseSample2D(
                        Mathf.Lerp(-0.08f, 0.06f, Smooth(t)),
                        castPulse * 0.07f,
                        Mathf.Lerp(4f, -3f, Smooth(t)),
                        -6f + castWave * 1.8f,
                        3f - castPulse * 5f,
                        Mathf.Lerp(28f, -44f, Smooth(t)),
                        Mathf.Lerp(-36f, -12f, Smooth(t)),
                        Mathf.Lerp(-24f, 42f, Smooth(t)),
                        Mathf.Lerp(28f, 10f, Smooth(t)),
                        -10f + castPulse * 8f, 16f - castPulse * 5f,
                        8f - castPulse * 8f, -13f + castPulse * 5f,
                        12f + castWave * 4f,
                        Mathf.Lerp(-52f, 58f, Smooth(t)));
                case BoneRigPose2D.Strike:
                    return new BoneRigPoseSample2D(
                        0.20f, 0.07f, -6f, 8f, -4f,
                        -58f, -24f, 38f, -12f,
                        16f, -20f, -13f, 18f, -14f, 72f);
                case BoneRigPose2D.Return:
                    return Lerp(
                        Sample(BoneRigPose2D.Strike, 1f, phase),
                        Sample(BoneRigPose2D.Idle, 0f, phase),
                        Smooth(t));
                case BoneRigPose2D.Hit:
                    return new BoneRigPoseSample2D(
                        -0.16f, -0.08f, 8f, -10f, 7f,
                        16f, 18f, -13f, -16f,
                        -8f, 14f, 5f, -8f, 13f, -16f);
                case BoneRigPose2D.Guard:
                    return new BoneRigPoseSample2D(
                        -0.10f, -0.05f, 4f, -8f, 3f,
                        22f, -38f, -34f, 48f,
                        -12f, 22f, 9f, -16f, 8f, -24f);
                case BoneRigPose2D.Victory:
                    return new BoneRigPoseSample2D(
                        0f, Mathf.Abs(wave) * 0.05f, wave * 0.65f,
                        -2f + wave * 0.8f, -3f - wave * 0.45f,
                        -42f + wave * 8f, -30f - wave * 6f,
                        36f - wave * 7f, 24f + wave * 5f,
                        -4f + wave * 4f, 7f + Mathf.Max(0f, -wave) * 5f,
                        5f - wave * 4f, -8f + Mathf.Max(0f, wave) * 5f,
                        -5f + wave * 4f, -68f + wave * 7f);
                case BoneRigPose2D.Defeat:
                    return new BoneRigPoseSample2D(
                        -0.24f, -0.72f, 8f, -19f, 18f,
                        31f, 24f, -28f, -18f,
                        -31f, 52f, 27f, -47f, 18f, 32f);
                default:
                    return new BoneRigPoseSample2D(
                        0f, wave * 0.025f, wave * 0.22f,
                        wave * 0.75f, wave * -0.42f,
                        -3f + wave * 0.7f, 5f - wave * 0.45f,
                        4f - wave * 0.65f, -5f + wave * 0.4f,
                        wave * 0.25f, wave * -0.2f,
                        wave * -0.25f, wave * 0.2f,
                        wave * -1.15f, wave * 0.42f);
            }
        }

        public static BoneRigPoseSample2D Lerp(
            BoneRigPoseSample2D from,
            BoneRigPoseSample2D to,
            float t)
        {
            t = Mathf.Clamp01(t);
            return new BoneRigPoseSample2D(
                Mathf.Lerp(from.RootX, to.RootX, t),
                Mathf.Lerp(from.RootY, to.RootY, t),
                Mathf.LerpAngle(from.RootRotation, to.RootRotation, t),
                Mathf.LerpAngle(from.TorsoRotation, to.TorsoRotation, t),
                Mathf.LerpAngle(from.HeadRotation, to.HeadRotation, t),
                Mathf.LerpAngle(from.UpperArmLeftRotation, to.UpperArmLeftRotation, t),
                Mathf.LerpAngle(from.ForearmLeftRotation, to.ForearmLeftRotation, t),
                Mathf.LerpAngle(from.UpperArmRightRotation, to.UpperArmRightRotation, t),
                Mathf.LerpAngle(from.ForearmRightRotation, to.ForearmRightRotation, t),
                Mathf.LerpAngle(from.ThighLeftRotation, to.ThighLeftRotation, t),
                Mathf.LerpAngle(from.ShinLeftRotation, to.ShinLeftRotation, t),
                Mathf.LerpAngle(from.ThighRightRotation, to.ThighRightRotation, t),
                Mathf.LerpAngle(from.ShinRightRotation, to.ShinRightRotation, t),
                Mathf.LerpAngle(from.CapeRotation, to.CapeRotation, t),
                Mathf.LerpAngle(from.WeaponRotation, to.WeaponRotation, t));
        }

        public static BoneRigPoseSample2D ComposeLocomotionAndUpperBody(
            BoneRigPoseSample2D locomotion,
            BoneRigPoseSample2D upperBody,
            float upperBodyWeight)
        {
            BoneRigPoseSample2D blend = Lerp(locomotion, upperBody, upperBodyWeight);
            return new BoneRigPoseSample2D(
                Mathf.Lerp(locomotion.RootX, upperBody.RootX, upperBodyWeight * 0.35f),
                locomotion.RootY + upperBody.RootY * upperBodyWeight * 0.45f,
                blend.RootRotation,
                blend.TorsoRotation,
                blend.HeadRotation,
                blend.UpperArmLeftRotation,
                blend.ForearmLeftRotation,
                blend.UpperArmRightRotation,
                blend.ForearmRightRotation,
                locomotion.ThighLeftRotation,
                locomotion.ShinLeftRotation,
                locomotion.ThighRightRotation,
                locomotion.ShinRightRotation,
                blend.CapeRotation,
                blend.WeaponRotation);
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static BoneRigLayout2D Layout(
            float referenceHeight,
            float hipsY,
            float headY,
            float shoulderX,
            float shoulderY,
            float elbowY,
            float hipX,
            float kneeY,
            Vector2 cape,
            Vector2 weapon,
            Vector2 weaponPivot,
            BoneAttachment2D weaponAttachment,
            BoneAttachment2D capeAttachment,
            Vector2? upperArmLeftPivot = null,
            Vector2? upperArmRightPivot = null,
            Vector2? capePivot = null)
        {
            return new BoneRigLayout2D
            {
                ReferenceHeight = referenceHeight,
                Hips = new Vector2(0f, hipsY),
                Head = new Vector2(0f, headY),
                UpperArmLeft = new Vector2(-shoulderX, shoulderY),
                UpperArmRight = new Vector2(shoulderX, shoulderY),
                ForearmLeft = new Vector2(0f, -elbowY),
                ForearmRight = new Vector2(0f, -elbowY),
                ThighLeft = new Vector2(-hipX, -0.05f),
                ThighRight = new Vector2(hipX, -0.05f),
                ShinLeft = new Vector2(0f, -kneeY),
                ShinRight = new Vector2(0f, -kneeY),
                Cape = cape,
                Weapon = weapon,
                WeaponPivot = weaponPivot,
                WeaponAttachment = weaponAttachment,
                CapeAttachment = capeAttachment,
                CapePivot = capePivot ?? new Vector2(0.5f, 0.94f),
                UpperArmLeftPivot = upperArmLeftPivot ?? new Vector2(0.5f, 0.91f),
                UpperArmRightPivot = upperArmRightPivot ?? new Vector2(0.5f, 0.91f)
            };
        }
    }
}
