#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using OdysseyOracle.CampaignNpcs;

namespace OdysseyOracle.EditorAssets
{
    // # ARCHITECTURE SNAPSHOT
    // Cold Editor asset adapter. Exact runtime recipe/pose bridge -> readable
    // owned meshes + one original palette material and two textures + script-free neutral prefabs.
    // No gameplay instance, resource search, animation tick, physics or credit use.
    // Each version is immutable; foreign, partial or modified leaves fail closed.
    public static class OracleRefugeNpcAssetLibraryV1
    {
        public const string Version="v1_0_0";
        public const string AssetRoot="Assets/Art/OracleRefugeNpcLibrary/"+Version;
        const string Owner="odyssey.oracle.original-refuge-npc-library.20261016.v1";
        const string ManifestPath=AssetRoot+"/manifest.json",OwnerPath=AssetRoot+"/ownership.json";
        const string ExporterPath="Assets/Editor/RefugeNpcLibrary/OracleRefugeNpcAssetLibraryV1.cs";
        const string UrpVersionScript="Packages/com.unity.render-pipelines.universal/Editor/AssetVersion.cs";
        static readonly string[] Slugs={"ilyra","bram"};
        static readonly int[] PartCounts={9,9},TriangleCounts={1548,1468};
        static readonly Color[] PaletteColors={new Color(.075f,.09f,.10f),new Color(.66f,.43f,.29f),new Color(.84f,.81f,.68f),new Color(.12f,.30f,.48f),new Color(.17f,.60f,.35f),new Color(.70f,.37f,.15f),new Color(.16f,.68f,.64f),new Color(.18f,.16f,.15f),new Color(.76f,.55f,.40f),new Color(.85f,.66f,.22f),new Color(.94f,.93f,.86f),new Color(.035f,.045f,.04f),new Color(.08f,.95f,.40f),new Color(.12f,.85f,.78f),Color.black,Color.black};
        static readonly string[] Sources={"NpcModelBuilder.cs","CampaignNpcRecipes.cs","CampaignNpcPresentation.cs","RefugeNpcNeutralModelAuthoring.cs"};
        static readonly string[] SourceHashes={"cbefa20bc1cb32885e8368a5f5cc9f3efb52c73f258cfe46bc4b4cb099efd241","99ba4d9d5da5e86ae0a287f01a1eedb7745a2e17262abf800043ff3d47eebbc5","78bb627b40a1645eb94c75aea5ba90ec7f10335dc555f5faa528bac2bd288130","7c4e9ba222221fd3fa8de4df3d51085cb80f949ab191b5ff8272727b79da962f"};

        [Serializable] sealed class Ownership {public string owner,version,fingerprint;}
        [Serializable] public sealed class AssetRecord {public string path,kind,guid,sha256,metaSha256,geometryHash;}
        [Serializable] public sealed class ModelRecord
        {
            public int kind,role,triangles,renderers;
            public string path,name,hierarchyHash;
            public Vector3 boundsCenter,boundsSize,feet,interact,voice,handTool;
        }
        [Serializable] sealed class Manifest
        {
            public string owner,version,fingerprint,createdUtc,manifestGuid,manifestMetaSha256;
            public string[] sourceFiles,sourceSha256;
            public AssetRecord[] assets;
            public ModelRecord[] models;
        }
        [Serializable] public sealed class Receipt
        {
            public string status,version,assetRoot,manifestSha256,sourceFingerprint,utc;
            public bool alreadyExisted,neutralPrefabs,mainScenesPreserved;
            public int meshes,materials,palettes,prefabs,triangles;
            public ModelRecord[] models;
            public string[] limitations;
        }

        public static string PrefabPath(int kind)
        {Require(kind>=0&&kind<2,"NPC kind must be0(Ilyra) or1(Bram).");return AssetRoot+"/Prefabs/"+Slugs[kind]+".prefab";}
        static string MeshPath(int kind,int part)=>AssetRoot+"/Meshes/"+Slugs[kind]+"/part_"+part.ToString("D2")+".asset";
        static string MaterialPath(int paint)=>AssetRoot+"/Materials/refuge_shared.mat";
        static string PalettePath(int index)=>AssetRoot+"/Palettes/"+(index==0?"base":"emission")+".asset";

        [MenuItem("Tools/Infinite Odyssey/Refuge NPC library/Export neutral v1")]
        static void ExportMenu()=>Debug.Log(ExportVersionedLibrary());
        [MenuItem("Tools/Infinite Odyssey/Refuge NPC library/Validate neutral v1")]
        static void ValidateMenu()=>Debug.Log(ValidateVersionedLibrary());

        public static void AssertEditorIdle()
        {Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!BuildPipeline.isBuildingPlayer,
            "Run on the Editor main thread after Play, compilation, import and builds have stopped.");}

        public static string ExportVersionedLibrary(string absoluteReceiptDirectory=null)
        {
            AssertEditorIdle();CheckReceiptDirectory(absoluteReceiptDirectory);
            string fingerprint=VerifySources(),before=MainSceneSignature();
            bool existing=Directory.Exists(Absolute(AssetRoot));
            CheckNoReparse(Absolute(AssetRoot));
            if(existing)ReadOwnedManifest(fingerprint);
            else
            {
                Require(!File.Exists(Absolute(AssetRoot))&&!File.Exists(Absolute(AssetRoot)+".meta")&&!AssetDatabase.IsValidFolder(AssetRoot),"New version leaf is occupied.");
                Bake(fingerprint);
            }
            Receipt receipt=Validate(fingerprint);
            receipt.alreadyExisted=existing;receipt.mainScenesPreserved=before==MainSceneSignature();
            Require(receipt.mainScenesPreserved,"Main scene setup or dirty state changed; inspect the scene before proceeding.");
            string json=JsonUtility.ToJson(receipt,true);WriteReceipt(absoluteReceiptDirectory,"export-receipt.json",json);return json;
        }

        public static string ValidateVersionedLibrary(string absoluteReceiptDirectory=null)
        {
            AssertEditorIdle();CheckReceiptDirectory(absoluteReceiptDirectory);
            string before=MainSceneSignature();Receipt receipt=Validate(VerifySources());
            receipt.alreadyExisted=true;receipt.mainScenesPreserved=before==MainSceneSignature();
            Require(receipt.mainScenesPreserved,"Validation changed the main scene setup or dirty state.");
            string json=JsonUtility.ToJson(receipt,true);WriteReceipt(absoluteReceiptDirectory,"validation-receipt.json",json);return json;
        }

        static string VerifySources()
        {
            var text=new StringBuilder();
            for(int i=0;i<Sources.Length;i++)
            {
                string path=Absolute("Assets/Scripts/"+Sources[i]);Require(File.Exists(path),"Missing reviewed source: "+Sources[i]);
                string hash=SourceHash(path);Require(hash==SourceHashes[i],"Source drift; review a new version before export: "+Sources[i]);
                text.Append(Sources[i]).Append(':').Append(hash).Append('\n');
            }
            Require(File.Exists(Absolute(ExporterPath)),"Install exporter at its reviewed Editor path.");
            text.Append("exporter:").Append(SourceHash(Absolute(ExporterPath)));
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }

        static void Bake(string fingerprint)
        {
            EnsureFolder(AssetRoot);var assets=new List<AssetRecord>();
            WriteNewText(OwnerPath,JsonUtility.ToJson(new Ownership{owner=Owner,version=Version,fingerprint=fingerprint},true));
            assets.Add(Record(AssetRoot,"folder"));assets.Add(Record(OwnerPath,"ownership"));
            foreach(string folder in new[]{"Meshes","Materials","Palettes","Prefabs"})
            {EnsureFolder(AssetRoot+"/"+folder);assets.Add(Record(AssetRoot+"/"+folder,"folder"));}
            Material material=null;
            try
            {
                for(int index=0;index<2;index++)
                {
                    Texture2D texture=NewPalette(index);
                    try{CreateAsset(texture,PalettePath(index));}
                    finally{if(texture!=null&&!AssetDatabase.Contains(texture))UnityEngine.Object.DestroyImmediate(texture);}
                }
                Shader shader=Shader.Find("Universal Render Pipeline/Lit");
                Require(shader!=null&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Installed URP/Lit required; no shader fallback.");
                material=new Material(shader){name="Original shared Ilyra and Bram palette"};
                ConfigurePaint(material,0);CreateAsset(material,MaterialPath(0));ConfigurePaint(material,0);AssetDatabase.SaveAssetIfDirty(material);
                var models=new List<ModelRecord>(2);
                for(int kind=0;kind<2;kind++)
                {
                    EnsureFolder(AssetRoot+"/Meshes/"+Slugs[kind]);assets.Add(Record(AssetRoot+"/Meshes/"+Slugs[kind],"folder"));
                    Scene scene=EditorSceneManager.NewPreviewScene();
                    try
                    {
                        using(var lease=RefugeNpcNeutralModelAuthoring.Create((RefugeNpcRole)(kind+2),material,scene))
                        {
                            MeshFilter[] parts=lease.Root.GetComponentsInChildren<MeshFilter>(true);
                            Require(parts.Length==9,"Frozen NPC mesh part count changed.");
                            for(int part=0;part<parts.Length;part++)
                            {
                                Mesh source=parts[part].sharedMesh,copy=CopyReadable(source);
                                try{CreateAsset(copy,MeshPath(kind,part));Require(MeshHash(source)==MeshHash(copy),"Readable mesh copy drift.");parts[part].sharedMesh=copy;}
                                finally{if(copy!=null&&!AssetDatabase.Contains(copy))UnityEngine.Object.DestroyImmediate(copy);}
                            }
                            string path=PrefabPath(kind);RefuseExisting(path);PrefabUtility.SaveAsPrefabAsset(lease.Root,path,out bool success);Require(success,"Neutral NPC prefab save failed.");
                        }
                    }
                    finally{EditorSceneManager.ClosePreviewScene(scene);}
                }
                foreach(UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(MaterialPath(0)))AssetDatabase.SaveAssetIfDirty(item);
                assets.Add(Record(MaterialPath(0),"material"));
                for(int index=0;index<2;index++)assets.Add(Record(PalettePath(index),"palette"));
                for(int kind=0;kind<2;kind++)
                {
                    for(int part=0;part<9;part++){AssetRecord entry=Record(MeshPath(kind,part),"mesh");entry.geometryHash=MeshHash(AssetDatabase.LoadAssetAtPath<Mesh>(entry.path));assets.Add(entry);}
                    assets.Add(Record(PrefabPath(kind),"prefab"));models.Add(InspectPrefab(kind));
                }
                var manifest=new Manifest{owner=Owner,version=Version,fingerprint=fingerprint,createdUtc=DateTime.UtcNow.ToString("o"),sourceFiles=Sources,sourceSha256=SourceHashes,assets=assets.ToArray(),models=models.ToArray()};
                WriteNewText(ManifestPath,JsonUtility.ToJson(manifest,true));manifest.manifestGuid=AssetDatabase.AssetPathToGUID(ManifestPath);manifest.manifestMetaSha256=FileHash(Absolute(ManifestPath)+".meta");
                File.WriteAllText(Absolute(ManifestPath),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));AssetDatabase.ImportAsset(ManifestPath,ImportAssetOptions.ForceSynchronousImport);
            }
            finally{if(material!=null&&!AssetDatabase.Contains(material))UnityEngine.Object.DestroyImmediate(material);}
        }

        static Manifest ReadOwnedManifest(string fingerprint)
        {
            CheckNoReparse(Absolute(AssetRoot));
            Require(File.Exists(Absolute(OwnerPath))&&File.Exists(Absolute(ManifestPath)),"Foreign or incomplete version leaf; refusing overwrite/automatic recovery.");
            var owner=JsonUtility.FromJson<Ownership>(File.ReadAllText(Absolute(OwnerPath)));
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Absolute(ManifestPath)));
            Require(owner!=null&&owner.owner==Owner&&owner.version==Version&&owner.fingerprint==fingerprint&&
                manifest!=null&&manifest.owner==Owner&&manifest.version==Version&&manifest.fingerprint==fingerprint,"Ownership/version/source fingerprint mismatch.");
            Require(manifest.assets!=null&&manifest.models!=null&&manifest.models.Length==2&&manifest.sourceFiles!=null&&manifest.sourceFiles.Length==4&&manifest.sourceSha256!=null&&manifest.sourceSha256.Length==4,"Incomplete asset manifest.");
            for(int i=0;i<4;i++)Require(manifest.sourceFiles[i]==Sources[i]&&manifest.sourceSha256[i]==SourceHashes[i],"Manifest source identity mismatch.");
            Require(AssetDatabase.AssetPathToGUID(ManifestPath)==manifest.manifestGuid&&FileHash(Absolute(ManifestPath)+".meta")==manifest.manifestMetaSha256,"Manifest GUID/meta drift.");
            var listed=new HashSet<string>(StringComparer.Ordinal){ManifestPath};
            foreach(AssetRecord record in manifest.assets)
            {
                Require(record!=null&&Owned(record.path)&&listed.Add(record.path),"Duplicate/foreign manifest path.");CheckNoReparse(Absolute(record.path));
                Require(AssetDatabase.AssetPathToGUID(record.path)==record.guid&&FileHash(Absolute(record.path)+".meta")==record.metaSha256,"Owned GUID/meta drift: "+record.path);
                Require(record.kind=="folder"?Directory.Exists(Absolute(record.path)):FileHash(Absolute(record.path))==record.sha256,"Owned source bytes changed: "+record.path);
            }
            Require(listed.SetEquals(PlannedPaths()),"Manifest differs from the exact reviewed asset catalog.");
            CheckClosedTree(listed);
            return manifest;
        }

        static Receipt Validate(string fingerprint)
        {
            Manifest manifest=ReadOwnedManifest(fingerprint);Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(0));
            ValidatePaint(material,0);ValidateMaterialObjects(material);
            for(int index=0;index<2;index++)ValidatePalette(index);
            var models=new List<ModelRecord>(2);int total=0,meshes=0;
            for(int kind=0;kind<2;kind++)
            {
                ModelRecord actual=InspectPrefab(kind);Require(actual.hierarchyHash==manifest.models[kind].hierarchyHash,"Saved hierarchy drift.");
                Scene scene=EditorSceneManager.NewPreviewScene();
                try
                {
                    using(var lease=RefugeNpcNeutralModelAuthoring.Create((RefugeNpcRole)(kind+2),material,scene))
                    {
                        Require(HierarchyHash(lease.Root)==actual.hierarchyHash,"Saved NPC differs from frozen original recipe/pivots.");
                        MeshFilter[] parts=lease.Root.GetComponentsInChildren<MeshFilter>(true);
                        for(int part=0;part<9;part++)
                        {
                            string path=MeshPath(kind,part);Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                            Require(saved!=null&&saved.isReadable&&AssetDatabase.IsMainAsset(saved)&&MeshHash(parts[part].sharedMesh)==MeshHash(saved),"Saved mesh differs from original NPC recipe.");
                            foreach(UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(path))Require(item==saved,"Unexpected mesh subobject.");meshes++;
                        }
                    }
                }
                finally{EditorSceneManager.ClosePreviewScene(scene);}
                models.Add(actual);total+=actual.triangles;
            }
            Require(meshes==18&&total==3016,"Frozen NPC library counts changed.");
            return new Receipt{status="native-editor-assets-validated",version=Version,assetRoot=AssetRoot,manifestSha256=FileHash(Absolute(ManifestPath)),sourceFingerprint=fingerprint,
                neutralPrefabs=true,meshes=18,materials=1,palettes=2,prefabs=2,triangles=total,models=models.ToArray(),utc=DateTime.UtcNow.ToString("o"),
                limitations=new[]{"Original neutral NPC assets only; no gameplay components, collision, dialogue, audio or authority.","World008 owns placement/yaw; no refuge coordinates are baked.","Readable authoring meshes/palettes retained; no browser/performance/animation acceptance is implied."}};
        }

        static ModelRecord InspectPrefab(int kind)
        {
            string path=PrefabPath(kind);GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab!=null,"Missing neutral NPC prefab.");
            Require(prefab.activeSelf&&prefab.transform.localPosition==Vector3.zero&&prefab.transform.localRotation==Quaternion.identity&&prefab.transform.localScale==Vector3.one,"Neutral root unit feet pivot changed.");
            foreach(Component component in prefab.GetComponentsInChildren<Component>(true))Require(component!=null&&(component is Transform||component is MeshFilter||component is MeshRenderer),"Neutral NPC contains script/physics/audio/light/gameplay component.");
            foreach(string dependency in AssetDatabase.GetDependencies(path,true))Require((!dependency.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)&&!dependency.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))||dependency==UrpVersionScript&&HasVerifiedUrpVersion(prefab),"Foreign script/assembly dependency.");
            Transform feet=UniqueAnchor(prefab,"Feet"),interact=UniqueAnchor(prefab,"Interact"),voice=UniqueAnchor(prefab,"Voice"),tool=UniqueAnchor(prefab,"HandTool");
            Require(feet.parent==prefab.transform&&feet.localPosition==Vector3.zero&&interact.parent==prefab.transform&&interact.localPosition==new Vector3(0,1.52f,0)&&voice.parent==prefab.transform&&voice.localPosition==new Vector3(0,1.56f,.18f),"Original feet/interact/voice anchors changed.");
            Require(tool.parent!=null&&tool.parent.name==(kind==0?"Split crown emerald staff":"Forked copper watch spear")&&tool.localPosition==Vector3.zero,"Primary held-tool grip socket changed.");
            Require(prefab.transform.Find("Anchor_Mount")==null&&prefab.transform.Find("Anchor_Gaze")==null&&prefab.transform.Find("Anchor_Interaction")==null&&prefab.transform.Find("Anchor_Voice")==null,"Unexpected legacy anchor aliases.");
            int triangles=0,renderers=0;bool first=true;Bounds bounds=default;
            foreach(MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh=filter.sharedMesh;MeshRenderer renderer=filter.GetComponent<MeshRenderer>();
                Require(mesh!=null&&mesh.subMeshCount==1&&AssetDatabase.GetAssetPath(mesh)==MeshPath(kind,renderers),"Foreign or reordered mesh asset.");
                Require(renderer!=null&&renderer.sharedMaterials.Length==1&&renderer.sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(0)),"NPCs must borrow one exact shared palette.");
                triangles+=(int)mesh.GetIndexCount(0)/3;renderers++;Matrix4x4 matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;Bounds b=mesh.bounds;
                for(int corner=0;corner<8;corner++){Vector3 point=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
            }
            Require(!first&&renderers==9&&triangles==TriangleCounts[kind]&&Mathf.Abs(bounds.min.y)<.00001f&&bounds.max.y<2.40f&&bounds.size.x<1.18f&&bounds.size.z<.45f,"Frozen original NPC count/feet/extent mismatch.");
            return new ModelRecord{kind=kind,role=kind+2,name=kind==0?"Ilyra":"Bram",path=path,triangles=triangles,renderers=renderers,hierarchyHash=HierarchyHash(prefab),boundsCenter=bounds.center,boundsSize=bounds.size,
                feet=prefab.transform.InverseTransformPoint(feet.position),interact=prefab.transform.InverseTransformPoint(interact.position),voice=prefab.transform.InverseTransformPoint(voice.position),handTool=prefab.transform.InverseTransformPoint(tool.position)};
        }

        static Transform UniqueAnchor(GameObject root,string name)
        {Transform result=null;foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name==name){Require(result==null,"Duplicate anchor: "+name);result=t;}Require(result!=null,"Missing anchor: "+name);return result;}

        static void ConfigurePaint(Material material,int paint)
        {
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath(0)));material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath(1)));material.SetColor("_EmissionColor",Color.white*1.35f);
            material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);material.SetTexture("_BumpMap",null);
            material.SetFloat("_Metallic",.08f);material.SetFloat("_Smoothness",.23f);material.SetFloat("_Surface",0);material.SetFloat("_AlphaClip",0);material.SetFloat("_ZWrite",1);material.SetFloat("_SrcBlend",1);material.SetFloat("_DstBlend",0);material.SetFloat("_Cull",2);
            foreach(string keyword in new[]{"_ALPHATEST_ON","_SURFACE_TYPE_TRANSPARENT","_ALPHAPREMULTIPLY_ON","_ALPHAMODULATE_ON","_NORMALMAP","_PARALLAXMAP","_DETAIL_MULX2","_DETAIL_SCALED","_METALLICSPECGLOSSMAP"})material.DisableKeyword(keyword);
            material.SetOverrideTag("RenderType","Opaque");material.SetShaderPassEnabled("ShadowCaster",true);material.renderQueue=2000;
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;BaseShaderGUI.SetMaterialKeywords(material);ValidatePaint(material,paint);
        }
        static void ValidatePaint(Material material,int paint)
        {
            Require(material!=null&&material.shader!=null&&material.shader.name=="Universal Render Pipeline/Lit"&&material.shader.isSupported&&!ShaderUtil.ShaderHasError(material.shader),"Invalid saved URP palette material.");
            Require(material.GetColor("_BaseColor")==Color.white&&material.GetColor("_EmissionColor")==Color.white*1.35f&&material.GetFloat("_Metallic")==.08f&&material.GetFloat("_Smoothness")==.23f,"Original shared NPC surface changed.");
            Require(material.GetTexture("_BaseMap")==AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath(0))&&material.GetTexture("_EmissionMap")==AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath(1))&&material.GetTexture("_BumpMap")==null,"Foreign palette reference.");
            Require(material.IsKeywordEnabled("_EMISSION")&&material.globalIlluminationFlags==MaterialGlobalIlluminationFlags.BakedEmissive,"URP authoring emission metadata mismatch.");
            Require(material.GetFloat("_Surface")==0&&material.GetFloat("_Cull")==2&&material.GetFloat("_AlphaClip")==0&&material.GetFloat("_ZWrite")==1&&material.renderQueue==2000,"Original opaque surface settings changed.");
        }
        static Texture2D NewPalette(int index)
        {
            var texture=new Texture2D(16,1,TextureFormat.RGB24,false,index==1){name=index==0?"Original shared refuge NPC palette":"Refuge staff and lantern emission mask",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[16];for(int i=0;i<16;i++)pixels[i]=index==0||i==12||i==13?PaletteColors[i]:Color.black;
            texture.SetPixels(pixels);texture.Apply(false,false);return texture;
        }
        static void ValidatePalette(int index)
        {
            Texture2D saved=AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath(index)),expected=NewPalette(index);
            try
            {
                Require(saved!=null&&AssetDatabase.IsMainAsset(saved)&&saved.isReadable&&saved.width==16&&saved.height==1&&saved.format==TextureFormat.RGB24&&saved.mipmapCount==1&&saved.filterMode==FilterMode.Point&&saved.wrapMode==TextureWrapMode.Clamp,"Palette shape/import properties changed.");
                Color32[] a=saved.GetPixels32(),b=expected.GetPixels32();Require(a.Length==b.Length,"Palette length changed.");
                for(int i=0;i<a.Length;i++)Require(a[i].Equals(b[i]),"Original palette pixel changed.");
                Require(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(saved.graphicsFormat)==(index==0),"Palette linear/color-space flag changed.");
                foreach(UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(PalettePath(index)))Require(item==saved,"Unexpected palette subobject.");
            }
            finally{UnityEngine.Object.DestroyImmediate(expected);}
        }

        // Exact exception for URP's persistent hidden material-version metadata.
        // IsSubAsset is deliberately not assumed: native IDs may be negative.
        static bool ValidateMaterialObjects(Material material)
        {
            string path=AssetDatabase.GetAssetPath(material);Require(Owned(path)&&path.EndsWith(".mat",StringComparison.Ordinal)&&EditorUtility.IsPersistent(material)&&AssetDatabase.Contains(material)&&AssetDatabase.IsMainAsset(material),"Expected owned persistent main material.");
            string owningGuid=AssetDatabase.AssetPathToGUID(path);
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material,out string materialGuid,out long materialId)&&materialGuid==owningGuid&&materialId!=0,"Material identity is unknown.");
            MonoScript script=AssetDatabase.LoadAssetAtPath<MonoScript>(UrpVersionScript);Type metadata=script!=null?script.GetClass():null;
            Require(metadata!=null&&metadata.FullName=="UnityEditor.Rendering.Universal.AssetVersion"&&metadata.Assembly.GetName().Name=="Unity.RenderPipelines.Universal.Editor"&&typeof(ScriptableObject).IsAssignableFrom(metadata),"Unexpected installed URP metadata type.");
            bool found=false;
            foreach(UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if(item==material)continue;var version=item as ScriptableObject;HideFlags flags=HideFlags.HideInHierarchy|HideFlags.HideInInspector|HideFlags.NotEditable;
                Require(version!=null&&version.GetType()==metadata&&EditorUtility.IsPersistent(version)&&AssetDatabase.Contains(version)&&!AssetDatabase.IsMainAsset(version)&&
                    AssetDatabase.GetAssetPath(version)==path&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(version,out string guid,out long id)&&guid==owningGuid&&id!=0&&id!=materialId&&
                    (version.hideFlags&flags)==flags&&MonoScript.FromScriptableObject(version)==script,"Foreign or unverifiable material subobject.");found=true;
            }
            return found;
        }
        static bool HasVerifiedUrpVersion(GameObject prefab)
        {bool found=false;foreach(MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))foreach(Material material in renderer.sharedMaterials)found|=ValidateMaterialObjects(material);return found;}

        static Mesh CopyReadable(Mesh source)
        {
            Require(source!=null&&source.subMeshCount==1,"Expected one-submesh runtime part.");
            using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(source))
            {
                var data=snapshot[0];using(var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))
                using(var normals=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))using(var uv=new NativeArray<Vector2>(data.vertexCount,Allocator.Temp))
                using(var indices=new NativeArray<int>(checked((int)source.GetIndexCount(0)),Allocator.Temp))
                {
                    data.GetVertices(vertices);data.GetNormals(normals);data.GetUVs(0,uv);data.GetIndices(indices,0,true);
                    var copy=new Mesh{name=source.name+" editable",indexFormat=source.indexFormat};
                    copy.SetVertices(vertices);copy.SetNormals(normals);copy.SetUVs(0,uv);copy.SetTriangles(indices.ToArray(),0,false);copy.bounds=source.bounds;
                    return copy; // Retain CPU data for later modeling and validation.
                }
            }
        }
        static string MeshHash(Mesh mesh)
        {
            Require(mesh!=null&&mesh.subMeshCount==1&&mesh.GetTopology(0)==MeshTopology.Triangles,"Unsupported mesh topology.");
            using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh))using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                var data=snapshot[0];using(var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))
                using(var normals=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))using(var uv=new NativeArray<Vector2>(data.vertexCount,Allocator.Temp))
                using(var indices=new NativeArray<int>(checked((int)mesh.GetIndexCount(0)),Allocator.Temp))
                {
                    data.GetVertices(vertices);data.GetNormals(normals);data.GetUVs(0,uv);data.GetIndices(indices,0,true);
                    writer.Write(data.vertexCount);writer.Write(indices.Length);writer.Write((int)mesh.indexFormat);
                    for(int i=0;i<vertices.Length;i++){Write(writer,vertices[i]);Write(writer,normals[i]);writer.Write(uv[i].x);writer.Write(uv[i].y);}
                    for(int i=0;i<indices.Length;i++)writer.Write(indices[i]);Write(writer,mesh.bounds.center);Write(writer,mesh.bounds.size);writer.Flush();return Hash(stream.ToArray());
                }
            }
        }
        static string HierarchyHash(GameObject root)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    writer.Write(AnimationUtility.CalculateTransformPath(t,root.transform));Write(writer,t.localPosition);Write(writer,t.localScale);
                    Quaternion q=t.localRotation;writer.Write(q.x);writer.Write(q.y);writer.Write(q.z);writer.Write(q.w);
                    MeshFilter filter=t.GetComponent<MeshFilter>();writer.Write(filter!=null);
                    if(filter!=null)
                    {
                        writer.Write(MeshHash(filter.sharedMesh));MeshRenderer renderer=t.GetComponent<MeshRenderer>();Require(renderer!=null&&renderer.sharedMaterials.Length==1,"Missing neutral renderer.");
                        writer.Write(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sharedMaterial)));writer.Write((int)renderer.shadowCastingMode);writer.Write(renderer.receiveShadows);
                        writer.Write((int)renderer.lightProbeUsage);writer.Write((int)renderer.reflectionProbeUsage);writer.Write(renderer.enabled);
                    }
                }
                writer.Flush();return Hash(stream.ToArray());
            }
        }

        static void CreateAsset(UnityEngine.Object value,string path)
        {RefuseExisting(path);AssetDatabase.CreateAsset(value,path);AssetDatabase.SaveAssetIfDirty(value);}
        static void WriteNewText(string path,string text)
        {RefuseExisting(path);File.WriteAllText(Absolute(path),text,new UTF8Encoding(false));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
        static void RefuseExisting(string path)
        {Require(Owned(path)&&!File.Exists(Absolute(path))&&!Directory.Exists(Absolute(path))&&!File.Exists(Absolute(path)+".meta")&&AssetDatabase.LoadMainAssetAtPath(path)==null,"Refusing overwrite: "+path);}
        static AssetRecord Record(string path,string kind)
        {return new AssetRecord{path=path,kind=kind,guid=AssetDatabase.AssetPathToGUID(path),sha256=kind=="folder"?null:FileHash(Absolute(path)),metaSha256=FileHash(Absolute(path)+".meta")};}
        static void EnsureFolder(string path)
        {
            Require(path=="Assets"||path.StartsWith("Assets/",StringComparison.Ordinal),"Invalid asset folder.");CheckNoReparse(Absolute(path));
            if(AssetDatabase.IsValidFolder(path))return;string parent=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(parent);
            Require(!Directory.Exists(Absolute(path))&&!File.Exists(Absolute(path))&&!File.Exists(Absolute(path)+".meta"),"Unimported/foreign folder path.");
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1))),"Asset folder creation failed.");
        }
        static void CheckClosedTree(HashSet<string> expected)
        {
            var observed=new HashSet<string>(StringComparer.Ordinal);
            var pending=new Stack<string>();pending.Push(Absolute(AssetRoot));int entries=0;
            while(pending.Count>0)foreach(string path in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                Require(++entries<=256,"Unexpectedly large version leaf.");CheckNoReparse(path);string relative=ProjectRelative(path);
                if(relative.EndsWith(".meta",StringComparison.Ordinal)){Require(expected.Contains(relative.Substring(0,relative.Length-5)),"Unexpected orphan/foreign meta file.");continue;}
                Require(expected.Contains(relative)&&observed.Add(relative),"Unexpected output-tree entry: "+relative);
                if(Directory.Exists(path))pending.Push(path);
            }
            foreach(string path in expected)Require(path==AssetRoot||observed.Contains(path),"Missing planned output: "+path);
        }
        static HashSet<string> PlannedPaths()
        {
            var paths=new HashSet<string>(StringComparer.Ordinal){AssetRoot,OwnerPath,ManifestPath,AssetRoot+"/Meshes",AssetRoot+"/Materials",AssetRoot+"/Palettes",AssetRoot+"/Prefabs",MaterialPath(0),PalettePath(0),PalettePath(1)};
            for(int kind=0;kind<2;kind++){paths.Add(AssetRoot+"/Meshes/"+Slugs[kind]);paths.Add(PrefabPath(kind));for(int part=0;part<9;part++)paths.Add(MeshPath(kind,part));}
            return paths;
        }
        static bool Owned(string path)=>path==AssetRoot||path!=null&&path.StartsWith(AssetRoot+"/",StringComparison.Ordinal)&&!path.Contains("..")&&!path.Contains("\\");
        static string ProjectRoot=>Directory.GetParent(Application.dataPath).FullName;
        static string Absolute(string path)
        {Require(path=="Assets"||path.StartsWith("Assets/",StringComparison.Ordinal),"Expected Assets-relative path.");string full=Path.GetFullPath(Path.Combine(ProjectRoot,path));string assets=Path.GetFullPath(Application.dataPath);Require(full==assets||full.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Resolved path escaped Assets.");return full;}
        static string ProjectRelative(string path)=>Path.GetFullPath(path).Substring(ProjectRoot.Length+1).Replace('\\','/');
        static void CheckNoReparse(string path)
        {
            for(string current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
            {
                try {Require((File.GetAttributes(current)&FileAttributes.ReparsePoint)==0,"Reparse/symlink paths are not owned.");}
                catch(FileNotFoundException) {} catch(DirectoryNotFoundException) {}
            }
        }
        static string SourceHash(string path)=>Hash(Encoding.UTF8.GetBytes(File.ReadAllText(path).TrimStart('\uFEFF').Replace("\r\n","\n")));
        static string FileHash(string path){Require(File.Exists(path),"Missing owned file: "+path);return Hash(File.ReadAllBytes(path));}
        static string Hash(byte[] bytes){using(var hash=SHA256.Create()){var s=new StringBuilder();foreach(byte b in hash.ComputeHash(bytes))s.Append(b.ToString("x2"));return s.ToString();}}
        static void Write(BinaryWriter writer,Vector3 p){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
        static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        internal static string MainSceneSignature()
        {var s=new StringBuilder();foreach(var setup in EditorSceneManager.GetSceneManagerSetup())s.Append(setup.path).Append('|').Append(setup.isActive).Append('|').Append(setup.isLoaded).Append(';');for(int i=0;i<SceneManager.sceneCount;i++){Scene scene=SceneManager.GetSceneAt(i);s.Append(scene.path).Append(':').Append(scene.isDirty).Append(';');}return s.ToString();}
        internal static void CheckReceiptDirectory(string path)
        {if(path==null)return;Require(Path.IsPathRooted(path),"Receipt directory must be absolute.");string full=Path.GetFullPath(path),assets=Path.GetFullPath(Application.dataPath);CheckNoReparse(full);Require(!string.Equals(full,assets,StringComparison.OrdinalIgnoreCase)&&!full.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Receipts/previews stay outside Assets.");}
        internal static void WriteReceipt(string directory,string name,string json)
        {if(directory==null)return;CheckReceiptDirectory(directory);Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,name),json,new UTF8Encoding(false));}
    }
}
#endif
