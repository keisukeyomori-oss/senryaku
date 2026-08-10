using System.Collections.Generic;
using UnityEngine;

namespace BirthdayTactics.Presentation
{
    public sealed class BoneRig2DView : MonoBehaviour, IRuntimeBoneRig2D
    {
        private const float PixelsPerUnit = 100f;

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
        private float _scale;
        private bool _flipX;

        public BoneRigPoseSample2D CurrentSample { get; private set; }

        public BoneRigPoseSample2D Sample(
            BoneRigPose2D pose,
            float normalizedTime,
            float phase = 0f) =>
            BoneRig2DProfile.Sample(pose, normalizedTime, phase);

        public static BoneRig2DView TryCreate(
            Transform parent,
            string sourceUnitId,
            float targetHeight,
            float parentScaleY,
            bool flipX)
        {
            if (!BoneRig2DProfile.Supports(sourceUnitId)) return null;
            string resourceRoot = $"Art/Battle/BoneParts/{sourceUnitId}";
            if (Resources.Load<Texture2D>($"{resourceRoot}/torso") == null) return null;
            BoneRigLayout2D layout = BoneRig2DProfile.GetLayout(sourceUnitId);

            var rigObject = new GameObject($"Unity 2D Bone Rig {sourceUnitId}");
            rigObject.transform.SetParent(parent, false);
            var view = rigObject.AddComponent<BoneRig2DView>();
            view.Build(resourceRoot, sourceUnitId, layout);
            view._scale = targetHeight /
                (layout.ReferenceHeight * Mathf.Max(0.0001f, Mathf.Abs(parentScaleY)));
            view._flipX = flipX;
            view.ApplyRigScale();
            view.Apply(BoneRig2DProfile.Sample(BoneRigPose2D.Idle, 0f));
            return view;
        }

        public void Apply(BoneRigPoseSample2D sample)
        {
            CurrentSample = sample;
            _poseRoot.localPosition = new Vector3(sample.RootX, sample.RootY, 0f);
            SetRotation(_poseRoot, sample.RootRotation);
            SetRotation(_torso, sample.TorsoRotation);
            SetRotation(_head, sample.HeadRotation);
            SetRotation(_upperArmLeft, sample.UpperArmLeftRotation);
            SetRotation(_forearmLeft, sample.ForearmLeftRotation);
            SetRotation(_upperArmRight, sample.UpperArmRightRotation);
            SetRotation(_forearmRight, sample.ForearmRightRotation);
            SetRotation(_thighLeft, sample.ThighLeftRotation);
            SetRotation(_shinLeft, sample.ShinLeftRotation);
            SetRotation(_thighRight, sample.ThighRightRotation);
            SetRotation(_shinRight, sample.ShinRightRotation);
            SetRotation(_cape, sample.CapeRotation);
            SetRotation(_weapon, sample.WeaponRotation);
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

        private void OnDestroy()
        {
            foreach (Sprite sprite in _ownedSprites)
            {
                if (sprite != null) Destroy(sprite);
            }
        }

        private void Build(
            string resourceRoot,
            string sourceUnitId,
            BoneRigLayout2D layout)
        {
            _poseRoot = CreateBone("root", transform, Vector2.zero);
            Transform hips = CreateBone("hips", _poseRoot, layout.Hips);
            _torso = CreateBone("torso", hips, Vector2.zero);
            _head = CreateBone("head", _torso, layout.Head);
            _upperArmLeft = CreateBone("upper-arm-left", _torso, layout.UpperArmLeft);
            _forearmLeft = CreateBone("forearm-left", _upperArmLeft, layout.ForearmLeft);
            _upperArmRight = CreateBone("upper-arm-right", _torso, layout.UpperArmRight);
            _forearmRight = CreateBone("forearm-right", _upperArmRight, layout.ForearmRight);
            _thighLeft = CreateBone("thigh-left", hips, layout.ThighLeft);
            _shinLeft = CreateBone("shin-left", _thighLeft, layout.ShinLeft);
            _thighRight = CreateBone("thigh-right", hips, layout.ThighRight);
            _shinRight = CreateBone("shin-right", _thighRight, layout.ShinRight);
            Transform capeParent = Attachment(
                layout.CapeAttachment,
                hips,
                _torso,
                _forearmLeft,
                _forearmRight);
            _cape = CreateBone("cape", capeParent, layout.Cape);
            Transform weaponParent = Attachment(
                layout.WeaponAttachment,
                hips,
                _torso,
                _forearmLeft,
                _forearmRight);
            _weapon = CreateBone("weapon", weaponParent, layout.Weapon);

            bool shieldInFront = sourceUnitId == "e_knight";
            if (!shieldInFront)
                AddPart(resourceRoot, "cape", _cape, layout.CapePivot);
            AddPart(resourceRoot, "thigh_right", _thighRight, new Vector2(0.5f, 0.88f));
            AddPart(resourceRoot, "shin_right", _shinRight, new Vector2(0.5f, 0.90f));
            AddPart(resourceRoot, "upper_arm_right", _upperArmRight, layout.UpperArmRightPivot);
            AddPart(resourceRoot, "forearm_right", _forearmRight, new Vector2(0.5f, 0.91f));
            AddPart(resourceRoot, "torso", _torso, layout.TorsoPivot);
            AddPart(resourceRoot, "thigh_left", _thighLeft, new Vector2(0.5f, 0.88f));
            AddPart(resourceRoot, "shin_left", _shinLeft, new Vector2(0.5f, 0.90f));
            AddPart(resourceRoot, "upper_arm_left", _upperArmLeft, layout.UpperArmLeftPivot);
            AddPart(resourceRoot, "forearm_left", _forearmLeft, new Vector2(0.5f, 0.91f));
            AddPart(resourceRoot, "weapon", _weapon, layout.WeaponPivot);
            if (shieldInFront)
                AddPart(resourceRoot, "cape", _cape, layout.CapePivot);
            AddPart(resourceRoot, "head", _head, layout.HeadPivot);
        }

        private void AddPart(
            string resourceRoot,
            string partId,
            Transform bone,
            Vector2 pivot)
        {
            Texture2D texture = Resources.Load<Texture2D>($"{resourceRoot}/{partId}");
            if (texture == null) throw new MissingReferenceException($"Missing bone part {resourceRoot}/{partId}");
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                pivot,
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = partId;
            _ownedSprites.Add(sprite);
            SpriteRenderer renderer = bone.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            _renderers.Add(renderer);
        }

        private void ApplyRigScale()
        {
            float x = _flipX ? -_scale : _scale;
            transform.localScale = new Vector3(x, _scale, 1f);
        }

        private static Transform CreateBone(string name, Transform parent, Vector2 localPosition)
        {
            var boneObject = new GameObject(name);
            Transform bone = boneObject.transform;
            bone.SetParent(parent, false);
            bone.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            return bone;
        }

        private static Transform Attachment(
            BoneAttachment2D attachment,
            Transform hips,
            Transform torso,
            Transform forearmLeft,
            Transform forearmRight)
        {
            switch (attachment)
            {
                case BoneAttachment2D.Torso: return torso;
                case BoneAttachment2D.LeftForearm: return forearmLeft;
                case BoneAttachment2D.RightForearm: return forearmRight;
                default: return hips;
            }
        }

        private static void SetRotation(Transform bone, float degrees)
        {
            bone.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }
    }
}
