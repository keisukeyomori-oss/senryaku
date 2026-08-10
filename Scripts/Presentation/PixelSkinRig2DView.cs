using System;
using System.Collections.Generic;
using System.IO;
using BirthdayTactics.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BirthdayTactics.Presentation
{
    internal sealed class PixelSkinData2D
    {
        public readonly byte[] X;
        public readonly byte[] Y;
        public readonly Color32[] Colors;
        public readonly byte[] ParentBones;
        public readonly byte[] ChildBones;
        public readonly byte[] ChildWeights;
        public byte Left { get; private set; }
        public byte Top { get; private set; }
        public byte Right { get; private set; }
        public byte Bottom { get; private set; }
        public int Count => X.Length;

        private PixelSkinData2D(int count)
        {
            X = new byte[count];
            Y = new byte[count];
            Colors = new Color32[count];
            ParentBones = new byte[count];
            ChildBones = new byte[count];
            ChildWeights = new byte[count];
        }

        public static PixelSkinData2D Load(string sourceUnitId)
        {
            TextAsset asset = Resources.Load<TextAsset>($"Art/Pixel/SkinData/{sourceUnitId}");
            if (asset == null) return null;
            using (var stream = new MemoryStream(asset.bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                string magic = new string(reader.ReadChars(4));
                if (magic != "PSK1") throw new InvalidDataException("Unsupported pixel skin data.");
                int width = reader.ReadByte();
                int height = reader.ReadByte();
                int count = reader.ReadInt32();
                if (width != 128 || height != 128 || count <= 0)
                    throw new InvalidDataException($"Invalid pixel skin header: {sourceUnitId}");
                var data = new PixelSkinData2D(count);
                for (int i = 0; i < count; i++)
                {
                    data.X[i] = reader.ReadByte();
                    data.Y[i] = reader.ReadByte();
                    data.Colors[i] = new Color32(
                        reader.ReadByte(),
                        reader.ReadByte(),
                        reader.ReadByte(),
                        reader.ReadByte());
                    data.ParentBones[i] = reader.ReadByte();
                    data.ChildBones[i] = reader.ReadByte();
                    data.ChildWeights[i] = reader.ReadByte();
                    if (data.ParentBones[i] >= PixelSkinSkeleton2D.BoneCount ||
                        data.ChildBones[i] >= PixelSkinSkeleton2D.BoneCount)
                        throw new InvalidDataException($"Invalid bone index: {sourceUnitId}");
                }
                data.Left = data.X[0];
                data.Right = data.X[0];
                data.Top = data.Y[0];
                data.Bottom = data.Y[0];
                for (int i = 1; i < count; i++)
                {
                    data.Left = Math.Min(data.Left, data.X[i]);
                    data.Right = Math.Max(data.Right, data.X[i]);
                    data.Top = Math.Min(data.Top, data.Y[i]);
                    data.Bottom = Math.Max(data.Bottom, data.Y[i]);
                }
                if (stream.Position != stream.Length)
                    throw new InvalidDataException($"Trailing pixel skin bytes: {sourceUnitId}");
                return data;
            }
        }
    }

    internal static class PixelSkinSkeleton2D
    {
        public const int BoneCount = 12;
        public const int SourceSize = 128;
        public const int CpuCanvasSize = 160;

        private static readonly int[] Parents =
        {
            -1, 0, 0, 2, 0, 4, 0, 6, 0, 8, 0, 5
        };

        public static int ParentOf(int index) => Parents[index];

        public static Vector2[] Joints(bool quadruped, PixelSkinData2D data)
        {
            if (quadruped)
            {
                return new[]
                {
                    FromBounds(data, 0.52f, 0.43f), FromBounds(data, 0.70f, 0.43f),
                    FromBounds(data, 0.65f, 0.51f), FromBounds(data, 0.68f, 0.76f),
                    FromBounds(data, 0.56f, 0.51f), FromBounds(data, 0.58f, 0.76f),
                    FromBounds(data, 0.39f, 0.51f), FromBounds(data, 0.41f, 0.76f),
                    FromBounds(data, 0.29f, 0.50f), FromBounds(data, 0.30f, 0.76f),
                    FromBounds(data, 0.18f, 0.44f), FromBounds(data, 0.68f, 0.76f)
                };
            }
            return new[]
            {
                FromBounds(data, 0.50f, 0.60f), FromBounds(data, 0.50f, 0.29f),
                FromBounds(data, 0.36f, 0.36f), FromBounds(data, 0.28f, 0.50f),
                FromBounds(data, 0.64f, 0.36f), FromBounds(data, 0.72f, 0.50f),
                FromBounds(data, 0.46f, 0.60f), FromBounds(data, 0.45f, 0.80f),
                FromBounds(data, 0.54f, 0.60f), FromBounds(data, 0.55f, 0.80f),
                FromBounds(data, 0.42f, 0.52f), FromBounds(data, 0.70f, 0.54f)
            };
        }

        public static float[] Angles(BoneRigPoseSample2D sample)
        {
            return new[]
            {
                sample.TorsoRotation, sample.HeadRotation,
                sample.UpperArmLeftRotation, sample.ForearmLeftRotation,
                sample.UpperArmRightRotation, sample.ForearmRightRotation,
                sample.ThighLeftRotation, sample.ShinLeftRotation,
                sample.ThighRightRotation, sample.ShinRightRotation,
                sample.CapeRotation, sample.WeaponRotation
            };
        }

        public static Matrix4x4[] SkinMatrices(
            PixelSkinData2D data,
            bool quadruped,
            BoneRigPoseSample2D sample)
        {
            Vector2[] joints = Joints(quadruped, data);
            float[] angles = Angles(sample);
            var bind = new Matrix4x4[BoneCount];
            var posed = new Matrix4x4[BoneCount];
            Matrix4x4 poseRoot = Translate(sample.RootX, sample.RootY) *
                                 Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, sample.RootRotation));
            for (int i = 0; i < BoneCount; i++)
            {
                int parent = Parents[i];
                Vector2 local = parent < 0 ? joints[i] : joints[i] - joints[parent];
                Matrix4x4 localBind = Translate(local.x, local.y);
                Matrix4x4 localPose = localBind *
                                      Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angles[i]));
                bind[i] = parent < 0 ? localBind : bind[parent] * localBind;
                posed[i] = parent < 0
                    ? poseRoot * localPose
                    : posed[parent] * localPose;
            }
            var skin = new Matrix4x4[BoneCount];
            for (int i = 0; i < BoneCount; i++) skin[i] = posed[i] * bind[i].inverse;
            return skin;
        }

        public static BoneWeight SpatialWeight(
            Vector2 point,
            Vector2[] joints,
            float[] influence)
        {
            Array.Clear(influence, 0, influence.Length);
            // The connected grid shares vertices across every region, so cape,
            // tail and weapon anchors can participate without opening seams.
            for (int bone = 0; bone < BoneCount; bone++)
            {
                float distance = (point - joints[bone]).sqrMagnitude;
                influence[bone] = 1f / (0.012f + distance);
                if (bone == 10) influence[bone] *= 1.55f;
                if (bone == 11) influence[bone] *= 3.20f;
            }
            int first = -1;
            int second = -1;
            int third = -1;
            int fourth = -1;
            for (int bone = 0; bone < BoneCount; bone++)
            {
                if (first < 0 || influence[bone] > influence[first])
                {
                    fourth = third; third = second; second = first; first = bone;
                }
                else if (second < 0 || influence[bone] > influence[second])
                {
                    fourth = third; third = second; second = bone;
                }
                else if (third < 0 || influence[bone] > influence[third])
                {
                    fourth = third; third = bone;
                }
                else if (fourth < 0 || influence[bone] > influence[fourth])
                {
                    fourth = bone;
                }
            }
            float total = influence[first] + influence[second] +
                          influence[third] + influence[fourth];
            return new BoneWeight
            {
                boneIndex0 = first, weight0 = influence[first] / total,
                boneIndex1 = second, weight1 = influence[second] / total,
                boneIndex2 = third, weight2 = influence[third] / total,
                boneIndex3 = fourth, weight3 = influence[fourth] / total
            };
        }

        public static Vector3 SpatialSkinPoint(
            Vector3 point,
            Matrix4x4[] matrices,
            Vector2[] joints,
            float[] influence)
        {
            BoneWeight weight = SpatialWeight(point, joints, influence);
            return matrices[weight.boneIndex0].MultiplyPoint3x4(point) * weight.weight0 +
                   matrices[weight.boneIndex1].MultiplyPoint3x4(point) * weight.weight1 +
                   matrices[weight.boneIndex2].MultiplyPoint3x4(point) * weight.weight2 +
                   matrices[weight.boneIndex3].MultiplyPoint3x4(point) * weight.weight3;
        }

        public static BoneRigPoseSample2D Sample(
            string sourceUnitId,
            BoneRigPose2D pose,
            float normalizedTime,
            float phase = 0f)
        {
            if (pose == BoneRigPose2D.Run)
                return WalkSample(sourceUnitId, Mathf.Clamp01(normalizedTime) / 2.1f, 1f);
            BoneRigPoseSample2D source = BoneRig2DProfile.Sample(pose, normalizedTime, phase);
            bool quadruped = PixelAnimationProfile.UsesQuadrupedAtlas(sourceUnitId);
            float rootScale = quadruped
                ? 0.16f
                : pose == BoneRigPose2D.Defeat
                    ? 0.24f
                    : 0.22f;
            float rotationScale = pose == BoneRigPose2D.Idle
                ? 1f
                : pose == BoneRigPose2D.Strike
                    ? 0.72f
                    : pose == BoneRigPose2D.Windup
                        ? 0.68f
                    : pose == BoneRigPose2D.Cast
                        ? 0.66f
                    : pose == BoneRigPose2D.Guard
                        ? 0.54f
                : pose == BoneRigPose2D.Victory
                    ? 0.48f
                    : pose == BoneRigPose2D.Return
                        ? 0.56f
                        : pose == BoneRigPose2D.Hit
                            ? 0.44f
                    : 0.24f;
            if (!quadruped)
            {
                return Scale(source, rootScale, rotationScale);
            }
            return new BoneRigPoseSample2D(
                source.RootX * rootScale,
                source.RootY * 0.12f,
                source.RootRotation * 0.35f,
                source.TorsoRotation * 0.35f,
                source.HeadRotation * 0.70f,
                source.ThighLeftRotation * 0.72f,
                source.ShinLeftRotation * 0.72f,
                source.ThighRightRotation * 0.72f,
                source.ShinRightRotation * 0.72f,
                source.UpperArmLeftRotation * 0.72f,
                source.ForearmLeftRotation * 0.72f,
                source.UpperArmRightRotation * 0.72f,
                source.ForearmRightRotation * 0.72f,
                source.CapeRotation,
                0f);
        }

        public static BoneRigPoseSample2D WalkSample(
            string sourceUnitId,
            float elapsed,
            float runBlend)
        {
            float blend = Mathf.Clamp01(runBlend);
            float step = Mathf.Sin(elapsed * Mathf.PI * 2f * 2.1f);
            float opposite = -step;
            float lift = Mathf.Abs(step) * 0.025f * blend;
            if (PixelAnimationProfile.UsesQuadrupedAtlas(sourceUnitId))
            {
                return new BoneRigPoseSample2D(
                    0f, lift, step * 0.8f * blend,
                    opposite * 1.8f * blend,
                    step * 2.2f * blend,
                    step * 23f * blend,
                    Mathf.Max(0f, opposite) * 17f * blend,
                    opposite * 23f * blend,
                    Mathf.Max(0f, step) * 17f * blend,
                    opposite * 22f * blend,
                    Mathf.Max(0f, step) * 16f * blend,
                    step * 22f * blend,
                    Mathf.Max(0f, opposite) * 16f * blend,
                    opposite * 20f * blend,
                    0f);
            }
            float idle = Mathf.Sin(elapsed * Mathf.PI * 2f * 0.55f) * (1f - blend);
            return new BoneRigPoseSample2D(
                0f, lift + idle * 0.008f, step * 1.2f * blend,
                opposite * 2.2f * blend + idle * 0.7f,
                step * 1.8f * blend - idle * 0.4f,
                opposite * 27f * blend,
                step * 17f * blend,
                step * 27f * blend,
                opposite * 17f * blend,
                step * 26f * blend,
                Mathf.Max(0f, opposite) * 19f * blend,
                opposite * 26f * blend,
                Mathf.Max(0f, step) * 19f * blend,
                opposite * 16f * blend - idle,
                opposite * 26f * blend);
        }

        private static BoneRigPoseSample2D Scale(
            BoneRigPoseSample2D source,
            float rootScale,
            float rotationScale)
        {
            return new BoneRigPoseSample2D(
                source.RootX * rootScale, source.RootY * rootScale,
                source.RootRotation * rotationScale,
                source.TorsoRotation * rotationScale,
                source.HeadRotation * rotationScale,
                source.UpperArmLeftRotation * rotationScale,
                source.ForearmLeftRotation * rotationScale,
                source.UpperArmRightRotation * rotationScale,
                source.ForearmRightRotation * rotationScale,
                source.ThighLeftRotation * rotationScale,
                source.ShinLeftRotation * rotationScale,
                source.ThighRightRotation * rotationScale,
                source.ShinRightRotation * rotationScale,
                source.CapeRotation * rotationScale,
                source.WeaponRotation * rotationScale);
        }

        private static Vector2 FromBounds(PixelSkinData2D data, float nx, float ny)
        {
            float x = Mathf.Lerp(data.Left, data.Right + 1f, nx) / SourceSize - 0.5f;
            float y = 1f - Mathf.Lerp(data.Top, data.Bottom + 1f, ny) / SourceSize;
            return new Vector2(x, y);
        }
        private static Matrix4x4 Translate(float x, float y) =>
            Matrix4x4.Translate(new Vector3(x, y, 0f));
    }

    /// <summary>
    /// One quad per opaque source pixel, skinned on the GPU by two weighted bones.
    /// The source image never changes; movement is generated solely by bone matrices.
    /// </summary>
    public sealed class PixelSkinRig2DView : MonoBehaviour, IRuntimeBoneRig2D
    {
        private readonly Transform[] _bones = new Transform[PixelSkinSkeleton2D.BoneCount];
        private Transform _poseRoot;
        private SkinnedMeshRenderer _renderer;
        private Mesh _mesh;
        private Material _material;
        private Texture2D _skinTexture;
        private PixelSkinData2D _data;
        private string _sourceUnitId;
        private float _scale;
        private bool _flipX;

        public BoneRigPoseSample2D CurrentSample { get; private set; }
        public int PixelCount { get; private set; }

        public static PixelSkinRig2DView TryCreate(
            Transform parent,
            string sourceUnitId,
            float targetHeight,
            float parentScaleY,
            bool flipX)
        {
            PixelSkinData2D data = PixelSkinData2D.Load(sourceUnitId);
            if (data == null) return null;
            var host = new GameObject($"Per-Pixel Skin Rig {sourceUnitId}");
            host.transform.SetParent(parent, false);
            var view = host.AddComponent<PixelSkinRig2DView>();
            view._sourceUnitId = sourceUnitId;
            view.Build(data);
            view._scale = targetHeight / Mathf.Max(0.0001f, Mathf.Abs(parentScaleY));
            view._flipX = flipX;
            view.ApplyScale();
            view.Apply(view.Sample(BoneRigPose2D.Idle, 0f));
            return view;
        }

        public BoneRigPoseSample2D Sample(
            BoneRigPose2D pose,
            float normalizedTime,
            float phase = 0f) =>
            PixelSkinSkeleton2D.Sample(_sourceUnitId, pose, normalizedTime, phase);

        public BoneRigPoseSample2D WalkSample(float elapsed, float runBlend) =>
            PixelSkinSkeleton2D.WalkSample(_sourceUnitId, elapsed, runBlend);

        public float MeasureMaximumPixelDisplacement(
            BoneRigPoseSample2D from,
            BoneRigPoseSample2D to)
        {
            if (_data == null) return 0f;
            bool quadruped = PixelAnimationProfile.UsesQuadrupedAtlas(_sourceUnitId);
            Matrix4x4[] fromMatrices = PixelSkinSkeleton2D.SkinMatrices(_data, quadruped, from);
            Matrix4x4[] toMatrices = PixelSkinSkeleton2D.SkinMatrices(_data, quadruped, to);
            Vector2[] joints = PixelSkinSkeleton2D.Joints(quadruped, _data);
            var influence = new float[PixelSkinSkeleton2D.BoneCount];
            float maximum = 0f;
            for (int i = 0; i < _data.Count; i++)
            {
                Vector3 point = new Vector3(
                    (_data.X[i] + 0.5f) / PixelSkinSkeleton2D.SourceSize - 0.5f,
                    1f - (_data.Y[i] + 0.5f) / PixelSkinSkeleton2D.SourceSize,
                    0f);
                Vector3 a = PixelSkinSkeleton2D.SpatialSkinPoint(
                    point, fromMatrices, joints, influence);
                Vector3 b = PixelSkinSkeleton2D.SpatialSkinPoint(
                    point, toMatrices, joints, influence);
                maximum = Mathf.Max(maximum, Vector3.Distance(a, b));
            }
            return maximum;
        }

        public void Apply(BoneRigPoseSample2D sample)
        {
            CurrentSample = sample;
            _poseRoot.localPosition = new Vector3(sample.RootX, sample.RootY, 0f);
            _poseRoot.localRotation = Quaternion.Euler(0f, 0f, sample.RootRotation);
            float[] angles = PixelSkinSkeleton2D.Angles(sample);
            for (int i = 0; i < _bones.Length; i++)
                _bones[i].localRotation = Quaternion.Euler(0f, 0f, angles[i]);
        }

        public void SetFlipX(bool flipX)
        {
            _flipX = flipX;
            ApplyScale();
        }

        public void SetColor(Color color)
        {
            var block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            _renderer.SetPropertyBlock(block);
        }

        public void SetSortingOrder(int bodyOrder)
        {
            _renderer.sortingOrder = bodyOrder;
        }

        private void Build(PixelSkinData2D data)
        {
            _data = data;
            PixelCount = data.Count;
            bool quadruped = PixelAnimationProfile.UsesQuadrupedAtlas(_sourceUnitId);
            Vector2[] joints = PixelSkinSkeleton2D.Joints(quadruped, data);
            _poseRoot = NewBone("pixel-root", transform, Vector2.zero);
            for (int i = 0; i < _bones.Length; i++)
            {
                int parentIndex = PixelSkinSkeleton2D.ParentOf(i);
                Transform parent = parentIndex < 0 ? _poseRoot : _bones[parentIndex];
                Vector2 local = parentIndex < 0 ? joints[i] : joints[i] - joints[parentIndex];
                _bones[i] = NewBone("pixel-bone-" + i, parent, local);
            }

            _mesh = BuildMesh(data);
            _renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            _renderer.sharedMesh = _mesh;
            _renderer.bones = _bones;
            _renderer.rootBone = _poseRoot;
            _renderer.localBounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 2.4f, 1f));
            _renderer.updateWhenOffscreen = true;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new MissingReferenceException("Sprites/Default shader missing.");
            _material = new Material(shader) { name = "Per-Pixel Skin Unlit" };
            _skinTexture = BuildSkinTexture(data);
            _material.mainTexture = _skinTexture;
            _renderer.sharedMaterial = _material;

            var bindPoses = new Matrix4x4[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
                bindPoses[i] = _bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
            _mesh.bindposes = bindPoses;
        }

        private Mesh BuildMesh(PixelSkinData2D data)
        {
            const int size = PixelSkinSkeleton2D.SourceSize;
            const int stride = size + 1;
            int vertexCount = stride * stride;
            var vertices = new Vector3[vertexCount];
            var colors = new Color32[vertexCount];
            var uv = new Vector2[vertexCount];
            var weights = new BoneWeight[vertexCount];
            var triangles = new int[size * size * 6];
            Vector2[] joints = PixelSkinSkeleton2D.Joints(
                PixelAnimationProfile.UsesQuadrupedAtlas(_sourceUnitId), data);
            var influence = new float[PixelSkinSkeleton2D.BoneCount];
            for (int y = 0; y <= size; y++)
            {
                for (int x = 0; x <= size; x++)
                {
                    int vertex = y * stride + x;
                    Vector2 point = new Vector2(x / (float)size - 0.5f, 1f - y / (float)size);
                    vertices[vertex] = point;
                    colors[vertex] = Color.white;
                    uv[vertex] = new Vector2(x / (float)size, 1f - y / (float)size);
                    weights[vertex] = PixelSkinSkeleton2D.SpatialWeight(point, joints, influence);
                }
            }
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int topLeft = y * stride + x;
                    int bottomLeft = topLeft + stride;
                    int tri = (y * size + x) * 6;
                    triangles[tri] = topLeft;
                    triangles[tri + 1] = bottomLeft;
                    triangles[tri + 2] = bottomLeft + 1;
                    triangles[tri + 3] = topLeft;
                    triangles[tri + 4] = bottomLeft + 1;
                    triangles[tri + 5] = topLeft + 1;
                }
            }
            var mesh = new Mesh
            {
                name = $"Per-Pixel Mesh {_sourceUnitId}",
                indexFormat = IndexFormat.UInt32
            };
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.uv = uv;
            mesh.boneWeights = weights;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 2f, 1f));
            return mesh;
        }

        private Texture2D BuildSkinTexture(PixelSkinData2D data)
        {
            const int size = PixelSkinSkeleton2D.SourceSize;
            var pixels = new Color32[size * size];
            for (int i = 0; i < data.Count; i++)
                pixels[(size - 1 - data.Y[i]) * size + data.X[i]] = data.Colors[i];
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Pixel Skin " + _sourceUnitId,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void ApplyScale()
        {
            transform.localScale = new Vector3(_flipX ? -_scale : _scale, _scale, 1f);
        }

        private static Transform NewBone(string name, Transform parent, Vector2 local)
        {
            var host = new GameObject(name);
            Transform bone = host.transform;
            bone.SetParent(parent, false);
            bone.localPosition = new Vector3(local.x, local.y, 0f);
            return bone;
        }

        private void OnDestroy()
        {
            DestroyOwned(_mesh);
            DestroyOwned(_material);
            DestroyOwned(_skinTexture);
        }

        private static void DestroyOwned(UnityEngine.Object owned)
        {
            if (owned == null) return;
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }
    }

    public sealed class PixelSkinCpuRenderer : IDisposable
    {
        private readonly string _sourceUnitId;
        private readonly bool _quadruped;
        private readonly PixelSkinData2D _data;
        private readonly Vector2[] _joints;
        private readonly float[] _influence = new float[PixelSkinSkeleton2D.BoneCount];
        private readonly Color32[] _pixels;
        private readonly Color32[] _gapSource;
        private readonly Texture2D _texture;
        private int _renderedFrame = -1;
        private int _poseHash;

        private PixelSkinCpuRenderer(string sourceUnitId, PixelSkinData2D data)
        {
            _sourceUnitId = sourceUnitId;
            _quadruped = PixelAnimationProfile.UsesQuadrupedAtlas(sourceUnitId);
            _data = data;
            _joints = PixelSkinSkeleton2D.Joints(_quadruped, data);
            _pixels = new Color32[PixelSkinSkeleton2D.CpuCanvasSize * PixelSkinSkeleton2D.CpuCanvasSize];
            _gapSource = new Color32[_pixels.Length];
            _texture = new Texture2D(
                PixelSkinSkeleton2D.CpuCanvasSize,
                PixelSkinSkeleton2D.CpuCanvasSize,
                TextureFormat.RGBA32,
                false)
            {
                name = "CPU Per-Pixel Skin " + sourceUnitId,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        public static PixelSkinCpuRenderer TryCreate(string sourceUnitId)
        {
            PixelSkinData2D data = PixelSkinData2D.Load(sourceUnitId);
            return data == null ? null : new PixelSkinCpuRenderer(sourceUnitId, data);
        }

        public Texture2D Render(BoneRigPoseSample2D sample, bool flip)
        {
            int hash = SampleHash(sample, flip);
            if (_renderedFrame == Time.frameCount && _poseHash == hash) return _texture;
            Array.Clear(_pixels, 0, _pixels.Length);
            Matrix4x4[] matrices = PixelSkinSkeleton2D.SkinMatrices(_data, _quadruped, sample);
            const int margin = (PixelSkinSkeleton2D.CpuCanvasSize - PixelSkinSkeleton2D.SourceSize) / 2;
            for (int i = 0; i < _data.Count; i++)
            {
                Vector3 point = new Vector3(
                    (_data.X[i] + 0.5f) / PixelSkinSkeleton2D.SourceSize - 0.5f,
                    1f - (_data.Y[i] + 0.5f) / PixelSkinSkeleton2D.SourceSize,
                    0f);
                Vector3 skinned = PixelSkinSkeleton2D.SpatialSkinPoint(
                    point, matrices, _joints, _influence);
                int x = Mathf.FloorToInt(skinned.x * PixelSkinSkeleton2D.SourceSize) +
                        PixelSkinSkeleton2D.CpuCanvasSize / 2;
                int y = Mathf.FloorToInt(skinned.y * PixelSkinSkeleton2D.SourceSize) + margin;
                if (flip) x = PixelSkinSkeleton2D.CpuCanvasSize - 1 - x;
                if (x < 0 || y < 0 ||
                    x >= PixelSkinSkeleton2D.CpuCanvasSize ||
                    y >= PixelSkinSkeleton2D.CpuCanvasSize)
                    continue;
                int index = y * PixelSkinSkeleton2D.CpuCanvasSize + x;
                if (_data.Colors[i].a >= _pixels[index].a) _pixels[index] = _data.Colors[i];
            }
            FillInteriorGaps();
            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
            _renderedFrame = Time.frameCount;
            _poseHash = hash;
            return _texture;
        }

        private void FillInteriorGaps()
        {
            Array.Copy(_pixels, _gapSource, _pixels.Length);
            int size = PixelSkinSkeleton2D.CpuCanvasSize;
            for (int y = 2; y < size - 2; y++)
            {
                for (int x = 2; x < size - 2; x++)
                {
                    int index = y * size + x;
                    if (_gapSource[index].a > 0) continue;
                    Color32 a;
                    Color32 b;
                    if (OppositePixels(index, 1, out a, out b) ||
                        OppositePixels(index, 2, out a, out b) ||
                        OppositePixels(index, size, out a, out b) ||
                        OppositePixels(index, size * 2, out a, out b))
                        _pixels[index] = BlendOpaque(a, b);
                }
            }
        }

        private bool OppositePixels(int index, int offset, out Color32 a, out Color32 b)
        {
            a = _gapSource[index - offset];
            b = _gapSource[index + offset];
            return a.a > 0 && b.a > 0;
        }

        private static Color32 BlendOpaque(Color32 a, Color32 b) => new Color32(
            (byte)((a.r + b.r) / 2),
            (byte)((a.g + b.g) / 2),
            (byte)((a.b + b.b) / 2),
            (byte)Mathf.Max(a.a, b.a));

        public BoneRigPoseSample2D Walk(float elapsed, float runBlend) =>
            PixelSkinSkeleton2D.WalkSample(_sourceUnitId, elapsed, runBlend);

        public void Dispose()
        {
            if (_texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_texture);
            else UnityEngine.Object.DestroyImmediate(_texture);
        }

        private static int SampleHash(BoneRigPoseSample2D sample, bool flip)
        {
            unchecked
            {
                int hash = flip ? 17 : 31;
                hash = hash * 397 ^ Mathf.RoundToInt(sample.RootX * 1000f);
                hash = hash * 397 ^ Mathf.RoundToInt(sample.RootY * 1000f);
                hash = hash * 397 ^ Mathf.RoundToInt(sample.TorsoRotation * 10f);
                hash = hash * 397 ^ Mathf.RoundToInt(sample.UpperArmLeftRotation * 10f);
                hash = hash * 397 ^ Mathf.RoundToInt(sample.ThighLeftRotation * 10f);
                return hash;
            }
        }
    }
}
