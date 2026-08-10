using System;
using System.Collections.Generic;
using System.Linq;

using BirthdayTactics.Core;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    /// <summary>
    /// 仲間を全員集めたときの人数を、隊列がきちんと収容できることを検証する。
    ///
    /// 発見された不具合: PlayerAnchors は5枠しかないのに、
    /// RecruitmentRosterPolicy.CreateStage は加入済みの仲間を既存編成に足すため、
    /// 全ステージで味方が6体になっていた。従来の GetAnchor は超過分を
    /// 最終枠へクランプしていたので、6体目は5体目と位置・高さ・影・描画順が
    /// すべて同値になり、画面から消えていた。
    /// </summary>
    public sealed class FormationCapacityTests
    {
        private ContentCatalogData _catalog;

        [SetUp]
        public void LoadCatalog()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/mu2_content");
            Assert.That(json, Is.Not.Null, "M-U2 content catalog is missing.");
            _catalog = JsonUtility.FromJson<ContentCatalogData>(json.text);
        }

        /// <summary>仲間を全員集めた状態での、各ステージの味方人数。</summary>
        private IEnumerable<(string stageId, int playerCount)> FullRosterPartySizes()
        {
            string[] everyone = { "memory1", "memory2", "memory3" };
            foreach (StageData authored in _catalog.stages)
            {
                StageData staged = RecruitmentRosterPolicy.CreateStage(
                    authored,
                    _catalog.stages,
                    everyone);
                int players = staged.units.Count(unit =>
                    string.Equals(unit.team, "player", StringComparison.OrdinalIgnoreCase));
                yield return (authored.id, players);
            }
        }

        [Test]
        public void FullRoster_FitsInsideTheFormationCapacity()
        {
            foreach ((string stageId, int playerCount) in FullRosterPartySizes())
            {
                Assert.That(
                    playerCount,
                    Is.LessThanOrEqualTo(FormationPresentationProfile.MaxFormationSlots),
                    $"{stageId} の味方 {playerCount} 体が隊列に収まりません。");
            }
        }

        /// <summary>
        /// これが本体の検証。実際に配置される人数ぶんのスロットで、
        /// 位置・描画順がひとつも重ならないこと。
        /// </summary>
        [Test]
        public void EveryOccupiedSlot_HasItsOwnPlaceAndDrawOrder()
        {
            int widestParty = FullRosterPartySizes().Max(entry => entry.playerCount);
            Assert.That(widestParty, Is.GreaterThan(5), "5体以下なら以前の実装でも足りていたはず。");

            foreach (BattleTeam team in new[] { BattleTeam.Player, BattleTeam.Enemy })
            {
                var placements = new HashSet<string>();
                var orders = new HashSet<int>();
                for (int slot = 0; slot < widestParty; slot++)
                {
                    FormationAnchor anchor = FormationPresentationProfile.GetAnchor(team, slot);

                    Assert.That(
                        placements.Add($"{anchor.X:F3}:{anchor.Y:F3}"),
                        Is.True,
                        $"{team} slot {slot} が別のユニットと同じ位置に置かれています。");

                    foreach (FormationRenderLayer layer in Enum.GetValues(typeof(FormationRenderLayer)))
                    {
                        int order = FormationPresentationProfile.GetSortingOrder(team, anchor.Y, layer);
                        Assert.That(
                            orders.Add(order),
                            Is.True,
                            $"{team} slot {slot} の {layer} が描画順 {order} で衝突しています。");
                    }

                    Assert.That(anchor.Height, Is.InRange(1.40f, 2.20f), $"{team} slot {slot}");
                    Assert.That(anchor.ShadowWidth, Is.InRange(0.50f, 0.90f), $"{team} slot {slot}");
                    Assert.That(
                        team == BattleTeam.Player ? anchor.X : -anchor.X,
                        Is.GreaterThan(0f),
                        $"{team} slot {slot}");
                }
            }
        }

        [Test]
        public void BothTeams_OccupyOpposingDiagonalPlanes()
        {
            for (int slot = 0; slot < FormationPresentationProfile.MaxFormationSlots; slot++)
            {
                FormationAnchor player = FormationPresentationProfile.GetAnchor(BattleTeam.Player, slot);
                FormationAnchor enemy = FormationPresentationProfile.GetAnchor(BattleTeam.Enemy, slot);

                Assert.That(player.X, Is.GreaterThan(0f), $"player slot {slot}");
                Assert.That(enemy.X, Is.LessThan(0f), $"enemy slot {slot}");
                Assert.That(player.Y, Is.LessThan(enemy.Y), $"slot {slot}");
                Assert.That(player.Height, Is.GreaterThan(enemy.Height), $"slot {slot}");
            }
        }

        /// <summary>
        /// 手で調整した5枠の値は、今回の拡張で1ミリも動かしていないこと。
        /// 見慣れた並びが変わると、これまでの調整がやり直しになる。
        /// </summary>
        [Test]
        public void TheFiveBattleSlots_MatchTheFrontRearComposition()
        {
            var expected = new[]
            {
                (1.28f, -1.48f, 2.16f, 0.82f),
                (2.16f, -1.86f, 2.10f, 0.79f),
                (3.04f, -2.24f, 2.04f, 0.76f),
                (2.30f, -0.35f, 1.92f, 0.72f),
                (3.18f, -0.73f, 1.86f, 0.69f)
            };

            for (int slot = 0; slot < expected.Length; slot++)
            {
                FormationAnchor anchor = FormationPresentationProfile.GetAnchor(BattleTeam.Player, slot);
                Assert.That(anchor.X, Is.EqualTo(expected[slot].Item1).Within(0.0001f), $"slot {slot} X");
                Assert.That(anchor.Y, Is.EqualTo(expected[slot].Item2).Within(0.0001f), $"slot {slot} Y");
                Assert.That(anchor.Height, Is.EqualTo(expected[slot].Item3).Within(0.0001f), $"slot {slot} H");
                Assert.That(
                    anchor.ShadowWidth,
                    Is.EqualTo(expected[slot].Item4).Within(0.0001f),
                    $"slot {slot} ShadowWidth");
            }
        }

        /// <summary>
        /// 収容できない人数は、静かに重ねて描くのではなく例外にすること。
        /// 黙って重ねると、今回のように「見えないだけ」で気付けなくなる。
        /// </summary>
        [Test]
        public void SlotsBeyondCapacity_ThrowInsteadOfStackingSilently()
        {
            Assert.That(
                () => FormationPresentationProfile.GetAnchor(
                    BattleTeam.Player,
                    FormationPresentationProfile.MaxFormationSlots),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => FormationPresentationProfile.GetAnchor(BattleTeam.Player, -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
