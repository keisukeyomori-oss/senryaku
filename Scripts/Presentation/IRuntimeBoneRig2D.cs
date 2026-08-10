using UnityEngine;

namespace BirthdayTactics.Presentation
{
    public interface IRuntimeBoneRig2D
    {
        BoneRigPoseSample2D CurrentSample { get; }
        BoneRigPoseSample2D Sample(BoneRigPose2D pose, float normalizedTime, float phase = 0f);
        void Apply(BoneRigPoseSample2D sample);
        void SetColor(Color color);
        void SetSortingOrder(int bodyOrder);
    }
}
