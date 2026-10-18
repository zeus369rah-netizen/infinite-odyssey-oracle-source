#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OdysseyOracle.EditorAssets
{
    // Root-invoked only. Original immutable library is validated, never rebaked or
    // modified here. This creates one separately owned explicit-reference catalog
    // and binds the caller's actual scene session, without autoexecution/Resources.
    public static class OracleRefugeNpcBindingV1
    {
        public const string OutputRoot="Assets/Art/OracleRefugeNpcBindings/v1_0_0";
        public const string CatalogPath=OutputRoot+"/RefugeNpcAssetCatalog.asset";
        [Serializable] sealed class Receipt
        {
            public string status,catalog,scene,utc;public bool catalogCreated,sceneSaved,sceneDirty;
            public int models=2,meshes=18;public string[] limits;
        }
        public static string EditorBakeAndBind(GameSession session,string absoluteWorkReceiptDirectory,bool saveScene=false)
        {
            OracleRefugeNpcAssetLibraryV1.AssertEditorIdle();
            if(session==null||!session.gameObject.scene.IsValid()||!session.gameObject.scene.isLoaded||
                EditorSceneManager.IsPreviewScene(session.gameObject.scene))throw new ArgumentException("Pass the actual stopped bootstrap scene session.");
            if(string.IsNullOrEmpty(absoluteWorkReceiptDirectory)||!Path.IsPathRooted(absoluteWorkReceiptDirectory))
                throw new ArgumentException("Pass an absolute work receipt directory.");
            // Exact original manifest/source/material/18-mesh/hierarchy validation.
            OracleRefugeNpcAssetLibraryV1.ValidateVersionedLibrary(absoluteWorkReceiptDirectory);
            GameObject ilyra=AssetDatabase.LoadAssetAtPath<GameObject>(OracleRefugeNpcAssetLibraryV1.PrefabPath(0));
            GameObject bram=AssetDatabase.LoadAssetAtPath<GameObject>(OracleRefugeNpcAssetLibraryV1.PrefabPath(1));
            RefugeNpcAssetCatalog catalog;
            bool created=false;
            if(AssetDatabase.IsValidFolder(OutputRoot))
            {
                catalog=AssetDatabase.LoadAssetAtPath<RefugeNpcAssetCatalog>(CatalogPath);
                if(catalog==null||!catalog.TryValidate(out _)||catalog.Prefab(2)!=ilyra||catalog.Prefab(3)!=bram)
                    throw new InvalidOperationException("Existing NPC binding output is foreign, partial or changed; no overwrite was attempted.");
                string[] ids=AssetDatabase.FindAssets("",new[]{OutputRoot});
                if(ids.Length!=1||AssetDatabase.GUIDToAssetPath(ids[0])!=CatalogPath)
                    throw new InvalidOperationException("Existing binding leaf contains unexpected output; no overwrite was attempted.");
            }
            else
            {
                string absolute=Path.GetFullPath(Path.Combine(Application.dataPath,"../",OutputRoot));
                if(Directory.Exists(absolute)||File.Exists(absolute)||File.Exists(absolute+".meta"))
                    throw new InvalidOperationException("The new binding leaf is occupied outside AssetDatabase.");
                catalog=ScriptableObject.CreateInstance<RefugeNpcAssetCatalog>();
                try
                {
                    catalog.ConfigureValidatedOriginals(ilyra,bram);
                    EnsureFolder("Assets/Art/OracleRefugeNpcBindings");
                    EnsureFolder(OutputRoot);
                    AssetDatabase.CreateAsset(catalog,CatalogPath);AssetDatabase.SaveAssets();created=true;
                }
                catch
                {
                    // Never delete a persistent/partially saved asset or an existing
                    // leaf after failure. Destroy only our unsaved transient object.
                    if(catalog!=null&&!EditorUtility.IsPersistent(catalog))UnityEngine.Object.DestroyImmediate(catalog);
                    throw;
                }
            }
            bool changed=session.RefugeNpcModels!=catalog;
            if(changed)
            {
                Undo.RecordObject(session,"Bind original saved refuge NPC catalog");
                session.SetRefugeNpcCatalog(catalog);EditorUtility.SetDirty(session);
                EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            }
            bool saved=false;
            if(saveScene&&session.gameObject.scene.isDirty)
            {
                if(string.IsNullOrEmpty(session.gameObject.scene.path)||!session.gameObject.scene.path.StartsWith("Assets/",StringComparison.Ordinal))
                    throw new InvalidOperationException("Refuse saving an unnamed or nonproject scene.");
                saved=EditorSceneManager.SaveScene(session.gameObject.scene);
                if(!saved)throw new InvalidOperationException("Catalog bound, but the explicit scene save failed.");
            }
            var receipt=new Receipt{status="ORIGINAL_NPC_CATALOG_BOUND_NATIVE_RUNTIME_UNTESTED",catalog=CatalogPath,
                scene=session.gameObject.scene.path,catalogCreated=created,sceneSaved=saved,sceneDirty=session.gameObject.scene.isDirty,
                utc=DateTime.UtcNow.ToString("o"),limits=new[]{"No runtime walk/LOS/dialogue/authority or browser claim.",
                    "Neutral prefab/mesh/material assets were borrowed unchanged.","A dirty scene must be reviewed/saved before a build."}};
            string json=JsonUtility.ToJson(receipt,true);
            Directory.CreateDirectory(absoluteWorkReceiptDirectory);
            File.WriteAllText(Path.Combine(absoluteWorkReceiptDirectory,"npc-binding-receipt.json"),json);
            return json;
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=path.Substring(0,path.LastIndexOf('/')),name=path.Substring(path.LastIndexOf('/')+1);
            if(!AssetDatabase.IsValidFolder(parent))throw new InvalidOperationException("Expected explicit parent folder: "+parent);
            if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent,name)))throw new InvalidOperationException("Cannot create binding folder: "+path);
        }
    }
}
#endif
