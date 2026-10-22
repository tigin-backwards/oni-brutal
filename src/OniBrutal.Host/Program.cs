using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            var options = Options.Parse(args);
            if (options == null) return 2;
            // For the Restarter next to the host (App.Restart): the arguments to start again with
            // (shell-quoted for the Linux script; as a JSON array with the process id for the
            // Windows one) and the process to wait for.
            Environment.SetEnvironmentVariable("ONI_BRUTAL_RESTART_ARGS", string.Join(" ", args.Select(a => "'" + a.Replace("'", "'\\''") + "'")));
            Environment.SetEnvironmentVariable("ONI_BRUTAL_RESTART_ARGV", System.Text.Json.JsonSerializer.Serialize(args));
            Environment.SetEnvironmentVariable("ONI_BRUTAL_RESTART_PID", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            var dataDir = Path.Combine(options.GameDir, "OxygenNotIncluded_Data");
            if (!Directory.Exists(dataDir))
            {
                Console.Error.WriteLine($"Game data not found: {dataDir}");
                return 2;
            }

            // Code pages (IBM437 etc.), which Mono provided through I18N.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Paths.Configure(dataDir, options);
            EngineLog.Open(options.LogPath ?? EngineConfig.ConsoleLogPath);
            EngineLog.Engine($"OniBrutal host starting: game={options.GameDir} .NET={Environment.Version}");
            UnhandledExceptions.Install();

            AssemblyPolicy.Install(dataDir, AppContext.BaseDirectory, options.UiPackages, options.ShippedUiPackages);
            var assemblies = AssemblyPolicy.PreloadScriptingAssemblies(dataDir);
            EngineLog.Engine($"Preloaded {assemblies.Count} scripting assemblies");

            if (options.CheckTypes) return CheckTypes(assemblies);
            if (options.CheckAssets) return AssetCheck.Run(dataDir, options.ListDataScripts);
            if (options.CheckBind) return BindCheck.Run(dataDir);
            if (options.CheckShaders) return ShaderCheck.Run(dataDir);
            if (options.DumpShader != null) return ShaderDump.Run(dataDir, options.DumpShader);
            if (options.DumpScript != null) return AssetCheck.Dump(dataDir, options.DumpScript);

            if (options.TraceExceptions) ExceptionTrace.Install();
            if (options.RunHome != null) RunHome.Install(options.RunHome);
            if (options.AudioWav != null) AudioCapture.Install(options.AudioWav);
            ModVersionStamp.Install();
            ModAccess.Install();
            TranspiledCalls.Install();
            TranspiledTryEntries.Install();
            UnboxedMethods.Install();
            SharedGenerics.Install();
            PatchFrames.Install();
            PInvokePatches.Install();
            PatchDependents.Install();
            MonoEnumFormat.Install();
            GameTweaks.GameTweaks.Install(options.GameTweaks);
            StateMachineSaveNames.Install(dataDir, AppContext.BaseDirectory);
            return Player.Run(options, dataDir);
        }

        /// M0 gate: every type of every loaded assembly must load, as the game's whole-domain
        /// GetTypes() scans require.
        static int CheckTypes(List<Assembly> assemblies)
        {
            int failures = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = asm.GetTypes();
                    Console.WriteLine($"ok   {asm.GetName().Name,-45} {types.Length,6} types");
                }
                catch (ReflectionTypeLoadException e)
                {
                    failures++;
                    Console.WriteLine($"FAIL {asm.GetName().Name}");
                    foreach (var le in e.LoaderExceptions)
                        if (le != null) Console.WriteLine("       " + le.Message);
                }
            }
            Console.WriteLine(failures == 0 ? "All assemblies type-load." : $"{failures} assemblies failed to type-load.");

            // Where the replaced assemblies came from.
            foreach (var name in new[] { "UnityEngine.CoreModule", "OniBrutal.UnityEngine", "com.rlabrecque.steamworks.net", "mscorlib" })
                Console.WriteLine($"from {name,-35} {Assembly.Load(name).Location}");

            failures += CheckRuntimeServices();
            return failures == 0 ? 0 : 1;
        }

        /// CheckRuntimeServices' patch of a value type's GetHashCode.
        static void PatchedHash(ref int __result) => __result = -1;

        /// The services Mono provided in-box, exercised through the game's own code paths
        /// (ARCHITECTURE §2.1): Newtonsoft.Json (save header), BinaryFormatter (worldgen settings
        /// cloning), code pages (Ionic.Zip).
        static int CheckRuntimeServices()
        {
            int failures = 0;
            void Check(string what, Func<string> test)
            {
                try
                {
                    Console.WriteLine($"ok   {what}: {test()}");
                }
                catch (Exception e)
                {
                    failures++;
                    Console.WriteLine($"FAIL {what}: {(e is TargetInvocationException t ? t.InnerException : e)}");
                }
            }

            Check("Newtonsoft.Json round trip", () =>
            {
                var convert = Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert", true)!;
                var json = (string)convert.GetMethod("SerializeObject", [typeof(object)])!.Invoke(null, [new Dictionary<string, int> { ["cycle"] = 42 }])!;
                var back = convert.GetMethods().First(m => m.Name == "DeserializeObject" && m.IsGenericMethod && m.GetParameters().Length == 1)
                    .MakeGenericMethod(typeof(Dictionary<string, int>)).Invoke(null, [json]);
                return ((Dictionary<string, int>)back!)["cycle"] == 42 ? json : throw new InvalidOperationException("value lost");
            });
            Check("BinaryFormatter via ObjectCloner.SerializingCloner.Copy", () =>
            {
                var cloner = Assembly.Load("Assembly-CSharp-firstpass").GetType("ObjectCloner.SerializingCloner", true)!;
                var copy = cloner.GetMethod("Copy")!.MakeGenericMethod(typeof(List<string>)).Invoke(null, [new List<string> { "a", "b" }]);
                return string.Join(",", (List<string>)copy!);
            });
            Check("Code page IBM437 (Ionic.Zip)", () => Encoding.GetEncoding("IBM437").EncodingName);
            Check("System.Configuration's settings classes (mods' generated Properties.Settings)", () =>
                (Type.GetType("System.Configuration.ApplicationSettingsBase, System", throwOnError: true)!).Assembly.GetName().Name!);
            Check("A Harmony patch of a value type's GetHashCode after tier-up (UnboxedMethods)", () =>
            {
                UnboxedMethods.Install();
                // The value type is emitted (dynamic code is optimized whatever this build is): an
                // int field and GetHashCode returning it, patched to return -1 for every key.
                var module = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("OniBrutal.Host.Check"),
                    System.Reflection.Emit.AssemblyBuilderAccess.Run).DefineDynamicModule("Check");
                var builder = module.DefineType("PatchedHash", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout, typeof(ValueType));
                var value = builder.DefineField("Value", typeof(int), FieldAttributes.Public);
                var hash = builder.DefineMethod(nameof(GetHashCode), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig, typeof(int), Type.EmptyTypes);
                var il = hash.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Ldfld, value);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                builder.DefineMethodOverride(hash, typeof(object).GetMethod(nameof(GetHashCode))!);
                var type = builder.CreateType();
                new HarmonyLib.Harmony("OniBrutal.Host.Check").Patch(type.GetMethod(nameof(GetHashCode)),
                    postfix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(PatchedHash)));
                // Keys hashed by the patch at tier 0, then looked up for long enough that the
                // dictionary's methods (CoreLib's, which inline the key's GetHashCode) reach tier 1:
                // a lookup inlining the original body misses.
                var keys = new object[100];
                var dictionary = (System.Collections.IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(type, typeof(int)))!;
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i] = Activator.CreateInstance(type)!;
                    type.GetField("Value")!.SetValue(keys[i], i);
                    dictionary[keys[i]] = i;
                }
                int misses = 0;
                for (int round = 0; round < 100; round++)
                {
                    for (int i = 0; i < 10000; i++)
                        if (!dictionary.Contains(keys[i % keys.Length])) misses++;
                    System.Threading.Thread.Sleep(20);
                }
                return misses == 0 ? "no lookup missed" : throw new InvalidOperationException($"{misses} lookups missed");
            });
            Check("A transpiler leaving a value on the stack before a try block (TranspiledTryEntries)", () =>
            {
                TranspiledTryEntries.Install();
                new HarmonyLib.Harmony("OniBrutal.Host.Check").Patch(typeof(Program).GetMethod(nameof(Guarded), BindingFlags.NonPublic | BindingFlags.Static),
                    transpiler: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(LeaveValueBehind)));
                int doubled = Guarded(21);
                return doubled == 42 ? "the method compiles and runs as in Mono" : throw new InvalidOperationException($"returned {doubled}");
            });
            Check("A Harmony patch of a generic class's method for one reference type argument (SharedGenerics)", () =>
            {
                SharedGenerics.Install();
                // Emitted (dynamic code is optimized whatever this build is): Pool<T>.Draw, and a
                // caller of Pool<string>.Draw; the patch is made on Pool<object>.Draw, whose code
                // Pool<string> shares, as Better Info Cards patches HoverTextDrawer.Pool<MonoBehaviour>.
                var module = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("OniBrutal.Host.SharedCheck"),
                    System.Reflection.Emit.AssemblyBuilderAccess.Run).DefineDynamicModule("SharedCheck");
                var pool = module.DefineType("Pool", TypeAttributes.Public | TypeAttributes.Sealed);
                var t = pool.DefineGenericParameters("T")[0];
                t.SetGenericParameterAttributes(GenericParameterAttributes.ReferenceTypeConstraint);
                var last = pool.DefineField("last", t, FieldAttributes.Public);
                pool.DefineDefaultConstructor(MethodAttributes.Public);
                var draw = pool.DefineMethod("Draw", MethodAttributes.Public | MethodAttributes.HideBySig, t, [t]);
                var il = draw.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                il.Emit(System.Reflection.Emit.OpCodes.Stfld, last);
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                var poolType = pool.CreateType();
                var caller = module.DefineType("Caller", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
                var stringPool = poolType.MakeGenericType(typeof(string));
                var run = caller.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [stringPool]);
                il = run.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Ldstr, "x");
                il.Emit(System.Reflection.Emit.OpCodes.Call, stringPool.GetMethod("Draw")!);
                il.Emit(System.Reflection.Emit.OpCodes.Pop);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                var callerType = caller.CreateType();
                new HarmonyLib.Harmony("OniBrutal.Host.Check").Patch(poolType.MakeGenericType(typeof(object)).GetMethod("Draw"),
                    postfix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(CountDraw)));
                var instance = Activator.CreateInstance(stringPool)!;
                var runMethod = callerType.GetMethod("Run")!;
                int missed = 0;
                for (int round = 0; round < 100; round++)
                {
                    int before = drawn;
                    for (int i = 0; i < 1000; i++) runMethod.Invoke(null, [instance]);
                    missed += 1000 - (drawn - before);
                    System.Threading.Thread.Sleep(20);
                }
                return missed == 0 ? "every call ran the patch" : throw new InvalidOperationException($"{missed} calls ran the original");
            });
            Check("GetCallingAssembly in a helper a Harmony postfix calls (PatchFrames)", () =>
            {
                PatchFrames.Install();
                // Assembly A: the patched method. B: a helper returning GetCallingAssembly(). C: the
                // mod, whose postfix calls the helper and keeps what it returned.
                System.Reflection.Emit.ModuleBuilder Module(string name) => System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), System.Reflection.Emit.AssemblyBuilderAccess.Run).DefineDynamicModule(name);
                var a = Module("CheckTarget").DefineType("Target", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                var run = a.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
                var il = run.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1); il.Emit(System.Reflection.Emit.OpCodes.Add); il.Emit(System.Reflection.Emit.OpCodes.Ret);
                var target = a.CreateType();
                var b = Module("CheckHelper").DefineType("Helper", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                var who = b.DefineMethod("Who", MethodAttributes.Public | MethodAttributes.Static, typeof(Assembly), Type.EmptyTypes);
                il = who.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Call, typeof(Assembly).GetMethod(nameof(Assembly.GetCallingAssembly))!); il.Emit(System.Reflection.Emit.OpCodes.Ret);
                var helper = b.CreateType();
                var cModule = Module("CheckMod");
                var c = cModule.DefineType("Mod", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                var seen = c.DefineField("Seen", typeof(Assembly), FieldAttributes.Public | FieldAttributes.Static);
                var postfix = c.DefineMethod("Postfix", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
                il = postfix.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Call, helper.GetMethod("Who")!); il.Emit(System.Reflection.Emit.OpCodes.Stsfld, seen); il.Emit(System.Reflection.Emit.OpCodes.Ret);
                var mod = c.CreateType();
                new HarmonyLib.Harmony("OniBrutal.Host.Check").Patch(target.GetMethod("Run"), postfix: new HarmonyLib.HarmonyMethod(mod.GetMethod("Postfix")));
                var results = new HashSet<string>();
                var runMethod = target.GetMethod("Run")!;
                for (int round = 0; round < 60; round++)
                {
                    for (int i = 0; i < 200; i++) runMethod.Invoke(null, [i]);
                    results.Add(((Assembly)mod.GetField("Seen")!.GetValue(null)!).GetName().Name!);
                    System.Threading.Thread.Sleep(20);
                }
                return results.Count == 1 && results.Contains("CheckMod") ? "the postfix's assembly calls, as in Mono" : throw new InvalidOperationException($"the caller was {string.Join(", ", results)}");
            });
            Check("A Harmony prefix on a P/Invoke whose export is missing (PInvokePatches)", () =>
            {
                PInvokePatches.Install();
                new HarmonyLib.Harmony("OniBrutal.Host.Check").Patch(typeof(Program).GetMethod(nameof(MissingExport), BindingFlags.NonPublic | BindingFlags.Static),
                    prefix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(SkipMissing)));
                for (int i = 0; i < 1000; i++) MissingExport();
                // A P/Invoke never called before still binds to its own export (libc's: Linux).
                int pid = OperatingSystem.IsLinux() ? OtherExport() : Environment.ProcessId;
                return skipped == 1000 && pid == Environment.ProcessId ? "the prefix runs; other P/Invokes bind as before" : throw new InvalidOperationException($"skipped {skipped}, getpid {pid}");
            });
            Check("A Harmony prefix on a method an earlier patch's replacement calls (PatchDependents)", () =>
            {
                PatchDependents.Install();
                // B: a small static method; A calls it. A is patched first (its replacement is compiled
                // optimized), then B gets a prefix, as True Tiles patches GetConnectionBits and then
                // MatchesDef.
                var module = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("OniBrutal.Host.LaterCheck"),
                    System.Reflection.Emit.AssemblyBuilderAccess.Run).DefineDynamicModule("LaterCheck");
                var type = module.DefineType("Later", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                var b = type.DefineMethod("B", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
                var il = b.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_2); il.Emit(System.Reflection.Emit.OpCodes.Mul); il.Emit(System.Reflection.Emit.OpCodes.Ret);
                var a = type.DefineMethod("A", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
                il = a.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Call, b); il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1); il.Emit(System.Reflection.Emit.OpCodes.Add); il.Emit(System.Reflection.Emit.OpCodes.Ret);
                var later = type.CreateType();
                var harmony = new HarmonyLib.Harmony("OniBrutal.Host.Check");
                harmony.Patch(later.GetMethod("A"), postfix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(CountA)));
                harmony.Patch(later.GetMethod("B"), prefix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(CountB)));
                var run = later.GetMethod("A")!;
                int before = prefixedB;
                for (int i = 0; i < 100; i++) run.Invoke(null, [i]);
                return prefixedB - before == 100 ? "the later prefix runs from the earlier replacement" : throw new InvalidOperationException($"B's prefix ran {prefixedB - before} of 100 calls");
            });
            Check("Exceptions nothing catches on other threads (UnhandledExceptions)", () =>
            {
                UnhandledExceptions.Install();
                int raised = 0;
                UnhandledExceptionEventHandler count = (_, args) =>
                {
                    if (args.ExceptionObject is InvalidOperationException { Message: "check" }) Interlocked.Increment(ref raised);
                };
                AppDomain.CurrentDomain.UnhandledException += count;
                try
                {
                    var thread = new Thread(() => throw new InvalidOperationException("check"));
                    thread.Start();
                    thread.Join();
                    ThreadPool.QueueUserWorkItem(_ => throw new InvalidOperationException("check"));
                    Thread.Sleep(500);
                }
                finally
                {
                    AppDomain.CurrentDomain.UnhandledException -= count;
                }
                return raised == 1 ? "the process goes on; the thread's raised UnhandledException, the pool's did not" : throw new InvalidOperationException($"UnhandledException raised {raised} times");
            });
            Check("A mod's copy of a library, and references to it, when another version is loaded (ModAccess)", () =>
            {
                ModAccess.Install();
                var dir = Directory.CreateTempSubdirectory("oni-brutal-check-").FullName;
                try
                {
                    Type? marker2 = null;
                    string Emit(string version)
                    {
                        var builder = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckLibrary") { Version = new Version(version) }, typeof(object).Assembly);
                        var marker = builder.DefineDynamicModule("OniBrutalCheckLibrary").DefineType("Marker", TypeAttributes.Public);
                        marker.CreateType();
                        if (version == "2.0.0.0") marker2 = marker;
                        var path = Path.Combine(dir, version, "OniBrutalCheckLibrary.dll");
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        builder.Save(path);
                        return path;
                    }
                    var first = ModAccess.LoadFrom(Emit("1.0.0.0"));
                    var second = ModAccess.LoadFrom(Emit("2.0.0.0"));
                    if (!ReferenceEquals(first, second)) throw new InvalidOperationException("two versions loaded");
                    // A mod built against version 2, with its copy beside it, while version 1 is the
                    // one loaded: its reference binds to version 1.
                    var user = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckUser"), typeof(object).Assembly);
                    var use = user.DefineDynamicModule("OniBrutalCheckUser").DefineType("Use", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                    var get = use.DefineMethod("Get", MethodAttributes.Public | MethodAttributes.Static, typeof(Type), Type.EmptyTypes);
                    var il = get.GetILGenerator();
                    il.Emit(System.Reflection.Emit.OpCodes.Ldtoken, marker2!);
                    il.Emit(System.Reflection.Emit.OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
                    il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    use.CreateType();
                    var userPath = Path.Combine(dir, "2.0.0.0", "OniBrutalCheckUser.dll");
                    user.Save(userPath);
                    var bound = (Type)Assembly.LoadFrom(userPath).GetType("Use")!.GetMethod("Get")!.Invoke(null, null)!;
                    return ReferenceEquals(bound.Assembly, first) ? "the loaded version stands in for files and references, as in Mono" : throw new InvalidOperationException($"the reference bound to {bound.Assembly.GetName().Version}");
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            });
            Check("Reflection writing a mod's static readonly field after its type is initialized (ModAccess)", () =>
            {
                ModAccess.Install();
                var dir = Directory.CreateTempSubdirectory("oni-brutal-check-").FullName;
                try
                {
                    var builder = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckStrings"), typeof(object).Assembly);
                    var strings = builder.DefineDynamicModule("OniBrutalCheckStrings").DefineType("Strings", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                    var field = strings.DefineField("NAME", typeof(string), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly);
                    var cctor = strings.DefineTypeInitializer().GetILGenerator();
                    cctor.Emit(System.Reflection.Emit.OpCodes.Ldstr, "before");
                    cctor.Emit(System.Reflection.Emit.OpCodes.Stsfld, field);
                    cctor.Emit(System.Reflection.Emit.OpCodes.Ret);
                    strings.CreateType();
                    var path = Path.Combine(dir, "OniBrutalCheckStrings.dll");
                    builder.Save(path);
                    var name = ModAccess.LoadFrom(path).GetType("Strings")!.GetField("NAME")!;
                    if ((string)name.GetValue(null)! != "before") throw new InvalidOperationException("type initializer did not run");
                    name.SetValue(null, "after");
                    return (string)name.GetValue(null)! == "after" ? "the write holds, as in Mono" : throw new InvalidOperationException("the write was lost");
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            });
            Check("A mod's class missing a method of a game interface it lists (MissingInterfaceMethods)", () =>
            {
                ModAccess.Install();
                var dir = Directory.CreateTempSubdirectory("oni-brutal-check-").FullName;
                try
                {
                    // The game's interface as the mod was built against (Old) and as it is now (New).
                    System.Reflection.Emit.TypeBuilder Api(string method, string folder)
                    {
                        var builder = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckApi"), typeof(object).Assembly);
                        var module = builder.DefineDynamicModule("OniBrutalCheckApi");
                        // The new method takes one of the game's own types (Trashcans' missing
                        // method names the game's ButtonMenuTextOverride).
                        var arg = module.DefineType("Arg", TypeAttributes.Public);
                        arg.CreateType();
                        var thing = module.DefineType("IThing", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
                        thing.DefineMethod(method, MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig, typeof(int), method == "New" ? [arg] : Type.EmptyTypes);
                        thing.CreateType();
                        Directory.CreateDirectory(Path.Combine(dir, folder));
                        builder.Save(Path.Combine(dir, folder, "OniBrutalCheckApi.dll"));
                        return thing;
                    }
                    var oldThing = Api("Old", "old");
                    Api("New", "new");
                    var current = Assembly.LoadFrom(Path.Combine(dir, "new", "OniBrutalCheckApi.dll"));
                    var mod = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckOldMod"), typeof(object).Assembly);
                    var impl = mod.DefineDynamicModule("OniBrutalCheckOldMod").DefineType("Impl", TypeAttributes.Public, typeof(object), [oldThing]);
                    var old = impl.DefineMethod("Old", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.HideBySig, typeof(int), Type.EmptyTypes);
                    var il = old.GetILGenerator();
                    il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_7); il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    impl.DefineDefaultConstructor(MethodAttributes.Public);
                    impl.CreateType();
                    var modPath = Path.Combine(dir, "mod", "OniBrutalCheckOldMod.dll");
                    Directory.CreateDirectory(Path.GetDirectoryName(modPath)!);
                    mod.Save(modPath);
                    var type = ModAccess.LoadFrom(modPath).GetTypes().Single(t => t.Name == "Impl");
                    // The probe's copy is gone: a library that picks its instance from the
                    // process's assemblies (PLib) must find the loaded one only.
                    int copies = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "OniBrutalCheckOldMod");
                    if (copies != 1) throw new InvalidOperationException($"{copies} copies of the mod are listed");
                    var instance = Activator.CreateInstance(type)!;
                    if ((int)type.GetMethod("Old")!.Invoke(instance, null)! != 7) throw new InvalidOperationException("the mod's own method changed");
                    var newMethod = current.GetType("IThing")!.GetMethod("New")!;
                    try
                    {
                        newMethod.Invoke(instance, [null]);
                    }
                    catch (TargetInvocationException e) when (e.InnerException is TypeLoadException)
                    {
                        return "the class loads; calling the missing method throws TypeLoadException";
                    }
                    throw new InvalidOperationException("the missing method did not throw");
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            });
            Check("A mod's stack walk through a patched method (MonoStackFrames)", () =>
            {
                ModAccess.Install();
                var dir = Directory.CreateTempSubdirectory("oni-brutal-check-").FullName;
                try
                {
                    // Walk() names the type that declares its caller's frame; Caller() calls it.
                    var mod = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckStackMod"), typeof(object).Assembly);
                    var type = mod.DefineDynamicModule("OniBrutalCheckStackMod").DefineType("Plants", TypeAttributes.Public);
                    var walk = type.DefineMethod("Walk", MethodAttributes.Public | MethodAttributes.Static, typeof(string), Type.EmptyTypes);
                    walk.SetImplementationFlags(MethodImplAttributes.NoInlining);
                    var il = walk.GetILGenerator();
                    il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(System.Diagnostics.StackTrace).GetConstructor(Type.EmptyTypes)!);
                    il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1);
                    il.Emit(System.Reflection.Emit.OpCodes.Callvirt, typeof(System.Diagnostics.StackTrace).GetMethod("GetFrame")!);
                    il.Emit(System.Reflection.Emit.OpCodes.Callvirt, typeof(System.Diagnostics.StackFrame).GetMethod("GetMethod")!);
                    il.Emit(System.Reflection.Emit.OpCodes.Callvirt, typeof(MemberInfo).GetProperty("ReflectedType")!.GetMethod!);
                    il.Emit(System.Reflection.Emit.OpCodes.Callvirt, typeof(Type).GetProperty("FullName")!.GetMethod!);
                    il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    var caller = type.DefineMethod("Caller", MethodAttributes.Public | MethodAttributes.Static, typeof(string), Type.EmptyTypes);
                    caller.SetImplementationFlags(MethodImplAttributes.NoInlining);
                    il = caller.GetILGenerator();
                    il.Emit(System.Reflection.Emit.OpCodes.Call, walk);
                    il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    type.CreateType();
                    var modPath = Path.Combine(dir, "OniBrutalCheckStackMod.dll");
                    mod.Save(modPath);
                    var plants = ModAccess.LoadFrom(modPath).GetType("Plants")!;
                    new HarmonyLib.Harmony("OniBrutal.Host.Check").Patch(plants.GetMethod("Caller"), prefix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(NoOp)));
                    // Measured in the original player: a patched method's frame is declared by
                    // MonoMod's DynamicMethodDefinition.
                    var owner = (string)plants.GetMethod("Caller")!.Invoke(null, null)!;
                    return owner == "MonoMod.Utils.DynamicMethodDefinition" ? "the patched frame is declared by " + owner : throw new InvalidOperationException("the patched frame is declared by " + owner);
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            });
            // Last: once a mod patches enum formatting, enum names go through it for the process.
            Check("A Harmony patch of Enum.ToString() alone (MonoEnumFormat)", () =>
            {
                MonoEnumFormat.Install();
                new HarmonyLib.Harmony("OniBrutal.Host.Check").Patch(HarmonyLib.AccessTools.Method(typeof(Enum), nameof(Enum.ToString), Type.EmptyTypes),
                    postfix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(NamedDay)));
                var value = (DayOfWeek)100;
                var texts = new[] { value.ToString(), value.ToString("G"), value.ToString((string?)null), string.Format("{0}", value), string.Format("{0:G}", value), new StringBuilder().AppendFormat("{0}", value).ToString() };
                foreach (var text in texts)
                    if (text != "Named") throw new InvalidOperationException($"formatted as {string.Join(", ", texts)}");
                return value.ToString("D") == "100" ? "every \"G\" formatting path reaches it" : throw new InvalidOperationException("\"D\" renamed");
            });
            Check("A Harmony patch of Mono's Enum.InternalFormat (MonoEnumFormat)", () =>
            {
                // As [HarmonyPatch(typeof(Enum), "InternalFormat")] finds it.
                var internalFormat = HarmonyLib.AccessTools.DeclaredMethod(typeof(Enum), "InternalFormat")
                    ?? throw new InvalidOperationException("no InternalFormat to patch");
                new HarmonyLib.Harmony("OniBrutal.Host.Check").Patch(internalFormat,
                    postfix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(NamedValue)));
                var value = (AttributeTargets)0x40000000;
                // The game's and mods' code targets .NET Framework, where an interpolated string is
                // string.Format (this host's would use .NET's interpolation handler instead).
                var texts = new[] { value.ToString(), value.ToString("G"), string.Format("{0}", value), string.Concat("", value), Enum.Format(typeof(AttributeTargets), value, "G"), Enum.Format(typeof(AttributeTargets), 0x40000000, "G") };
                foreach (var text in texts)
                    if (text != "Named") throw new InvalidOperationException($"formatted as {string.Join(", ", texts)}");
                try
                {
                    Enum.Format(typeof(AttributeTargets), value, "");
                    throw new InvalidOperationException("Enum.Format accepted an empty format");
                }
                catch (FormatException) { }
                return AttributeTargets.Class.ToString() == "Class" ? "every \"G\" formatting path names the value" : throw new InvalidOperationException("other values renamed");
            });
            return failures;
        }

        /// CheckRuntimeServices' P/Invokes: one whose export no library has, one that exists.
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "oni_brutal_missing_export")]
        static extern void MissingExport();

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getpid")]
        static extern int OtherExport();

        static int skipped;
        static bool SkipMissing() { skipped++; return false; }

        sealed class SharedPool<T> where T : class
        {
            public T? last;
            public T Draw(T item) { last = item; return item; }
        }

        static int prefixedB, countedA;
        static void CountA() => countedA++;
        static void CountB() => prefixedB++;

        static int drawn;
        static void CountDraw() => drawn++;

        /// CheckRuntimeServices' method whose transpiled copy enters its try block with a value on the stack.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static int Guarded(int value)
        {
            try
            {
                value = checked(value * 2);
            }
            finally
            {
                guardedRuns++;
            }
            return value;
        }

        static int guardedRuns;

        /// The transpiler: a value pushed first and never used, as Better Info Cards leaves one.
        static IEnumerable<HarmonyLib.CodeInstruction> LeaveValueBehind(IEnumerable<HarmonyLib.CodeInstruction> instructions) =>
            instructions.Prepend(new HarmonyLib.CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_I4_7));

        /// CheckRuntimeServices' patch of Enum.ToString(), as element mods write theirs.
        static void NoOp() { }

        static void NamedDay(Enum __instance, ref string __result)
        {
            if (__instance is DayOfWeek day && (int)day == 100) __result = "Named";
        }

        /// CheckRuntimeServices' patch of Enum.InternalFormat, as element mods write theirs.
        static void NamedValue(Type eT, object value, ref string __result)
        {
            if (eT == typeof(AttributeTargets) && (AttributeTargets)value == (AttributeTargets)0x40000000) __result = "Named";
        }
    }

    internal sealed class Options
    {
        public string GameDir = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "OxygenNotIncluded")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects", "oni-linux");
        /// Test runs (`--run-home DIR`): the player's per-user data (Unity's persistent and cache
        /// paths, and on Windows the Documents and local application data folders the game reads)
        /// under DIR instead of the user's (RunHome).
        public string? RunHome;
        /// Test runs (`--audio-wav DIR`): the game's audio written to DIR/fmodoutput.wav instead of
        /// the sound device (AudioCapture).
        public string? AudioWav;
        public string? LogPath;
        public bool Headless;
        public bool BatchMode;
        public bool CheckTypes;
        /// This project's replacements of Unity's packages (PLAN M9) from this directory instead of
        /// packages/ next to the host; ShippedUiPackages loads Unity's from Managed/ instead.
        public string? UiPackages;
        public bool ShippedUiPackages;
        /// Optional changes to the game's own code (GameTweaks), by name.
        public readonly HashSet<string> GameTweaks = new(StringComparer.Ordinal);
        public bool CheckAssets;
        /// With --check-assets: where to list the script classes the data uses.
        public string? ListDataScripts;
        public bool CheckBind;
        public bool CheckShaders;
        public string? DumpShader;
        public string? DumpScript;
        public int Frames = -1;
        /// --max-frame-rate N[:FRAMES]: an upper bound on the frame rate whatever the game asks
        /// for (scripted runs whose schedules count frames), for the first FRAMES frames when
        /// given; 0 for none.
        public int MaxFrameRate, MaxFrameRateFrames = int.MaxValue;
        /// Diagnostics: log every first-chance exception with IL offsets per frame (game code
        /// has no symbols, so offsets locate the throwing instruction).
        public bool TraceExceptions;
        /// Diagnostics (--gl-debug): the driver's GL debug output in the log (debug context).
        public bool GlDebug;
        /// Diagnostics (--vsync-count N): the swap interval regardless of the quality settings.
        public int VSyncCount = -1;
        /// Diagnostics (--gpu-profile): GPU time by operation with the frame counters.
        public bool GpuProfile;
        /// Diagnostics (--no-bindless): textures bound to units even where bindless textures exist.
        public bool NoBindless;
        public readonly ScriptedInput Input = new();
        // Unity's standalone player arguments.
        public int ScreenWidth = -1, ScreenHeight = -1;
        public int ScreenFullscreen = -1;
        public string? WindowMode;
        /// Development: "frame:path.png[:superSize]" calls ScreenCapture.CaptureScreenshot in
        /// that frame.
        public readonly List<(int Frame, string Path, int Scale)> Captures = new();
        /// --probe-ui FRAME:X,Y: logs the canvas elements drawn at a window point (top-left
        /// origin, as --input) as of that frame.
        public readonly List<(int Frame, float X, float Y)> Probes = new();
        /// --dump-hierarchy FRAME:PATH: logs the subtree at a GameObject path (GameObject.Find
        /// syntax) as of that frame: active flags, rect transforms, components.
        public readonly List<(int Frame, string Path)> HierarchyDumps = new();
        /// --report-builtin-assets FILE: at exit, the objects the run loaded from Unity's built-in
        /// resource files (tab-separated).
        public string? BuiltinReport;
        /// --dump-component FRAME:PATH|TYPE: logs the serialized fields of a component (by type
        /// name) on the GameObject at PATH as of that frame.
        public readonly List<(int Frame, string Spec)> ComponentDumps = new();
        /// --dump-ui FRAME:PATH: writes every active UI element (rects, canvas renderer output,
        /// package component state) as of that frame (PLAN M9's differential oracle).
        public readonly List<(int Frame, string Path)> UiDumps = new();

        public static Options? Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--game": o.GameDir = args[++i]; break;
                    case "--":
                    {
                        // Steam launch options `OniBrutal [options] -- %command%`: Steam's command
                        // for the game, any wrappers of Steam's (launch wrapper, reaper) followed by
                        // the game's executable and its arguments. The game directory is the
                        // executable's; the arguments after it are read as the player's (Unity's
                        // -screen-width and the like).
                        int exe = Array.FindIndex(args, i + 1, a => Path.GetFileName(a) is "OxygenNotIncluded" or "OxygenNotIncluded.exe");
                        if (exe < 0)
                        {
                            Console.Error.WriteLine("OniBrutal: no OxygenNotIncluded executable after --; the Steam launch options should end with -- %command%");
                            return null;
                        }
                        o.GameDir = Path.GetDirectoryName(Path.GetFullPath(args[exe]))!;
                        i = exe;
                        break;
                    }
                    case "--log": o.LogPath = args[++i]; break;
                    case "--run-home": o.RunHome = Path.GetFullPath(args[++i]); break;
                    case "--audio-wav": o.AudioWav = Path.GetFullPath(args[++i]); break;
                    case "--headless": o.Headless = true; break;
                    case "--batchmode": o.BatchMode = true; break;
                    case "--check-types": o.CheckTypes = true; break;
                    case "--check-assets": o.CheckAssets = true; break;
                    case "--list-data-scripts": o.ListDataScripts = args[++i]; break;
                    case "--ui-packages": o.UiPackages = Path.GetFullPath(args[++i]); break;
                    case "--shipped-ui-packages": o.ShippedUiPackages = true; break;
                    case "--game-tweak":
                    {
                        var name = args[++i];
                        if (!OniBrutal.Host.GameTweaks.GameTweakNames.IsKnown(name))
                        {
                            Console.Error.WriteLine($"Unknown game tweak {name}; known: all, exact (all but {string.Join(", ", OniBrutal.Host.GameTweaks.GameTweakNames.ChangingBehaviour)}), {string.Join(", ", OniBrutal.Host.GameTweaks.GameTweakNames.All)}");
                            return null;
                        }
                        o.GameTweaks.Add(name);
                        break;
                    }
                    case "--check-bind": o.CheckBind = true; break;
                    case "--check-shaders": o.CheckShaders = true; break;
                    case "--dump-shader": o.DumpShader = args[++i]; break;
                    case "--dump-script": o.DumpScript = args[++i]; break;
                    case "--frames": o.Frames = int.Parse(args[++i]); break;
                    case "--trace-exceptions": o.TraceExceptions = true; break;
                    case "--gl-debug": o.GlDebug = true; break;
                    case "--vsync-count": o.VSyncCount = int.Parse(args[++i]); break;
                    case "--gpu-profile": o.GpuProfile = true; break;
                    case "--no-bindless": o.NoBindless = true; break;
                    case "--input": o.Input.Add(args[++i]); break;
                    case "--frame-stats": OniBrutal.Engine.PlayerLoop.FrameStats = true; break;
                    case "--max-frame-rate":
                        {
                            var spec = args[++i].Split(':');
                            o.MaxFrameRate = int.Parse(spec[0]);
                            if (spec.Length > 1) o.MaxFrameRateFrames = int.Parse(spec[1]);
                            break;
                        }
                    case "--dump-render-textures":
                        {
                            var spec = args[++i];
                            int colon = spec.IndexOf(':');
                            OniBrutal.Engine.Rendering.FrameRenderer.DumpFrame = int.Parse(spec[..colon]);
                            OniBrutal.Engine.Rendering.FrameRenderer.DumpDirectory = Path.GetFullPath(spec[(colon + 1)..]);
                            break;
                        }
                    case "--native-override":
                        {
                            var spec = args[++i];
                            int eq = spec.IndexOf('=');
                            if (eq <= 0) throw new ArgumentException("--native-override expects NAME=PATH");
                            AssemblyPolicy.NativeOverrides[spec[..eq]] = Path.GetFullPath(spec[(eq + 1)..]);
                            break;
                        }
                    case "-screen-width": o.ScreenWidth = int.Parse(args[++i]); break;
                    case "-screen-height": o.ScreenHeight = int.Parse(args[++i]); break;
                    case "-screen-fullscreen": o.ScreenFullscreen = int.Parse(args[++i]); break;
                    case "-window-mode": o.WindowMode = args[++i]; break;
                    case "--dump-component":
                    {
                        var spec = args[++i];
                        int colon = spec.IndexOf(':');
                        o.ComponentDumps.Add((int.Parse(spec[..colon]), spec[(colon + 1)..]));
                        break;
                    }
                    case "--report-builtin-assets": o.BuiltinReport = args[++i]; break;
                    case "--dump-hierarchy":
                    {
                        var spec = args[++i];
                        int colon = spec.IndexOf(':');
                        o.HierarchyDumps.Add((int.Parse(spec[..colon]), spec[(colon + 1)..]));
                        break;
                    }
                    case "--dump-ui-vertices": OniBrutal.Engine.UI.UiDump.VerticesFilter = args[++i]; break;
                    case "--dump-ui":
                    {
                        var spec = args[++i];
                        int colon = spec.IndexOf(':');
                        o.UiDumps.Add((int.Parse(spec[..colon]), spec[(colon + 1)..]));
                        break;
                    }
                    case "--probe-ui":
                    {
                        var parts = args[++i].Split(':', ',');
                        o.Probes.Add((int.Parse(parts[0]), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
                        break;
                    }
                    case "--capture":
                    {
                        // FRAME:PATH[:SCALE]; the path may hold a colon itself (a Windows drive).
                        var spec = args[++i];
                        int colon = spec.IndexOf(':');
                        var path = spec[(colon + 1)..];
                        int scale = 1;
                        int last = path.LastIndexOf(':');
                        if (last > 0 && int.TryParse(path[(last + 1)..], out int s)) { scale = s; path = path[..last]; }
                        o.Captures.Add((int.Parse(spec[..colon]), path, scale));
                        break;
                    }
                    default:
                        Console.Error.WriteLine($"Unknown option {args[i]}");
                        Console.Error.WriteLine("usage: OniBrutal [--game DIR] [--log FILE] [--run-home DIR] [--audio-wav DIR] [--headless] [--batchmode] [--frames N] [--native-override NAME=PATH] [--trace-exceptions] [--gl-debug] [--vsync-count N] [--gpu-profile] [--no-bindless] [--input FRAME:ACTION[:ARGS]] [--frame-stats] [--max-frame-rate N[:FRAMES]] [--dump-render-textures FRAME:DIR] [-screen-width W] [-screen-height H] [-screen-fullscreen 0|1] [-window-mode exclusive|borderless|windowed] [--check-types] [--ui-packages DIR | --shipped-ui-packages] [--game-tweak NAME|all|exact] [--check-assets [--list-data-scripts FILE]] [--check-bind] [--check-shaders] [--dump-shader NAME:DIR] [--capture FRAME:PATH[:SCALE]] [--probe-ui FRAME:X,Y] [--dump-ui FRAME:PATH] [--dump-ui-vertices PATH-SUBSTRING] [--dump-hierarchy FRAME:PATH] [--report-builtin-assets FILE] [--dump-component FRAME:PATH|TYPE] [-- STEAM-COMMAND]");
                        return null;
                }
            }
            return o;
        }
    }

    /// Unity's player paths for this game: the Linux player's (XDG directories) or the Windows
    /// player's (LocalLow, the temporary folder; with forward slashes, as Unity reports them).
    internal static class Paths
    {
        public static void Configure(string dataDir, Options options)
        {
            // app.info: company name, then product name.
            var info = File.ReadAllLines(Path.Combine(dataDir, "app.info"));
            EngineConfig.CompanyName = info.Length > 0 ? info[0] : "";
            EngineConfig.ProductName = info.Length > 1 ? info[1] : "";
            if (OperatingSystem.IsWindows()) ConfigureWindows(dataDir, options.RunHome);
            else ConfigureLinux(dataDir, options.RunHome);
            EngineConfig.ConsoleLogPath = EngineConfig.PersistentDataPath + "/Player.log";
            EngineConfig.IsBatchMode = options.BatchMode;
            EngineConfig.Headless = options.Headless;
        }

        static void ConfigureLinux(string dataDir, string? runHome)
        {
            EngineConfig.DataPath = dataDir;
            EngineConfig.StreamingAssetsPath = Path.Combine(dataDir, "StreamingAssets");
            // Unity's Linux player: $XDG_CONFIG_HOME/unity3d/<company>/<product>, defaulting to ~/.config.
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var config = runHome != null ? Path.Combine(runHome, "config") : Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(config)) config = Path.Combine(home, ".config");
            EngineConfig.PersistentDataPath = Path.Combine(config, "unity3d", EngineConfig.CompanyName, EngineConfig.ProductName);
            var cache = runHome != null ? Path.Combine(runHome, "cache") : Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (string.IsNullOrEmpty(cache)) cache = Path.Combine(home, ".cache");
            EngineConfig.TemporaryCachePath = Path.Combine(cache, EngineConfig.CompanyName, EngineConfig.ProductName);
        }

        static void ConfigureWindows(string dataDir, string? runHome)
        {
            static string Unity(string path) => path.Replace('\\', '/');
            EngineConfig.DataPath = Unity(Path.GetFullPath(dataDir));
            EngineConfig.StreamingAssetsPath = EngineConfig.DataPath + "/StreamingAssets";
            var localLow = runHome != null ? Path.Combine(runHome, "AppData", "LocalLow") : WindowsFolders.LocalAppDataLow();
            EngineConfig.PersistentDataPath = Unity(Path.Combine(localLow, EngineConfig.CompanyName, EngineConfig.ProductName));
            var temp = runHome != null ? Path.Combine(runHome, "AppData", "Local", "Temp") : Path.GetTempPath();
            EngineConfig.TemporaryCachePath = Unity(Path.Combine(temp, EngineConfig.CompanyName, EngineConfig.ProductName));
        }
    }

    /// Windows known folders .NET's SpecialFolder does not name.
    internal static class WindowsFolders
    {
        static readonly Guid LocalAppDataLowId = new("A520A1A4-1780-4FF6-BD18-167343C5AF16");

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out IntPtr path);

        [System.Runtime.InteropServices.DllImport("ole32.dll")]
        static extern void CoTaskMemFree(IntPtr p);

        /// %USERPROFILE%\AppData\LocalLow, where Unity's Windows player keeps persistent data.
        public static string LocalAppDataLow()
        {
            int hr = SHGetKnownFolderPath(LocalAppDataLowId, 0, IntPtr.Zero, out var p);
            if (hr != 0) throw new InvalidOperationException($"The LocalLow folder could not be found (0x{hr:X8}).");
            try { return System.Runtime.InteropServices.Marshal.PtrToStringUni(p)!; }
            finally { CoTaskMemFree(p); }
        }
    }
}
