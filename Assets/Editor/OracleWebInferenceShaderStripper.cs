#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.Rendering;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Editor build-time only. A verified WebGL-player guard owns one build session and receipts.
    // Public preprocessing callbacks remove ONLY variants whose actual package asset path matches
    // Packages/com.unity.ai.inference/. No package/settings/log edits, shader substitution or Editor stripping.
    public sealed class OracleWebInferenceShaderStripper : IPreprocessBuildWithReport,IPostprocessBuildWithReport,IPreprocessShaders,IPreprocessComputeShaders
    {
        public int callbackOrder=>-10000;
        [Serializable] public sealed class Guard {
            public bool passed;public string[] guardedScannedSources,scannedAssemblyDefinitions,modelAssets,blockedReasons;public int scannedProjectAssets;
        }
        [Serializable] public sealed class Variant {
            public string path,kind,kernelOrPass;public long removed;public int callbacks;
        }
        [Serializable] public sealed class Receipt {
            public string id,stage,utc,target,outputPath,result,unityVersion,preReceipt,postReceipt;
            public string scope="WebGL player only; exact Packages/com.unity.ai.inference/ path prefix. Editor AI tools and every other shader untouched.";
            public Guard guard;public int uniqueShaders,uniqueComputeShaders,uniqueKernels,shaderCallbacks,computeCallbacks;
            public long shaderVariantsRemoved,computeVariantsRemoved;public List<Variant> variants=new List<Variant>();
        }
        static Receipt current;
        static readonly Dictionary<string,Variant> variants=new Dictionary<string,Variant>(StringComparer.Ordinal);
        static readonly HashSet<string> shaders=new HashSet<string>(StringComparer.Ordinal),computes=new HashSet<string>(StringComparer.Ordinal),kernels=new HashSet<string>(StringComparer.Ordinal);
        public static string CurrentReceiptJson=>current==null?"No active inference-strip build session.":JsonUtility.ToJson(current,true);

        public static Guard ValidateProjectForWeb()
        {
            var sources=new SortedSet<string>(StringComparer.Ordinal);var definitions=new SortedSet<string>(StringComparer.Ordinal);
            var models=new SortedSet<string>(StringComparer.Ordinal);var reasons=new List<string>();var guard=new Guard();
            try{
                string project=Directory.GetParent(Application.dataPath).FullName;
                // Actual Player assembly sources supplement conservative non-Editor Assets scanning.
                // Auto-referenced package assemblies alone are not proof of use by Assembly-CSharp.
                foreach(var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))foreach(string source in assembly.sourceFiles){
                    string asset=ToProjectAssetPath(source,project);if(OracleWebInferenceStripPolicy.IsProjectAsset(asset))sources.Add(asset);
                }
                foreach(string file in Directory.EnumerateFiles(Application.dataPath,"*",SearchOption.AllDirectories)){
                    string asset=ToProjectAssetPath(file,project);guard.scannedProjectAssets++;
                    if(OracleWebInferenceStripPolicy.IsRuntimeSourceCandidate(asset))sources.Add(asset);
                    string extension=Path.GetExtension(asset).ToLowerInvariant();
                    if((extension==".asmdef"||extension==".asmref")&&!OracleWebInferenceStripPolicy.IsEditorPath(asset))definitions.Add(asset);
                    if(OracleWebInferenceStripPolicy.IsModelPath(asset))models.Add(asset);
                    if(extension==".dll"&&!OracleWebInferenceStripPolicy.IsEditorPath(asset)){
                        var plugin=AssetImporter.GetAtPath(asset) as PluginImporter;
                        if(plugin==null||plugin.GetCompatibleWithPlatform(BuildTarget.WebGL))reasons.Add("Unreviewed project runtime DLL: "+asset);
                    }
                }
                if(sources.Count==0)reasons.Add("No project runtime C# sources resolved; refuse an unproven guard.");
                foreach(string source in sources)if(OracleWebInferenceStripPolicy.HasInferenceReference(File.ReadAllText(Path.Combine(project,source))))reasons.Add("Runtime C# inference reference: "+source);
                foreach(string definition in definitions){
                    string text=File.ReadAllText(Path.Combine(project,definition));
                    if(OracleWebInferenceStripPolicy.HasInferenceReference(text))reasons.Add("Project assembly inference reference: "+definition);
                    foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text,"GUID:([a-fA-F0-9]{32})")){
                        string referenced=AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                        if(string.IsNullOrEmpty(referenced))reasons.Add("Unresolved assembly GUID: "+definition+" / "+match.Value);
                        else if(OracleWebInferenceStripPolicy.IsInferencePackageAsset(referenced))reasons.Add("Project assembly references inference package GUID: "+definition);
                    }
                }
                foreach(string guid in AssetDatabase.FindAssets("t:ModelAsset",new[]{"Assets"}))models.Add(AssetDatabase.GUIDToAssetPath(guid));
                foreach(string asset in AssetDatabase.GetAllAssetPaths())if(OracleWebInferenceStripPolicy.IsProjectAsset(asset)&&Path.GetExtension(asset).Equals(".asset",StringComparison.OrdinalIgnoreCase)){
                    Type type=AssetDatabase.GetMainAssetTypeAtPath(asset);
                    if(type!=null&&(type.Name=="ModelAsset"||(type.FullName??"").StartsWith("Unity.InferenceEngine.",StringComparison.Ordinal)||(type.FullName??"").StartsWith("Unity.Sentis.",StringComparison.Ordinal)))models.Add(asset);
                }
                foreach(string model in models)reasons.Add("Project inference/model asset requires its backend: "+model);
            }
            catch(Exception error){reasons.Add("Guard could not complete: "+error.GetType().Name+": "+error.Message);}
            guard.guardedScannedSources=new List<string>(sources).ToArray();guard.scannedAssemblyDefinitions=new List<string>(definitions).ToArray();guard.modelAssets=new List<string>(models).ToArray();guard.blockedReasons=reasons.ToArray();guard.passed=reasons.Count==0;
            return guard;
        }
        static string ToProjectAssetPath(string path,string project)
        {
            string absolute=Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine(project,path));string prefix=Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar;
            return absolute.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)?"Assets/"+absolute.Substring(prefix.Length).Replace('\\','/'):"";
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            current=null;variants.Clear();shaders.Clear();computes.Clear();kernels.Clear();
            if(report.summary.platform!=BuildTarget.WebGL)return;
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Library","OracleBuildReceipts");Directory.CreateDirectory(folder);
            string id=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-"+Guid.NewGuid().ToString("N");string prefix=Path.Combine(folder,"inference-strip-"+id);
            current=new Receipt{id=id,stage="prebuild",utc=DateTime.UtcNow.ToString("o"),target=report.summary.platform.ToString(),outputPath=report.summary.outputPath,unityVersion=Application.unityVersion,preReceipt=prefix+".pre.json",postReceipt=prefix+".post.json",guard=ValidateProjectForWeb(),result="pending"};
            Save(current.preReceipt);
            if(!current.guard.passed){current.result="guard-refused";Save(current.preReceipt);throw new BuildFailedException("Oracle inference stripping refused: "+string.Join("; ",current.guard.blockedReasons)+". Receipt: "+current.preReceipt);}
            Debug.Log("Oracle Web inference guard passed for "+current.guard.guardedScannedSources.Length+" project runtime sources. Prebuild receipt: "+current.preReceipt);
        }
        static bool GuardedWebPlayer()
        {
            if(!BuildPipeline.isBuildingPlayer||EditorUserBuildSettings.activeBuildTarget!=BuildTarget.WebGL)return false;
            if(current==null||current.guard==null||!current.guard.passed||current.target!=BuildTarget.WebGL.ToString())throw new BuildFailedException("Inference shader callback has no verified WebGL-player prebuild guard; refuse to strip.");
            return true;
        }
        public void OnProcessShader(Shader shader,ShaderSnippetData snippet,IList<ShaderCompilerData> data)
        {
            if(!GuardedWebPlayer()||shader==null)return;string path=AssetDatabase.GetAssetPath(shader);
            if(!OracleWebInferenceStripPolicy.IsInferencePackageAsset(path))return;
            shaders.Add(path);current.shaderCallbacks++;current.shaderVariantsRemoved+=data.Count;
            Record(path,"shader",snippet.passName+"/"+snippet.shaderType,data.Count);data.Clear();
        }
        public void OnProcessComputeShader(ComputeShader shader,string kernelName,IList<ShaderCompilerData> data)
        {
            if(!GuardedWebPlayer()||shader==null)return;string path=AssetDatabase.GetAssetPath(shader);
            if(!OracleWebInferenceStripPolicy.IsInferencePackageAsset(path))return;
            computes.Add(path);kernels.Add(path+"::"+kernelName);current.computeCallbacks++;current.computeVariantsRemoved+=data.Count;
            Record(path,"compute",kernelName,data.Count);data.Clear();
        }
        static void Record(string path,string kind,string kernel,long removed)
        {
            string key=kind+"|"+path+"|"+kernel;if(!variants.TryGetValue(key,out var record)){record=new Variant{path=path,kind=kind,kernelOrPass=kernel};variants.Add(key,record);current.variants.Add(record);}
            record.removed+=removed;record.callbacks++;
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.WebGL)return;
            if(current==null||current.guard==null||!current.guard.passed)throw new BuildFailedException("Missing inference-strip prebuild receipt/guard.");
            current.stage="postbuild";current.utc=DateTime.UtcNow.ToString("o");current.result=report.summary.result.ToString();current.uniqueShaders=shaders.Count;current.uniqueComputeShaders=computes.Count;current.uniqueKernels=kernels.Count;
            try{Save(current.postReceipt);Debug.Log("Oracle Web inference variants removed: shader="+current.shaderVariantsRemoved+", compute="+current.computeVariantsRemoved+". Postbuild receipt: "+current.postReceipt);}
            finally{current=null;variants.Clear();shaders.Clear();computes.Clear();kernels.Clear();}
        }
        static void Save(string path){File.WriteAllText(path,JsonUtility.ToJson(current,true));}
    }

    // BEGIN PURE POLICY
    public static class OracleWebInferenceStripPolicy
    {
        public const string InferencePrefix="Packages/com.unity.ai.inference/";
        public static bool IsInferencePackageAsset(string path)=>path!=null&&path.StartsWith(InferencePrefix,StringComparison.Ordinal);
        public static bool IsProjectAsset(string path)=>path!=null&&path.StartsWith("Assets/",StringComparison.Ordinal);
        public static bool IsEditorPath(string path)=>path!=null&&path.IndexOf("/Editor/",StringComparison.OrdinalIgnoreCase)>=0;
        public static bool IsRuntimeSourceCandidate(string path)=>IsProjectAsset(path)&&!IsEditorPath(path)&&path.EndsWith(".cs",StringComparison.OrdinalIgnoreCase);
        public static bool IsModelPath(string path)
        {
            if(!IsProjectAsset(path))return false;string extension=System.IO.Path.GetExtension(path);
            return extension.Equals(".onnx",StringComparison.OrdinalIgnoreCase)||extension.Equals(".sentis",StringComparison.OrdinalIgnoreCase)||extension.Equals(".nn",StringComparison.OrdinalIgnoreCase);
        }
        public static bool HasInferenceReference(string text)=>text==null||
            System.Text.RegularExpressions.Regex.IsMatch(text,@"\b(?:InferenceEngine|Sentis|Barracuda|ModelAsset|ModelLoader|WorkerFactory|IWorker)\b|com\.unity\.ai\.inference",System.Text.RegularExpressions.RegexOptions.IgnoreCase)||
            // C#'s actual Worker type is case-sensitive; generic prose about browser worker threads is not an inference API.
            System.Text.RegularExpressions.Regex.IsMatch(text,@"\bWorker\b");
    }
    // END PURE POLICY
}
#endif
