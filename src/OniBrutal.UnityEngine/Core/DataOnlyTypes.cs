namespace UnityEngine
{
    // Engine classes that occur in the game's data but that no game, package or plugin code refers
    // to. They exist so the objects load with their Unity type; their data stays on the native
    // record (StoredComponentBinder / StoredAssetBinder). Interfaces Unity implements on them
    // that need runtime systems we don't have are omitted, since nothing can observe them.

    /// A compute shader asset (referenced by materials/settings data; never dispatched by the game).
    public sealed class ComputeShader : Object
    {
        private ComputeShader() { }
    }
}
