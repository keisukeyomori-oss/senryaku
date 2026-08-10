using System;
using System.IO;
using System.Linq;
using BirthdayTactics.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    public sealed class ContentCatalogTests
    {
        private ContentCatalogData _catalog;

        [SetUp]
        public void LoadCatalog()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/mu2_content");
            Assert.That(json, Is.Not.Null, "M-U2 content catalog is missing.");
            _catalog = JsonUtility.FromJson<ContentCatalogData>(json.text);
        }

        [Test]
        public void Catalog_ContainsEveryWebContentRecord()
        {
            Assert.That(_catalog.schemaVersion, Is.EqualTo(2));
            Assert.That(_catalog.classes, Has.Length.EqualTo(7));
            Assert.That(_catalog.unitPrototypes, Has.Length.EqualTo(18));
            Assert.That(_catalog.stages, Has.Length.EqualTo(6));
            Assert.That(_catalog.warmaps, Has.Length.EqualTo(3));
            Assert.That(_catalog.stages.Select(stage => stage.id), Is.EqualTo(new[] { "s0", "s1", "s2", "s3", "s4", "s5" }));
        }

        [Test]
        public void EveryRegisteredUnit_HasAllDedicatedBattlePoseTextures()
        {
            foreach (string unitId in FormationPresentationProfile.RegisteredUnitIds)
            {
                AssertPoseExists(unitId, BattlePose.Attack);
                AssertPoseExists(unitId, BattlePose.Hit);
                AssertPoseExists(unitId, BattlePose.Victory);
                AssertPoseExists(unitId, BattlePose.Incapacitated);
            }
        }

        [Test]
        public void EveryBattlePose_MatchesRegisteredAlphaMetrics()
        {
            foreach (string unitId in FormationPresentationProfile.RegisteredUnitIds)
            {
                AssertTextureMetrics(unitId, BattlePose.Idle);
                AssertTextureMetrics(unitId, BattlePose.Attack);
                AssertTextureMetrics(unitId, BattlePose.Hit);
                AssertTextureMetrics(unitId, BattlePose.Victory);
                AssertTextureMetrics(unitId, BattlePose.Incapacitated);
            }
        }

        [Test]
        public void EveryBattleUnitTexture_UsesTransparentMipImportContract()
        {
            string[] textureGuids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { "Assets/Resources/Art/Battle/Units" });

            Assert.That(textureGuids, Has.Length.EqualTo(96));
            foreach (string guid in textureGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, assetPath);
                Assert.That(importer.alphaIsTransparency, Is.True, assetPath);
                Assert.That(importer.mipmapEnabled, Is.True, assetPath);
                Assert.That(importer.mipMapsPreserveCoverage, Is.True, assetPath);
                Assert.That(importer.maxTextureSize, Is.EqualTo(512), assetPath);
            }
        }

        [Test]
        public void EveryStage_HasValidUniquePlacementsAndIncreasingDifficulty()
        {
            string[] classIds = _catalog.classes.Select(item => item.id).ToArray();
            int previousEnemyScore = -1;

            foreach (StageData stage in _catalog.stages)
            {
                Assert.That(stage.units.Any(unit => unit.team == "player"), Is.True, stage.id);
                Assert.That(stage.units.Any(unit => unit.team == "enemy"), Is.True, stage.id);
                Assert.That(stage.units.Select(unit => unit.id).Distinct().Count(), Is.EqualTo(stage.units.Length), stage.id);
                Assert.That(stage.units.Select(unit => $"{unit.x},{unit.y}").Distinct().Count(), Is.EqualTo(stage.units.Length), stage.id);
                Assert.That(stage.units.All(unit => unit.x >= 0 && unit.x < stage.width && unit.y >= 0 && unit.y < stage.height), Is.True, stage.id);
                Assert.That(stage.units.All(unit => classIds.Contains(unit.className)), Is.True, stage.id);

                int enemyScore = stage.units.Where(unit => unit.team == "enemy")
                    .Sum(unit => unit.maxHp + unit.damage * 3);
                Assert.That(enemyScore, Is.GreaterThan(previousEnemyScore), stage.id);
                previousEnemyScore = enemyScore;
            }
        }

        [Test]
        public void EveryStage_DeterministicSimulationReachesAResult()
        {
            foreach (StageData stage in _catalog.stages)
            {
                (BattleWinner winner, int turns) first = Simulate(stage);
                (BattleWinner winner, int turns) second = Simulate(stage);

                Assert.That(first.winner, Is.Not.EqualTo(BattleWinner.None), stage.id);
                Assert.That(first, Is.EqualTo(second), stage.id);
            }
        }

        private static (BattleWinner winner, int turns) Simulate(StageData stage)
        {
            var battle = new BattleCore(stage);
            int safety = 0;
            while (battle.Winner == BattleWinner.None && safety++ < 120)
            {
                if (battle.ActiveTeam == BattleTeam.Enemy)
                {
                    battle.RunEnemyTurn();
                    continue;
                }

                UnitState[] ready = battle.Units
                    .Where(unit => unit.Team == BattleTeam.Player && unit.IsAlive && !unit.HasActed)
                    .OrderBy(unit => unit.Id, StringComparer.Ordinal)
                    .ToArray();
                foreach (UnitState unit in ready)
                {
                    if (battle.ActiveTeam != BattleTeam.Player || battle.Winner != BattleWinner.None) break;
                    battle.SelectUnit(unit.Id);
                    UnitState target = battle.GetAttackableEnemies(unit.Id).FirstOrDefault();
                    if (target == null)
                    {
                        GridPoint? destination = battle.GetMoveRange(unit.Id)
                            .OrderBy(point => battle.Units.Where(candidate => candidate.Team == BattleTeam.Enemy && candidate.IsAlive)
                                .Min(enemy => point.DistanceTo(enemy.Position)))
                            .ThenBy(point => point.X)
                            .ThenBy(point => point.Y)
                            .Cast<GridPoint?>()
                            .FirstOrDefault();
                        if (destination.HasValue) battle.TryMoveSelected(destination.Value, out _);
                        target = battle.GetAttackableEnemies(unit.Id).FirstOrDefault();
                    }
                    if (target != null) battle.TryAttackSelected(target.Id, out _);
                }

                if (battle.Winner == BattleWinner.None && battle.ActiveTeam == BattleTeam.Player)
                    battle.EndPlayerTurn();
            }

            return (battle.Winner, battle.TurnNumber);
        }

        private static void AssertPoseExists(string unitId, BattlePose pose)
        {
            string assetId = FormationPresentationProfile.GetPoseAssetId(unitId, pose);
            Texture2D texture = Resources.Load<Texture2D>($"Art/Battle/Units/Variants/{assetId}");
            Assert.That(texture, Is.Not.Null, $"{unitId} is missing its {pose} pose.");
        }

        private static void AssertTextureMetrics(string unitId, BattlePose pose)
        {
            string assetId = FormationPresentationProfile.GetPoseAssetId(unitId, pose);
            string resourcePath = pose == BattlePose.Idle
                ? $"Art/Battle/Units/{assetId}"
                : $"Art/Battle/Units/Variants/{assetId}";
            Texture2D imported = Resources.Load<Texture2D>(resourcePath);
            Assert.That(imported, Is.Not.Null, $"{assetId} texture is missing.");

            string assetPath = AssetDatabase.GetAssetPath(imported);
            byte[] png = File.ReadAllBytes(assetPath);
            var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(readable.LoadImage(png, false), Is.True, assetId);
                Color32[] pixels = readable.GetPixels32();
                int minX = readable.width;
                int minY = readable.height;
                int maxX = -1;
                int maxY = -1;
                for (int y = 0; y < readable.height; y++)
                {
                    for (int x = 0; x < readable.width; x++)
                    {
                        // 目視できないアルファ残渣を可視境界に含めないこと。
                        // 閾値を 0 に戻すと c_guard.png の不可視な 74px の帯を
                        // 足元として測ってしまい、接地の破綻を検知できなくなる。
                        if (pixels[y * readable.width + x].a <= FormationPresentationProfile.VisibleAlphaThreshold) continue;
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }

                Assert.That(maxX, Is.GreaterThanOrEqualTo(0), $"{assetId} has no visible pixels.");
                BattleSpriteMetrics expected = FormationPresentationProfile.GetSpriteMetrics(assetId);
                float pivotX = (minX + maxX + 1) * 0.5f / readable.width;
                float pivotY = minY / (float)readable.height;
                float visibleHeight = (maxY - minY + 1) / (float)readable.height;
                float tolerance = 1.1f / readable.height;
                Assert.That(pivotX, Is.EqualTo(expected.PivotX).Within(tolerance), $"{assetId} pivotX");
                Assert.That(pivotY, Is.EqualTo(expected.PivotY).Within(tolerance), $"{assetId} pivotY");
                Assert.That(
                    visibleHeight,
                    Is.EqualTo(expected.VisibleHeight).Within(tolerance),
                    $"{assetId} visibleHeight");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }
    }
}
