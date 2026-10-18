#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    /// <summary>Runtime-created bindings and native buffers require a fresh scene after recompilation.</summary>
    [InitializeOnLoad]
    public static class OraclePlayModeSafety
    {
        static OraclePlayModeSafety()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        static void OnCompilationStarted(object context) => StopBeforeReload();

        static void OnBeforeAssemblyReload()
        {
            // Normal entry to Play also reloads assemblies while willChange is
            // true. Only interrupt an already running scene in this fallback.
            if (EditorApplication.isPlaying) StopBeforeReload();
        }

        static void StopBeforeReload()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Stop early, before Unity backs up initialized flags without their
            // callback bindings, static session owner and Persistent native buffers.
            EditorApplication.isPlaying = false;
            Debug.Log("Oracle stopped Play before script recompilation. Start a fresh Play session after compilation finishes.");
        }

        // # ARCHITECTURE SNAPSHOT
        // Editor-only lifecycle policy. No player code, saved progression, scene
        // serialization, coordinates or runtime native ownership is changed here.
        // Compilation starts the stop; component teardown owns resource disposal.
    }
}
#endif
