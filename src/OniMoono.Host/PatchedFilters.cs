using System;

namespace OniMoono.Host
{
    /// ARCHITECTURE D53. MonoMod (in the game's 0Harmony) builds a replacement whose body has a
    /// filter (`catch … when`) or fault clause as a MethodBuilder in a new dynamic assembly
    /// (DynamicMethodDefinition.Generate), not as the DynamicMethod it builds for all others:
    /// .NET Framework's DynamicMethod could not hold those clauses. That generator's code
    /// references AssemblyBuilder.Save, which CoreCLR lacks, so the patch threw
    /// MissingMethodException (the background-save tweak's patch of LoadScreen.MigrateFile ended
    /// the player). MonoMod is told through its own switch (AppContext data `MonoMod.DMDType`;
    /// a `MONOMOD_DMDType` environment variable still takes precedence) to build every
    /// replacement as a DynamicMethod, which holds both clauses on CoreCLR.
    ///
    /// A filter clause still cannot be patched, as on Mono: Harmony copies it with MonoMod's
    /// Cecil IL generator, which never sets its handler start, so either generator ends the
    /// exception block with the filter open and the IL generator throws InvalidOperationException
    /// ("Incorrect code generation for exception block"; Mono's ILGenerator.EndExceptionBlock
    /// checks for an open filter too). Harmony.Patch throws it and the game goes on.
    internal static class PatchedFilters
    {
        public static void Install() => AppContext.SetData("MonoMod.DMDType", "dynamicmethod");
    }
}
