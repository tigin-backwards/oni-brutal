using System;
using System.Collections.Generic;
using OniMoono.Assets.TypeTrees;
using OniMoono.Engine.Serialization;
using UnityEngine;

namespace OniMoono.Engine.Mecanim
{
    /// Engine side of an Avatar (class 90): the avatar constant's summary and the stored
    /// HumanDescription.
    internal sealed class AvatarNative : NativeObject
    {
        public bool IsValid;
        public bool IsHuman;
        /// The avatar constant's root motion bone (-1 = none).
        public int RootMotionBoneIndex = -1;
        public HumanDescription Description;

        public override void Bind(Fields data, IReferenceResolver resolver)
        {
            IsValid = Convert.ToUInt32(data["m_AvatarSize"]) > 0;
            var avatar = data.Struct("m_Avatar");
            var humanSkeleton = avatar.Struct("m_Human").Struct("data").Struct("m_Skeleton").Struct("data");
            IsHuman = IsValid && humanSkeleton.Array("m_Node").Length > 0;
            RootMotionBoneIndex = avatar.Int("m_RootMotionBoneIndex");
            Description = ReadDescription(data.Struct("m_HumanDescription"));
            Serialized = null;
        }

        static HumanDescription ReadDescription(Fields f)
        {
            var human = f.Array("m_Human");
            var bones = new HumanBone[human.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var h = (Fields)human[i];
                var l = h.Struct("m_Limit");
                bones[i] = new HumanBone
                {
                    boneName = h.String("m_BoneName"),
                    humanName = h.String("m_HumanName"),
                    limit = new HumanLimit
                    {
                        min = Vector3Of(l.Struct("m_Min")),
                        max = Vector3Of(l.Struct("m_Max")),
                        center = Vector3Of(l.Struct("m_Value")),
                        axisLength = l.Float("m_Length"),
                        useDefaultValues = !l.Bool("m_Modified"),
                    },
                };
            }
            var skeleton = f.Array("m_Skeleton");
            var skel = new SkeletonBone[skeleton.Length];
            for (int i = 0; i < skel.Length; i++)
            {
                var s = (Fields)skeleton[i];
                var q = s.Struct("m_Rotation");
                skel[i] = new SkeletonBone
                {
                    name = s.String("m_Name"),
                    parentName = s.String("m_ParentName"),
                    position = Vector3Of(s.Struct("m_Position")),
                    rotation = new Quaternion(q.Float("x"), q.Float("y"), q.Float("z"), q.Float("w")),
                    scale = Vector3Of(s.Struct("m_Scale")),
                };
            }
            return new HumanDescription
            {
                human = bones,
                skeleton = skel,
                m_ArmTwist = f.Float("m_ArmTwist"),
                m_ForeArmTwist = f.Float("m_ForeArmTwist"),
                m_UpperLegTwist = f.Float("m_UpperLegTwist"),
                m_LegTwist = f.Float("m_LegTwist"),
                m_ArmStretch = f.Float("m_ArmStretch"),
                m_LegStretch = f.Float("m_LegStretch"),
                m_FeetSpacing = f.Float("m_FeetSpacing"),
                m_GlobalScale = f.Float("m_GlobalScale"),
                m_RootMotionBoneName = f.String("m_RootMotionBoneName"),
                m_HasTranslationDoF = f.Bool("m_HasTranslationDoF"),
                m_HasExtraRoot = f.Bool("m_HasExtraRoot"),
                m_SkeletonHasParents = f.Bool("m_SkeletonHasParents"),
            };
        }

        static Vector3 Vector3Of(Fields f) => new(f.Float("x"), f.Float("y"), f.Float("z"));
    }

    /// Engine side of an AvatarMask (class 319): humanoid body parts and transform paths with
    /// weights. A new mask has every body part enabled and no transforms.
    internal sealed class AvatarMaskNative : NativeObject
    {
        public uint[] BodyParts = Filled((int)AvatarMaskBodyPart.LastBodyPart, 1u);
        public List<string> Paths = new();
        public List<float> Weights = new();

        static uint[] Filled(int n, uint value)
        {
            var a = new uint[n];
            Array.Fill(a, value);
            return a;
        }

        public override void Bind(Fields data, IReferenceResolver resolver)
        {
            var mask = data.Get<uint[]>("m_Mask");
            BodyParts = new uint[(int)AvatarMaskBodyPart.LastBodyPart];
            Array.Copy(mask, BodyParts, Math.Min(mask.Length, BodyParts.Length));
            foreach (Fields e in data.Array("m_Elements"))
            {
                Paths.Add(e.String("m_Path"));
                Weights.Add(e.Float("m_Weight"));
            }
            Serialized = null;
        }
    }
}
