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

namespace OdysseyOracle.EditorAssets
{
    // # ARCHITECTURE SNAPSHOT
    // Cold Editor asset adapter. Exact runtime recipe/pose bridge -> readable
    // owned meshes + four original shared paints + script-free neutral prefabs.
    // No gameplay instance, resource search, animation tick, physics or credit use.
    // Each version is immutable; foreign, partial or modified leaves fail closed.
    public static class OracleMonsterAssetLibraryV1
    {
        public const string Version="v1_0_0";
        public const string AssetRoot="Assets/Art/OracleMonsterLibrary/"+Version;
        const string Owner="odyssey.oracle.original-monster-library.20261016.v1";
        const string ManifestPath=AssetRoot+"/manifest.json",OwnerPath=AssetRoot+"/ownership.json";
        const string ExporterPath="Assets/Editor/MonsterLibrary/OracleMonsterAssetLibraryV1.cs";
        const string UrpVersionScript="Packages/com.unity.render-pipelines.universal/Editor/AssetVersion.cs";
        static readonly string[] Slugs={"guardian_golem","stalker","shade","spider"};
        static readonly string[] Paints={"stone","joints","cyan","shade_mint"};
        static readonly int[] PartCounts={11,11,13,17},TriangleCounts={408,372,564,528};
        static readonly Color[] Colors={new Color(.26f,.29f,.31f,1),new Color(.085f,.105f,.13f,1),
            new Color(.282f,.871f,.835f,1),new Color(.498f,.914f,.862f,1)};
        static readonly float[] Emission={0,0,1.3f,1.25f};
        static readonly string[] Sources={"MonsterVoxelCosmetics.cs","GroundedGolemMotion.cs","MonsterNeutralModelAuthoring.cs"};
        // Filled from the reviewed actual003 recipe/motion + the new cold bridge.
        static readonly string[] SourceHashes={"8bea5505e39c2dd9451cc4d20f5b65838b7fae9bfbbe2e0ac38e0777510fd54a","df905e0780cebc7fa4ea6f6ef9ca4d5ee7ca718842448253909f69da4cfddea5","c04679f4e35e8cdf27af99d2dc573e2a56e54810e451aa49c7f60f18d41f3a39"};

        [Serializable] sealed class Ownership {public string owner,version,fingerprint;}
        [Serializable] public sealed class AssetRecord {public string path,kind,guid,sha256,metaSha256,geometryHash;}
        [Serializable] public sealed class ModelRecord
        {
            public int kind,triangles,renderers,articulatedLegs;
            public bool tutorialOnly;
            public string path,name,hierarchyHash;
            public Vector3 boundsCenter,boundsSize,feet,attack,sfx;
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
            public int meshes,materials,prefabs,triangles;
            public ModelRecord[] models;
            public string[] limitations;
        }

        public static string PrefabPath(int kind)
        {Require(kind>=0&&kind<4,"Monster kind must be0..3.");return AssetRoot+"/Prefabs/"+Slugs[kind]+".prefab";}
        static string MeshPath(int kind,int part)=>AssetRoot+"/Meshes/"+Slugs[kind]+"/part_"+part.ToString("D2")+".asset";
        static string MaterialPath(int paint)=>AssetRoot+"/Materials/"+Paints[paint]+".mat";

        [MenuItem("Tools/Infinite Odyssey/Monster library/Export neutral v1")]
        static void ExportMenu()=>Debug.Log(ExportVersionedLibrary());
        [MenuItem("Tools/Infinite Odyssey/Monster library/Validate neutral v1")]
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
            Require(OracleEnemy.PresentationScale==1.20f,"Actual visual scale changed; review a new version.");
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }

        static void Bake(string fingerprint)
        {
            // New leaf only. On failure, retain the incomplete leaf for inspection;
            // a later invocation refuses it instead of deleting or overwriting it.
            EnsureFolder(AssetRoot);
            var assets=new List<AssetRecord>();
            var owner=new Ownership{owner=Owner,version=Version,fingerprint=fingerprint};
            WriteNewText(OwnerPath,JsonUtility.ToJson(owner,true));
            assets.Add(Record(AssetRoot,"folder"));assets.Add(Record(OwnerPath,"ownership"));
            foreach(string folder in new[]{"Meshes","Materials","Prefabs"})
            {EnsureFolder(AssetRoot+"/"+folder);assets.Add(Record(AssetRoot+"/"+folder,"folder"));}
            var materials=new Material[4];var models=new List<ModelRecord>(4);
            try
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Lit");
                Require(shader!=null&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Installed URP/Lit is unavailable or has errors; no shader fallback.");
                for(int paint=0;paint<4;paint++)
                {
                    var material=new Material(shader){name="Oracle monster "+Paints[paint],enableInstancing=true};
                    materials[paint]=material;ConfigurePaint(material,paint);
                    CreateAsset(material,MaterialPath(paint));
                    // Reconcile only newly authored materials after the URP importer.
                    ConfigurePaint(material,paint);AssetDatabase.SaveAssetIfDirty(material);
                }
                for(int kind=0;kind<4;kind++)
                {
                    EnsureFolder(AssetRoot+"/Meshes/"+Slugs[kind]);assets.Add(Record(AssetRoot+"/Meshes/"+Slugs[kind],"folder"));
                    Scene scene=EditorSceneManager.NewPreviewScene();
                    try
                    {
                        using(var lease=MonsterNeutralModelAuthoring.Create((MonsterNeutralModelAuthoring.Kind)kind,materials[0],materials[1],materials[2],materials[3],scene))
                        {
                            SceneManager.MoveGameObjectToScene(lease.Root,scene);
                            MeshFilter[] parts=lease.Root.GetComponentsInChildren<MeshFilter>(true);
                            Require(parts.Length==PartCounts[kind],"Actual model part count changed.");
                            for(int part=0;part<parts.Length;part++)
                            {
                                Mesh source=parts[part].sharedMesh;Mesh mesh=CopyReadable(source);
                                try
                                {
                                    string path=MeshPath(kind,part);CreateAsset(mesh,path);
                                    Require(MeshHash(source)==MeshHash(mesh),"Readable mesh copy differs from original runtime data.");
                                    parts[part].sharedMesh=mesh;
                                }
                                finally {if(mesh!=null&&!AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);}
                            }
                            string prefabPath=PrefabPath(kind);RefuseExisting(prefabPath);
                            PrefabUtility.SaveAsPrefabAsset(lease.Root,prefabPath,out bool success);Require(success,"Prefab save failed: "+prefabPath);
                        }
                    }
                    finally {EditorSceneManager.ClosePreviewScene(scene);}
                }
                // Save only the new owned material objects, including URP's
                // hidden version metadata. Never save unrelated dirty assets.
                for(int paint=0;paint<4;paint++)foreach(UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(MaterialPath(paint)))AssetDatabase.SaveAssetIfDirty(item);
                for(int paint=0;paint<4;paint++)assets.Add(Record(MaterialPath(paint),"material"));
                for(int kind=0;kind<4;kind++)
                {
                    for(int part=0;part<PartCounts[kind];part++)
                    {AssetRecord record=Record(MeshPath(kind,part),"mesh");record.geometryHash=MeshHash(AssetDatabase.LoadAssetAtPath<Mesh>(record.path));assets.Add(record);}
                    assets.Add(Record(PrefabPath(kind),"prefab"));models.Add(InspectPrefab(kind));
                }
                var manifest=new Manifest{owner=Owner,version=Version,fingerprint=fingerprint,createdUtc=DateTime.UtcNow.ToString("o"),
                    sourceFiles=Sources,sourceSha256=SourceHashes,assets=assets.ToArray(),models=models.ToArray()};
                WriteNewText(ManifestPath,JsonUtility.ToJson(manifest,true));
                manifest.manifestGuid=AssetDatabase.AssetPathToGUID(ManifestPath);manifest.manifestMetaSha256=FileHash(Absolute(ManifestPath)+".meta");
                // The exporter owns this newly created manifest; include its stable
                // GUID/meta identity without attempting a self-referential hash.
                File.WriteAllText(Absolute(ManifestPath),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));
                AssetDatabase.ImportAsset(ManifestPath,ImportAssetOptions.ForceSynchronousImport);
            }
            finally {foreach(Material material in materials)if(material!=null&&!AssetDatabase.Contains(material))UnityEngine.Object.DestroyImmediate(material);}
        }

        static Manifest ReadOwnedManifest(string fingerprint)
        {
            CheckNoReparse(Absolute(AssetRoot));
            Require(File.Exists(Absolute(OwnerPath))&&File.Exists(Absolute(ManifestPath)),"Foreign or incomplete version leaf; refusing overwrite/automatic recovery.");
            var owner=JsonUtility.FromJson<Ownership>(File.ReadAllText(Absolute(OwnerPath)));
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Absolute(ManifestPath)));
            Require(owner!=null&&owner.owner==Owner&&owner.version==Version&&owner.fingerprint==fingerprint&&
                manifest!=null&&manifest.owner==Owner&&manifest.version==Version&&manifest.fingerprint==fingerprint,"Ownership/version/source fingerprint mismatch.");
            Require(manifest.assets!=null&&manifest.models!=null&&manifest.models.Length==4&&manifest.sourceFiles!=null&&manifest.sourceFiles.Length==3&&manifest.sourceSha256!=null&&manifest.sourceSha256.Length==3,"Incomplete asset manifest.");
            for(int i=0;i<3;i++)Require(manifest.sourceFiles[i]==Sources[i]&&manifest.sourceSha256[i]==SourceHashes[i],"Manifest source identity mismatch.");
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
            Manifest manifest=ReadOwnedManifest(fingerprint);var materials=new Material[4];
            for(int paint=0;paint<4;paint++)
            {materials[paint]=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(paint));ValidatePaint(materials[paint],paint);ValidateMaterialObjects(materials[paint]);}
            var models=new List<ModelRecord>(4);int meshes=0,total=0;
            for(int kind=0;kind<4;kind++)
            {
                ModelRecord actual=InspectPrefab(kind);
                Require(actual.hierarchyHash==manifest.models[kind].hierarchyHash,"Saved hierarchy or renderer settings changed.");
                Scene scene=EditorSceneManager.NewPreviewScene();
                try
                {
                    using(var lease=MonsterNeutralModelAuthoring.Create((MonsterNeutralModelAuthoring.Kind)kind,materials[0],materials[1],materials[2],materials[3],scene))
                    {
                        Require(HierarchyHash(lease.Root)==actual.hierarchyHash,"Saved prefab differs from actual003 neutral recipe/pivots.");
                        MeshFilter[] parts=lease.Root.GetComponentsInChildren<MeshFilter>(true);
                        for(int part=0;part<parts.Length;part++)
                        {
                            string path=MeshPath(kind,part);Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                            Require(saved!=null&&saved.isReadable&&AssetDatabase.IsMainAsset(saved)&&MeshHash(parts[part].sharedMesh)==MeshHash(saved),"Saved readable mesh differs from actual recipe.");
                            foreach(UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(path))Require(item==saved,"Unexpected owned mesh subobject.");
                            meshes++;
                        }
                    }
                }
                finally {EditorSceneManager.ClosePreviewScene(scene);}
                models.Add(actual);total+=actual.triangles;
            }
            Require(meshes==52&&total==1872,"Reviewed model library counts changed.");
            return new Receipt{status="native-editor-assets-validated",version=Version,assetRoot=AssetRoot,manifestSha256=FileHash(Absolute(ManifestPath)),sourceFingerprint=fingerprint,
                neutralPrefabs=true,meshes=meshes,materials=4,prefabs=4,triangles=total,models=models.ToArray(),utc=DateTime.UtcNow.ToString("o"),
                limitations=new[]{"Root-run asset validation only; no native result exists until this entry is executed.","Neutral original rigid parts/pivots, no Humanoid Avatar or clip-retargeting claim.","GuardianGolem is a tutorial identity, not an added co-op archetype or new stats.","No gameplay, browser, GPU/FPS, authority or delivery LOD acceptance is implied."}};
        }

        static ModelRecord InspectPrefab(int kind)
        {
            string path=PrefabPath(kind);GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab!=null,"Missing neutral prefab.");
            Require(prefab.transform.localPosition==Vector3.zero&&prefab.transform.localRotation==Quaternion.identity&&prefab.transform.localScale==Vector3.one,"Root feet pivot/scale changed.");
            foreach(Component component in prefab.GetComponentsInChildren<Component>(true))
                Require(component!=null&&(component is Transform||component is MeshFilter||component is MeshRenderer),"Neutral prefab contains missing/script/gameplay/physics/light/audio components.");
            foreach(string dependency in AssetDatabase.GetDependencies(path,true))
                Require((!dependency.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)&&!dependency.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))||
                    dependency==UrpVersionScript&&HasVerifiedUrpVersion(prefab),"Unapproved script/assembly dependency: "+dependency);
            Transform body=prefab.transform.Find("Body");Require(body!=null&&body.localScale==Vector3.one*1.20f,"Body canonical scale changed.");
            Require(body.Find("Left arm pivot")!=null&&body.Find("Right arm pivot")!=null,"Missing original shoulder pivots.");
            int legs=0,triangles=0,renderers=0;bool first=true;Bounds bounds=default;
            foreach(MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
                Require(mesh!=null&&mesh.isReadable&&mesh.subMeshCount==1&&Owned(AssetDatabase.GetAssetPath(mesh))&&renderer!=null&&renderer.sharedMaterials.Length==1&&Owned(AssetDatabase.GetAssetPath(renderer.sharedMaterial)),"Foreign or invalid neutral mesh/material.");
                triangles+=checked((int)mesh.GetIndexCount(0)/3);renderers++;
                if(filter.name=="Left leg"||filter.name=="Right leg"||filter.name.StartsWith("Spider leg ",StringComparison.Ordinal))legs++;
                Matrix4x4 m=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;Bounds b=mesh.bounds;
                for(int corner=0;corner<8;corner++)
                {Vector3 p=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            Require(!first&&renderers==PartCounts[kind]&&triangles==TriangleCounts[kind]&&legs==(kind==3?8:2)&&Mathf.Abs(bounds.min.y)<.0001f,"Neutral counts/feet plane differ from original recipes.");
            if(kind!=3)Require(body.Find("Procedural torso pivot")?.Find("Procedural head pivot")!=null,"Missing actual003 torso/head pivots.");
            Transform feet=UniqueAnchor(prefab,"Feet"),attack=UniqueAnchor(prefab,"Attack"),sfx=UniqueAnchor(prefab,"SFX");
            Vector3 feetPoint=prefab.transform.InverseTransformPoint(feet.position),attackPoint=prefab.transform.InverseTransformPoint(attack.position),sfxPoint=prefab.transform.InverseTransformPoint(sfx.position);
            Require(feetPoint==Vector3.zero&&(attackPoint-Vector3.up*1.2f).sqrMagnitude<1e-10f&&(sfxPoint-Vector3.up*1.2f).sqrMagnitude<1e-10f,"Actual Feet/Attack/SFX anchors changed.");
            return new ModelRecord{kind=kind,path=path,name=prefab.name,triangles=triangles,renderers=renderers,articulatedLegs=legs,tutorialOnly=kind==0,hierarchyHash=HierarchyHash(prefab),boundsCenter=bounds.center,boundsSize=bounds.size,feet=feetPoint,attack=attackPoint,sfx=sfxPoint};
        }

        static Transform UniqueAnchor(GameObject root,string name)
        {Transform result=null;foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name==name){Require(result==null,"Duplicate anchor: "+name);result=t;}Require(result!=null,"Missing anchor: "+name);return result;}

        // Exact authored state for this installed emission policy; reject other GI bits.
        static MaterialGlobalIlluminationFlags ExpectedAuthoringGi(bool luminous)
        {
            if(!luminous)return MaterialGlobalIlluminationFlags.EmissiveIsBlack;
#if UNITY_6000_7_OR_NEWER
            return MaterialGlobalIlluminationFlags.BakedEmission|MaterialGlobalIlluminationFlags.RealtimeDirectEmission;
#else
            return MaterialGlobalIlluminationFlags.BakedEmissive;
#endif
        }
        static void ConfigurePaint(Material material,int paint)
        {
            material.SetColor("_BaseColor",Colors[paint]);material.SetColor("_Color",Colors[paint]);
            material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.2f);material.SetFloat("_Surface",0);material.SetFloat("_Cull",2);material.SetFloat("_AlphaClip",0);
            material.SetTexture("_BaseMap",null);material.SetTexture("_BumpMap",null);material.SetTexture("_EmissionMap",null);
            material.SetColor("_EmissionColor",Emission[paint]>0?Colors[paint]*Emission[paint]:Color.black);
            material.globalIlluminationFlags=ExpectedAuthoringGi(Emission[paint]>0);
            BaseShaderGUI.SetMaterialKeywords(material);ValidatePaint(material,paint);
        }
        static void ValidatePaint(Material material,int paint)
        {
            Require(material!=null&&material.shader!=null&&material.shader.name=="Universal Render Pipeline/Lit"&&material.shader.isSupported&&!ShaderUtil.ShaderHasError(material.shader),"Invalid saved URP material.");
            Require(material.GetColor("_BaseColor")==Colors[paint]&&material.GetFloat("_Metallic")==0&&material.GetFloat("_Smoothness")==.2f&&material.GetFloat("_Surface")==0&&material.GetFloat("_Cull")==2&&material.GetFloat("_AlphaClip")==0,"Original surface/color drift.");
            Require(material.GetTexture("_BaseMap")==null&&material.GetTexture("_BumpMap")==null&&material.GetTexture("_EmissionMap")==null,"Unexpected material texture dependency.");
            bool luminous=Emission[paint]>0;Require(material.IsKeywordEnabled("_EMISSION")==luminous&&material.globalIlluminationFlags==(ExpectedAuthoringGi(luminous)),"URP emission keyword/GI metadata mismatch.");
            Require(material.GetColor("_EmissionColor")== (luminous?Colors[paint]*Emission[paint]:Color.black),"Original emission color changed.");
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
            var paths=new HashSet<string>(StringComparer.Ordinal){AssetRoot,OwnerPath,ManifestPath,AssetRoot+"/Meshes",AssetRoot+"/Materials",AssetRoot+"/Prefabs"};
            for(int paint=0;paint<4;paint++)paths.Add(MaterialPath(paint));
            for(int kind=0;kind<4;kind++){paths.Add(AssetRoot+"/Meshes/"+Slugs[kind]);paths.Add(PrefabPath(kind));for(int part=0;part<PartCounts[kind];part++)paths.Add(MeshPath(kind,part));}
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

// # ARCHITECTURE SNAPSHOT
// Editor-only authored GI metadata; Unity metres/Y-up geometry and native memory/job owners remain unchanged.

// Coordinate Space: Existing authored Unity metres/Y-up; no geometry or pivot edits.
// Array Formats: Cold Editor material metadata and exact manifest hashes only.
// Dependencies/Pointers: Versioned URP11 emission policy; strict ownership/source/GUID checks remain; no runtime native owners changed.
