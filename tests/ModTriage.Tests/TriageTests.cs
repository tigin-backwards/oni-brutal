// ModTriage without fixtures: a mod assembly is emitted in memory (PersistedAssemblyBuilder), with
// PLib merged and renamed into the mod's namespace, a stand-in Harmony, and patches made both by
// attribute and by a Harmony.Patch call. The scanner then reads it as it reads a Workshop DLL.
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace ModTriage.Tests;

public class FingerprintTests
{
    [Fact]
    public void PLibListHasTheNamesTheStrategyRecognisesItBy()
    {
        foreach (var name in new[] { "PDetours", "PPanel", "POptions", "PUIElements", "PRegistry" })
            Assert.True(Fingerprints.Default.PLib.ContainsKey(name), name);
        Assert.Equal("UI", Fingerprints.Default.PLib["PPanel"]);
        Assert.Equal("Options", Fingerprints.Default.PLib["POptions"]);
    }

    [Fact]
    public void RenamedPLibNamespaceIsPLib()
    {
        var fp = Fingerprints.Default;
        Assert.Equal(Library.PLib, fp.OfNamespace("SweepByType.PLib.UI", ["PPanel", "PUIElements", "PButton", "PLabel"]));
        Assert.Equal(Library.PLib, fp.OfNamespace("PeterHan.PLib.Core", ["Anything"]));
    }

    [Fact]
    public void ModNamespaceWithAFewPLibLikeNamesIsNot()
    {
        var fp = Fingerprints.Default;
        Assert.Equal(Library.None, fp.OfNamespace("MyMod", ["Patches", "MyBuildingConfig", "MyScreen", "PButton"]));
        Assert.Empty(fp.FlattenedPLib(["Patches", "MyBuildingConfig", "PButton", "ExtensionMethods"]));
    }

    [Fact]
    public void PLibFlattenedIntoTheModNamespaceIsRemovedTypeByType()
    {
        var names = new[] { "Patches", "MyBuildingConfig", "MyScreen", "Options1", "Options2", "Options3", "Options4", "Options5",
            "PDetours", "PPanel", "POptions", "PRegistry", "ExtensionMethods" };
        var fp = Fingerprints.Default;
        Assert.Equal(Library.None, fp.OfNamespace("MyMod", names));
        Assert.Equal(["PDetours", "PPanel", "POptions", "PRegistry", "ExtensionMethods"], fp.FlattenedPLib(names).ToArray());
    }

    [Fact]
    public void HarmonyAndNewtonsoftAreLibraries()
    {
        var fp = Fingerprints.Default;
        Assert.Equal(Library.Harmony, fp.OfNamespace("HarmonyLib", ["Harmony"]));
        Assert.Equal(Library.Harmony, fp.OfNamespace("MyMod.Merged", ["Harmony", "AccessTools", "Traverse"]));
        Assert.Equal(Library.Newtonsoft, fp.OfNamespace("Newtonsoft.Json.Linq", ["JToken"]));
        Assert.Equal(Library.Newtonsoft, Fingerprints.OfAssemblyName("Newtonsoft.Json"));
        Assert.Equal(Library.PLib, Fingerprints.OfAssemblyName("PLibOptions"));
    }

    [Fact]
    public void UtilLibsIsAHelperNotALibrary()
    {
        var fp = Fingerprints.Default;
        Assert.Equal(Library.None, fp.OfNamespace("UtilLibs.UIcmp", ["FButton", "FToggle", "FInputField2"]));
        Assert.True(fp.IsSharedHelper("UtilLibs.UIcmp", 1));
        Assert.False(fp.IsSharedHelper("SetStartDupes", 40));
    }
}

public class ScanTests
{
    static AssemblyFacts Scan()
    {
        using var stream = new MemoryStream(EmitMod());
        return AssemblyScan.Scan(stream, "TestMod.dll", Fingerprints.Default, GameIndex.Empty())!;
    }

    [Fact]
    public void MergedPLibAndHarmonyAreNotOwnTypes()
    {
        var facts = Scan();
        var own = facts.Types.Select(t => t.Name).ToHashSet();
        Assert.Contains("TestMod.Patches", own);
        Assert.Contains("TestMod.Patches+Console_Beep_Patch", own);
        Assert.DoesNotContain(own, n => n.Contains("PPanel") || n.Contains("PDetours") || n.StartsWith("HarmonyLib", StringComparison.Ordinal));
        Assert.Equal(5, facts.Excluded["PLib"]);
        Assert.Equal(4, facts.Excluded["Harmony"]);
        Assert.Contains("PLib:TestMod.PLib", facts.ExcludedNamespaces);
    }

    [Fact]
    public void CallsIntoMergedPLibAreRecordedByArea()
    {
        var facts = Scan();
        Assert.Contains("UI:PPanel::.ctor", facts.Mod.PLibRefs);
        Assert.Contains("Options:POptions::RegisterOptions", facts.Mod.PLibRefs);
    }

    [Fact]
    public void PatchTargetsFromAttributesAndPatchCalls()
    {
        var targets = Scan().Patches.Select(p => (p.Type, p.Method, p.Source)).ToList();
        Assert.Contains(("System.Console", "Beep", "attribute"), targets);
        // Harmony.Patch(AccessTools.Method(typeof(Console), "WriteLine"), new HarmonyMethod(typeof(Patches), "Prefix")):
        // the original, not the patch method.
        Assert.Contains(("System.Console", "WriteLine", "il"), targets);
        Assert.DoesNotContain(targets, t => t.Method == "Prefix");
    }

    /// TestMod.dll: PLib (renamed into TestMod.PLib), a stand-in HarmonyLib, and the mod.
    static byte[] EmitMod()
    {
        var ab = new PersistedAssemblyBuilder(new AssemblyName("TestMod"), typeof(object).Assembly);
        var mb = ab.DefineDynamicModule("TestMod");
        const TypeAttributes pub = TypeAttributes.Public | TypeAttributes.Class;

        // PLib, merged and renamed.
        var pPanel = mb.DefineType("TestMod.PLib.PPanel", pub);
        var panelCtor = pPanel.DefineDefaultConstructor(MethodAttributes.Public);
        var pOptions = mb.DefineType("TestMod.PLib.POptions", pub);
        var register = pOptions.DefineMethod("RegisterOptions", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [typeof(Type)]);
        register.GetILGenerator().Emit(OpCodes.Ret);
        foreach (var name in new[] { "PDetours", "PRegistry", "PUIElements" }) mb.DefineType("TestMod.PLib." + name, pub).CreateType();

        // Harmony's surface, as merged into a mod.
        var patchAttr = mb.DefineType("HarmonyLib.HarmonyPatch", pub, typeof(Attribute));
        var patchAttrCtor = patchAttr.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(Type), typeof(string)]);
        patchAttrCtor.GetILGenerator().Emit(OpCodes.Ret);
        var accessTools = mb.DefineType("HarmonyLib.AccessTools", pub);
        var method = accessTools.DefineMethod("Method", MethodAttributes.Public | MethodAttributes.Static, typeof(MethodInfo), [typeof(Type), typeof(string)]);
        Return(method, OpCodes.Ldnull);
        var harmonyMethod = mb.DefineType("HarmonyLib.HarmonyMethod", pub);
        var hmCtor = harmonyMethod.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(Type), typeof(string)]);
        hmCtor.GetILGenerator().Emit(OpCodes.Ret);
        var harmony = mb.DefineType("HarmonyLib.Harmony", pub);
        var patch = harmony.DefineMethod("Patch", MethodAttributes.Public, typeof(void), [typeof(MethodBase), harmonyMethod]);
        patch.GetILGenerator().Emit(OpCodes.Ret);

        // The mod.
        var patches = mb.DefineType("TestMod.Patches", pub);
        var getType = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!;
        var apply = patches.DefineMethod("Apply", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [harmony]);
        var il = apply.GetILGenerator();
        il.Emit(OpCodes.Newobj, panelCtor);
        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ldtoken, patches);
        il.Emit(OpCodes.Call, getType);
        il.Emit(OpCodes.Call, register);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldtoken, typeof(Console));
        il.Emit(OpCodes.Call, getType);
        il.Emit(OpCodes.Ldstr, "WriteLine");
        il.Emit(OpCodes.Call, method);
        il.Emit(OpCodes.Ldtoken, patches);
        il.Emit(OpCodes.Call, getType);
        il.Emit(OpCodes.Ldstr, "Prefix");
        il.Emit(OpCodes.Newobj, hmCtor);
        il.Emit(OpCodes.Callvirt, patch);
        il.Emit(OpCodes.Ret);
        var prefix = patches.DefineMethod("Prefix", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        prefix.GetILGenerator().Emit(OpCodes.Ret);
        var beep = patches.DefineNestedType("Console_Beep_Patch", TypeAttributes.NestedPublic | TypeAttributes.Class);
        beep.SetCustomAttribute(new CustomAttributeBuilder(patchAttrCtor, [typeof(Console), "Beep"]));
        var postfix = beep.DefineMethod("Postfix", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        postfix.GetILGenerator().Emit(OpCodes.Ret);

        foreach (var t in new[] { pPanel, pOptions, patchAttr, accessTools, harmonyMethod, harmony, patches, beep }) t.CreateType();
        using var output = new MemoryStream();
        ab.Save(output);
        return output.ToArray();
    }

    static void Return(MethodBuilder m, OpCode value)
    {
        var il = m.GetILGenerator();
        il.Emit(value);
        il.Emit(OpCodes.Ret);
    }
}
