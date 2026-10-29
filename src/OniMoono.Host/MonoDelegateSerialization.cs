#nullable enable
// Formatter-based serialization: what Mono's delegates write for it is what this reproduces.
#pragma warning disable SYSLIB0050, SYSLIB0051
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D48. Mono's Delegate.GetObjectData (MulticastDelegate's calls it) fills the
    /// SerializationInfo with DelegateSerializationHolder.GetDelegateData (mcs/class/corlib,
    /// Unity's fork): for each delegate of the invocation list a DelegateEntry chained to the one
    /// before it ("Delegate" holds the first), the target as "target<n>" when there is one, and
    /// the method as "method<n>", whose own GetObjectData writes MemberInfoSerializationHolder's
    /// entries. Newtonsoft.Json serializes ISerializable values through GetObjectData, so on Mono
    /// it writes those entries; CoreCLR's throws SerializationException ("Serializing delegates is
    /// not supported on this platform"), and an export mod's JsonConvert.SerializeObject of game
    /// data ended the game (OniExtract, issue #22). A prefix of both GetObjectData methods writes
    /// Mono's entries instead. CoreCLR's RuntimeMethodInfo is not ISerializable (Newtonsoft fails
    /// on it too), so "method<n>" holds a MethodInfo standing for the delegate's method that
    /// writes Mono's entries for it; BinaryFormatter gets back the delegate and the method.
    internal static class MonoDelegateSerialization
    {
        static Harmony? harmony;

        /// Type.FullName and the display name of a type's assembly as the original player gives
        /// them (D1's names: mscorlib for the class libraries, the Unity modules for the engine).
        internal static Func<Type, string> FullName = t => t.FullName ?? t.Name;
        internal static Func<Type, string> AssemblyOf = t => t.Assembly.FullName!;

        public static void Install(Func<Type, string>? fullName = null, Func<Type, string>? assemblyOf = null)
        {
            if (fullName != null) FullName = fullName;
            if (assemblyOf != null) AssemblyOf = assemblyOf;
            if (harmony != null) return;
            harmony = new Harmony("OniMoono.Host.MonoDelegateSerialization");
            var prefix = new HarmonyMethod(typeof(MonoDelegateSerialization), nameof(Prefix));
            foreach (var type in new[] { typeof(Delegate), typeof(MulticastDelegate) })
            {
                var method = type.GetMethod(nameof(ISerializable.GetObjectData), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    [typeof(SerializationInfo), typeof(StreamingContext)]) ?? throw new MissingMethodException(type.FullName, nameof(ISerializable.GetObjectData));
                harmony.Patch(method, prefix: prefix);
            }
        }

        static bool Prefix(Delegate __instance, SerializationInfo info)
        {
            DelegateSerializationHolder.GetDelegateData(__instance, info);
            return false;
        }

        /// The display name of the assembly a type is in, in the original player.
        internal static string AssemblyName(Type type) => AssemblyOf(type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type);
    }

    /// Mono's System.DelegateSerializationHolder: what a delegate's GetObjectData writes, and,
    /// set as the info's type, the object a formatter makes of it to give the delegate back.
    [Serializable]
    internal sealed class DelegateSerializationHolder : ISerializable, IObjectReference
    {
        readonly Delegate? _delegate;

        /// One delegate of the invocation list, with Mono's fields in Mono's order: Newtonsoft
        /// writes the public one, or all of them for a resolver that takes private fields.
        [Serializable]
        sealed class DelegateEntry
        {
#pragma warning disable IDE0044 // as Mono's: not readonly
            string type;
            string assembly;
            object? target;
            string targetTypeAssembly;
            string targetTypeName;
            string methodName;
#pragma warning restore IDE0044
            public DelegateEntry? delegateEntry;

            public DelegateEntry(Delegate del, string? targetLabel)
            {
                var delegateType = del.GetType();
                var method = del.Method;
                type = MonoDelegateSerialization.FullName(delegateType);
                assembly = MonoDelegateSerialization.AssemblyName(delegateType);
                target = targetLabel;
                // As in Mono, a method without a declaring type (a dynamic method) throws here.
                targetTypeAssembly = MonoDelegateSerialization.AssemblyName(method.DeclaringType!);
                targetTypeName = MonoDelegateSerialization.FullName(method.DeclaringType!);
                methodName = method.Name;
            }

            public Delegate DeserializeDelegate(SerializationInfo info, int index)
            {
                object? realTarget = null;
                if (target != null) realTarget = info.GetValue(target.ToString()!, typeof(object));
                var method = ValueOrNull(info, "method" + index) switch
                {
                    SerializedMethod standIn => standIn.Method,
                    MethodInfo m => m,
                    _ => null,
                };
                var dt = Type.GetType(type + ", " + assembly, throwOnError: true)!;
                if (realTarget != null)
                    return method == null ? Delegate.CreateDelegate(dt, realTarget, methodName) : Delegate.CreateDelegate(dt, realTarget, method);
                if (method != null) return Delegate.CreateDelegate(dt, null, method);
                return Delegate.CreateDelegate(dt, Type.GetType(targetTypeName + ", " + targetTypeAssembly, throwOnError: true)!, methodName);
            }
        }

        DelegateSerializationHolder(SerializationInfo info, StreamingContext context)
        {
            var chain = (DelegateEntry?)info.GetValue("Delegate", typeof(DelegateEntry));
            int count = 0;
            for (var entry = chain; entry != null; entry = entry.delegateEntry) count++;
            if (count == 1)
            {
                _delegate = chain!.DeserializeDelegate(info, 0);
                return;
            }
            var delegates = new Delegate[count];
            var e = chain;
            for (int n = 0; n < count; n++)
            {
                delegates[n] = e!.DeserializeDelegate(info, n);
                e = e.delegateEntry;
            }
            _delegate = Delegate.Combine(delegates);
        }

        public static void GetDelegateData(Delegate instance, SerializationInfo info)
        {
            var delegates = instance.GetInvocationList();
            DelegateEntry? lastEntry = null;
            for (int n = 0; n < delegates.Length; n++)
            {
                var del = delegates[n];
                string? targetLabel = del.Target != null ? "target" + n : null;
                var entry = new DelegateEntry(del, targetLabel);
                if (lastEntry == null) info.AddValue("Delegate", entry);
                else lastEntry.delegateEntry = entry;
                lastEntry = entry;
                if (del.Target != null) info.AddValue(targetLabel!, del.Target);
                info.AddValue("method" + n, new SerializedMethod(del.Method));
            }
            info.SetType(typeof(DelegateSerializationHolder));
        }

        static object? ValueOrNull(SerializationInfo info, string name)
        {
            foreach (var entry in info)
                if (entry.Name == name) return entry.Value;
            return null;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context) => throw new NotSupportedException();

        public object GetRealObject(StreamingContext context) => _delegate!;
    }

    /// A delegate's method in its SerializationInfo: the method itself to reflection, and to a
    /// formatter what Mono's RuntimeMethodInfo.GetObjectData writes (MemberInfoSerializationHolder's
    /// entries), which formatters give back as the method.
    [Serializable]
    internal sealed class SerializedMethod : MethodInfo, ISerializable
    {
        public readonly MethodInfo Method;

        public SerializedMethod(MethodInfo method) => Method = method;

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ArgumentNullException.ThrowIfNull(info);
            var reflected = Method.ReflectedType ?? Method.DeclaringType!;
            info.SetType(typeof(SerializedMemberHolder));
            info.AddValue("Name", Method.Name, typeof(string));
            info.AddValue("AssemblyName", MonoDelegateSerialization.AssemblyName(reflected), typeof(string));
            info.AddValue("ClassName", MonoDelegateSerialization.FullName(reflected), typeof(string));
            info.AddValue("Signature", MonoMethodSignatures.Basic(Method), typeof(string));
            info.AddValue("Signature2", MonoMethodSignatures.Serialization(Method), typeof(string));
            info.AddValue("MemberType", (int)MemberTypes.Method);
            // Mono's are Types, which Newtonsoft writes as their assembly-qualified names, with
            // mscorlib's: those names, which BinaryFormatter takes too (CoreCLR's Type is not
            // serializable).
            info.AddValue("GenericArguments", Method.IsGenericMethod && !Method.IsGenericMethodDefinition
                ? Method.GetGenericArguments().Select(t => MonoDelegateSerialization.FullName(t) + ", " + MonoDelegateSerialization.AssemblyName(t)).ToArray()
                : null, typeof(string[]));
        }

        public override string ToString() => Method.ToString()!;
        public override bool Equals(object? obj) => obj is SerializedMethod s ? s.Method.Equals(Method) : Method.Equals(obj);
        public override int GetHashCode() => Method.GetHashCode();
        public override string Name => Method.Name;
        public override Type? DeclaringType => Method.DeclaringType;
        public override Type? ReflectedType => Method.ReflectedType;
        public override Module Module => Method.Module;
        public override int MetadataToken => Method.MetadataToken;
        public override MethodAttributes Attributes => Method.Attributes;
        public override RuntimeMethodHandle MethodHandle => Method.MethodHandle;
        public override CallingConventions CallingConvention => Method.CallingConvention;
        public override Type ReturnType => Method.ReturnType;
        public override ParameterInfo ReturnParameter => Method.ReturnParameter;
        public override ICustomAttributeProvider ReturnTypeCustomAttributes => Method.ReturnTypeCustomAttributes;
        public override bool IsGenericMethod => Method.IsGenericMethod;
        public override bool IsGenericMethodDefinition => Method.IsGenericMethodDefinition;
        public override bool ContainsGenericParameters => Method.ContainsGenericParameters;
        public override Type[] GetGenericArguments() => Method.GetGenericArguments();
        public override MethodInfo GetGenericMethodDefinition() => Method.GetGenericMethodDefinition();
        public override MethodInfo MakeGenericMethod(params Type[] typeArguments) => Method.MakeGenericMethod(typeArguments);
        public override MethodInfo GetBaseDefinition() => Method.GetBaseDefinition();
        public override ParameterInfo[] GetParameters() => Method.GetParameters();
        public override MethodImplAttributes GetMethodImplementationFlags() => Method.GetMethodImplementationFlags();
        public override MethodBody? GetMethodBody() => Method.GetMethodBody();
        public override object[] GetCustomAttributes(bool inherit) => Method.GetCustomAttributes(inherit);
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Method.GetCustomAttributes(attributeType, inherit);
        public override bool IsDefined(Type attributeType, bool inherit) => Method.IsDefined(attributeType, inherit);
        public override Delegate CreateDelegate(Type delegateType) => Method.CreateDelegate(delegateType);
        public override Delegate CreateDelegate(Type delegateType, object? target) => Method.CreateDelegate(delegateType, target);
        public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) =>
            Method.Invoke(obj, invokeAttr, binder, parameters, culture);
    }

    /// Mono's MemberInfoSerializationHolder for a method: what a formatter makes of
    /// SerializedMethod's entries, giving back the method they name.
    [Serializable]
    internal sealed class SerializedMemberHolder : ISerializable, IObjectReference
    {
        readonly MethodInfo method;

        SerializedMemberHolder(SerializationInfo info, StreamingContext context)
        {
            string name = info.GetString("Name")!, className = info.GetString("ClassName")!, assemblyName = info.GetString("AssemblyName")!;
            string signature = info.GetString("Signature")!, signature2 = info.GetString("Signature2")!;
            var genericArguments = ((string[]?)info.GetValue("GenericArguments", typeof(string[])))?.Select(n => Type.GetType(n, throwOnError: true)!).ToArray();
            var type = Type.GetType(className + ", " + assemblyName, throwOnError: true)!;
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var candidates = type.GetMethods(all).Where(m => m.Name == name)
                .Select(m => genericArguments != null && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == genericArguments.Length ? m.MakeGenericMethod(genericArguments) : m)
                .ToArray();
            var found = candidates.FirstOrDefault(m => MonoMethodSignatures.Serialization(m) == signature2)
                ?? candidates.FirstOrDefault(m => MonoMethodSignatures.Basic(m) == signature)
                ?? throw new SerializationException($"Cannot get the member '{name}'.");
            method = found;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context) => throw new NotSupportedException();

        public object GetRealObject(StreamingContext context) => method;
    }

    /// A method's signature as Mono prints it: MethodInfo.ToString() ("Signature") and its
    /// SerializationToString() ("Signature2").
    internal static class MonoMethodSignatures
    {
        public static string Basic(MethodInfo method) => method.ToString()!;

        /// The return type, name and parameter types, each as its ToString() prints it (Mono's
        /// FormatTypeName(true): TypeNameKind.SerializationName), by-ref ones with their '&', and
        /// the type arguments as MethodInfo.ToString() prints them ("Same[Int32]", measured).
        public static string Serialization(MethodInfo method)
        {
            var sb = new StringBuilder();
            sb.Append(method.ReturnType.ToString()).Append(' ').Append(method.Name);
            if (method.IsGenericMethod) sb.Append('[').Append(string.Join(",", method.GetGenericArguments().Select(BasicTypeName))).Append(']');
            sb.Append('(').Append(string.Join(", ", method.GetParameters().Select(p => p.ParameterType.ToString())));
            if ((method.CallingConvention & CallingConventions.VarArgs) != 0) sb.Append(method.GetParameters().Length > 0 ? ", ..." : "...");
            return sb.Append(')').ToString();
        }

        /// Mono's FormatTypeName(false): a nested type by its name, a primitive's without
        /// "System.", any other type as ToString() prints it.
        static string BasicTypeName(Type type)
        {
            var root = type;
            while (root.HasElementType) root = root.GetElementType()!;
            if (root.IsNested) return type.Name;
            var name = type.ToString();
            return root.IsPrimitive || root == typeof(void) || root == typeof(TypedReference) ? name.Substring("System.".Length) : name;
        }
    }
}
