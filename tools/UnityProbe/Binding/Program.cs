// Writes the assemblies of the binding probe (AssemblyBinding.cs, PROBE_SCENARIO=asmbind) into
// DIR (the probe's mod folder's binding/): libraries in several versions, with and without a strong
// name, each in a folder of its own, and users built against them, which call the library's
// Who() and return what it says ("BindA 1.0 A1": name, version, folder). The probe loads each
// folder as KMod loads a mod's (DLLLoader.LoadDLLs) and calls the users, so each answer tells
// which copy a reference bound to.
//
//   dotnet run --project tools/UnityProbe/Binding -- DIR
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

var root = args.Length == 1 ? args[0] : throw new ArgumentException("usage: ProbeBindGen DIR");
Directory.CreateDirectory(root);

// K signs BindC and BindD (its token is read back from the first library it signs); another
// key's token stands in for a reference with the wrong key.
var (keyBlob, publicKey) = Key();
byte[] token = [], otherToken = Token(Key().publicKey);

// Unsigned BindA in three versions, each in a folder of its own; users of 2.0, 3.0, 0.0 and of
// "binda" 9.0 (another case), none with a copy beside it; UE uses 2.0 with a 2.0 copy beside it.
Library("A1", "BindA", "1.0.0.0");
Library("A2", "BindA", "2.0.0.0");
Library("A3", "BindA", "3.0.0.0");
User("UA2", "UseA2", "BindA", "BindA", "2.0.0.0");
User("UA3", "UseA3", "BindA", "BindA", "3.0.0.0");
User("UA0", "UseA0", "BindA", "BindA", "0.0.0.0");
User("UAcase", "UseAcase", "BindA", "binda", "9.0.0.0");
User("UE", "UseE", "BindA", "BindA", "2.0.0.0");
Library("UE", "BindA", "2.0.0.0");
// Unsigned BindB, loaded nowhere: UB uses 2.0 with a 1.0 copy beside it; UB3 uses 3.0, no copy.
User("UB", "UseB", "BindB", "BindB", "2.0.0.0");
Library("UB", "BindB", "1.0.0.0");
User("UB3", "UseB3", "BindB", "BindB", "3.0.0.0");
// BindN exists nowhere.
User("UN", "UseN", "BindN", "BindN", "1.0.0.0");
// BindC, signed with K, in 1.0 and 2.0; users of 2.0, 0.5 and 1.0 with K, of 1.0 with no key and
// of 1.0 with another key's token, UC1, of 1.0 with K, and UC2b, of 2.0 after 2.0 is loaded; C1b is a copy of 1.0.
Library("C1", "BindC", "1.0.0.0", signed: true);
using (var c1 = AssemblyDefinition.ReadAssembly(Path.Combine(root, "C1", "BindC.dll"))) token = c1.Name.PublicKeyToken;
Library("C1b", "BindC", "1.0.0.0", signed: true);
Library("C2", "BindC", "2.0.0.0", signed: true);
User("UC2", "UseC2", "BindC", "BindC", "2.0.0.0", token);
User("UC05", "UseC05", "BindC", "BindC", "0.5.0.0", token);
User("UCnk", "UseCnk", "BindC", "BindC", "1.0.0.0");
User("UCbad", "UseCbad", "BindC", "BindC", "1.0.0.0", otherToken);
User("UC1", "UseC1", "BindC", "BindC", "1.0.0.0", token);
User("UC2b", "UseC2b", "BindC", "BindC", "2.0.0.0", token);
// BindD, signed with K, loaded nowhere: UD uses 2.0 with a 1.0 copy beside it; UD1 uses 1.0
// with 1.0 beside it.
User("UD", "UseD", "BindD", "BindD", "2.0.0.0", token);
Library("UD", "BindD", "1.0.0.0", signed: true);
User("UD1", "UseD1", "BindD", "BindD", "1.0.0.0", token);
Library("UD1", "BindD", "1.0.0.0", signed: true);
Console.WriteLine($"wrote {Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories).Length} assemblies to {root}");

// A library NAME VERSION in FOLDER: public static class NAME { public static string Who() =>
// "NAME VERSION FOLDER"; }.
void Library(string folder, string name, string version, bool signed = false)
{
    var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(name, Version.Parse(version)), name, ModuleKind.Dll);
    var module = assembly.MainModule;
    var type = new TypeDefinition("", name, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
    module.Types.Add(type);
    var who = new MethodDefinition("Who", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.String);
    var v = Version.Parse(version);
    who.Body.GetILProcessor().Emit(OpCodes.Ldstr, $"{name} {v.Major}.{v.Minor} {folder}");
    who.Body.GetILProcessor().Emit(OpCodes.Ret);
    type.Methods.Add(who);
    Write(assembly, folder, signed);
}

// A user USER in FOLDER of library TYPE, its reference naming the assembly REFNAME VERSION (and
// TOKEN): public static class USER { public static string Call() => TYPE.Who(); }.
void User(string folder, string user, string type, string refName, string version, byte[]? refToken = null)
{
    var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(user, new Version(1, 0, 0, 0)), user, ModuleKind.Dll);
    var module = assembly.MainModule;
    var reference = new AssemblyNameReference(refName, Version.Parse(version));
    if (refToken != null) reference.PublicKeyToken = refToken;
    module.AssemblyReferences.Add(reference);
    var target = new TypeReference("", type, module, reference);
    var who = new MethodReference("Who", module.TypeSystem.String, target);
    var use = new TypeDefinition("", user, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
    module.Types.Add(use);
    var call = new MethodDefinition("Call", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.String);
    call.ImplAttributes |= MethodImplAttributes.NoInlining;
    call.Body.GetILProcessor().Emit(OpCodes.Call, who);
    call.Body.GetILProcessor().Emit(OpCodes.Ret);
    use.Methods.Add(call);
    Write(assembly, folder, false);
}

void Write(AssemblyDefinition assembly, string folder, bool signed)
{
    var dir = Path.Combine(root, folder);
    Directory.CreateDirectory(dir);
    var parameters = new WriterParameters();
    if (signed)
    {
        assembly.Name.PublicKey = publicKey;
        assembly.Name.HashAlgorithm = AssemblyHashAlgorithm.SHA1;
        parameters.StrongNameKeyBlob = keyBlob;
    }
    assembly.Write(Path.Combine(dir, assembly.Name.Name + ".dll"), parameters);
}

// A new 1024-bit RSA key: its private CSP blob (what Cecil signs with) and the assembly public
// key (signature and hash algorithm ids, the blob's size, then the public CSP blob).
static (byte[] blob, byte[] publicKey) Key()
{
    using var rsa = new RSACryptoServiceProvider(1024);
    var publicBlob = rsa.ExportCspBlob(false);
    var key = new byte[12 + publicBlob.Length];
    BitConverter.GetBytes(0x00002400).CopyTo(key, 0); // CALG_RSA_SIGN
    BitConverter.GetBytes(0x00008004).CopyTo(key, 4); // CALG_SHA1
    BitConverter.GetBytes(publicBlob.Length).CopyTo(key, 8);
    publicBlob.CopyTo(key, 12);
    return (rsa.ExportCspBlob(true), key);
}

// The public key token: the last eight bytes of the key's SHA-1, reversed.
static byte[] Token(byte[] publicKey)
{
    var hash = SHA1.HashData(publicKey);
    var t = hash[^8..];
    Array.Reverse(t);
    return t;
}
