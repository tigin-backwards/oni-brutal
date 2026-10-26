using System;
using System.Collections.Generic;
using System.IO;
using OniMoono.Assets.TypeTrees;
using OniMoono.Engine.Serialization;
using UnityEngine;

namespace OniMoono.Engine.Mecanim
{
    /// Unity's AnimatorControllerParameterType values (mecanim value types).
    internal enum ParameterType
    {
        Float = 1,
        Int = 3,
        Bool = 4,
        Trigger = 9,
    }

    /// Mecanim condition modes.
    internal enum ConditionMode
    {
        If = 1,
        IfNot = 2,
        Greater = 3,
        Less = 4,
        ExitTime = 5,
        Equals = 6,
        NotEqual = 7,
    }

    /// Which states' transitions may interrupt an active transition.
    internal enum InterruptionSource
    {
        None = 0,
        Source = 1,
        Destination = 2,
        SourceThenDestination = 3,
        DestinationThenSource = 4,
    }

    internal sealed class ParameterData
    {
        public uint Id;
        public ParameterType Type;
        public float DefaultFloat;
        public int DefaultInt;
        public bool DefaultBool;
    }

    internal readonly record struct ConditionData(ConditionMode Mode, uint EventId, float Threshold, float ExitTime);

    internal sealed class TransitionData
    {
        public ConditionData[] Conditions;
        /// A state index, or SelectorFlag + selector index (entry/exit nodes); uint.MaxValue = none.
        public uint Destination;
        public uint FullPathId, Id, UserId;
        public float Duration, Offset, ExitTime;
        public bool HasExitTime, HasFixedDuration, OrderedInterruption, CanTransitionToSelf;
        public InterruptionSource Interruption;

        public const uint SelectorFlag = 30000;
    }

    internal sealed class StateData
    {
        public TransitionData[] Transitions;
        /// The clip played by this state for each motion set (layer); -1 = no motion.
        public int[] ClipByMotionSet;
        public uint NameId, PathId, FullPathId, TagId;
        public uint SpeedParamId, CycleOffsetParamId, TimeParamId;
        public float Speed, CycleOffset;
        public bool WriteDefaultValues, Loop;
    }

    internal sealed class SelectorTransitionData
    {
        public uint Destination;
        public ConditionData[] Conditions;
    }

    internal sealed class SelectorData
    {
        public SelectorTransitionData[] Transitions;
        public uint FullPathId;
        public bool IsEntry;
    }

    internal sealed class StateMachineData
    {
        public StateData[] States;
        public TransitionData[] AnyStateTransitions;
        public SelectorData[] Selectors;
        public int DefaultState;
    }

    internal sealed class LayerData
    {
        public int StateMachineIndex;
        public int MotionSetIndex;
        public uint NameHash;
        public bool Additive;
        public float DefaultWeight;
    }

    /// A player-build AnimatorController: m_Controller (ControllerConstant), the hash → name
    /// table (m_TOS) and the clips its states reference by index.
    internal sealed class ControllerData
    {
        public LayerData[] Layers;
        public StateMachineData[] StateMachines;
        public ParameterData[] Parameters;
        public Dictionary<uint, string> Names;
        public AnimationClip[] Clips;
        readonly Dictionary<uint, int> parameterIndex = new();

        public int ParameterIndex(uint id) => parameterIndex.TryGetValue(id, out var i) ? i : -1;

        public string NameOf(uint hash) => Names.TryGetValue(hash, out var s) ? s : null;

        public static ControllerData Read(Fields f, IReferenceResolver resolver, string controllerName)
        {
            var d = new ControllerData();
            var clipRefs = f.Array("m_AnimationClips");
            d.Clips = new AnimationClip[clipRefs.Length];
            for (int i = 0; i < clipRefs.Length; i++) d.Clips[i] = resolver.Resolve((PPtr)clipRefs[i]) as AnimationClip;

            d.Names = new Dictionary<uint, string>();
            foreach (Fields pair in f.Array("m_TOS")) d.Names[Convert.ToUInt32(pair["first"])] = pair.String("second");

            if (f.Array("m_StateMachineBehaviours").Length > 0)
                throw new InvalidDataException($"AnimatorController {controllerName}: StateMachineBehaviours are not implemented.");

            var c = f.Struct("m_Controller");
            var layers = c.Array("m_LayerArray");
            d.Layers = new LayerData[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var l = Data(layers[i]);
                if (l.Struct("m_SkeletonMask").Struct("data").Array("m_Data").Length > 0)
                    throw new InvalidDataException($"AnimatorController {controllerName}: layer masks are not implemented.");
                d.Layers[i] = new LayerData
                {
                    StateMachineIndex = Convert.ToInt32(l["m_StateMachineIndex"]),
                    MotionSetIndex = Convert.ToInt32(l["m_StateMachineSynchronizedLayerIndex"]),
                    NameHash = Convert.ToUInt32(l["m_Binding"]),
                    Additive = Convert.ToInt32(l["(int&)m_LayerBlendingMode"]) == 1,
                    DefaultWeight = l.Float("m_DefaultWeight"),
                };
                if (d.Layers[i].MotionSetIndex != 0)
                    throw new InvalidDataException($"AnimatorController {controllerName}: synchronized layers are not implemented.");
            }

            var machines = c.Array("m_StateMachineArray");
            d.StateMachines = new StateMachineData[machines.Length];
            for (int i = 0; i < machines.Length; i++) d.StateMachines[i] = ReadStateMachine(Data(machines[i]), d.Clips.Length, controllerName);

            var values = Data(c["m_Values"]).Array("m_ValueArray");
            var defaults = Data(c["m_DefaultValues"]);
            var floats = defaults.Get<float[]>("m_FloatValues");
            var ints = defaults.Get<int[]>("m_IntValues");
            var bools = defaults.Array("m_BoolValues");
            d.Parameters = new ParameterData[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                var v = (Fields)values[i];
                var p = new ParameterData { Id = Convert.ToUInt32(v["m_ID"]), Type = (ParameterType)Convert.ToInt32(v["m_Type"]) };
                int index = Convert.ToInt32(v["m_Index"]);
                switch (p.Type)
                {
                    case ParameterType.Float: p.DefaultFloat = floats[index]; break;
                    case ParameterType.Int: p.DefaultInt = ints[index]; break;
                    case ParameterType.Bool:
                    case ParameterType.Trigger: p.DefaultBool = (bool)bools[index]; break;
                    default: throw new InvalidDataException($"AnimatorController {controllerName}: parameter type {(int)p.Type} is not supported.");
                }
                d.Parameters[i] = p;
                d.parameterIndex.TryAdd(p.Id, i);
            }
            return d;
        }

        /// OffsetPtr wrappers serialize their target under "data".
        static Fields Data(object o) => ((Fields)o).Struct("data");

        static StateMachineData ReadStateMachine(Fields m, int clipCount, string controllerName)
        {
            var sm = new StateMachineData { DefaultState = Convert.ToInt32(m["m_DefaultState"]) };
            var states = m.Array("m_StateConstantArray");
            sm.States = new StateData[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                var s = Data(states[i]);
                var state = new StateData
                {
                    Transitions = ReadTransitions(s.Array("m_TransitionConstantArray")),
                    NameId = Convert.ToUInt32(s["m_NameID"]),
                    PathId = Convert.ToUInt32(s["m_PathID"]),
                    FullPathId = Convert.ToUInt32(s["m_FullPathID"]),
                    TagId = Convert.ToUInt32(s["m_TagID"]),
                    SpeedParamId = Convert.ToUInt32(s["m_SpeedParamID"]),
                    CycleOffsetParamId = Convert.ToUInt32(s["m_CycleOffsetParamID"]),
                    TimeParamId = Convert.ToUInt32(s["m_TimeParamID"]),
                    Speed = s.Float("m_Speed"),
                    CycleOffset = s.Float("m_CycleOffset"),
                    WriteDefaultValues = s.Bool("m_WriteDefaultValues"),
                    Loop = s.Bool("m_Loop"),
                };
                if (s.Bool("m_Mirror") || s.Bool("m_IKOnFeet") || Convert.ToUInt32(s["m_MirrorParamID"]) != 0)
                    throw new InvalidDataException($"AnimatorController {controllerName}: humanoid state options (mirror, foot IK) are not implemented.");
                var treeIndices = s.Get<int[]>("m_BlendTreeConstantIndexArray");
                var trees = s.Array("m_BlendTreeConstantArray");
                state.ClipByMotionSet = new int[treeIndices.Length];
                for (int set = 0; set < treeIndices.Length; set++)
                {
                    int tree = treeIndices[set];
                    state.ClipByMotionSet[set] = -1;
                    if (tree < 0) continue;
                    var nodes = Data(trees[tree]).Array("m_NodeArray");
                    if (nodes.Length != 1 || Data(nodes[0]).Get<uint[]>("m_ChildIndices").Length != 0)
                        throw new InvalidDataException($"AnimatorController {controllerName}: blend trees are not implemented.");
                    uint clip = Convert.ToUInt32(Data(nodes[0])["m_ClipID"]);
                    if (clip != uint.MaxValue)
                    {
                        if (clip >= clipCount) throw new InvalidDataException($"AnimatorController {controllerName}: clip index {clip} of {clipCount}.");
                        state.ClipByMotionSet[set] = (int)clip;
                    }
                }
                sm.States[i] = state;
            }
            sm.AnyStateTransitions = ReadTransitions(m.Array("m_AnyStateTransitionConstantArray"));
            var selectors = m.Array("m_SelectorStateConstantArray");
            sm.Selectors = new SelectorData[selectors.Length];
            for (int i = 0; i < selectors.Length; i++)
            {
                var s = Data(selectors[i]);
                var transitions = s.Array("m_TransitionConstantArray");
                var sel = new SelectorData
                {
                    FullPathId = Convert.ToUInt32(s["m_FullPathID"]),
                    IsEntry = s.Bool("m_IsEntry"),
                    Transitions = new SelectorTransitionData[transitions.Length],
                };
                for (int t = 0; t < transitions.Length; t++)
                {
                    var tr = Data(transitions[t]);
                    sel.Transitions[t] = new SelectorTransitionData
                    {
                        Destination = Convert.ToUInt32(tr["m_Destination"]),
                        Conditions = ReadConditions(tr.Array("m_ConditionConstantArray")),
                    };
                }
                sm.Selectors[i] = sel;
            }
            return sm;
        }

        static TransitionData[] ReadTransitions(object[] array)
        {
            var result = new TransitionData[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                var t = Data(array[i]);
                result[i] = new TransitionData
                {
                    Conditions = ReadConditions(t.Array("m_ConditionConstantArray")),
                    Destination = Convert.ToUInt32(t["m_DestinationState"]),
                    FullPathId = Convert.ToUInt32(t["m_FullPathID"]),
                    Id = Convert.ToUInt32(t["m_ID"]),
                    UserId = Convert.ToUInt32(t["m_UserID"]),
                    Duration = t.Float("m_TransitionDuration"),
                    Offset = t.Float("m_TransitionOffset"),
                    ExitTime = t.Float("m_ExitTime"),
                    HasExitTime = t.Bool("m_HasExitTime"),
                    HasFixedDuration = t.Bool("m_HasFixedDuration"),
                    Interruption = (InterruptionSource)Convert.ToInt32(t["m_InterruptionSource"]),
                    OrderedInterruption = t.Bool("m_OrderedInterruption"),
                    CanTransitionToSelf = t.Bool("m_CanTransitionToSelf"),
                };
            }
            return result;
        }

        static ConditionData[] ReadConditions(object[] array)
        {
            var result = new ConditionData[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                var c = Data(array[i]);
                result[i] = new ConditionData((ConditionMode)Convert.ToInt32(c["m_ConditionMode"]), Convert.ToUInt32(c["m_EventID"]),
                    c.Float("m_EventThreshold"), c.Float("m_ExitTime"));
            }
            return result;
        }
    }

    /// Engine side of a RuntimeAnimatorController loaded from an AnimatorController asset.
    internal sealed class AnimatorControllerNative : NativeObject
    {
        public ControllerData Data;

        public override void Bind(Fields data, IReferenceResolver resolver)
        {
            Data = ControllerData.Read(data, resolver, Name);
            Serialized = null;
        }
    }
}
