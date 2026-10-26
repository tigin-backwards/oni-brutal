using System;
using OniMoono.Engine;
using OniMoono.Engine.Mecanim;

namespace UnityEngine
{
    public partial class Avatar
    {
        private Avatar() { }

        internal AvatarNative AvatarData => (AvatarNative)NativeOrThrow;

        /// The avatar was built (has an avatar constant).
        public bool isValid => AvatarData.IsValid;

        /// The avatar has a human skeleton (a humanoid rig).
        public bool isHuman => AvatarData.IsHuman;

        public HumanDescription humanDescription => AvatarData.Description.Copy();
    }

    public partial class AvatarMask
    {
        public AvatarMask() => Bind(new AvatarMaskNative());

        internal AvatarMaskNative Mask => (AvatarMaskNative)NativeOrThrow;

        public int transformCount
        {
            get => Mask.Paths.Count;
            set
            {
                var m = Mask;
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                while (m.Paths.Count > value)
                {
                    m.Paths.RemoveAt(m.Paths.Count - 1);
                    m.Weights.RemoveAt(m.Weights.Count - 1);
                }
                while (m.Paths.Count < value)
                {
                    m.Paths.Add("");
                    m.Weights.Add(0f);
                }
            }
        }

        public bool GetHumanoidBodyPartActive(AvatarMaskBodyPart index)
        {
            var m = Mask;
            if ((uint)index >= (uint)AvatarMaskBodyPart.LastBodyPart) throw new IndexOutOfRangeException("Index out of bounds.");
            return m.BodyParts[(int)index] != 0;
        }

        public void SetHumanoidBodyPartActive(AvatarMaskBodyPart index, bool value)
        {
            var m = Mask;
            if ((uint)index >= (uint)AvatarMaskBodyPart.LastBodyPart) throw new IndexOutOfRangeException("Index out of bounds.");
            m.BodyParts[(int)index] = value ? 1u : 0u;
        }

        public string GetTransformPath(int index) => Mask.Paths[CheckIndex(index)];

        public void SetTransformPath(int index, string path) => Mask.Paths[CheckIndex(index)] = path ?? "";

        public bool GetTransformActive(int index) => GetTransformWeight(index) > 0.5f;

        public void SetTransformActive(int index, bool value) => SetTransformWeight(index, value ? 1f : 0f);

        private float GetTransformWeight(int index) => Mask.Weights[CheckIndex(index)];

        private void SetTransformWeight(int index, float weight) => Mask.Weights[CheckIndex(index)] = weight;

        int CheckIndex(int index)
        {
            if ((uint)index >= (uint)Mask.Paths.Count) throw new IndexOutOfRangeException("Index out of bounds.");
            return index;
        }
    }

    public partial struct HumanDescription
    {
        public HumanBone[] human;
        public SkeletonBone[] skeleton;

        internal float m_ArmTwist;
        internal float m_ForeArmTwist;
        internal float m_UpperLegTwist;
        internal float m_LegTwist;
        internal float m_ArmStretch;
        internal float m_LegStretch;
        internal float m_FeetSpacing;
        internal float m_GlobalScale;
        internal string m_RootMotionBoneName;
        internal bool m_HasTranslationDoF;
        internal bool m_HasExtraRoot;
        internal bool m_SkeletonHasParents;

        public float upperArmTwist { get => m_ArmTwist; set => m_ArmTwist = value; }
        public float lowerArmTwist { get => m_ForeArmTwist; set => m_ForeArmTwist = value; }
        public float upperLegTwist { get => m_UpperLegTwist; set => m_UpperLegTwist = value; }
        public float lowerLegTwist { get => m_LegTwist; set => m_LegTwist = value; }
        public float armStretch { get => m_ArmStretch; set => m_ArmStretch = value; }
        public float legStretch { get => m_LegStretch; set => m_LegStretch = value; }
        public float feetSpacing { get => m_FeetSpacing; set => m_FeetSpacing = value; }
        public bool hasTranslationDoF { get => m_HasTranslationDoF; set => m_HasTranslationDoF = value; }

        /// A copy with its own arrays, as the native getter returns.
        internal HumanDescription Copy()
        {
            var d = this;
            d.human = (HumanBone[])human?.Clone() ?? Array.Empty<HumanBone>();
            d.skeleton = (SkeletonBone[])skeleton?.Clone() ?? Array.Empty<SkeletonBone>();
            return d;
        }
    }

    public partial struct HumanBone
    {
        private string m_BoneName;
        private string m_HumanName;
        public HumanLimit limit;

        public string boneName { get => m_BoneName; set => m_BoneName = value; }
        public string humanName { get => m_HumanName; set => m_HumanName = value; }
    }

    public partial struct SkeletonBone
    {
        public string name;
        internal string parentName;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        /// Unity keeps this obsolete member as a constant 0 with an ignored setter.
        public int transformModified
        {
            get => 0;
            set { }
        }
    }

    public partial struct HumanLimit
    {
        private Vector3 m_Min;
        private Vector3 m_Max;
        private Vector3 m_Center;
        private float m_AxisLength;
        private int m_UseDefaultValues;

        public bool useDefaultValues { get => m_UseDefaultValues != 0; set => m_UseDefaultValues = value ? 1 : 0; }
        public Vector3 min { get => m_Min; set => m_Min = value; }
        public Vector3 max { get => m_Max; set => m_Max = value; }
        public Vector3 center { get => m_Center; set => m_Center = value; }
        public float axisLength { get => m_AxisLength; set => m_AxisLength = value; }
    }
}
