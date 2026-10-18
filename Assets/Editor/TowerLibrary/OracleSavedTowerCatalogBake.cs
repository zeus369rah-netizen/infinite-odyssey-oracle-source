#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OdysseyOracle.EditorAssets
{
    // # ARCHITECTURE SNAPSHOT
    // Root-run Editor adapter. Validates the unchanged native-owned neutral
    // library, then serializes only canonical Mesh/Material references. Never
    // instantiates arbitrary prefab content or rewrites the owned library leaf.
    public static class OracleSavedTowerCatalogBake
    {
        public const string CatalogPath="Assets/Art/OracleTowerRuntime/tower-v2-catalog-v1.asset";
        public const string BootstrapScenePath="Assets/Scenes/OracleTrial.unity";
        static readonly string[] Slugs={"fire","water","earth","air"};
        [Serializable] sealed class Entry {public string path,kind,guid,sha256,metaSha256;}
        [Serializable] sealed class Manifest {public string version,sourceFingerprint;public string[] sourceFiles,sourceSha256;public Entry[] assets;}

        public static TowerModelAssetCatalog BakeAndBind(GameSession session)
        {
            Idle();
            if(session==null||!session.gameObject.scene.IsValid()||session.gameObject.scene.path!=BootstrapScenePath||!session.isActiveAndEnabled)
                throw new ArgumentException("Select the active OracleTrial bootstrap scene session.");
            ValidateOriginalLibrary();
            Mesh[] meshes=CanonicalMeshes();Material[] materials=CanonicalMaterials();
            var existing=AssetDatabase.LoadAssetAtPath<TowerModelAssetCatalog>(CatalogPath);
            if(existing==null)
            {
                if(File.Exists(CatalogPath)||File.Exists(CatalogPath+".meta")||AssetDatabase.LoadMainAssetAtPath(CatalogPath)!=null)
                    throw new InvalidOperationException("A different asset owns the requested runtime catalog path.");
                if(!AssetDatabase.IsValidFolder("Assets/Art/OracleTowerRuntime"))
                    AssetDatabase.CreateFolder("Assets/Art","OracleTowerRuntime");
                existing=ScriptableObject.CreateInstance<TowerModelAssetCatalog>();
                try
                {
                    existing.SetBakedReferences(TowerModelAssetCatalog.LibraryFingerprint,FileHash(OracleTowerAssetLibraryV2.AssetRoot+"/manifest.json"),meshes,materials);
                    AssetDatabase.CreateAsset(existing,CatalogPath);AssetDatabase.SaveAssetIfDirty(existing);
                }
                catch{if(existing!=null&&!AssetDatabase.Contains(existing))UnityEngine.Object.DestroyImmediate(existing);throw;}
            }
            ValidateCatalogReferences(existing);
            ValidateFrozenFiles(existing);
            Undo.RecordObject(session,"Bind validated saved tower catalog");
            session.SetTowerModelCatalog(existing);EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            return existing;
        }

        public static void ValidateBound(GameSession session)
        {
            Idle();ValidateOriginalLibrary();
            if(session==null||session.gameObject.scene.path!=BootstrapScenePath||!session.isActiveAndEnabled||session.TowerModels==null)
                throw new InvalidOperationException("The active OracleTrial scene session has no saved tower catalog.");
            if(AssetDatabase.GetAssetPath(session.TowerModels)!=CatalogPath)throw new InvalidOperationException("The scene session references a foreign catalog.");
            ValidateCatalogReferences(session.TowerModels);
        }

        public static void ValidateCatalogReferences(TowerModelAssetCatalog catalog)
        {
            if(catalog==null)throw new InvalidOperationException("Catalog missing.");
            if(!catalog.TryValidate(out string reason))throw new InvalidOperationException(reason);
            Mesh[] canonical=CanonicalMeshes();Material[] paints=CanonicalMaterials();
            for(int type=0;type<4;type++)
            {
                RequireSameAsset(catalog.GetMaterial(type),paints[type]);
                for(int tier=1;tier<=3;tier++)for(int group=0;group<6;group++)
                    RequireSameAsset(catalog.GetMesh(type,tier,group),canonical[(type*3+tier-1)*6+group]);
            }
        }

        static void ValidateOriginalLibrary()
        {
            var receipt=JsonUtility.FromJson<OracleTowerAssetLibraryV2.ValidationReceipt>(OracleTowerAssetLibraryV2.ValidateVersionedLibrary());
            if(receipt==null||receipt.status!="native-editor-asset-validated"||!receipt.neutralPrefabs||
                receipt.sourceFingerprint!=TowerModelAssetCatalog.LibraryFingerprint||receipt.version!=TowerModelAssetCatalog.LibraryVersion)
                throw new InvalidOperationException("The original native neutral library did not validate.");
        }
        static Mesh[] CanonicalMeshes()
        {
            var result=new Mesh[72];
            for(int type=0;type<4;type++)for(int tier=1;tier<=3;tier++)
            {
                // The original validator already checked the exact neutral
                // hierarchy, pivots, components, hashes, bounds and palette maps.
                GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OracleTowerAssetLibraryV2.PrefabPath(type,tier));
                if(prefab==null)throw new InvalidOperationException("A validated neutral tower prefab is missing.");
                for(int group=0;group<6;group++)
                {
                    string path=OracleTowerAssetLibraryV2.AssetRoot+"/Meshes/"+Slugs[type]+"_t"+tier+"_"+((TowerModelGroup)group).ToString().ToLowerInvariant()+".asset";
                    Mesh expected=AssetDatabase.LoadAssetAtPath<Mesh>(path);MeshFilter found=null;
                    foreach(MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                        if(filter.name==((TowerModelGroup)group).ToString())
                        {if(found!=null)throw new InvalidOperationException("Duplicated neutral prefab group.");found=filter;}
                    Mesh actual=found!=null?found.sharedMesh:null;RequireSameAsset(actual,expected);
                    if(found!=null)
                    {
                        MeshRenderer renderer=found.GetComponent<MeshRenderer>();
                        Material expectedPaint=AssetDatabase.LoadAssetAtPath<Material>(OracleTowerAssetLibraryV2.AssetRoot+"/Materials/"+Slugs[type]+".mat");
                        if(renderer==null||renderer.sharedMaterials.Length!=1)throw new InvalidOperationException("Neutral group must use one saved palette material.");
                        RequireSameAsset(renderer.sharedMaterial,expectedPaint);
                    }
                    result[(type*3+tier-1)*6+group]=actual;
                }
            }
            return result;
        }
        static Material[] CanonicalMaterials()
        {
            var result=new Material[4];for(int type=0;type<4;type++)
            {result[type]=AssetDatabase.LoadAssetAtPath<Material>(OracleTowerAssetLibraryV2.AssetRoot+"/Materials/"+Slugs[type]+".mat");if(result[type]==null)throw new InvalidOperationException("Missing saved palette.");}
            return result;
        }
        static void RequireSameAsset(UnityEngine.Object actual,UnityEngine.Object expected)
        {
            if(actual==null||expected==null){if(actual==null&&expected==null)return;throw new InvalidOperationException("Saved group presence differs.");}
            if(actual!=expected||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actual,out string a,out long x)||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(expected,out string b,out long y)||a!=b||x!=y)
                throw new InvalidOperationException("Catalog reference is not the exact validated saved asset GUID/local ID.");
        }
        public static void ValidateFrozenFiles(TowerModelAssetCatalog catalog)
        {
            if(catalog==null)throw new InvalidOperationException("Saved catalog is missing.");
            string manifestPath=OracleTowerAssetLibraryV2.AssetRoot+"/manifest.json";
            if(FileHash(manifestPath)!=catalog.ValidatedManifestHash)throw new InvalidOperationException("Saved tower manifest changed after native binding.");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if(manifest==null||manifest.version!=TowerModelAssetCatalog.LibraryVersion||manifest.sourceFingerprint!=TowerModelAssetCatalog.LibraryFingerprint||
                manifest.sourceFiles==null||manifest.sourceFiles.Length!=5||manifest.sourceSha256==null||manifest.sourceSha256.Length!=5||
                manifest.assets==null||manifest.assets.Length>160)throw new InvalidOperationException("Frozen tower manifest shape changed.");
            for(int i=0;i<5;i++)
            {
                string name=manifest.sourceFiles[i];
                if(string.IsNullOrEmpty(name)||Path.GetFileName(name)!=name)throw new InvalidOperationException("Invalid frozen tower source path.");
                string normalized=File.ReadAllText("Assets/Scripts/"+name).TrimStart('\uFEFF').Replace("\r\n","\n");
                if(Hash(Encoding.UTF8.GetBytes(normalized))!=manifest.sourceSha256[i])throw new InvalidOperationException("Frozen tower runtime source changed.");
            }
            foreach(Entry entry in manifest.assets)
            {
                if(entry==null||entry.path==null||!(entry.path==OracleTowerAssetLibraryV2.AssetRoot||entry.path.StartsWith(OracleTowerAssetLibraryV2.AssetRoot+"/",StringComparison.Ordinal))||entry.path.Contains(".."))
                    throw new InvalidOperationException("Unexpected saved tower ownership path.");
                if(AssetDatabase.AssetPathToGUID(entry.path)!=entry.guid||FileHash(entry.path+".meta")!=entry.metaSha256||
                    entry.kind!="folder"&&FileHash(entry.path)!=entry.sha256)throw new InvalidOperationException("Saved tower asset bytes, GUID or metadata changed.");
            }
        }
        static string FileHash(string path)=>Hash(File.ReadAllBytes(path));
        static string Hash(byte[] bytes)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",string.Empty).ToLowerInvariant();}
        static void Idle()
        {if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Run saved-catalog binding while the Editor is idle and Play is stopped.");}
    }

    // This preflight validates asset ownership again after a prior bind. No bake,
    // repair, scene mutation, material cloning or other implicit build-time write.
    public sealed class OracleSavedTowerCatalogPreflight : IPreprocessBuildWithReport
    {
        public int callbackOrder=>-50;
        public void OnPreprocessBuild(BuildReport report)
        {
            // The bootstrap scene must retain the catalog reference. Root's
            // explicit ValidateBound(session) verifies that scene assignment;
            // this additionally fences edits to the borrowed asset leaf.
            var catalog=AssetDatabase.LoadAssetAtPath<TowerModelAssetCatalog>(OracleSavedTowerCatalogBake.CatalogPath);
            OracleSavedTowerCatalogBake.ValidateFrozenFiles(catalog);
            OracleSavedTowerCatalogBake.ValidateCatalogReferences(catalog);
            bool bootstrapEnabled=false;
            foreach(var scene in EditorBuildSettings.scenes)
                if(scene.enabled&&scene.path==OracleSavedTowerCatalogBake.BootstrapScenePath)bootstrapEnabled=true;
            if(!bootstrapEnabled)throw new BuildFailedException("The validated OracleTrial bootstrap must be enabled in build settings.");
            int sessionsFound=0;
            foreach(GameSession session in UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsInactive.Include))
                if(session.gameObject.scene.IsValid()&&session.gameObject.scene.isLoaded&&session.gameObject.scene.path==OracleSavedTowerCatalogBake.BootstrapScenePath)
                {sessionsFound++;if(!session.isActiveAndEnabled||session.TowerModels!=catalog||session.gameObject.scene.isDirty)throw new BuildFailedException("Save the loaded OracleTrial scene with its active session and validated saved tower catalog reference before building.");}
            if(sessionsFound!=1)throw new BuildFailedException("Load the bound OracleTrial scene with exactly one active session before building saved tower gameplay.");
        }
    }
}
#endif
