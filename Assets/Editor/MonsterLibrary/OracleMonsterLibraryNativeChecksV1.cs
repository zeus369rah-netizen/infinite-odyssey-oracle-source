#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OdysseyOracle.EditorAssets
{
    // Root-run only: saves the absent owned version, then proves that a second
    // export changed no output bytes, meta bytes or asset GUIDs. No tamper writes.
    public static class OracleMonsterLibraryNativeChecksV1
    {
        public static bool IsRunning { get; private set; }
        public static string LastResult { get; private set; }
        public static string LastError { get; private set; }
        static string queuedDirectory;
        static double readyAt;
        // Queues exactly one main-thread callback so a short remote eval can
        // return before the cold asset import. This is not a background thread
        // or a claim that the Editor stays responsive while imports execute.
        public static void QueueExportTwiceAndVerify(string absoluteReceiptDirectory)
        {
            OracleMonsterAssetLibraryV1.AssertEditorIdle();OracleMonsterAssetLibraryV1.CheckReceiptDirectory(absoluteReceiptDirectory);
            if(string.IsNullOrEmpty(absoluteReceiptDirectory))throw new ArgumentException("Queued work requires an external receipt directory.");
            if(IsRunning)throw new InvalidOperationException("One monster-library export is already queued/running; inspect its receipt instead of retrying.");
            IsRunning=true;LastResult=LastError=null;
            queuedDirectory=absoluteReceiptDirectory;readyAt=EditorApplication.timeSinceStartup+1d;
            EditorApplication.update+=RunQueued;
            AssemblyReloadEvents.beforeAssemblyReload+=CancelQueued;
        }
        static void CancelQueued()
        {
            EditorApplication.update-=RunQueued;AssemblyReloadEvents.beforeAssemblyReload-=CancelQueued;
            queuedDirectory=null;IsRunning=false;
        }
        static void RunQueued()
        {
            if(EditorApplication.timeSinceStartup<readyAt)return;
            string directory=queuedDirectory;CancelQueued();IsRunning=true;
            try {LastResult=ExportTwiceAndVerify(directory);}
            catch(Exception error)
            {
                LastError=error.ToString();
                OracleMonsterAssetLibraryV1.WriteReceipt(directory,"native-export-failure.txt",LastError);
            }
            finally {IsRunning=false;}
        }
        [Serializable] sealed class Receipt
        {
            public string status,utc,firstManifestSha256,secondManifestSha256;
            public int filesAndMeta,assetGuids;
            public bool byteIdentical,guidIdentical,mainScenesPreserved;
            public string[] limitations;
        }
        public static string ExportTwiceAndVerify(string absoluteReceiptDirectory)
        {
            OracleMonsterAssetLibraryV1.AssertEditorIdle();OracleMonsterAssetLibraryV1.CheckReceiptDirectory(absoluteReceiptDirectory);
            string before=OracleMonsterAssetLibraryV1.MainSceneSignature();
            OracleMonsterAssetLibraryV1.ExportVersionedLibrary(absoluteReceiptDirectory);
            var first=Snapshot();var guids=Guids();
            OracleMonsterAssetLibraryV1.ExportVersionedLibrary(absoluteReceiptDirectory);
            var second=Snapshot();var repeated=Guids();
            bool bytes=Equal(first,second),identities=Equal(guids,repeated),scenes=before==OracleMonsterAssetLibraryV1.MainSceneSignature();
            if(!bytes||!identities||!scenes)throw new InvalidOperationException("Repeated export changed asset bytes/GUIDs or main scenes. Inspect before continuing.");
            var receipt=new Receipt{status="native-neutral-assets-and-idempotence-validated",utc=DateTime.UtcNow.ToString("o"),filesAndMeta=first.Count,assetGuids=guids.Count,
                firstManifestSha256=first["manifest.json"],secondManifestSha256=second["manifest.json"],byteIdentical=bytes,guidIdentical=identities,mainScenesPreserved=scenes,
                limitations=new[]{"Actual asset/metadata/neutral-component checks only; no gameplay, motion, preview, browser or FPS acceptance.","No foreign/modified output was overwritten. Deliberate corruption tests were not performed on user assets."}};
            string json=JsonUtility.ToJson(receipt,true);OracleMonsterAssetLibraryV1.WriteReceipt(absoluteReceiptDirectory,"native-idempotence-receipt.json",json);return json;
        }
        static string Root=>Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,OracleMonsterAssetLibraryV1.AssetRoot));
        static SortedDictionary<string,string> Snapshot()
        {
            var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
            foreach(string path in Directory.GetFiles(Root,"*",SearchOption.AllDirectories))result.Add(path.Substring(Root.Length+1).Replace('\\','/'),Hash(File.ReadAllBytes(path)));
            result.Add("../version-leaf.meta",Hash(File.ReadAllBytes(Root+".meta")));return result;
        }
        static SortedDictionary<string,string> Guids()
        {
            var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
            foreach(string file in Directory.EnumerateFileSystemEntries(Root,"*",SearchOption.AllDirectories))
            {
                if(file.EndsWith(".meta",StringComparison.Ordinal))continue;
                string path=OracleMonsterAssetLibraryV1.AssetRoot+"/"+file.Substring(Root.Length+1).Replace('\\','/');
                string guid=AssetDatabase.AssetPathToGUID(path);if(string.IsNullOrEmpty(guid))throw new InvalidOperationException("Native catalog GUID missing: "+path);
                result.Add(path,guid);
            }
            result.Add(OracleMonsterAssetLibraryV1.AssetRoot,AssetDatabase.AssetPathToGUID(OracleMonsterAssetLibraryV1.AssetRoot));
            if(result.Count!=70)throw new InvalidOperationException("Native GUID inventory differs from the70-path closed catalog.");return result;
        }
        static bool Equal(SortedDictionary<string,string> a,SortedDictionary<string,string> b)
        {if(a.Count!=b.Count)return false;foreach(var item in a)if(!b.TryGetValue(item.Key,out string value)||value!=item.Value)return false;return true;}
        static string Hash(byte[] bytes){using(var hash=SHA256.Create()){var s=new StringBuilder();foreach(byte b in hash.ComputeHash(bytes))s.Append(b.ToString("x2"));return s.ToString();}}
    }
}
#endif
