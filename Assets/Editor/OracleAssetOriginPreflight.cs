using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace OdysseyOracle.EditorAssets
{
    public sealed class OracleAssetOriginPreflight:IPreprocessBuildWithReport
    {
        public int callbackOrder=>-1000;
        public void OnPreprocessBuild(BuildReport report){Validate();}
        public static string Validate()
        {
            var roots=new List<string>();
            foreach(var scene in EditorBuildSettings.scenes)if(scene.enabled)roots.Add(scene.path);
            foreach(string path in AssetDatabase.GetAllAssetPaths())
                if(path.StartsWith("Assets/",StringComparison.Ordinal)&&path.Contains("/Resources/")&&!AssetDatabase.IsValidFolder(path))roots.Add(path);
            foreach(string dependency in AssetDatabase.GetDependencies(roots.ToArray(),true))
                if(dependency.StartsWith("Assets/PreservedUnverified/",StringComparison.Ordinal))throw new BuildFailedException("Unverified asset dependency is excluded from release: "+dependency);
            foreach(string resource in new[]{"Audio/odyssey_shattered_olympus_loop","Audio/odyssey_campfire_loop","Audio/odyssey_oracle_soft_cue","Interface/UnityLogo","OracleTutorialNightSky"})
                if(UnityEngine.Resources.Load(resource)!=null)throw new BuildFailedException("Unverified Resources entry remains: "+resource);
            return "PASS_KNOWN_UNVERIFIED_CONTENT_EXCLUDED_NOT_LEGAL_CLEARANCE";
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Enabled saved scenes and actual Resources dependency graph at cold build time.
// Array Formats: Finite asset-path dependency set and five banned resource identities; no runtime allocations.
// Dependencies/Pointers: Official public AssetDatabase build preflight refuses known uncertain assets; origin manifest remains separate evidence, not a legal guarantee.
