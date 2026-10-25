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
            WindowsConsole.AttachToParent();
            var options = Options.Parse(args);
            if (options == null) return 2;
            // For the Restarter next to the host (App.Restart): the arguments to start again with
            // (shell-quoted for the Linux script; as a JSON array with the process id for the
            // Windows one) and the process to wait for.
            Environment.SetEnvironmentVariable("ONI_BRUTAL_RESTART_ARGS", string.Join(" ", args.Select(a => "'" + a.Replace("'", "'\\''") + "'")));
            Environment.SetEnvironmentVariable("ONI_BRUTAL_RESTART_ARGV", System.Text.Json.JsonSerializer.Serialize(args));
            Environment.SetEnvironmentVariable("ONI_BRUTAL_RESTART_PID", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // The install's layout: OxygenNotIncluded_Data beside the game (Linux, Windows) or the
            // macOS bundle OxygenNotIncluded.app, told from the files (GameLayout).
            var layout = OniBrutal.Engine.Platform.GameLayout.Find(options.GameDir);
            if (layout == null)
            {
                Console.Error.WriteLine($"Game data not found: {Path.Combine(options.GameDir, OniBrutal.Engine.Platform.GameLayout.DataFolderName)} (or an OxygenNotIncluded.app there)");
                return 2;
            }
            var dataDir = layout.DataDir;

            // Code pages (IBM437 etc.), which Mono provided through I18N.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Paths.Configure(layout, options);
            EngineLog.Open(options.LogPath ?? EngineConfig.ConsoleLogPath, keepPrevious: options.LogPath == null);
            // Which build this is (the commit the SDK stamps into the informational version) and
            // where it runs from, so a log tells which release, or which stray copy, wrote it.
            var built = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            string commit = built?.IndexOf('+') is int plus and >= 0 ? built[(plus + 1)..] : "unknown";
            EngineLog.Engine($"OniBrutal host starting: commit={commit} exe={Environment.ProcessPath} game={options.GameDir} .NET={Environment.Version}");
            StartupStats.Mark("host starts (runtime up)");
            UnhandledExceptions.Install();
            // The options the main menu's Brutal Tweaks screen sets: defaults, then its settings
            // file in the per-user folder, then the command line.
            var settingsPath = PlayerSettings.PathIn(EngineConfig.PersistentDataPath);
            var settings = PlayerSettings.Read(settingsPath, out var settingsProblem);
            EngineLog.Engine(settingsProblem != null ? $"settings: {settingsPath} could not be read, so defaults apply: {settingsProblem}"
                : File.Exists(settingsPath) ? $"settings: {settingsPath}" : $"settings: {settingsPath} (none; defaults apply)");
            var effective = EffectiveOptions.Resolve(GameTweaks.GameTweakNames.All, GameTweaks.GameTweakNames.ChangingBehaviour,
                options.GameTweaks, options.Splash, options.FrameStats, settings);
            foreach (var line in effective.Describe()) EngineLog.Engine("option " + line);
            if (effective.UnknownTweaks.Count > 0) EngineLog.Engine($"settings: not tweaks of this player, ignored: {string.Join(", ", effective.UnknownTweaks)}");
            options.Splash = effective.Splash;
            OniBrutal.Engine.PlayerLoop.FrameStats = effective.FrameStats;

            AssemblyPolicy.Install(layout, AppContext.BaseDirectory, options.UiPackages, options.ShippedUiPackages);
            if (options.CheckNatives) return NativeCheck.Run(layout, AssemblyPolicy.RuntimeAssemblyPaths(), options.NativeArch ?? AssemblyPolicy.ProcessCpu, options.NativeDir);
            var assemblies = AssemblyPolicy.PreloadScriptingAssemblies(dataDir);
            EngineConfig.BuildAssemblies = AssemblyPolicy.BuildAssemblyNames(dataDir);
            EngineLog.Engine($"Preloaded {assemblies.Count} scripting assemblies");
            StartupStats.Mark("assemblies preloaded");

            if (options.CheckTypes) return CheckTypes(assemblies);
            if (options.CheckAssets) return AssetCheck.Run(dataDir, options.ListDataScripts);
            if (options.CheckBind) return BindCheck.Run(dataDir);
            if (options.CheckShaders) return ShaderCheck.Run(dataDir);
            if (options.DumpShader != null) return ShaderDump.Run(dataDir, options.DumpShader);
            if (options.DumpScript != null) return AssetCheck.Dump(dataDir, options.DumpScript);

            if (options.TraceExceptions) ExceptionTrace.Install();
            if (options.RunHome != null) RunHome.Install(options.RunHome);
            if (options.AudioWav != null) AudioCapture.Install(options.AudioWav);
            // The patches of the game's and Harmony's own methods are made on another thread while
            // the player opens its window (no game code runs before they are all in: Player.Run
            // waits for them before the first scene). Those of the runtime's own methods, which the
            // engine itself calls meanwhile, are made after, on this thread, in the same order.
            Exception? patchFailure = null;
            var gamePatches = new Thread(() =>
            {
                try
                {
                    ModVersionStamp.Install();
                    MonoValueTypeHashes.Install();
                    ModAccess.Install();
                    TranspiledCalls.Install();
                    TranspiledTryEntries.Install();
                    UnboxedMethods.Install();
                    SharedGenerics.Install();
                    PatchFrames.Install();
                    PInvokePatches.Install();
                    PatchDependents.Install();
                }
                catch (Exception e)
                {
                    patchFailure = e;
                }
            }) { Name = "Host patches" };
            gamePatches.Start();
            return Player.Run(options, dataDir, () =>
            {
                gamePatches.Join();
                if (patchFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(patchFailure);
                MonoEnumFormat.Install();
                // Special folders' paths are given whether or not they exist, as Mono gives them (D24).
                MonoFolderPaths.Install();
                GameTweaks.GameTweaks.Install(effective.EnabledTweaks.ToHashSet(StringComparer.Ordinal));
                BrutalTweaksMenu.Install(effective, settings, settingsPath);
                StateMachineSaveNames.Install(dataDir, AppContext.BaseDirectory);
                StartupStats.Mark("host patches installed");
            });
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
            Check("A Harmony prefix on a method an earlier replacement reaches through an inlined one (PatchDependents)", () =>
            {
                PatchDependents.Install();
                // A calls B, which calls C: static, and B small, so A's replacement inlines B and,
                // through it, C. A is patched first, then C gets a prefix, as Forever Eruption Geyser
                // patches Geyser.GetEmitRate and then GetIterationPercent, which GetOnDuration calls.
                // C has 4 bytes of IL, then the most the JIT inlines (1024), then 2 more, which the
                // JIT must call (no replacement is rebuilt for it): the bound PatchDependents assumes.
                var ran = new List<string>();
                foreach (var size in new[] { 4, 1024, 1026 })
                {
                    var module = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"OniBrutal.Host.NestedCheck{size}"),
                        System.Reflection.Emit.AssemblyBuilderAccess.Run).DefineDynamicModule("NestedCheck");
                    var type = module.DefineType("Nested", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                    var c = type.DefineMethod("C", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
                    var il = c.GetILGenerator();
                    il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    for (int i = 0; i < (size - 2) / 2; i++)
                    {
                        il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_3);
                        il.Emit(i % 2 == 0 ? System.Reflection.Emit.OpCodes.Mul : System.Reflection.Emit.OpCodes.Xor);
                    }
                    il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    var b = type.DefineMethod("B", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
                    il = b.GetILGenerator();
                    il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Call, c); il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_2); il.Emit(System.Reflection.Emit.OpCodes.Add); il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    var a = type.DefineMethod("A", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
                    il = a.GetILGenerator();
                    il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Call, b); il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1); il.Emit(System.Reflection.Emit.OpCodes.Add); il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    var nested = type.CreateType();
                    int length = nested.GetMethod("C")!.GetMethodBody()!.GetILAsByteArray()!.Length;
                    if (length != size) throw new InvalidOperationException($"C has {length} bytes of IL, not {size}");
                    var harmony = new HarmonyLib.Harmony("OniBrutal.Host.Check");
                    harmony.Patch(nested.GetMethod("A"), postfix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(CountA)));
                    harmony.Patch(nested.GetMethod("C"), prefix: new HarmonyLib.HarmonyMethod(typeof(Program), nameof(CountC)));
                    var run = nested.GetMethod("A")!;
                    int before = prefixedC;
                    for (int i = 0; i < 100; i++) run.Invoke(null, [i]);
                    if (prefixedC - before != 100) throw new InvalidOperationException($"with {size} bytes of IL in C, its prefix ran {prefixedC - before} of 100 calls");
                    ran.Add(size.ToString());
                }
                return $"the later prefix runs from the earlier replacement (C of {string.Join(", ", ran)} bytes of IL)";
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
                        // A second new method: CoreCLR names one missing method of a class at a time.
                        if (method == "New")
                            thing.DefineMethod("Newer", MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig, typeof(int), Type.EmptyTypes);
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
                    foreach (var name in new[] { "New", "Newer" })
                    {
                        var newMethod = current.GetType("IThing")!.GetMethod(name)!;
                        try
                        {
                            newMethod.Invoke(instance, newMethod.GetParameters().Length == 0 ? null : [null]);
                            throw new InvalidOperationException($"the missing method {name} did not throw");
                        }
                        catch (TargetInvocationException e) when (e.InnerException is TypeLoadException)
                        {
                        }
                    }
                    return "the class loads; calling either missing method throws TypeLoadException";
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
                    // A game enum in another folder: writing the rewritten mod back resolves it, as
                    // the type of a parameter's default value (PLib's mods).
                    var api = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckStackApi"), typeof(object).Assembly);
                    var kindBuilder = api.DefineDynamicModule("OniBrutalCheckStackApi").DefineEnum("Kind", TypeAttributes.Public, typeof(int));
                    kindBuilder.DefineLiteral("A", 0);
                    kindBuilder.DefineLiteral("B", 1);
                    kindBuilder.CreateType();
                    Directory.CreateDirectory(Path.Combine(dir, "api"));
                    api.Save(Path.Combine(dir, "api", "OniBrutalCheckStackApi.dll"));
                    var kind = Assembly.LoadFrom(Path.Combine(dir, "api", "OniBrutalCheckStackApi.dll")).GetType("Kind")!;
                    // Walk() names the type that declares its caller's frame; Caller() calls it.
                    var mod = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckStackMod"), typeof(object).Assembly);
                    var type = mod.DefineDynamicModule("OniBrutalCheckStackMod").DefineType("Plants", TypeAttributes.Public);
                    var pick = type.DefineMethod("Pick", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [kind]);
                    pick.DefineParameter(1, ParameterAttributes.Optional | ParameterAttributes.HasDefault, "kind").SetConstant(Enum.ToObject(kind, 1));
                    pick.GetILGenerator().Emit(System.Reflection.Emit.OpCodes.Ret);
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
            Check("A mod's recursion without end (MonoStackOverflow)", () =>
            {
                ModAccess.Install();
                var dir = Directory.CreateTempSubdirectory("oni-brutal-check-").FullName;
                try
                {
                    // Recurse calls itself, Ping and Pong call each other, Plain calls neither; each
                    // adds one after its call, so no call is a tail call.
                    var mod = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckOverflowMod"), typeof(object).Assembly);
                    var type = mod.DefineDynamicModule("OniBrutalCheckOverflowMod").DefineType("Deep", TypeAttributes.Public);
                    System.Reflection.Emit.MethodBuilder Define(string name) => type.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
                    var recurse = Define("Recurse");
                    var ping = Define("Ping");
                    var pong = Define("Pong");
                    var plain = Define("Plain");
                    foreach (var (method, callee) in new[] { (recurse, recurse), (ping, pong), (pong, ping) })
                    {
                        var il = method.GetILGenerator();
                        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                        il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1);
                        il.Emit(System.Reflection.Emit.OpCodes.Add);
                        il.Emit(System.Reflection.Emit.OpCodes.Call, callee);
                        il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1);
                        il.Emit(System.Reflection.Emit.OpCodes.Add);
                        il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    }
                    var plainIl = plain.GetILGenerator();
                    plainIl.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    plainIl.Emit(System.Reflection.Emit.OpCodes.Ret);
                    type.CreateType();
                    var modPath = Path.Combine(dir, "OniBrutalCheckOverflowMod.dll");
                    mod.Save(modPath);
                    int probed = MonoStackOverflow.RecursiveMethods(File.ReadAllBytes(modPath)).Count;
                    if (probed != 3) throw new InvalidOperationException($"{probed} methods found that can recurse, not 3");
                    var deep = ModAccess.LoadFrom(modPath).GetType("Deep")!;
                    if ((int)deep.GetMethod("Plain")!.Invoke(null, [7])! != 7) throw new InvalidOperationException("Plain changed");
                    string Overflow(string name)
                    {
                        try
                        {
                            deep.GetMethod(name)!.Invoke(null, [0]);
                        }
                        catch (TargetInvocationException e) when (e.InnerException is StackOverflowException s)
                        {
                            return s.Message;
                        }
                        throw new InvalidOperationException($"{name} returned");
                    }
                    // Measured in the original player: Mono's message.
                    const string mono = "The requested operation caused a stack overflow.";
                    foreach (var name in new[] { "Recurse", "Ping" })
                        if (Overflow(name) is var message && message != mono) throw new InvalidOperationException($"{name}: {message}");
                    return "both recursions throw StackOverflowException with Mono's message; the process goes on";
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            });
            Check("A special folder that does not exist (MonoFolderPaths)", () =>
            {
                MonoFolderPaths.Install();
                if (OperatingSystem.IsWindows()) return "Windows: unchanged";
                var home = Directory.CreateTempSubdirectory("oni-brutal-check-").FullName;
                var saved = (Environment.GetEnvironmentVariable("HOME"), Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"), Environment.GetEnvironmentVariable("XDG_DESKTOP_DIR"));
                try
                {
                    Environment.SetEnvironmentVariable("HOME", home);
                    Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(home, ".config"));
                    Environment.SetEnvironmentVariable("XDG_DESKTOP_DIR", null);
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    // Harmony's debug log does this with it (FileLog.LogPath).
                    Directory.CreateDirectory(desktop);
                    return desktop == Path.Combine(home, "Desktop") ? "~/Desktop is given although it did not exist, as on Mono" : throw new InvalidOperationException($"got '{desktop}'");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("HOME", saved.Item1);
                    Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", saved.Item2);
                    Environment.SetEnvironmentVariable("XDG_DESKTOP_DIR", saved.Item3);
                    try { Directory.Delete(home, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            });
            Check("A mod's string-keyed ConcurrentDictionary enumerated while it grows (MonoConcurrentDictionaries)", () =>
            {
                ModAccess.Install();
                var dir = Directory.CreateTempSubdirectory("oni-brutal-check-").FullName;
                try
                {
                    // A mod making a dictionary as PLib's registry does: (concurrencyLevel, capacity).
                    var cd = typeof(System.Collections.Concurrent.ConcurrentDictionary<string, object>);
                    var mod = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckDictionaries"), typeof(object).Assembly);
                    var type = mod.DefineDynamicModule("OniBrutalCheckDictionaries").DefineType("Registry", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                    var sized = type.DefineMethod("Sized", MethodAttributes.Public | MethodAttributes.Static, cd, [typeof(int), typeof(int)]).GetILGenerator();
                    sized.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    sized.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                    sized.Emit(System.Reflection.Emit.OpCodes.Newobj, cd.GetConstructor([typeof(int), typeof(int)])!);
                    sized.Emit(System.Reflection.Emit.OpCodes.Ret);
                    type.CreateType();
                    var modPath = Path.Combine(dir, "OniBrutalCheckDictionaries.dll");
                    mod.Save(modPath);
                    var registry = ModAccess.LoadFrom(modPath).GetType("Registry")!;
                    System.Collections.Concurrent.ConcurrentDictionary<string, object> Make(int concurrencyLevel, int capacity) =>
                        (System.Collections.Concurrent.ConcurrentDictionary<string, object>)registry.GetMethod("Sized")!.Invoke(null, [concurrencyLevel, capacity])!;
                    // Mono's order, from its ConcurrentDictionary (Managed/mscorlib.dll): buckets
                    // ascending, a bucket's newest entry first; bucket = (hash & 0x7fffffff) % size.
                    static string MonoOrder(IEnumerable<string> added, int size) => string.Join(" ", added
                        .Select((key, i) => (key, i, bucket: (MonoStringHash.Compute(key) & 0x7fffffff) % size))
                        .OrderBy(e => e.bucket).ThenByDescending(e => e.i).Select(e => e.key));
                    var keys = new[] { "PeterHan.PLib.Core.PLibCorePatches", "PeterHan.PLib.Database.PLocalization", "PeterHan.PLib.Buildings.PBuildingManager",
                        "PeterHan.PLib.AVC.PVersionCheck", "PeterHan.PLib.Options.POptions", "PeterHan.PLib.Lighting.PLightManager", "PeterHan.PLib.Actions.PActionManager",
                        "PeterHan.PLib.Detours.PDetours", "PeterHan.PLib.UI.PUIElements", "PeterHan.PLib.Codex.PCodexManager", "Alpha", "Beta", "Gamma" };
                    // PLib's sizes; 13 keys stay within Mono's lock budget (capacity / concurrencyLevel).
                    foreach (var (concurrencyLevel, capacity) in new[] { (2, 32), (2, 64), (1, 17) })
                    {
                        var dictionary = Make(concurrencyLevel, capacity);
                        foreach (var key in keys) dictionary.TryAdd(key, key);
                        var order = string.Join(" ", dictionary.Select(e => e.Key));
                        var mono = MonoOrder(keys, Math.Max(capacity, concurrencyLevel));
                        if (order != mono) throw new InvalidOperationException($"({concurrencyLevel}, {capacity}): order {order}; Mono's is {mono}");
                        if (order != string.Join(" ", dictionary.Keys)) throw new InvalidOperationException("Keys differ from the enumeration");
                    }
                    // PLib 4.16's registry: PBuildingManager's Initialize registers PPatchManager
                    // while the registry is enumerated; Mono's order reaches it afterwards.
                    var plib = Make(2, 32);
                    foreach (var key in keys.Take(5)) plib.TryAdd(key, key);
                    var visited = new List<string>();
                    foreach (var entry in plib)
                    {
                        visited.Add(entry.Key);
                        if (entry.Key.EndsWith(".PBuildingManager", StringComparison.Ordinal)) plib.TryAdd("PeterHan.PLib.PatchManager.PPatchManager", "");
                    }
                    if (!visited.Contains("PeterHan.PLib.PatchManager.PPatchManager")) throw new InvalidOperationException("PPatchManager, added during the enumeration, was not reached: " + string.Join(" ", visited));
                    // Many keys: the table grows and every key stays reachable.
                    var big = Make(2, 32);
                    for (int i = 0; i < 5000; i++) big[$"key{i}"] = i;
                    for (int i = 0; i < 5000; i++) if (!big.TryGetValue($"key{i}", out var v) || (int)v != i) throw new InvalidOperationException($"key{i} lost");
                    return "enumerated in Mono's bucket order; PPatchManager, added during PLib's registry walk, is reached";
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            });
            Check("A mod's conversions of NaN and out-of-range floats to integers (MonoConversions)", () =>
            {
                ModAccess.Install();
                var dir = Directory.CreateTempSubdirectory("oni-brutal-check-").FullName;
                try
                {
                    var mod = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckConversions"), typeof(object).Assembly);
                    var type = mod.DefineDynamicModule("OniBrutalCheckConversions").DefineType("Convert", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                    void Define(string name, Type result, Type[] parameters, System.Reflection.Emit.OpCode conversion)
                    {
                        var il = type.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static, result, parameters).GetILGenerator();
                        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                        il.Emit(conversion);
                        il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    }
                    Define("I4F", typeof(int), [typeof(float)], System.Reflection.Emit.OpCodes.Conv_I4);
                    Define("I4D", typeof(int), [typeof(double)], System.Reflection.Emit.OpCodes.Conv_I4);
                    Define("U1F", typeof(int), [typeof(float)], System.Reflection.Emit.OpCodes.Conv_U1);
                    Define("U4D", typeof(uint), [typeof(double)], System.Reflection.Emit.OpCodes.Conv_U4);
                    Define("U8D", typeof(ulong), [typeof(double)], System.Reflection.Emit.OpCodes.Conv_U8);
                    Define("I8F", typeof(long), [typeof(float)], System.Reflection.Emit.OpCodes.Conv_I8);
                    Define("IF", typeof(nint), [typeof(float)], System.Reflection.Emit.OpCodes.Conv_I);
                    // An integer's conversion stays as it is.
                    Define("Long", typeof(int), [typeof(long)], System.Reflection.Emit.OpCodes.Conv_I4);
                    // A product, a generic type's float result, and floats of two types meeting at
                    // a join.
                    var product = type.DefineMethod("Product", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(float), typeof(float)]).GetILGenerator();
                    product.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    product.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                    product.Emit(System.Reflection.Emit.OpCodes.Mul);
                    product.Emit(System.Reflection.Emit.OpCodes.Conv_I4);
                    product.Emit(System.Reflection.Emit.OpCodes.Ret);
                    var listed = type.DefineMethod("Listed", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(List<float>)]).GetILGenerator();
                    listed.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    listed.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0);
                    listed.Emit(System.Reflection.Emit.OpCodes.Callvirt, typeof(List<float>).GetMethod("get_Item")!);
                    listed.Emit(System.Reflection.Emit.OpCodes.Conv_I4);
                    listed.Emit(System.Reflection.Emit.OpCodes.Ret);
                    var joined = type.DefineMethod("Joined", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(bool), typeof(float), typeof(double)]).GetILGenerator();
                    var second = joined.DefineLabel();
                    var join = joined.DefineLabel();
                    joined.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    joined.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, second);
                    joined.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                    joined.Emit(System.Reflection.Emit.OpCodes.Br_S, join);
                    joined.MarkLabel(second);
                    joined.Emit(System.Reflection.Emit.OpCodes.Ldarg_2);
                    joined.MarkLabel(join);
                    joined.Emit(System.Reflection.Emit.OpCodes.Conv_I4);
                    joined.Emit(System.Reflection.Emit.OpCodes.Ret);
                    type.CreateType();
                    var modPath = Path.Combine(dir, "OniBrutalCheckConversions.dll");
                    mod.Save(modPath);
                    using (var module = Mono.Cecil.ModuleDefinition.ReadModule(modPath))
                    {
                        int found = MonoConversions.RewriteModule(module, out _, out int unsure);
                        if (found != 10 || unsure != 0) throw new InvalidOperationException($"{found} conversions of floats found ({unsure} not followed), not 10");
                    }
                    var convert = ModAccess.LoadFrom(modPath).GetType("Convert")!;
                    object Call(string name, object[] args) => convert.GetMethod(name)!.Invoke(null, args)!;
                    // The original player's values (UnityProbe convert scenario).
                    var cases = new (string Name, object[] Args, object Mono)[]
                    {
                        ("I4F", [float.NaN], int.MinValue), ("I4F", [float.PositiveInfinity], int.MinValue), ("I4F", [-1.5f], -1), ("I4F", [3e9f], int.MinValue),
                        ("I4D", [double.NegativeInfinity], int.MinValue), ("I4D", [2147483647.9], int.MaxValue), ("I4D", [-2147483648.9], int.MinValue),
                        ("U1F", [300f], 44), ("U1F", [float.NaN], 0), ("U4D", [-1.0], 4294967295u), ("U4D", [5e9], 705032704u),
                        ("U8D", [double.NegativeInfinity], 9223372036854775808ul), ("U8D", [double.NaN], 0ul), ("U8D", [1e19], 10000000000000000000ul),
                        ("I8F", [float.NaN], long.MinValue), ("IF", [-1f], unchecked((nint)4294967295L)), ("IF", [float.NaN], unchecked((nint)2147483648L)),
                        ("Long", [0x100000005L], 5), ("Product", [1e30f, 1e10f], int.MinValue), ("Product", [1.5f, 3f], 4),
                        ("Listed", [new List<float> { float.NaN }], int.MinValue), ("Joined", [false, 3e9f, 0.0], int.MinValue),
                        ("Joined", [true, 0f, double.NaN], int.MinValue), ("Joined", [true, 0f, -7.5], -7),
                    };
                    foreach (var (name, args, mono) in cases)
                        if (Call(name, args) is var got && !Equals(got, mono)) throw new InvalidOperationException($"{name}({string.Join(", ", args)}) gave {got}; Mono gives {mono}");
                    return $"{cases.Length} conversions give the original's values; an integer's conversion is untouched";
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            });
            Check("A mod's directory listings (MonoDirectoryListing)", () =>
            {
                ModAccess.Install();
                var dir = Directory.CreateTempSubdirectory("oni-brutal-check-").FullName;
                try
                {
                    // A mod listing folders as True Tiles does, a DirectoryInfo's recursive listing,
                    // and a delegate of Directory.GetFiles.
                    var mod = new System.Reflection.Emit.PersistedAssemblyBuilder(new AssemblyName("OniBrutalCheckListings"), typeof(object).Assembly);
                    var type = mod.DefineDynamicModule("OniBrutalCheckListings").DefineType("Listings", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                    var folders = type.DefineMethod("Folders", MethodAttributes.Public | MethodAttributes.Static, typeof(string[]), [typeof(string)]).GetILGenerator();
                    folders.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    folders.Emit(System.Reflection.Emit.OpCodes.Call, typeof(Directory).GetMethod(nameof(Directory.GetDirectories), [typeof(string)])!);
                    folders.Emit(System.Reflection.Emit.OpCodes.Ret);
                    var files = type.DefineMethod("Files", MethodAttributes.Public | MethodAttributes.Static, typeof(IEnumerable<FileInfo>), [typeof(DirectoryInfo)]).GetILGenerator();
                    files.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    files.Emit(System.Reflection.Emit.OpCodes.Ldstr, "*");
                    files.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1);
                    files.Emit(System.Reflection.Emit.OpCodes.Callvirt, typeof(DirectoryInfo).GetMethod(nameof(DirectoryInfo.EnumerateFiles), [typeof(string), typeof(SearchOption)])!);
                    files.Emit(System.Reflection.Emit.OpCodes.Ret);
                    var lister = type.DefineMethod("Lister", MethodAttributes.Public | MethodAttributes.Static, typeof(Func<string, string[]>), Type.EmptyTypes).GetILGenerator();
                    lister.Emit(System.Reflection.Emit.OpCodes.Ldnull);
                    lister.Emit(System.Reflection.Emit.OpCodes.Ldftn, typeof(Directory).GetMethod(nameof(Directory.GetFiles), [typeof(string)])!);
                    lister.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(Func<string, string[]>).GetConstructors()[0]);
                    lister.Emit(System.Reflection.Emit.OpCodes.Ret);
                    type.CreateType();
                    var modPath = Path.Combine(dir, "OniBrutalCheckListings.dll");
                    mod.Save(modPath);
                    using (var module = Mono.Cecil.ModuleDefinition.ReadModule(modPath))
                    {
                        int found = MonoDirectoryListing.RewriteModule(module);
                        int expected = OperatingSystem.IsWindows() ? 0 : 3;
                        if (found != expected) throw new InvalidOperationException($"{found} listings found, not {expected}");
                    }
                    // True Tiles' packs, made in an order that is not sorted; a folder of each.
                    var packs = Path.Combine(dir, "true_tiles_addon");
                    foreach (var pack in new[] { "Specialty", "Aquatic", "Prehistoric", "aquatic", "_Shared" })
                    {
                        Directory.CreateDirectory(Path.Combine(packs, pack, "textures"));
                        File.WriteAllText(Path.Combine(packs, pack, "tiles.json"), "{}");
                    }
                    var listings = ModAccess.LoadFrom(modPath).GetType("Listings")!;
                    string Names(IEnumerable<string> paths) => string.Join(" ", paths.Select(p => Path.GetRelativePath(packs, p)));
                    string Expect(string order) => OperatingSystem.IsWindows() ? null! : order;
                    var listed = Names((string[])listings.GetMethod("Folders")!.Invoke(null, [packs])!);
                    // Mono's order: each folder's entries by their UTF-8 bytes, breadth first.
                    if (Expect("Aquatic Prehistoric Specialty _Shared aquatic") is { } folderOrder && listed != folderOrder)
                        throw new InvalidOperationException($"GetDirectories gave {listed}; Mono gives {folderOrder}");
                    var recursive = Names(((IEnumerable<FileInfo>)listings.GetMethod("Files")!.Invoke(null, [new DirectoryInfo(packs)])!).Select(f => f.FullName));
                    if (Expect("Aquatic/tiles.json Prehistoric/tiles.json Specialty/tiles.json _Shared/tiles.json aquatic/tiles.json") is { } fileOrder && recursive != fileOrder)
                        throw new InvalidOperationException($"EnumerateFiles(*, AllDirectories) gave {recursive}; Mono gives {fileOrder}");
                    File.WriteAllText(Path.Combine(packs, "b"), "");
                    File.WriteAllText(Path.Combine(packs, "B"), "");
                    File.WriteAllText(Path.Combine(packs, "a"), "");
                    var delegated = Names(((Func<string, string[]>)listings.GetMethod("Lister")!.Invoke(null, null)!)(packs));
                    if (Expect("B a b") is { } delegateOrder && delegated != delegateOrder)
                        throw new InvalidOperationException($"a delegate of GetFiles gave {delegated}; Mono gives {delegateOrder}");
                    return OperatingSystem.IsWindows() ? "left as they are on Windows" : $"folders {listed}; files {recursive}; through a delegate {delegated}";
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            });
            Check("Bubble archetype ids of two elements (MonoValueTypeHashes)", () =>
            {
                MonoValueTypeHashes.Install();
                var game = Assembly.Load("Assembly-CSharp");
                var archetype = game.GetType("BubbleManager+Archetype", true)!;
                var simHashes = game.GetType("SimHashes", true)!;
                var getId = archetype.GetMethod("GetId")!;
                int Id(string element)
                {
                    var value = Activator.CreateInstance(archetype, BindingFlags.Public | BindingFlags.Instance, null, [new UnityEngine.Vector2(0f, 1f), Enum.Parse(simHashes, element)], null)!;
                    var id = getId.Invoke(value, null)!;
                    return (int)id.GetType().GetField("hashCode")!.GetValue(id)!;
                }
                int oxygen = Id("Oxygen"), methane = Id("Methane");
                // The fields' XOR, as Mono's ValueType.GetHashCode computes it (velocity, element,
                // alphaFadeSpeed = |velocity|).
                int expected = new UnityEngine.Vector2(0f, 1f).GetHashCode() ^ (int)Enum.Parse(simHashes, "Methane") ^ 1f.GetHashCode();
                if (methane != expected) throw new InvalidOperationException($"Methane's id is {methane}, Mono's fields give {expected}");
                return oxygen != methane ? $"Oxygen {oxygen}, Methane {methane}" : throw new InvalidOperationException($"both are {oxygen}");
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

        static int prefixedB, prefixedC, countedA;
        static void CountA() => countedA++;
        static void CountB() => prefixedB++;
        static void CountC() => prefixedC++;

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
            : OperatingSystem.IsMacOS()
            ? OniBrutal.Engine.Platform.GameLayout.DefaultMacSteamInstall(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
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
        /// `--game-tweak` values, in order (GameTweaks, EffectiveOptions): when any is given, they
        /// alone choose the game tweaks, over the Brutal Tweaks settings.
        public readonly List<string> GameTweaks = new();
        public bool CheckAssets;
        /// With --check-assets: where to list the script classes the data uses.
        public string? ListDataScripts;
        public bool CheckBind;
        public bool CheckShaders;
        /// --check-natives: the P/Invokes against the install's plugins (NativeCheck).
        public bool CheckNatives;
        /// With --check-natives: the architecture checked (`--native-arch x86_64|arm64`; the
        /// process's by default) and a host folder whose natives are checked (`--native-dir`).
        public int? NativeArch;
        public string? NativeDir;
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
        /// --background: the window takes no focus and the desktop's mouse and keyboard reach
        /// nothing (test runs on a desktop someone is using: PerfProbe-driven runs).
        public bool Background;
        /// --splash moo|none: the player's own loading splash (Gassy Moos falling while it loads),
        /// over the Brutal Tweaks settings; null when not given (then the effective value).
        public string? Splash;
        /// --frame-stats: average frame phase times in the log (PlayerLoop.FrameStats), over the
        /// Brutal Tweaks settings.
        public bool FrameStats;
        /// Diagnostics (--log-pointer-hits): the UI element under the mouse, logged when it changes.
        public bool LogPointerHits;
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
        public int Monitor;
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
                        int exe = Array.FindIndex(args, i + 1, a => OniBrutal.Engine.Platform.GameLayout.GameDirOfExecutable(a) != null);
                        if (exe < 0)
                        {
                            Console.Error.WriteLine("OniBrutal: no OxygenNotIncluded executable after --; the Steam launch options should end with -- %command%");
                            return null;
                        }
                        o.GameDir = OniBrutal.Engine.Platform.GameLayout.GameDirOfExecutable(args[exe])!;
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
                            Console.Error.WriteLine($"Unknown game tweak {name}; known: all, none, exact (all but {string.Join(", ", OniBrutal.Host.GameTweaks.GameTweakNames.ChangingBehaviour)}), {string.Join(", ", OniBrutal.Host.GameTweaks.GameTweakNames.All)}");
                            return null;
                        }
                        o.GameTweaks.Add(name);
                        break;
                    }
                    case "--check-bind": o.CheckBind = true; break;
                    case "--check-shaders": o.CheckShaders = true; break;
                    case "--check-natives": o.CheckNatives = true; break;
                    case "--native-arch": o.NativeArch = OniBrutal.Engine.Platform.NativeExports.Cpu(args[++i]) ?? throw new ArgumentException("--native-arch expects x86_64 or arm64"); break;
                    case "--native-dir": o.NativeDir = Path.GetFullPath(args[++i]); break;
                    case "--dump-shader": o.DumpShader = args[++i]; break;
                    case "--dump-script": o.DumpScript = args[++i]; break;
                    case "--frames": o.Frames = int.Parse(args[++i]); break;
                    case "--trace-exceptions": o.TraceExceptions = true; break;
                    case "--gl-debug": o.GlDebug = true; break;
                    case "--background": o.Background = true; break;
                    case "--splash":
                        o.Splash = args[++i];
                        if (!PlayerSettings.IsSplash(o.Splash))
                        {
                            Console.Error.WriteLine($"Unknown splash {o.Splash}; known: moo, none");
                            return null;
                        }
                        break;
                    case "--log-pointer-hits": o.LogPointerHits = true; break;
                    case "--vsync-count": o.VSyncCount = int.Parse(args[++i]); break;
                    case "--gpu-profile": o.GpuProfile = true; break;
                    case "--no-bindless": o.NoBindless = true; break;
                    case "--input": o.Input.Add(args[++i]); break;
                    // --input-file PATH: --input steps, one a line (scripts too long for a command line).
                    case "--input-file":
                        foreach (var line in File.ReadAllLines(args[++i]))
                            if (line.Trim() is { Length: > 0 } step && !step.StartsWith('#')) o.Input.Add(step);
                        break;
                    case "--frame-stats": o.FrameStats = true; break;
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
                    case "-monitor": o.Monitor = int.Parse(args[++i]); break;
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
                        Console.Error.WriteLine("usage: OniBrutal [--game DIR] [--log FILE] [--run-home DIR] [--audio-wav DIR] [--headless] [--batchmode] [--frames N] [--native-override NAME=PATH] [--trace-exceptions] [--gl-debug] [--background] [--splash moo|none] [--log-pointer-hits] [--vsync-count N] [--gpu-profile] [--no-bindless] [--input FRAME:ACTION[:ARGS]] [--input-file PATH] [--frame-stats] [--max-frame-rate N[:FRAMES]] [--dump-render-textures FRAME:DIR] [-screen-width W] [-screen-height H] [-screen-fullscreen 0|1] [-window-mode exclusive|borderless|windowed] [-monitor N] [--check-types] [--ui-packages DIR | --shipped-ui-packages] [--game-tweak NAME|-NAME|all|exact|none] [--check-assets [--list-data-scripts FILE]] [--check-bind] [--check-shaders] [--check-natives [--native-arch x86_64|arm64] [--native-dir DIR]] [--dump-shader NAME:DIR] [--capture FRAME:PATH[:SCALE]] [--probe-ui FRAME:X,Y] [--dump-ui FRAME:PATH] [--dump-ui-vertices PATH-SUBSTRING] [--dump-hierarchy FRAME:PATH] [--report-builtin-assets FILE] [--dump-component FRAME:PATH|TYPE] [-- STEAM-COMMAND]");
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
        public static void Configure(OniBrutal.Engine.Platform.GameLayout layout, Options options)
        {
            var dataDir = layout.DataDir;
            // app.info: company name, then product name.
            var info = File.ReadAllLines(Path.Combine(dataDir, "app.info"));
            EngineConfig.CompanyName = info.Length > 0 ? info[0] : "";
            EngineConfig.ProductName = info.Length > 1 ? info[1] : "";
            if (OperatingSystem.IsWindows()) ConfigureWindows(dataDir, options.RunHome);
            else if (OperatingSystem.IsMacOS() && layout.IsMacBundle) ConfigureMac(layout, options.RunHome);
            else ConfigureLinux(dataDir, options.RunHome);
            if (!OperatingSystem.IsMacOS() || !layout.IsMacBundle) EngineConfig.ConsoleLogPath = EngineConfig.PersistentDataPath + "/Player.log";
            // A bundle's Application.dataPath is its Contents (a Mac bundle read on Linux too, by
            // the check modes); the data it reads stays in Contents/Resources/Data.
            if (layout.IsMacBundle && !OperatingSystem.IsMacOS())
            {
                EngineConfig.DataPath = layout.DataPath;
                EngineConfig.StreamingAssetsPath = layout.StreamingAssetsPath;
            }
            EngineConfig.IsBatchMode = options.BatchMode;
            EngineConfig.Headless = options.Headless;
        }

        /// Unity's macOS player (OniBrutal.Engine.Platform.MacUserFolders): dataPath the bundle's
        /// Contents, persistent data and caches under ~/Library by the bundle identifier, the log
        /// in ~/Library/Logs.
        static void ConfigureMac(OniBrutal.Engine.Platform.GameLayout layout, string? runHome)
        {
            EngineConfig.DataPath = layout.DataPath;
            EngineConfig.StreamingAssetsPath = layout.StreamingAssetsPath;
            var home = runHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var folders = OniBrutal.Engine.Platform.MacUserFolders.For(home, layout.BundleIdentifier ?? $"unity.{EngineConfig.CompanyName}.{EngineConfig.ProductName}",
                EngineConfig.CompanyName, EngineConfig.ProductName);
            EngineConfig.PersistentDataPath = folders.PersistentDataPath;
            EngineConfig.TemporaryCachePath = folders.TemporaryCachePath;
            EngineConfig.ConsoleLogPath = folders.ConsoleLogPath;
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
