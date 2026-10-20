// The members of the JetBrains code annotations Unity's core module carries (shapes generated in
// Generated/UnityEngine.CoreModule/JetBrains.Annotations.cs); mods built against it reference
// them. They describe code for analysers and do nothing at run time.
namespace JetBrains.Annotations
{
    public sealed partial class PublicAPIAttribute
    {
        public PublicAPIAttribute() { }

        public PublicAPIAttribute(string comment) => Comment = comment;

        public string Comment { get; }
    }

    public sealed partial class UsedImplicitlyAttribute
    {
        public UsedImplicitlyAttribute() : this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default) { }

        public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags) : this(useKindFlags, ImplicitUseTargetFlags.Default) { }

        public UsedImplicitlyAttribute(ImplicitUseTargetFlags targetFlags) : this(ImplicitUseKindFlags.Default, targetFlags) { }

        public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags)
        {
            UseKindFlags = useKindFlags;
            TargetFlags = targetFlags;
        }

        public ImplicitUseKindFlags UseKindFlags { get; }

        public ImplicitUseTargetFlags TargetFlags { get; }
    }

    public sealed partial class MeansImplicitUseAttribute
    {
        public MeansImplicitUseAttribute() : this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default) { }

        public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags) : this(useKindFlags, ImplicitUseTargetFlags.Default) { }

        public MeansImplicitUseAttribute(ImplicitUseTargetFlags targetFlags) : this(ImplicitUseKindFlags.Default, targetFlags) { }

        public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags)
        {
            UseKindFlags = useKindFlags;
            TargetFlags = targetFlags;
        }

        public ImplicitUseKindFlags UseKindFlags { get; }

        public ImplicitUseTargetFlags TargetFlags { get; }
    }
}
