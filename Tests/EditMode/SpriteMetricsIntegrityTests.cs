using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BirthdayTactics.Core;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    /// <summary>
    /// SpriteMetrics テーブルが実際の PNG と一致していることを検証する。
    ///
    /// 背景: このテーブルは「立ち絵の可視領域」を表す契約データで、ずれると
    /// キャラクターが地面から浮いたり、ポーズ切替で身長が変わったりする。
    /// 実例として c_guard.png はキャンバス下端に alpha 1〜8（目視できない）の帯が
    /// 74px あり、閾値0で測ったメトリクスがそれを足元として記録していたため、
    /// 可視キャラクターが約0.27ワールドユニット浮き、身長も約7.9%小さく描画されていた。
    ///
    /// テーブルは Tools/generate_sprite_metrics.py で PNG から生成する。
    /// 本テストはその生成結果が commit されているかを保証するゲートである。
    /// </summary>
    public sealed class SpriteMetricsIntegrityTests
    {
        /// <summary>
        /// 可視とみなすアルファの下限。契約は FormationPresentationProfile 側に一本化してあり、
        /// Tools/generate_sprite_metrics.py の ALPHA_THRESHOLD もこの値と一致させること。
        /// </summary>
        private const byte AlphaThreshold = FormationPresentationProfile.VisibleAlphaThreshold;

        /// <summary>PNG の量子化誤差を吸収する許容差（キャンバス比）。1024px なら約0.5px 相当。</summary>
        private const float Tolerance = 0.0005f;

        private static readonly string[] PoseSuffixes = { "", "_attack", "_hit", "_victory", "_defeat" };

        private static IEnumerable<string> AllPoseAssetIds()
        {
            foreach (string unitId in FormationPresentationProfile.RegisteredUnitIds)
                foreach (string suffix in PoseSuffixes)
                    yield return unitId + suffix;

            foreach (string suffix in PoseSuffixes)
                yield return RecruitmentRosterPolicy.MemoryMinstrelId + suffix;
        }

        private static string ResolvePngPath(string assetId)
        {
            string root = Path.Combine(Application.dataPath, "Resources/Art/Battle/Units");
            string variant = Path.Combine(root, "Variants", assetId + ".png");
            if (File.Exists(variant)) return variant;
            string direct = Path.Combine(root, assetId + ".png");
            return File.Exists(direct) ? direct : null;
        }

        /// <summary>
        /// インポート設定に依存せず読める一時テクスチャとして PNG を復号する。
        /// Resources.Load したテクスチャは isReadable=0 のため GetPixels32 が使えない。
        /// </summary>
        private static Texture2D DecodeReadable(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path), markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }
            return texture;
        }

        private static (float pivotX, float pivotY, float visibleHeight) MeasureVisibleBounds(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;

            int left = width, right = -1, bottom = height, top = -1;
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[rowOffset + x].a <= AlphaThreshold) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < bottom) bottom = y;
                    if (y > top) top = y;
                }
            }

            Assert.That(right, Is.GreaterThanOrEqualTo(0), "可視画素が存在しません。");

            // GetPixels32 は左下原点、BattleSpriteMetrics は「下端からの比率」なので
            // bottom がそのまま足元の高さになる。
            return (
                (left + right + 1) / 2f / width,
                bottom / (float)height,
                (top - bottom + 1) / (float)height);
        }

        [Test]
        public void SpriteMetrics_MatchTheVisibleBoundsOfEveryPngOnDisk()
        {
            var failures = new List<string>();

            foreach (string assetId in AllPoseAssetIds())
            {
                string path = ResolvePngPath(assetId);
                if (path == null)
                {
                    failures.Add($"{assetId}: PNG が見つかりません");
                    continue;
                }

                Texture2D texture = DecodeReadable(path);
                if (texture == null)
                {
                    failures.Add($"{assetId}: PNG を復号できません");
                    continue;
                }

                try
                {
                    (float pivotX, float pivotY, float visibleHeight) = MeasureVisibleBounds(texture);
                    BattleSpriteMetrics metrics = FormationPresentationProfile.GetSpriteMetrics(assetId);

                    float delta = Mathf.Max(
                        Mathf.Abs(metrics.PivotX - pivotX),
                        Mathf.Abs(metrics.PivotY - pivotY),
                        Mathf.Abs(metrics.VisibleHeight - visibleHeight));

                    if (delta > Tolerance)
                    {
                        failures.Add(
                            $"{assetId}: 実測 ({pivotX:F6}, {pivotY:F6}, {visibleHeight:F6}) / " +
                            $"テーブル ({metrics.PivotX:F6}, {metrics.PivotY:F6}, {metrics.VisibleHeight:F6}) " +
                            $"最大差 {delta:F6}");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            Assert.That(
                failures,
                Is.Empty,
                "SpriteMetrics が PNG とずれています。Tools/generate_sprite_metrics.py --apply で再生成してください。\n" +
                string.Join("\n", failures));
        }

        [Test]
        public void AllNinetyFivePoseSlots_RenderAtTheSameRequestedHeight()
        {
            const float targetHeight = 3.72f;
            const float pixelsPerUnit = 100f;
            string[] assetIds = AllPoseAssetIds().ToArray();

            Assert.That(assetIds.Length, Is.EqualTo(95));
            Assert.That(assetIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(95));

            foreach (string assetId in assetIds)
            {
                string path = ResolvePngPath(assetId);
                Assert.That(path, Is.Not.Null, assetId);
                Texture2D texture = DecodeReadable(path);
                Assert.That(texture, Is.Not.Null, assetId);
                try
                {
                    BattleSpriteMetrics metrics = FormationPresentationProfile.GetSpriteMetrics(assetId);
                    float scale = FormationPresentationProfile.GetNormalizedPoseScale(
                        assetId,
                        targetHeight,
                        texture.height,
                        pixelsPerUnit);
                    float renderedVisibleHeight =
                        metrics.VisibleHeight * texture.height / pixelsPerUnit * scale;
                    Assert.That(
                        renderedVisibleHeight,
                        Is.EqualTo(targetHeight).Within(0.001f),
                        $"{assetId} のポーズ切替で表示身長が変化します。");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        /// <summary>
        /// 足元は必ずピボットに一致していなければならない。
        /// PivotY と可視下端が同一値であることは上のテストで担保されるが、
        /// ここでは「非飛行ユニットが接地して見える」ための上限を別途固定する。
        /// キャンバス下部の余白が大きい素材は、そのぶん足元が浮いて見える。
        /// </summary>
        [Test]
        public void EveryPose_KeepsItsFootprintWithinTheGroundedBudget()
        {
            foreach (string assetId in AllPoseAssetIds())
            {
                BattleSpriteMetrics metrics = FormationPresentationProfile.GetSpriteMetrics(assetId);

                // ピボットは可視下端に置かれるため、この値はそのまま
                // 「スプライト原点から足元までの距離（キャンバス比）」になる。
                // 0.25 を超えるとアンカー基準で 0.9 ワールドユニット以上ずれ、
                // 影と足元の乖離が画面上で明確に見える。
                Assert.That(metrics.PivotY, Is.InRange(0f, 0.25f), $"{assetId} の足元がキャンバス下部から離れすぎています。");
                Assert.That(metrics.VisibleHeight, Is.GreaterThan(0.4f), $"{assetId} の可視高さが小さすぎます。");
                // 可視領域はキャンバスをはみ出せない（浮動小数の丸めぶんだけ許容）。
                Assert.That(metrics.PivotY + metrics.VisibleHeight, Is.LessThanOrEqualTo(1.001f), assetId);
                Assert.That(metrics.PivotX, Is.InRange(0.2f, 0.8f), $"{assetId} の可視中心が偏りすぎています。");
            }
        }

        /// <summary>
        /// 作画規約は「右向き（味方基準）」で、敵は flipX で反転する。
        /// 元から左向きに描かれた素材だけが例外で、その集合を固定する。
        /// 例外リストを増減させたらこのテストも更新すること
        /// （Tools/audit_sprite_facing.py で実表示を確認してから変更する）。
        /// </summary>
        [Test]
        public void LeftFacingExceptions_AreExactlyTheDocumentedSet()
        {
            var expected = new[]
            {
                "e_cavalry_attack",
                "e_archer_attack",
                "e_flier_attack",
                "e_cleric_attack"
            };

            string[] actual = AllPoseAssetIds()
                .Where(assetId => FormationPresentationProfile.GetFlipX(BattleTeam.Enemy, assetId))
                .OrderBy(assetId => assetId, StringComparer.Ordinal)
                .ToArray();

            Assert.That(
                actual,
                Is.EqualTo(expected.OrderBy(id => id, StringComparer.Ordinal).ToArray()),
                "左向き素材の集合が変わりました。Tools/audit_sprite_facing.py で実表示を確認してください。");

            // 例外素材はプレイヤー側では逆に反転される（＝どちらのチームでも敵の方を向く）。
            foreach (string assetId in expected)
                Assert.That(FormationPresentationProfile.GetFlipX(BattleTeam.Player, assetId), Is.False, assetId);
        }

        /// <summary>
        /// 未登録の assetId は静かに既定値へ落ちず、必ず例外になること。
        /// 落ちてしまうと、ポーズ追加時の登録漏れが実行時の見た目の破綻として現れる。
        /// </summary>
        [Test]
        public void UnregisteredAssetIds_ThrowInsteadOfSilentlyFallingBack()
        {
            Assert.That(
                () => FormationPresentationProfile.GetSpriteMetrics("partner_cast"),
                Throws.TypeOf<ArgumentException>(),
                "partner_cast は使用されていない素材で、参照されたら気付けるようにする。");

            Assert.That(
                () => FormationPresentationProfile.GetFlipX(BattleTeam.Enemy, "does_not_exist"),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
