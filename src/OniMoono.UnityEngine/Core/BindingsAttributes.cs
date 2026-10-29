using System;

// Unity's binding attributes (internal to its modules). Nothing in the engine reads them: they
// exist so that mods which merge copies of Unity's modules into their own assembly (Twitch
// Integration carries UnityEngine.InputLegacyModule and TextRenderingModule) resolve the
// attributes on those copies, as they do against Unity's modules, when reflection reads them
// (Harmony's PatchAll reads every type's). Their constructors are Unity's, and keep what they are
// given.

namespace UnityEngine.Bindings
{
    internal partial class NativeMethodAttribute
    {
        public string Name { get; set; }
        public bool IsThreadSafe { get; set; }
        public bool IsFreeFunction { get; set; }
        public bool ThrowsException { get; set; }
        public bool HasExplicitThis { get; set; }

        public NativeMethodAttribute() { }
        public NativeMethodAttribute(string name) { Name = name; }
        public NativeMethodAttribute(string name, bool isFreeFunction) : this(name) { IsFreeFunction = isFreeFunction; }
        public NativeMethodAttribute(string name, bool isFreeFunction, bool isThreadSafe) : this(name, isFreeFunction) { IsThreadSafe = isThreadSafe; }
        public NativeMethodAttribute(string name, bool isFreeFunction, bool isThreadSafe, bool throws) : this(name, isFreeFunction, isThreadSafe) { ThrowsException = throws; }
    }

    internal partial class FreeFunctionAttribute
    {
        public FreeFunctionAttribute() { IsFreeFunction = true; }
        public FreeFunctionAttribute(string name) : base(name, true) { }
        public FreeFunctionAttribute(string name, bool isThreadSafe) : base(name, true, isThreadSafe) { }
    }

    internal partial class NativePropertyAttribute
    {
        public TargetType TargetType { get; set; }

        public NativePropertyAttribute() { }
        public NativePropertyAttribute(string name) : base(name) { }
        public NativePropertyAttribute(string name, TargetType targetType) : base(name) { TargetType = targetType; }
        public NativePropertyAttribute(string name, bool isFree, TargetType targetType) : base(name, isFree) { TargetType = targetType; }
        public NativePropertyAttribute(string name, bool isFree, TargetType targetType, bool isThreadSafe) : base(name, isFree, isThreadSafe) { TargetType = targetType; }
    }

    internal partial class ThreadSafeAttribute
    {
        public ThreadSafeAttribute() { IsThreadSafe = true; }
    }

    internal partial class NativeConditionalAttribute
    {
        public string Condition { get; set; }
        public string StubReturnStatement { get; set; }
        public bool Enabled { get; set; }

        public NativeConditionalAttribute() { }
        public NativeConditionalAttribute(string condition) { Condition = condition; Enabled = true; }
        public NativeConditionalAttribute(bool enabled) { Enabled = enabled; }
        public NativeConditionalAttribute(string condition, bool enabled) : this(condition) { Enabled = enabled; }
        public NativeConditionalAttribute(string condition, string stubReturnStatement, bool enabled) : this(condition, enabled) { StubReturnStatement = stubReturnStatement; }
        public NativeConditionalAttribute(string condition, string stubReturnStatement) : this(condition, stubReturnStatement, true) { }
    }

    internal partial class NativeHeaderAttribute
    {
        public string Header { get; set; }

        public NativeHeaderAttribute() { }
        public NativeHeaderAttribute(string header) { Header = header; }
    }

    internal partial class NativeNameAttribute
    {
        public string Name { get; set; }

        public NativeNameAttribute() { }
        public NativeNameAttribute(string name) { Name = name; }
    }

    internal partial class NativeThrowsAttribute
    {
        public bool ThrowsException { get; set; }

        public NativeThrowsAttribute() { ThrowsException = true; }
        public NativeThrowsAttribute(bool throwsException) { ThrowsException = throwsException; }
    }

    internal partial class StaticAccessorAttribute
    {
        public string Name { get; set; }
        public StaticAccessorType Type { get; set; }

        public StaticAccessorAttribute() { }
        internal StaticAccessorAttribute(string name) { Name = name; }
        public StaticAccessorAttribute(StaticAccessorType type) { Type = type; }
        public StaticAccessorAttribute(string name, StaticAccessorType type) { Name = name; Type = type; }
    }

    internal partial class UnityMarshalAsAttribute
    {
        public NativeType NativeType { get; }
        public Type CustomMarshaller { get; set; }

        public UnityMarshalAsAttribute(NativeType nativeType) { NativeType = nativeType; }
    }

    internal partial class VisibleToOtherModulesAttribute
    {
        public VisibleToOtherModulesAttribute() { }
        public VisibleToOtherModulesAttribute(params string[] modules) { }
    }
}

namespace UnityEngine
{
    internal sealed partial class NativeClassAttribute
    {
        public string QualifiedNativeName { get; private set; }
        public string Declaration { get; private set; }

        public NativeClassAttribute(string qualifiedCppName) { QualifiedNativeName = qualifiedCppName; Declaration = "class " + qualifiedCppName; }
        public NativeClassAttribute(string qualifiedCppName, string declaration) { QualifiedNativeName = qualifiedCppName; Declaration = declaration; }
    }
}

namespace UnityEngine.Scripting
{
    internal partial class RequiredByNativeCodeAttribute
    {
        public string Name { get; set; }
        public bool Optional { get; set; }
        public bool GenerateProxy { get; set; }

        public RequiredByNativeCodeAttribute() { }
        public RequiredByNativeCodeAttribute(string name) { Name = name; }
        public RequiredByNativeCodeAttribute(bool optional) { Optional = optional; }
        public RequiredByNativeCodeAttribute(string name, bool optional) { Name = name; Optional = optional; }
    }

    internal partial class UsedByNativeCodeAttribute
    {
        public string Name { get; set; }

        public UsedByNativeCodeAttribute() { }
        public UsedByNativeCodeAttribute(string name) { Name = name; }
    }
}

namespace UnityEngine.Internal
{
    public partial class DefaultValueAttribute
    {
        private readonly object DefaultValue;

        public DefaultValueAttribute(string value) { DefaultValue = value; }

        public object Value => DefaultValue;

        public override bool Equals(object obj) =>
            obj is DefaultValueAttribute other && (DefaultValue == null ? other.Value == null : DefaultValue.Equals(other.Value));

        public override int GetHashCode() => DefaultValue == null ? base.GetHashCode() : DefaultValue.GetHashCode();
    }
}
