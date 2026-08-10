using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using BirthdayTactics.Core;
using BirthdayTactics.Presentation;

namespace BirthdayTactics.Editor
{
    public static class MU1Build
    {
        [MenuItem("Birthday Tactics/Verify Realtime Pixel + 1-2-3")]
        public static void VerifyRealtimePixel123()
        {
            string[] parts =
            {
                "cape", "thigh_right", "shin_right", "upper_arm_right", "forearm_right",
                "torso", "thigh_left", "shin_left", "upper_arm_left", "forearm_left",
                "weapon", "head"
            };
            int resourceCount = 0;
            int skinnedPixelCount = 0;
            int motionAtlasCount = 0;
            foreach (string unitId in PixelAnimationProfile.SupportedUnitIds)
            {
                TextAsset skin = Resources.Load<TextAsset>($"Art/Pixel/SkinData/{unitId}");
                if (skin == null || skin.bytes.Length < 10)
                    throw new InvalidOperationException($"Missing per-pixel skin: {unitId}");
                using (var stream = new MemoryStream(skin.bytes, false))
                using (var reader = new BinaryReader(stream))
                {
                    if (new string(reader.ReadChars(4)) != "PSK1" ||
                        reader.ReadByte() != 128 || reader.ReadByte() != 128)
                        throw new InvalidOperationException($"Invalid per-pixel skin header: {unitId}");
                    int count = reader.ReadInt32();
                    if (count <= 4000 || stream.Length != 10L + count * 9L)
                        throw new InvalidOperationException($"Invalid per-pixel skin payload: {unitId}");
                    skinnedPixelCount += count;
                }
                foreach (string part in parts)
                {
                    Texture2D texture = Resources.Load<Texture2D>(
                        $"Art/Pixel/BoneParts/{unitId}/{part}");
                    if (texture == null || texture.width != 128 || texture.height != 128)
                        throw new InvalidOperationException($"Invalid realtime part: {unitId}/{part}");
                    resourceCount++;
                }
                if (!PixelAnimationProfile.UsesQuadrupedAtlas(unitId))
                {
                    string[] motionNames = { "field60", "battle60a", "battle60b" };
                    int[] expectedHeights = { 2048, 1536, 1024 };
                    for (int motionIndex = 0; motionIndex < motionNames.Length; motionIndex++)
                    {
                        Texture2D motion = Resources.Load<Texture2D>(
                            $"Art/Pixel/Characters/Motion60/{unitId}_{motionNames[motionIndex]}");
                        if (motion == null || motion.width != 1920 || motion.height != expectedHeights[motionIndex])
                            throw new InvalidOperationException(
                                $"Invalid 60fps motion atlas: {unitId}/{motionNames[motionIndex]}");
                        motionAtlasCount++;
                    }
                }
            }
            if (motionAtlasCount != 54)
                throw new InvalidOperationException($"60fps motion atlas count mismatch: {motionAtlasCount}/54");
            if (VerticalSliceController.ShouldReversePixelSequenceToIdle(60))
                throw new InvalidOperationException(
                    "Continuous attack motion would replay embedded effects after impact.");
            if (!VerticalSliceController.ShouldReversePixelSequenceToIdle(3))
                throw new InvalidOperationException(
                    "Legacy short pose transition no longer preserves reverse blending.");
            float castContinuationStart = VerticalSliceController.ResolvePixelMotionNormalized(
                0.62f, 1f, 0f, false);
            float castContinuationEnd = VerticalSliceController.ResolvePixelMotionNormalized(
                0.62f, 1f, 1f, false);
            if (Mathf.Abs(castContinuationStart - 0.62f) > 0.0001f ||
                Mathf.Abs(castContinuationEnd - 1f) > 0.0001f)
                throw new InvalidOperationException(
                    "Mage cast release restarted instead of continuing from gather phase.");
            if (!FormationPresentationProfile.GetFlipX(BattleTeam.Enemy, "e_cavalry_attack") ||
                FormationPresentationProfile.GetFlipX(BattleTeam.Enemy, "e_knight_attack") ||
                !FormationPresentationProfile.GetFlipX(BattleTeam.Enemy, "e_boss_attack"))
                throw new InvalidOperationException(
                    "Enemy attack sprite facing policy is reversed.");
            if (VerticalSliceController.ShouldShowSoloCutIn(true, false) ||
                !VerticalSliceController.ShouldShowSoloCutIn(true, true))
                throw new InvalidOperationException(
                    "Solo cut-in is not limited to special attacks.");
            if (!VerticalSliceController.UsesStablePixelEntrance(60) ||
                VerticalSliceController.UsesStablePixelEntrance(4))
                throw new InvalidOperationException(
                    "Continuous pixel entrance still switches run frames.");
            if (Resources.Load<Texture2D>("Art/Battle/Units/Variants/hero_attack") == null)
                throw new InvalidOperationException(
                    "High-resolution special attack cut-in is missing.");
            float hitOutEnd = VerticalSliceController.ResolvePixelMotionNormalized(
                0f, 0.72f, 1f, false);
            float hitReturnStart = VerticalSliceController.ResolvePixelMotionNormalized(
                0.72f, 1f, 0f, false);
            float hitReturnEnd = VerticalSliceController.ResolvePixelMotionNormalized(
                0.72f, 1f, 1f, false);
            if (Mathf.Abs(hitOutEnd - hitReturnStart) > 0.0001f ||
                Mathf.Abs(hitReturnEnd - 1f) > 0.0001f)
                throw new InvalidOperationException(
                    "Hit reaction restarted between recoil and return.");

            FormationBattleCore battle = new FormationBattleCore(CommandStage());
            FormationAction cooperation = battle.Advance(
                new FormationBattleCommand(FormationCommandKind.Cooperation));
            if (!cooperation.IsCooperation)
                throw new InvalidOperationException("Cooperation command did not cooperate.");

            battle = new FormationBattleCore(CommandStage());
            if (battle.Advance(new FormationBattleCommand(FormationCommandKind.Magic)).Kind !=
                FormationActionKind.Magic)
                throw new InvalidOperationException("Magic command did not produce magic.");

            battle = new FormationBattleCore(CommandStage());
            FormationAction defence = battle.Advance(
                new FormationBattleCommand(FormationCommandKind.Defend));
            if (!defence.IsDefending || defence.Actor.Status != FormationStatus.Fortified)
                throw new InvalidOperationException("Defend command did not fortify.");

            battle = new FormationBattleCore(CommandStage());
            FormationAction escape = battle.Advance(
                new FormationBattleCommand(FormationCommandKind.Flee));
            if (!escape.IsEscape || battle.Winner != BattleWinner.Escaped)
                throw new InvalidOperationException("Flee command did not escape.");

            FieldExplorationCore field = FieldExplorationCore.Create(2);
            FieldEntity treasure = field.Entities[2];
            FieldExplorationResult fieldResult = FieldExplorationResult.Idle;
            for (int i = 0; i < 240 && fieldResult != FieldExplorationResult.Interacted; i++)
                fieldResult = field.MoveToward(treasure.X, treasure.Y, 0.01f);
            if (fieldResult != FieldExplorationResult.Interacted || treasure.IsResolved)
                throw new InvalidOperationException("Field treasure activated without confirmation.");
            field.ConfirmCurrentInteraction();
            if (!treasure.IsResolved)
                throw new InvalidOperationException("Field treasure confirmation failed.");

            var rigRoot = new GameObject("Per-Pixel Motion Verification");
            PixelSkinRig2DView motionRig = PixelSkinRig2DView.TryCreate(
                rigRoot.transform, "hero", 1.9f, 1f, false);
            if (motionRig == null || motionRig.PixelCount <= 4000)
                throw new InvalidOperationException("Hero per-pixel rig did not build.");
            BoneRigPoseSample2D idle = motionRig.Sample(BoneRigPose2D.Idle, 0f);
            BoneRigPoseSample2D strike = motionRig.Sample(BoneRigPose2D.Strike, 1f);
            BoneRigPoseSample2D walk = motionRig.WalkSample(0.125f, 1f);
            BoneRigPoseSample2D oppositeWalk = motionRig.Sample(BoneRigPose2D.Run, 0.75f);
            BoneRigPoseSample2D windup = motionRig.Sample(BoneRigPose2D.Windup, 1f);
            BoneRigPoseSample2D castStart = motionRig.Sample(BoneRigPose2D.Cast, 0f);
            BoneRigPoseSample2D castRelease = motionRig.Sample(BoneRigPose2D.Cast, 1f);
            float strikeMotion = motionRig.MeasureMaximumPixelDisplacement(idle, strike);
            float walkMotion = motionRig.MeasureMaximumPixelDisplacement(idle, walk);
            float oppositeWalkMotion = motionRig.MeasureMaximumPixelDisplacement(walk, oppositeWalk);
            float windupToStrikeMotion = motionRig.MeasureMaximumPixelDisplacement(windup, strike);
            float castMotion = motionRig.MeasureMaximumPixelDisplacement(castStart, castRelease);
            float hitMotion = motionRig.MeasureMaximumPixelDisplacement(
                idle, motionRig.Sample(BoneRigPose2D.Hit, 1f));
            float guardMotion = motionRig.MeasureMaximumPixelDisplacement(
                idle, motionRig.Sample(BoneRigPose2D.Guard, 1f));
            float victoryMotion = motionRig.MeasureMaximumPixelDisplacement(
                idle, motionRig.Sample(BoneRigPose2D.Victory, 0.25f));
            float defeatMotion = motionRig.MeasureMaximumPixelDisplacement(
                idle, motionRig.Sample(BoneRigPose2D.Defeat, 1f));
            UnityEngine.Object.DestroyImmediate(rigRoot);
            if (strikeMotion < 0.08f || walkMotion < 0.04f ||
                oppositeWalkMotion < 0.06f || windupToStrikeMotion < 0.08f || castMotion < 0.08f ||
                hitMotion < 0.04f || guardMotion < 0.05f ||
                victoryMotion < 0.02f || defeatMotion < 0.20f)
                throw new InvalidOperationException(
                    $"Per-pixel motion too small: strike={strikeMotion:F3} walk={walkMotion:F3} " +
                    $"oppositeWalk={oppositeWalkMotion:F3} attackArc={windupToStrikeMotion:F3} " +
                    $"cast={castMotion:F3} " +
                    $"hit={hitMotion:F3} guard={guardMotion:F3} " +
                    $"victory={victoryMotion:F3} defeat={defeatMotion:F3}");

            var azukiRoot = new GameObject("Quadruped Motion Verification");
            PixelSkinRig2DView azukiRig = PixelSkinRig2DView.TryCreate(
                azukiRoot.transform, "azuki", 1.9f, 1f, false);
            if (azukiRig == null)
                throw new InvalidOperationException("Azuki per-pixel rig did not build.");
            float biteMotion = azukiRig.MeasureMaximumPixelDisplacement(
                azukiRig.Sample(BoneRigPose2D.Idle, 0f),
                azukiRig.Sample(BoneRigPose2D.Strike, 1f));
            BoneRigPoseSample2D azukiFront = azukiRig.Sample(BoneRigPose2D.Run, 0.25f);
            BoneRigPoseSample2D azukiBack = azukiRig.Sample(BoneRigPose2D.Run, 0.75f);
            UnityEngine.Object.DestroyImmediate(azukiRoot);
            if (biteMotion < 0.05f)
                throw new InvalidOperationException($"Quadruped bite motion too small: {biteMotion:F3}");
            if (Mathf.Sign(azukiFront.UpperArmLeftRotation) == Mathf.Sign(azukiBack.UpperArmLeftRotation) ||
                Mathf.Abs(azukiFront.HeadRotation - azukiBack.HeadRotation) < 2f)
                throw new InvalidOperationException("Quadruped gait did not alternate front legs and head balance.");

            PixelSkinCpuRenderer cpuRenderer = PixelSkinCpuRenderer.TryCreate("hero");
            if (cpuRenderer == null)
                throw new InvalidOperationException("Field per-pixel renderer did not build.");
            Texture2D fieldIdle = cpuRenderer.Render(cpuRenderer.Walk(0f, 1f), false);
            int visibleFieldPixels = 0;
            foreach (Color32 pixel in fieldIdle.GetPixels32())
                if (pixel.a > 0) visibleFieldPixels++;
            if (visibleFieldPixels < 4000)
                throw new InvalidOperationException(
                    $"Field idle lost source pixels: visible={visibleFieldPixels}");
            var cpuTimer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 60; i++)
                cpuRenderer.Render(cpuRenderer.Walk(i / 60f, 1f), false);
            cpuTimer.Stop();
            cpuRenderer.Dispose();
            if (cpuTimer.ElapsedMilliseconds >= 1000)
                throw new InvalidOperationException(
                    $"Field per-pixel renderer missed 60fps budget: {cpuTimer.ElapsedMilliseconds}ms/60");

            Debug.Log(
                $"REALTIME_PIXEL_123_OK resources={resourceCount} motionAtlases={motionAtlasCount} pixels={skinnedPixelCount} " +
                $"strike={strikeMotion:F3} walk={walkMotion:F3} hit={hitMotion:F3} guard={guardMotion:F3} " +
                $"oppositeWalk={oppositeWalkMotion:F3} attackArc={windupToStrikeMotion:F3} cast={castMotion:F3} " +
                $"victory={victoryMotion:F3} defeat={defeatMotion:F3} bite={biteMotion:F3} " +
                $"fieldPixels={visibleFieldPixels} cpu60={cpuTimer.ElapsedMilliseconds}ms " +
                "commands=attack,cooperation,magic,defend,flee interaction=confirm " +
                "postImpactAttackReplay=0 mageCastRestarts=0 enemyFacingErrors=0 " +
                "hitRestarts=0 cutInPolicy=specialOnly stableEntrance=1");
        }

        public static void RenderRealtimePixelRigPreview()
        {
            Directory.CreateDirectory("TestResults");
            var cameraObject = new GameObject("Realtime Pixel Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.3f;
            camera.transform.position = new Vector3(0f, 0.92f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.28f, 0.30f, 0.34f, 1f);

            string[] units = { "hero", "azuki", "e_knight" };
            BoneRigPose2D[] poses =
            {
                BoneRigPose2D.Strike,
                BoneRigPose2D.Idle,
                BoneRigPose2D.Guard
            };
            var roots = new GameObject[units.Length];
            for (int i = 0; i < units.Length; i++)
            {
                roots[i] = new GameObject("Preview " + units[i]);
                roots[i].transform.position = new Vector3((i - 1) * 1.35f, 0f, 0f);
                PixelSkinRig2DView rig = PixelSkinRig2DView.TryCreate(
                    roots[i].transform,
                    units[i],
                    1.9f,
                    1f,
                    false);
                if (rig == null)
                    throw new InvalidOperationException("Preview rig missing: " + units[i]);
                BoneRigPoseSample2D pose = i == 1
                    ? rig.WalkSample(0.125f, 1f)
                    : rig.Sample(poses[i], 1f, 0f);
                rig.Apply(pose);
                rig.SetSortingOrder(20);
            }

            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.aspect = 960f / 540f;
            camera.Render();
            RenderTexture.active = target;
            var screenshot = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0f, 0f, 960f, 540f), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(
                "TestResults/RealtimePixelRigPreview.png",
                screenshot.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(target);
            foreach (GameObject root in roots) UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            Debug.Log("REALTIME_PIXEL_PREVIEW_OK TestResults/RealtimePixelRigPreview.png");
        }

        public static void RenderArticulatedMotionPreview()
        {
            Directory.CreateDirectory("TestResults");
            var cameraObject = new GameObject("Articulated Motion Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.6f;
            camera.transform.position = new Vector3(0f, 1.0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.19f, 0.21f, 0.25f, 1f);

            string[] units =
            {
                "hero", "hero", "hero", "hero", "hero",
                "c_mage", "c_mage", "c_mage", "c_mage", "c_mage",
                "azuki", "azuki", "azuki", "azuki", "azuki"
            };
            BoneRigPose2D[] poses =
            {
                BoneRigPose2D.Run, BoneRigPose2D.Windup, BoneRigPose2D.Strike,
                BoneRigPose2D.Return, BoneRigPose2D.Guard,
                BoneRigPose2D.Cast, BoneRigPose2D.Cast, BoneRigPose2D.Cast,
                BoneRigPose2D.Cast, BoneRigPose2D.Hit,
                BoneRigPose2D.Run, BoneRigPose2D.Run, BoneRigPose2D.Run,
                BoneRigPose2D.Windup, BoneRigPose2D.Strike
            };
            float[] times =
            {
                0.25f, 1f, 1f, 0.55f, 1f,
                0f, 0.33f, 0.66f, 1f, 1f,
                0.25f, 0.50f, 0.75f, 1f, 1f
            };
            var roots = new GameObject[units.Length];
            for (int i = 0; i < units.Length; i++)
            {
                int column = i % 5;
                int row = i / 5;
                roots[i] = new GameObject($"Motion {row}-{column} {units[i]}");
                roots[i].transform.position = new Vector3((column - 2) * 2.15f, 2.65f - row * 2.25f, 0f);
                PixelSkinRig2DView rig = PixelSkinRig2DView.TryCreate(
                    roots[i].transform,
                    units[i],
                    1.55f,
                    1f,
                    false);
                if (rig == null)
                    throw new InvalidOperationException("Articulated preview rig missing: " + units[i]);
                rig.Apply(rig.Sample(poses[i], times[i], i * 0.07f));
                rig.SetSortingOrder(20);
            }

            var target = new RenderTexture(1500, 900, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.aspect = 1500f / 900f;
            camera.Render();
            RenderTexture.active = target;
            var screenshot = new Texture2D(1500, 900, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0f, 0f, 1500f, 900f), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(
                "TestResults/ArticulatedMotionPreview.png",
                screenshot.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(target);
            foreach (GameObject root in roots) UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            Debug.Log("ARTICULATED_MOTION_PREVIEW_OK TestResults/ArticulatedMotionPreview.png");
        }

        public static void RenderFieldPixelWalkPreview()
        {
            Directory.CreateDirectory("TestResults");
            const int cell = 160;
            var preview = new Texture2D(cell * 3, cell * 2, TextureFormat.RGBA32, false);
            var background = new Color32[cell * 3 * cell * 2];
            var gray = new Color32(72, 77, 87, 255);
            for (int i = 0; i < background.Length; i++) background[i] = gray;
            preview.SetPixels32(background);

            string[] units = { "hero", "azuki" };
            float[] phases = { 0f, 0.125f, 0.25f };
            for (int row = 0; row < units.Length; row++)
            {
                PixelSkinCpuRenderer renderer = PixelSkinCpuRenderer.TryCreate(units[row]);
                if (renderer == null)
                    throw new InvalidOperationException("Field preview renderer missing: " + units[row]);
                for (int column = 0; column < phases.Length; column++)
                {
                    Texture2D frame = renderer.Render(renderer.Walk(phases[column], 1f), false);
                    preview.SetPixels32(column * cell, (1 - row) * cell, cell, cell, frame.GetPixels32());
                }
                renderer.Dispose();
            }
            preview.Apply(false, false);
            File.WriteAllBytes("TestResults/FieldPixelWalkPreview.png", preview.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(preview);
            Debug.Log("FIELD_PIXEL_WALK_PREVIEW_OK TestResults/FieldPixelWalkPreview.png");
        }

        public static void VerifyPixelSkinRenderPerformance()
        {
            var cameraObject = new GameObject("Per-Pixel Performance Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.5f;
            camera.transform.position = new Vector3(0f, 2.15f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var target = new RenderTexture(960, 540, 16, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            string[] units =
            {
                "hero", "partner", "azuki", "memory1", "memory2", "memory3",
                "c_lancer", "c_skywarden", "c_guard", "e_knight", "e_flier", "e_boss"
            };
            var roots = new GameObject[units.Length];
            var rigs = new PixelSkinRig2DView[units.Length];
            for (int i = 0; i < units.Length; i++)
            {
                roots[i] = new GameObject("Performance " + units[i]);
                roots[i].transform.position = new Vector3(
                    (i % 6 - 2.5f) * 0.82f,
                    i < 6 ? 1.1f : 0f,
                    0f);
                rigs[i] = PixelSkinRig2DView.TryCreate(
                    roots[i].transform, units[i], 0.92f, 1f, i >= 6);
                if (rigs[i] == null)
                    throw new InvalidOperationException("Performance rig missing: " + units[i]);
                rigs[i].SetSortingOrder(20 + i);
            }

            camera.Render();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int frame = 0; frame < 60; frame++)
            {
                float elapsed = frame / 60f;
                for (int i = 0; i < rigs.Length; i++)
                    rigs[i].Apply(rigs[i].WalkSample(elapsed, 1f));
                camera.Render();
            }
            timer.Stop();
            float averageMs = timer.ElapsedMilliseconds / 60f;

            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            foreach (GameObject root in roots) UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            if (averageMs >= 16.667f)
                throw new InvalidOperationException(
                    $"12-unit per-pixel render missed 60fps: {averageMs:F2}ms/frame");
            Debug.Log(
                $"PER_PIXEL_60FPS_OK units={units.Length} frames=60 " +
                $"total={timer.ElapsedMilliseconds}ms average={averageMs:F2}ms");
        }

        [MenuItem("Birthday Tactics/Build Windows (M-U1)")]
        public static void BuildWindows()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            const string executablePath = "Builds/MU1-Windows/BirthdayTactics.exe";

            Directory.CreateDirectory(Path.GetDirectoryName(executablePath) ?? "Builds/MU1-Windows");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1);
            // Unity 6000.5 の自動選択はこの環境で D3D12 を選び、同一の
            // UnityPlayer.dll access violation を繰り返した。配布ビルドは
            // Windows の安定経路である D3D11 だけを明示的に使用する。
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"M-U1 Windows build failed: {report.summary.result}");
            }

            Debug.Log($"M-U1 Windows build succeeded: {report.summary.outputPath}");
        }

        private static StageData CommandStage()
        {
            return new StageData
            {
                id = "command-smoke",
                width = 9,
                height = 7,
                units = new[]
                {
                    SmokeUnit("hero", "hero", "trickster", "player", 1, 1, 40, 10),
                    SmokeUnit("partner", "partner", "mage", "player", 1, 2, 36, 9),
                    SmokeUnit("enemy", "e_knight", "knight", "enemy", 7, 1, 70, 7)
                }
            };
        }

        private static StageUnitData SmokeUnit(
            string id,
            string source,
            string className,
            string team,
            int x,
            int y,
            int hp,
            int damage)
        {
            return new StageUnitData
            {
                id = id,
                sourceUnitId = source,
                displayName = id,
                className = className,
                team = team,
                level = 1,
                x = x,
                y = y,
                maxHp = hp,
                damage = damage,
                moveRange = 1,
                attackRange = 1,
                weaponId = BattlePreparationCatalog.GetDefaultWeapon(className),
                tactic = TacticPolicy.Balanced
            };
        }
    }
}
