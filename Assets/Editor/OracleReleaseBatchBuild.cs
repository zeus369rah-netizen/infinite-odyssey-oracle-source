#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit batch entry only. Open the already saved bootstrap so the normal
    // build preflight can validate its catalog. No scene/asset bake or save.
    public static class OracleReleaseBatchBuild
    {
        public static void BuildWeb()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Use an explicit stopped, idle batch build.");
            EditorSceneManager.OpenScene("Assets/Scenes/OracleTrial.unity", OpenSceneMode.Single);
            OdysseyProjectSetup.BuildWeb();
        }
    }
}
#endif
