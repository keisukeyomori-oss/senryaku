using System;
using System.Collections.Generic;

namespace BirthdayTactics.Core
{
    public enum BattlePose
    {
        Idle,
        Attack,
        Hit,
        Victory,
        Incapacitated
    }

    public enum FormationRenderLayer
    {
        Shadow = -1,
        Body = 0,
        Blend = 1
    }

    public enum FormationRow
    {
        Front,
        Rear
    }

    public readonly struct BattleSpriteMetrics
    {
        public float PivotX { get; }
        public float PivotY { get; }
        public float VisibleHeight { get; }

        public BattleSpriteMetrics(float pivotX, float pivotY, float visibleHeight)
        {
            PivotX = pivotX;
            PivotY = pivotY;
            VisibleHeight = visibleHeight;
        }
    }

    public readonly struct BattleMotionProfile
    {
        public float WindupDistance { get; }
        public float StopDistance { get; }
        public float ApproachDuration { get; }
        public float TravelArc { get; }
        public float ImpactRecoil { get; }
        public float FollowThrough { get; }
        public float ReturnDuration { get; }
        public float Squash { get; }
        public float Stretch { get; }
        public float HitRecoil { get; }

        public BattleMotionProfile(
            float windupDistance,
            float stopDistance,
            float approachDuration,
            float travelArc,
            float impactRecoil,
            float followThrough,
            float returnDuration,
            float squash,
            float stretch,
            float hitRecoil)
        {
            WindupDistance = windupDistance;
            StopDistance = stopDistance;
            ApproachDuration = approachDuration;
            TravelArc = travelArc;
            ImpactRecoil = impactRecoil;
            FollowThrough = followThrough;
            ReturnDuration = returnDuration;
            Squash = squash;
            Stretch = stretch;
            HitRecoil = hitRecoil;
        }
    }

    public readonly struct FormationAnchor
    {
        public float X { get; }
        public float Y { get; }
        public float Height { get; }
        public float ShadowWidth { get; }

        public FormationAnchor(float x, float y, float height, float shadowWidth)
        {
            X = x;
            Y = y;
            Height = height;
            ShadowWidth = shadowWidth;
        }
    }

    /// <summary>
    /// Shared presentation contract for formation placement and battle pose assets.
    /// Keeping this deterministic lets EditMode tests validate the full 18-unit roster
    /// without depending on scene objects.
    /// </summary>
    public static class FormationPresentationProfile
    {
        /// <summary>
        /// SpriteMetrics を測るときに「見えている」とみなすアルファの下限。
        ///
        /// 0 にしてはならない。目視できないアルファ残渣まで可視境界に含めてしまい、
        /// メトリクスがプレイヤーの見た目と乖離する。実例として c_guard.png は
        /// キャンバス下端に alpha 1〜8 の帯が 74px あり、閾値0で測ると足元が
        /// キャンバス最下端だと記録される。その結果、可視キャラクターは地面から
        /// 約0.27ワールドユニット浮き、身長も約7.9%小さく描画されていた。
        ///
        /// この値は以下の3か所で共有する。変更するときは必ず全部を合わせること。
        ///   - Tools/generate_sprite_metrics.py の ALPHA_THRESHOLD
        ///   - Tools/audit_sprite_facing.py の ALPHA_THRESHOLD
        ///   - SpriteMetricsIntegrityTests / ContentCatalogTests の可視判定
        /// </summary>
        public const byte VisibleAlphaThreshold = 8;

        private static readonly string[] UnitIds =
        {
            "hero",
            "partner",
            "azuki",
            "memory1",
            "memory2",
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

        // The two sides deliberately do not mirror each other.  The player party
        // occupies the near, lower-right plane; enemies occupy the far, upper-left
        // plane.  Slots 0-2 are the front row and 3-5 the rear row.
        private static readonly FormationAnchor[] PlayerAnchors =
        {
            new FormationAnchor( 1.28f, -1.48f, 2.16f, 0.82f),
            new FormationAnchor( 2.16f, -1.86f, 2.10f, 0.79f),
            new FormationAnchor( 3.04f, -2.24f, 2.04f, 0.76f),
            new FormationAnchor( 2.30f, -0.35f, 1.92f, 0.72f),
            new FormationAnchor( 3.18f, -0.73f, 1.86f, 0.69f),
            new FormationAnchor( 4.06f, -1.11f, 1.80f, 0.66f),
            new FormationAnchor( 3.92f, -2.62f, 1.74f, 0.63f),
            new FormationAnchor( 4.94f, -0.05f, 1.68f, 0.60f)
        };

        private static readonly FormationAnchor[] EnemyAnchors =
        {
            new FormationAnchor(-1.22f,  0.42f, 1.94f, 0.74f),
            new FormationAnchor(-2.04f,  0.80f, 1.88f, 0.71f),
            new FormationAnchor(-2.86f,  1.18f, 1.82f, 0.68f),
            new FormationAnchor(-2.22f,  1.56f, 1.70f, 0.64f),
            new FormationAnchor(-3.04f,  1.94f, 1.64f, 0.61f),
            new FormationAnchor(-3.86f,  2.32f, 1.58f, 0.58f),
            new FormationAnchor(-3.68f,  0.04f, 1.54f, 0.56f),
            new FormationAnchor(-4.72f,  2.70f, 1.48f, 0.54f)
        };

        /// <summary>
        /// 既定の作画規約は「右向き（味方基準）」で、敵チームは flipX で反転する。
        /// ここに挙げた素材だけは元から左向きに描かれているため、反転の向きが逆になる。
        /// 新しいポーズを追加したら Tools/audit_sprite_facing.py で実表示を確認すること。
        /// </summary>
        private static readonly string[] LeftFacingSourceAssets =
        {
            "e_cavalry_attack",
            "e_archer_attack",
            "e_flier_attack",
            "e_cleric_attack",
            "e_boss_attack"
        };

        private static readonly Dictionary<string, BattleSpriteMetrics> SpriteMetrics =
            new Dictionary<string, BattleSpriteMetrics>(StringComparer.Ordinal)
            {
                { "hero", new BattleSpriteMetrics(0.506579f, 0.013672f, 0.969727f) },
                { "hero_attack", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.709961f) },
                { "hero_hit", new BattleSpriteMetrics(0.555423f, 0.180223f, 0.669856f) },
                { "hero_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.937500f) },
                { "hero_defeat", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.536133f) },
                { "partner", new BattleSpriteMetrics(0.465049f, 0.101562f, 0.819336f) },
                { "partner_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.963867f) },
                { "partner_hit", new BattleSpriteMetrics(0.462919f, 0.111643f, 0.769537f) },
                { "partner_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.937500f) },
                { "partner_defeat", new BattleSpriteMetrics(0.500000f, 0.016602f, 0.598633f) },
                { "azuki", new BattleSpriteMetrics(0.535556f, 0.044922f, 0.936523f) },
                { "azuki_attack", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.613281f) },
                { "azuki_hit", new BattleSpriteMetrics(0.464514f, 0.157895f, 0.671451f) },
                { "azuki_victory", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.937500f) },
                { "azuki_defeat", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.737305f) },
                { "memory1", new BattleSpriteMetrics(0.514340f, 0.047852f, 0.925781f) },
                { "memory1_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.937500f) },
                { "memory1_hit", new BattleSpriteMetrics(0.407097f, 0.000000f, 0.895534f) },
                { "memory1_victory", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.937500f) },
                { "memory1_defeat", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.715820f) },
                { "memory2", new BattleSpriteMetrics(0.506494f, 0.038086f, 0.934570f) },
                { "memory2_attack", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.933594f) },
                { "memory2_hit", new BattleSpriteMetrics(0.500000f, 0.000000f, 0.965710f) },
                { "memory2_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.937500f) },
                { "memory2_defeat", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.809570f) },
                { "memory3", new BattleSpriteMetrics(0.509310f, 0.052234f, 0.903076f) },
                { "memory3_attack", new BattleSpriteMetrics(0.500000f, 0.051103f, 0.874564f) },
                { "memory3_hit", new BattleSpriteMetrics(0.480328f, 0.000000f, 0.921466f) },
                { "memory3_victory", new BattleSpriteMetrics(0.493976f, 0.054556f, 0.904237f) },
                { "memory3_defeat", new BattleSpriteMetrics(0.500000f, 0.207777f, 0.590250f) },
                { "c_lancer", new BattleSpriteMetrics(0.467257f, 0.017578f, 0.969727f) },
                { "c_lancer_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.703125f) },
                { "c_lancer_hit", new BattleSpriteMetrics(0.505311f, 0.088852f, 0.820620f) },
                { "c_lancer_victory", new BattleSpriteMetrics(0.651367f, 0.015625f, 0.937500f) },
                { "c_lancer_defeat", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.586914f) },
                { "c_skywarden", new BattleSpriteMetrics(0.496528f, 0.035156f, 0.952148f) },
                { "c_skywarden_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.659180f) },
                { "c_skywarden_hit", new BattleSpriteMetrics(0.452552f, 0.170654f, 0.671451f) },
                { "c_skywarden_victory", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.937500f) },
                { "c_skywarden_defeat", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.704102f) },
                { "c_cleric", new BattleSpriteMetrics(0.456597f, 0.019531f, 0.962891f) },
                { "c_cleric_attack", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.933594f) },
                { "c_cleric_hit", new BattleSpriteMetrics(0.450957f, 0.122807f, 0.758373f) },
                { "c_cleric_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.936523f) },
                { "c_cleric_defeat", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.660156f) },
                { "c_guard", new BattleSpriteMetrics(0.490741f, 0.072266f, 0.871094f) },
                { "c_guard_attack", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.747070f) },
                { "c_guard_hit", new BattleSpriteMetrics(0.436603f, 0.206539f, 0.609250f) },
                { "c_guard_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.937500f) },
                { "c_guard_defeat", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.578125f) },
                { "c_archer", new BattleSpriteMetrics(0.516822f, 0.046875f, 0.890625f) },
                { "c_archer_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.954102f) },
                { "c_archer_hit", new BattleSpriteMetrics(0.506380f, 0.110048f, 0.775917f) },
                { "c_archer_victory", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.937500f) },
                { "c_archer_defeat", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.676758f) },
                { "c_mage", new BattleSpriteMetrics(0.525773f, 0.043945f, 0.927734f) },
                { "c_mage_attack", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.860352f) },
                { "c_mage_hit", new BattleSpriteMetrics(0.512138f, 0.138799f, 0.752435f) },
                { "c_mage_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.937500f) },
                { "c_mage_defeat", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.680664f) },
                { "e_knight", new BattleSpriteMetrics(0.500926f, 0.131836f, 0.771484f) },
                { "e_knight_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.654297f) },
                { "e_knight_hit", new BattleSpriteMetrics(0.482456f, 0.204944f, 0.588517f) },
                { "e_knight_victory", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.937500f) },
                { "e_knight_defeat", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.472656f) },
                { "e_cavalry", new BattleSpriteMetrics(0.508475f, 0.046875f, 0.934570f) },
                { "e_cavalry_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.648438f) },
                { "e_cavalry_hit", new BattleSpriteMetrics(0.495444f, 0.051089f, 0.825796f) },
                { "e_cavalry_victory", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.851562f) },
                { "e_cavalry_defeat", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.579102f) },
                { "e_archer", new BattleSpriteMetrics(0.500000f, 0.053711f, 0.885742f) },
                { "e_archer_attack", new BattleSpriteMetrics(0.500488f, 0.015625f, 0.917969f) },
                { "e_archer_hit", new BattleSpriteMetrics(0.483254f, 0.067783f, 0.855662f) },
                { "e_archer_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.937500f) },
                { "e_archer_defeat", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.618164f) },
                { "e_flier", new BattleSpriteMetrics(0.502627f, 0.149414f, 0.721680f) },
                { "e_flier_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.708984f) },
                { "e_flier_hit", new BattleSpriteMetrics(0.486045f, 0.050239f, 0.778309f) },
                { "e_flier_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.927734f) },
                { "e_flier_defeat", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.628906f) },
                { "e_mage", new BattleSpriteMetrics(0.491396f, 0.063477f, 0.883789f) },
                { "e_mage_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.946289f) },
                { "e_mage_hit", new BattleSpriteMetrics(0.504785f, 0.027113f, 0.936204f) },
                { "e_mage_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.937500f) },
                { "e_mage_defeat", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.843750f) },
                { "e_cleric", new BattleSpriteMetrics(0.493311f, 0.036133f, 0.927734f) },
                { "e_cleric_attack", new BattleSpriteMetrics(0.500488f, 0.015625f, 0.969727f) },
                { "e_cleric_hit", new BattleSpriteMetrics(0.515949f, 0.017544f, 0.963317f) },
                { "e_cleric_victory", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.937500f) },
                { "e_cleric_defeat", new BattleSpriteMetrics(0.499512f, 0.015625f, 0.625000f) },
                { "e_boss", new BattleSpriteMetrics(0.495028f, 0.037109f, 0.941406f) },
                { "e_boss_attack", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.845703f) },
                { "e_boss_hit", new BattleSpriteMetrics(0.488836f, 0.150718f, 0.640351f) },
                { "e_boss_victory", new BattleSpriteMetrics(0.500000f, 0.015625f, 0.916992f) },
                { "e_boss_defeat", new BattleSpriteMetrics(0.499512f, 0.016602f, 0.641602f) }
            };

        public static IReadOnlyList<string> RegisteredUnitIds => UnitIds;

        /// <summary>
        /// 配置できるスロットの上限。手で調整した5枠に、生成で3枠を足した数。
        ///
        /// 以前はスロットを最後の枠へクランプしていたため、6体目が5体目に
        /// 完全に重なって（位置・高さ・影・描画順すべて同値で）見えなくなっていた。
        /// 仲間を全員集めると味方は6体になるため、これは実際に踏む不具合だった。
        /// </summary>
        public const int MaxFormationSlots = 8;

        public static FormationAnchor GetAnchor(BattleTeam team, int slot)
        {
            if (slot < 0 || slot >= MaxFormationSlots)
                throw new ArgumentOutOfRangeException(
                    nameof(slot),
                    slot,
                    $"隊列に配置できるのは 0〜{MaxFormationSlots - 1} のスロットまでです。");

            return team == BattleTeam.Enemy
                ? EnemyAnchors[slot]
                : PlayerAnchors[slot];
        }

        public static FormationRow GetFormationRow(int slot)
        {
            if (slot < 0 || slot >= MaxFormationSlots)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return slot >= 3 && slot <= 5 || slot == 7
                ? FormationRow.Rear
                : FormationRow.Front;
        }

        public static FormationAnchor GetIncapacitatedAnchor(BattleTeam team, int slot)
        {
            int clampedSlot = Math.Max(0, Math.Min(slot, MaxFormationSlots - 1));
            FormationAnchor source = GetAnchor(team, clampedSlot);
            float direction = team == BattleTeam.Player ? 1f : -1f;
            float outwardOffset = 0.28f + clampedSlot * 0.14f;
            return new FormationAnchor(
                source.X + direction * outwardOffset,
                source.Y - 0.18f - (clampedSlot % 3) * 0.08f,
                source.Height,
                source.ShadowWidth * 1.08f);
        }

        public static float GetIncapacitatedTransitionDuration(string className)
        {
            BattleMotionProfile motion = GetMotionProfile(className);
            return 0.24f + motion.ApproachDuration * 0.24f;
        }

        public static float GetIncapacitatedSettleDuration(string className)
        {
            BattleMotionProfile motion = GetMotionProfile(className);
            return 0.36f + motion.ReturnDuration * 0.18f;
        }

        public static float GetSafeBattleCameraSize(int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));

            const float baseSize = 5.4f;
            const float referenceAspect = 16f / 9f;
            float aspect = pixelWidth / (float)pixelHeight;
            float aspectPadding = Math.Max(0f, referenceAspect - aspect) * 1.1f;
            float heightPadding = pixelHeight < 720
                ? (720 - pixelHeight) / 720f * 0.45f
                : 0f;
            return baseSize + aspectPadding + heightPadding;
        }

        public static string GetPoseAssetId(string sourceUnitId, BattlePose pose)
        {
            if (Array.IndexOf(UnitIds, sourceUnitId) < 0 &&
                !string.Equals(
                    sourceUnitId,
                    RecruitmentRosterPolicy.MemoryMinstrelId,
                    StringComparison.Ordinal))
                throw new ArgumentException($"Unknown presentation unit id: {sourceUnitId}", nameof(sourceUnitId));

            switch (pose)
            {
                case BattlePose.Idle:
                    return sourceUnitId;
                case BattlePose.Attack:
                    return sourceUnitId + "_attack";
                case BattlePose.Hit:
                    return sourceUnitId + "_hit";
                case BattlePose.Victory:
                    return sourceUnitId + "_victory";
                case BattlePose.Incapacitated:
                    return sourceUnitId + "_defeat";
                default:
                    throw new ArgumentOutOfRangeException(nameof(pose), pose, null);
            }
        }

        public static BattleSpriteMetrics GetSpriteMetrics(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId) || !SpriteMetrics.TryGetValue(assetId, out BattleSpriteMetrics metrics))
                throw new ArgumentException($"Unknown presentation asset id: {assetId}", nameof(assetId));
            return metrics;
        }

        public static float GetNormalizedPoseScale(
            string assetId,
            float targetHeight,
            int textureHeight,
            float pixelsPerUnit)
        {
            if (targetHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(targetHeight));
            if (textureHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textureHeight));
            if (pixelsPerUnit <= 0f) throw new ArgumentOutOfRangeException(nameof(pixelsPerUnit));

            BattleSpriteMetrics metrics = GetSpriteMetrics(assetId);
            float visibleWorldHeight = metrics.VisibleHeight * textureHeight / pixelsPerUnit;
            return targetHeight / visibleWorldHeight;
        }

        public static BattleMotionProfile GetMotionProfile(string className)
        {
            switch (className)
            {
                case "knight":
                    return new BattleMotionProfile(0.44f, 1.06f, 0.19f, 0.10f, 0.16f, 0.22f, 0.31f, 0.055f, 0.060f, 0.34f);
                case "cavalry":
                    return new BattleMotionProfile(0.62f, 1.18f, 0.16f, 0.14f, 0.22f, 0.34f, 0.34f, 0.060f, 0.075f, 0.42f);
                case "trickster":
                    return new BattleMotionProfile(0.30f, 0.86f, 0.13f, 0.22f, 0.10f, 0.28f, 0.24f, 0.040f, 0.085f, 0.28f);
                case "flier":
                    return new BattleMotionProfile(0.34f, 0.94f, 0.15f, 0.30f, 0.12f, 0.30f, 0.27f, 0.035f, 0.075f, 0.30f);
                case "archer":
                    return new BattleMotionProfile(0.26f, 1.42f, 0.17f, 0.08f, 0.08f, 0.14f, 0.25f, 0.030f, 0.055f, 0.29f);
                case "mage":
                    return new BattleMotionProfile(0.22f, 1.56f, 0.18f, 0.10f, 0.10f, 0.18f, 0.29f, 0.025f, 0.070f, 0.32f);
                case "cleric":
                    return new BattleMotionProfile(0.20f, 1.52f, 0.20f, 0.08f, 0.08f, 0.14f, 0.30f, 0.025f, 0.055f, 0.30f);
                default:
                    throw new ArgumentException($"Unknown battle motion class: {className}", nameof(className));
            }
        }

        public static bool SupportsBondTechnique(string sourceUnitId)
        {
            return string.Equals(sourceUnitId, "hero", StringComparison.Ordinal) ||
                   string.Equals(sourceUnitId, "partner", StringComparison.Ordinal);
        }

        public static bool IsBondTechniquePair(string firstSourceUnitId, string secondSourceUnitId)
        {
            return string.Equals(firstSourceUnitId, "hero", StringComparison.Ordinal) &&
                   string.Equals(secondSourceUnitId, "partner", StringComparison.Ordinal) ||
                   string.Equals(firstSourceUnitId, "partner", StringComparison.Ordinal) &&
                   string.Equals(secondSourceUnitId, "hero", StringComparison.Ordinal);
        }

        public static int GetSortingOrder(BattleTeam team, float groundY, FormationRenderLayer layer)
        {
            int depthBand = Math.Max(0, (int)Math.Floor((3.00f - groundY) * 4f + 0.5f));
            int teamBias = team == BattleTeam.Enemy ? 3 : 0;
            return depthBand * 6 + teamBias + (int)layer;
        }

        public static int GetIncapacitatedSortingOrder(BattleTeam team, FormationRenderLayer layer)
        {
            int teamBias = team == BattleTeam.Enemy ? 3 : 0;
            return 144 + teamBias + (int)layer;
        }

        public static bool GetFlipX(BattleTeam team, string assetId)
        {
            GetSpriteMetrics(assetId);
            bool sourceFacesLeft = Array.IndexOf(LeftFacingSourceAssets, assetId) >= 0;
            return (team == BattleTeam.Player) ^ sourceFacesLeft;
        }
    }
}
