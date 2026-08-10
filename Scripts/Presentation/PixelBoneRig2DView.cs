using System.Collections.Generic;
using BirthdayTactics.Core;
using UnityEngine;

namespace BirthdayTactics.Presentation
{
    /// <summary>
    /// Runtime rigid-part pixel rig.  Every source pixel belongs to one body part and
    /// all motion is produced by per-frame bone transforms; no whole-pose sprite swap.
    /// </summary>
    public sealed class PixelBoneRig2DView : MonoBehaviour, IRuntimeBoneRig2D
    {
        private const float PixelsPerUnit = 128f;
        private readonly List<Sprite> _ownedSprites = new List<Sprite>();
        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private Transform _poseRoot;
        private Transform _torso;
        private Transform _head;
        private Transform _upperArmLeft;
        private Transform _forearmLeft;
        private Transform _upperArmRight;
        private Transform _forearmRight;
        private Transform _thighLeft;
        private Transform _shinLeft;
        private Transform _thighRight;
        private Transform _shinRight;
        private Transform _cape;
        private Transform _weapon;
        private Material _spriteMaterial;
        private float _scale;
        private bool _flipX;

        public BoneRigPoseSample2D CurrentSample { get; private set; }

        public static PixelBoneRig2DView TryCreate(
            Transform parent,
            string sourceUnitId,
            float targetHeight,
            float parentScaleY,
            bool flipX)
        {
            if (!PixelAnimationProfile.IsSupported(sourceUnitId)) return null;
            string root = $"Art/Pixel/BoneParts/{sourceUnitId}";
            if (Resources.Load<Texture2D>($"{root}/torso") == null) return null;

            var rigObject = new GameObject($"Realtime Pixel Bone Rig {sourceUnitId}");
            rigObject.transform.SetParent(parent, false);
            var view = rigObject.AddComponent<PixelBoneRig2DView>();
            bool quadruped = PixelAnimationProfile.UsesQuadrupedAtlas(sourceUnitId);
            view.Build(root, quadruped);
            view._scale = targetHeight /
                Mathf.Max(0.0001f, Mathf.Abs(parentScaleY));
            view._flipX = flipX;
            view.ApplyRigScale();
            view.Apply(view.Sample(BoneRigPose2D.Idle, 0f, 0f));
            return view;
        }

        public BoneRigPoseSample2D Sample(
            BoneRigPose2D pose,
            float normalizedTime,
            float phase = 0f)
        {
            BoneRigPoseSample2D sample = BoneRig2DProfile.Sample(pose, normalizedTime, phase);
            float rotationScale = RotationScaleFor(pose);
            if (!name.Contains("azuki"))
                return WithMotionScale(sample, 0.22f, 0.22f, rotationScale);
            // Re-map humanoid channels onto fore/hind legs and keep the feline spine low.
            return new BoneRigPoseSample2D(
                sample.RootX * 0.18f,
                sample.RootY * 0.10f,
                sample.RootRotation * 0.20f * rotationScale,
                sample.TorsoRotation * 0.18f * rotationScale,
                sample.HeadRotation * 0.24f * rotationScale,
                sample.ThighLeftRotation * 0.28f * rotationScale,
                sample.ShinLeftRotation * 0.28f * rotationScale,
                sample.ThighRightRotation * 0.28f * rotationScale,
                sample.ShinRightRotation * 0.28f * rotationScale,
                sample.UpperArmLeftRotation * 0.28f * rotationScale,
                sample.ForearmLeftRotation * 0.28f * rotationScale,
                sample.UpperArmRightRotation * 0.28f * rotationScale,
                sample.ForearmRightRotation * 0.28f * rotationScale,
                sample.CapeRotation * 0.40f * rotationScale,
                0f);
        }

        private static float RotationScaleFor(BoneRigPose2D pose)
        {
            switch (pose)
            {
                case BoneRigPose2D.Idle: return 1f;
                case BoneRigPose2D.Entrance: return 0.24f;
                case BoneRigPose2D.Run: return 0.72f;
                case BoneRigPose2D.Cast: return 0.46f;
                case BoneRigPose2D.Victory: return 0.18f;
                case BoneRigPose2D.Hit: return 0.12f;
                case BoneRigPose2D.Guard: return 0.28f;
                case BoneRigPose2D.Defeat: return 0.10f;
                default: return 0.08f;
            }
        }

        private static BoneRigPoseSample2D WithMotionScale(
            BoneRigPoseSample2D sample,
            float xScale,
            float yScale,
            float rotationScale)
        {
            return new BoneRigPoseSample2D(
                sample.RootX * xScale,
                sample.RootY * yScale,
                sample.RootRotation * rotationScale,
                sample.TorsoRotation * rotationScale,
                sample.HeadRotation * rotationScale,
                sample.UpperArmLeftRotation * rotationScale,
                sample.ForearmLeftRotation * rotationScale,
                sample.UpperArmRightRotation * rotationScale,
                sample.ForearmRightRotation * rotationScale,
                sample.ThighLeftRotation * rotationScale,
                sample.ShinLeftRotation * rotationScale,
                sample.ThighRightRotation * rotationScale,
                sample.ShinRightRotation * rotationScale,
                sample.CapeRotation * rotationScale,
                sample.WeaponRotation * rotationScale);
        }

        public void Apply(BoneRigPoseSample2D sample)
        {
            CurrentSample = sample;
            _poseRoot.localPosition = new Vector3(sample.RootX, sample.RootY, 0f);
            Rotate(_poseRoot, sample.RootRotation);
            Rotate(_torso, sample.TorsoRotation);
            Rotate(_head, sample.HeadRotation);
            Rotate(_upperArmLeft, sample.UpperArmLeftRotation);
            Rotate(_forearmLeft, sample.ForearmLeftRotation);
            Rotate(_upperArmRight, sample.UpperArmRightRotation);
            Rotate(_forearmRight, sample.ForearmRightRotation);
            Rotate(_thighLeft, sample.ThighLeftRotation);
            Rotate(_shinLeft, sample.ShinLeftRotation);
            Rotate(_thighRight, sample.ThighRightRotation);
            Rotate(_shinRight, sample.ShinRightRotation);
            Rotate(_cape, sample.CapeRotation);
            Rotate(_weapon, sample.WeaponRotation);
        }

        public void SetFlipX(bool flipX)
        {
            _flipX = flipX;
            ApplyRigScale();
        }

        public void SetColor(Color color)
        {
            foreach (SpriteRenderer renderer in _renderers) renderer.color = color;
        }

        public void SetSortingOrder(int bodyOrder)
        {
            for (int i = 0; i < _renderers.Count; i++)
                _renderers[i].sortingOrder = bodyOrder + i - 4;
        }

        private void Build(string root, bool quadruped)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
                _spriteMaterial = new Material(spriteShader)
                {
                    name = "Realtime Pixel Unlit"
                };
            _poseRoot = Bone("root", transform, Vector2.zero);
            Vector2 hipsPoint = quadruped ? new Vector2(0.54f, 0.39f) : new Vector2(0.50f, 0.42f);
            Transform hips = Bone("hips", _poseRoot, FromGround(hipsPoint));
            _torso = Bone("torso", hips, Vector2.zero);

            Vector2 head = quadruped ? new Vector2(0.76f, 0.59f) : new Vector2(0.50f, 0.73f);
            Vector2 shoulderLeft = quadruped ? new Vector2(0.65f, 0.44f) : new Vector2(0.40f, 0.64f);
            Vector2 elbowLeft = quadruped ? new Vector2(0.69f, 0.24f) : new Vector2(0.33f, 0.52f);
            Vector2 shoulderRight = quadruped ? new Vector2(0.57f, 0.43f) : new Vector2(0.60f, 0.64f);
            Vector2 elbowRight = quadruped ? new Vector2(0.59f, 0.23f) : new Vector2(0.67f, 0.52f);
            Vector2 hipLeft = quadruped ? new Vector2(0.39f, 0.41f) : new Vector2(0.46f, 0.42f);
            Vector2 kneeLeft = quadruped ? new Vector2(0.42f, 0.22f) : new Vector2(0.45f, 0.23f);
            Vector2 hipRight = quadruped ? new Vector2(0.29f, 0.40f) : new Vector2(0.54f, 0.42f);
            Vector2 kneeRight = quadruped ? new Vector2(0.31f, 0.21f) : new Vector2(0.55f, 0.23f);

            _head = Bone("head", _torso, Delta(hipsPoint, head));
            _upperArmLeft = Bone("upper-arm-left", _torso, Delta(hipsPoint, shoulderLeft));
            _forearmLeft = Bone("forearm-left", _upperArmLeft, Delta(shoulderLeft, elbowLeft));
            _upperArmRight = Bone("upper-arm-right", _torso, Delta(hipsPoint, shoulderRight));
            _forearmRight = Bone("forearm-right", _upperArmRight, Delta(shoulderRight, elbowRight));
            _thighLeft = Bone("thigh-left", hips, Delta(hipsPoint, hipLeft));
            _shinLeft = Bone("shin-left", _thighLeft, Delta(hipLeft, kneeLeft));
            _thighRight = Bone("thigh-right", hips, Delta(hipsPoint, hipRight));
            _shinRight = Bone("shin-right", _thighRight, Delta(hipRight, kneeRight));
            _cape = Bone("cape", hips, Vector2.zero);
            _weapon = Bone("weapon", _forearmLeft, Vector2.zero);

            AddPart(root, "cape", _cape, hipsPoint);
            AddPart(root, "thigh_right", _thighRight, hipRight);
            AddPart(root, "shin_right", _shinRight, kneeRight);
            AddPart(root, "upper_arm_right", _upperArmRight, shoulderRight);
            AddPart(root, "forearm_right", _forearmRight, elbowRight);
            AddPart(root, "torso", _torso, hipsPoint);
            AddPart(root, "thigh_left", _thighLeft, hipLeft);
            AddPart(root, "shin_left", _shinLeft, kneeLeft);
            AddPart(root, "upper_arm_left", _upperArmLeft, shoulderLeft);
            AddPart(root, "forearm_left", _forearmLeft, elbowLeft);
            AddPart(root, "weapon", _weapon, elbowLeft);
            AddPart(root, "head", _head, head);
        }

        private void AddPart(string root, string id, Transform bone, Vector2 joint)
        {
            Texture2D texture = Resources.Load<Texture2D>($"{root}/{id}");
            if (texture == null) return;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                joint,
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = id;
            _ownedSprites.Add(sprite);
            SpriteRenderer renderer = bone.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (_spriteMaterial != null) renderer.sharedMaterial = _spriteMaterial;
            _renderers.Add(renderer);
        }

        private void ApplyRigScale()
        {
            transform.localScale = new Vector3(_flipX ? -_scale : _scale, _scale, 1f);
        }

        private static Vector2 FromGround(Vector2 point) =>
            new Vector2(point.x - 0.5f, point.y);

        private static Vector2 Delta(Vector2 from, Vector2 to) => to - from;

        private static Transform Bone(string name, Transform parent, Vector2 position)
        {
            var gameObject = new GameObject(name);
            Transform bone = gameObject.transform;
            bone.SetParent(parent, false);
            bone.localPosition = new Vector3(position.x, position.y, 0f);
            return bone;
        }

        private static void Rotate(Transform bone, float degrees)
        {
            bone.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in _ownedSprites)
                if (sprite != null) Destroy(sprite);
            if (_spriteMaterial != null) Destroy(_spriteMaterial);
        }
    }
}
