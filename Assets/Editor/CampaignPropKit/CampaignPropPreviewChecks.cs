#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit root-run receipt wrapper for the six original mathematical prop assets.
    // No automatic execution, asset mutation, prefab instantiation, fallback material,
    // mesh rewrite, generation/account access or main-scene placement.
    public static class CampaignPropPreviewChecks
    {
        static readonly string[] Names={"RunicBookstand","ForgeWorkbench","SuppliesRack","BrazierRing","WallLantern","CrystalReliquary"};
        const string PrefabRoot="Assets/Art/CampaignLibrary/OraclePropKit_v1/Prefabs/CP_";
        [Serializable] public sealed class LibraryReceipt
        {
            public string utc,status,exception="";
            public bool actualAssetsWritten;
            public GeneratedPropChecks.PreviewReport[] previews;
        }

        public static string RenderProp(string exactName,string absoluteNewPng,int size=512)
        {
            RequireName(exactName);RequireNewOutput(absoluteNewPng);
            return GeneratedPropChecks.RenderPreview(PrefabRoot+exactName+".prefab",absoluteNewPng,size);
        }

        // Optional finite six-render call. No retry, update handler or deferred callback.
        // For minimal native diagnosis call RenderProp for RunicBookstand first instead.
        public static string RenderLibrary(string absoluteExistingWorkDirectory,int size=512)
        {
            var result=new LibraryReceipt{utc=DateTime.UtcNow.ToString("o"),status="not run",previews=new GeneratedPropChecks.PreviewReport[Names.Length]};
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
                    throw new InvalidOperationException("Play must be stopped and import/compilation settled.");
                if(!Path.IsPathRooted(absoluteExistingWorkDirectory)||!Directory.Exists(absoluteExistingWorkDirectory))
                    throw new ArgumentException("Supply an existing absolute work output directory.");
                for(int i=0;i<Names.Length;i++)RequireNewOutput(Path.Combine(absoluteExistingWorkDirectory,"CP_"+Names[i]+".png"));
                int accepted=0;
                for(int i=0;i<Names.Length;i++)
                {
                    string json=RenderProp(Names[i],Path.Combine(absoluteExistingWorkDirectory,"CP_"+Names[i]+".png"),size);
                    var preview=JsonUtility.FromJson<GeneratedPropChecks.PreviewReport>(json);result.previews[i]=preview;
                    if(preview!=null&&string.IsNullOrEmpty(preview.exception)&&preview.contentPixels>=128&&preview.cleaned)accepted++;
                }
                result.status=accepted==Names.Length?"Six native previews rendered; root visual review still required.":accepted+" of six native previews rendered; inspect individual failures. No automatic retry or asset conclusion.";
            }
            catch(Exception error){result.status="Preview preflight failed; no asset accepted.";result.exception=error.ToString();}
            return JsonUtility.ToJson(result,true);
        }
        static void RequireName(string exactName)
        {
            for(int i=0;i<Names.Length;i++)if(string.Equals(exactName,Names[i],StringComparison.Ordinal))return;
            throw new ArgumentException("Use one exact kit name: RunicBookstand, ForgeWorkbench, SuppliesRack, BrazierRing, WallLantern or CrystalReliquary.");
        }
        static void RequireNewOutput(string absoluteNewPng)
        {
            if(string.IsNullOrWhiteSpace(absoluteNewPng)||!Path.IsPathRooted(absoluteNewPng))throw new ArgumentException("An absolute new PNG path is required.");
            string path=Path.GetFullPath(absoluteNewPng),work=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work"))+Path.DirectorySeparatorChar;
            if(!path.StartsWith(work,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||!Directory.Exists(Path.GetDirectoryName(path))||File.Exists(path))
                throw new ArgumentException("Use a new PNG under an existing repository work directory; never overwrite prior evidence.");
        }
    }
}
#endif
