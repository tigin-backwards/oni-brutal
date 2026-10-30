using System.Runtime.InteropServices;

namespace ShaderSurvey;

/// SPIRV-Tools' validator (the libSPIRV-Tools-shared that Ahwoo.BRUTAL.ShaderC ships), for SPIR-V
/// as a Vulkan 1.0 driver receives it.
static unsafe class SpirvValidator
{
    const int SpvEnvVulkan10 = 1;

    [StructLayout(LayoutKind.Sequential)]
    struct Diagnostic
    {
        public nuint Line, Column, Index;
        public byte* Error;
        public byte IsTextSource;
    }

    static readonly nint library = Load();
    static readonly delegate* unmanaged[Cdecl]<int, nint> contextCreate = (delegate* unmanaged[Cdecl]<int, nint>)NativeLibrary.GetExport(library, "spvContextCreate");
    static readonly delegate* unmanaged[Cdecl]<nint, uint*, nuint, Diagnostic**, int> validate =
        (delegate* unmanaged[Cdecl]<nint, uint*, nuint, Diagnostic**, int>)NativeLibrary.GetExport(library, "spvValidateBinary");
    static readonly delegate* unmanaged[Cdecl]<Diagnostic*, void> diagnosticDestroy = (delegate* unmanaged[Cdecl]<Diagnostic*, void>)NativeLibrary.GetExport(library, "spvDiagnosticDestroy");
    static readonly nint context = contextCreate(SpvEnvVulkan10);

    static nint Load()
    {
        string name = OperatingSystem.IsWindows() ? "SPIRV-Tools-shared.dll" : "libSPIRV-Tools-shared.so";
        foreach (var dir in new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "runtimes", OperatingSystem.IsWindows() ? "win-x64" : "linux-x64", "native") })
            if (NativeLibrary.TryLoad(Path.Combine(dir, name), out var h)) return h;
        return NativeLibrary.Load(name);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Text
    {
        public byte* Str;
        public nuint Length;
    }

    static readonly delegate* unmanaged[Cdecl]<nint, uint*, nuint, uint, Text**, Diagnostic**, int> toText =
        (delegate* unmanaged[Cdecl]<nint, uint*, nuint, uint, Text**, Diagnostic**, int>)NativeLibrary.GetExport(library, "spvBinaryToText");

    /// The module as SPIR-V assembly (spirv-dis with friendly names).
    public static string Disassemble(byte[] spirv)
    {
        const uint FriendlyNames = 0x40, Indent = 0x20;
        Text* text = null;
        Diagnostic* diagnostic = null;
        int result;
        fixed (byte* p = spirv) result = toText(context, (uint*)p, (nuint)(spirv.Length / 4), FriendlyNames | Indent, &text, &diagnostic);
        if (result != 0) return $"disassembly failed: {result}";
        return Marshal.PtrToStringUTF8((nint)text->Str, (int)text->Length);
    }

    /// Null when valid, else the validator's message.
    public static string? Validate(byte[] spirv)
    {
        Diagnostic* diagnostic = null;
        int result;
        fixed (byte* p = spirv) result = validate(context, (uint*)p, (nuint)(spirv.Length / 4), &diagnostic);
        if (result == 0) return null;
        string message = diagnostic != null && diagnostic->Error != null ? Marshal.PtrToStringUTF8((nint)diagnostic->Error)! : $"result {result}";
        if (diagnostic != null) diagnosticDestroy(diagnostic);
        return message;
    }
}
