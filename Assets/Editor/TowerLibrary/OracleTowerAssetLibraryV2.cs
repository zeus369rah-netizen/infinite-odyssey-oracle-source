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
    // Editor-only immutable asset bake. Public frozen recipe/library APIs are the only model source.
    // Neutral prefabs contain standard transforms/meshes/renderers and no runtime controller.
    // Ownership/hash checks run before reuse; no overwrite, deletion, hidden reflection or gameplay hook.
    public static class OracleTowerAssetLibraryV2
    {
        public const string Version="v2_0_0";
        public const string AssetRoot="Assets/Art/OracleTowerLibrary/"+Version;
        const string Owner="odyssey.oracle.original-tower-library.v2.20261016";
        const string ManifestPath=AssetRoot+"/manifest.json",OwnerPath=AssetRoot+"/ownership.json";
        static readonly string[] Slugs={"fire","water","earth","air"};
        static readonly string[] SourceFiles={"TowerModelRecipes.cs","TowerModelPulseState.cs","TowerModelLibrary.cs","TowerModelPresentation.cs","TowerModelPreview.cs"};
        static readonly string[] SourceHashes={
            "ec2b0ba7211c8d58844efd177559d875426ee0fbe1f28bbe9003af626ae65653",
            "4feccf68fa75f016cd388b9d1891da986a2b2d6f4f855ede69db43267b6646b6",
            "e36be3cdfcd410e73c4669b71e552890f558aa5d9fa08a424adb0bfa51e545b7",
            "87e865840a690cbd8c427c06749e26f0ef4c75fa942124b795543c7465019093",
            "23cdd4b2ee80a8b9c430bd0688ab2d0c2862393df35165b6cab58006c784a4b1"};

        [Serializable] sealed class Ownership {public string owner,version,sourceFingerprint;}
        [Serializable] sealed class Entry {public string path,kind,guid,sha256,metaSha256,geometryHash,pixelHash;}
        [Serializable] public sealed class ModelRecord
        {
            public int type,tier,triangles,renderers;public string path,name;
            public Vector3 boundsCenter,boundsSize,muzzle,interaction,buildSfx;
        }
        [Serializable] sealed class Manifest
        {
            public string owner,version,sourceFingerprint,createdUtc,shader,manifestGuid,manifestMetaSha256;
            public bool neutralPrefabs;
            public string[] sourceFiles,sourceSha256,semanticKeys;
            public Entry[] assets;public ModelRecord[] models;
        }
        [Serializable] public sealed class ValidationReceipt
        {
            public string status,version,assetRoot,sourceFingerprint,manifestSha256;
            public bool alreadyExisted,neutralPrefabs,mainSceneSetupPreserved;
            public int meshAssets,materialAssets,paletteAssets,prefabAssets,maximumTriangles,allTier3Triangles;
            public string[] semanticKeys;
            public string[] warnings;
            public ModelRecord[] models;
            public MaterialValidation[] shaderMaterials;
            public string utc;
        }
        [Serializable] public sealed class MaterialValidation
        {public string path,shader,basePaletteGuid,emissionPaletteGuid;public bool shaderSupported,shaderHasErrors,emissionEnabled;public int giFlags;public Color baseColor,emissionColor;public float metallic,smoothness;}
        sealed class RuntimeReference:IDisposable
        {
            public readonly TowerModelLibrary Library=new TowerModelLibrary();
            readonly HashSet<UnityEngine.Object> temporary=new HashSet<UnityEngine.Object>();
            public RuntimeReference()
            {
                for(int type=0;type<4;type++)
                {
                    Material material=Library.GetMaterial(type);temporary.Add(material);
                    temporary.Add(material.GetTexture("_BaseMap"));temporary.Add(material.GetTexture("_EmissionMap"));
                    for(int tier=1;tier<=3;tier++)foreach(Mesh mesh in Library.Get(type,tier).Groups)if(mesh!=null)temporary.Add(mesh);
                }
            }
            public void Dispose()
            {
                // Frozen runtime Dispose uses delayed Destroy for play mode. Edit-time cleanup uses
                // public references only, and never destroys a persistent asset.
                foreach(UnityEngine.Object item in temporary)
                    if(item!=null&&!AssetDatabase.Contains(item))UnityEngine.Object.DestroyImmediate(item);
                temporary.Clear();
            }
        }

        [MenuItem("Tools/Infinite Odyssey/Tower library/Export frozen v2")]
        static void ExportMenu()=>Debug.Log(ExportVersionedLibrary());
        [MenuItem("Tools/Infinite Odyssey/Tower library/Validate frozen v2")]
        static void ValidateMenu()=>Debug.Log(ValidateVersionedLibrary());

        public static string PrefabPath(int type,int tier)
        {if(type<0||type>=4||tier<1||tier>3)throw new ArgumentOutOfRangeException();return AssetRoot+"/Prefabs/"+Slugs[type]+"_t"+tier+".prefab";}

        public static string ExportVersionedLibrary(string receiptDirectory=null)
        {
            AssertEditorIdle();string fingerprint=VerifyFrozenSources();string sceneSetup=SceneSetupSignature();
            bool existing=Directory.Exists(Absolute(AssetRoot));
            if(existing)ReadAndVerifyOwnership(fingerprint);
            using(var reference=new RuntimeReference())
            {
                if(!existing)Bake(reference.Library,fingerprint);
                ValidationReceipt receipt=Validate(reference.Library,fingerprint);
                receipt.alreadyExisted=existing;receipt.mainSceneSetupPreserved=sceneSetup==SceneSetupSignature();
                Require(receipt.mainSceneSetupPreserved,"Exporter changed the main scene setup.");
                string json=JsonUtility.ToJson(receipt,true);
                WriteReceipt(receiptDirectory,"export-receipt.json",json);return json;
            }
        }
        public static string ValidateVersionedLibrary(string receiptDirectory=null)
        {
            AssertEditorIdle();string fingerprint=VerifyFrozenSources();ReadAndVerifyOwnership(fingerprint);
            using(var reference=new RuntimeReference())
            {
                string setup=SceneSetupSignature();ValidationReceipt receipt=Validate(reference.Library,fingerprint);
                receipt.alreadyExisted=true;receipt.mainSceneSetupPreserved=setup==SceneSetupSignature();
                string json=JsonUtility.ToJson(receipt,true);WriteReceipt(receiptDirectory,"validation-receipt.json",json);return json;
            }
        }
        public static void AssertEditorIdle()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating
                &&!BuildPipeline.isBuildingPlayer,"Run this public Editor entry point on the main thread after Play, import, compilation and build have stopped.");
        }
        static string VerifyFrozenSources()
        {
            var fingerprint=new StringBuilder();
            for(int i=0;i<SourceFiles.Length;i++)
            {
                string path=Absolute("Assets/Scripts/"+SourceFiles[i]);Require(File.Exists(path),"Copy the five exact frozen runtime source files before baking: "+SourceFiles[i]);
                string text=File.ReadAllText(path).TrimStart('\uFEFF').Replace("\r\n","\n");string hash=Hash(Encoding.UTF8.GetBytes(text));
                Require(hash==SourceHashes[i],"Frozen runtime source drift: "+SourceFiles[i]+". Use a new explicitly reviewed library version.");
                fingerprint.Append(SourceFiles[i]).Append(':').Append(hash).Append('\n');
            }
            return Hash(Encoding.UTF8.GetBytes(fingerprint.ToString()));
        }
        static HashSet<string> PlannedFiles()
        {
            var files=new HashSet<string>(StringComparer.Ordinal){OwnerPath,ManifestPath};
            for(int type=0;type<4;type++)
            {
                files.Add(AssetRoot+"/Materials/"+Slugs[type]+".mat");
                files.Add(AssetRoot+"/Palettes/"+Slugs[type]+"_base.asset");files.Add(AssetRoot+"/Palettes/"+Slugs[type]+"_emission.asset");
                for(int tier=1;tier<=3;tier++)
                {
                    files.Add(PrefabPath(type,tier));var recipe=TowerModelRecipes.Build(type,tier);
                    for(int group=0;group<6;group++)
                    {bool used=false;for(int p=0;p<recipe.Count;p++)if((int)recipe.Piece(p).Group==group){used=true;break;}
                     if(used)files.Add(MeshPath(type,tier,group));}
                }
            }
            return files;
        }
        static Manifest ReadAndVerifyOwnership(string fingerprint)
        {
            CheckNoReparse(Absolute(AssetRoot));
            CheckManagedTree();
            Require(File.Exists(Absolute(OwnerPath))&&File.Exists(Absolute(ManifestPath)),"Existing library folder is foreign or incomplete. Refusing overwrite or automatic repair.");
            Ownership owner=JsonUtility.FromJson<Ownership>(File.ReadAllText(Absolute(OwnerPath)));
            Manifest manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Absolute(ManifestPath)));
            Require(owner!=null&&owner.owner==Owner&&owner.version==Version&&owner.sourceFingerprint==fingerprint
                &&manifest!=null&&manifest.owner==Owner&&manifest.version==Version&&manifest.sourceFingerprint==fingerprint,
                "Existing library ownership/version/fingerprint does not match. Refusing overwrite.");
            Require(AssetDatabase.AssetPathToGUID(ManifestPath)==manifest.manifestGuid&&FileHash(Absolute(ManifestPath)+".meta")==manifest.manifestMetaSha256,"Manifest GUID/meta changed.");
            string[] semanticKeys={"tower_construct","tower_builder_open","tower_confirm","tower_upgrade"};
            Require(manifest.semanticKeys!=null&&manifest.semanticKeys.Length==4&&manifest.sourceSha256!=null&&manifest.sourceSha256.Length==5,"Manifest contracts incomplete.");
            for(int i=0;i<4;i++)Require(manifest.semanticKeys[i]==semanticKeys[i],"Canonical sound semantics changed.");
            for(int i=0;i<5;i++)Require(manifest.sourceSha256[i]==SourceHashes[i],"Manifest source hash changed.");
            var planned=PlannedFiles();var recorded=new HashSet<string>(StringComparer.Ordinal);
            Require(manifest.assets!=null,"Missing owned asset records.");
            foreach(Entry entry in manifest.assets)
            {
                Require(entry!=null&&IsOwnedPath(entry.path)&&recorded.Add(entry.path),"Invalid or duplicated manifest path.");
                if(entry.kind=="folder")Require(entry.path==AssetRoot||entry.path==AssetRoot+"/Meshes"||entry.path==AssetRoot+"/Materials"||entry.path==AssetRoot+"/Palettes"||entry.path==AssetRoot+"/Prefabs","Unplanned owned folder.");
                Require(AssetDatabase.AssetPathToGUID(entry.path)==entry.guid&&!String.IsNullOrEmpty(entry.guid),"Owned GUID changed: "+entry.path);
                Require(File.Exists(Absolute(entry.path)+".meta")&&FileHash(Absolute(entry.path)+".meta")==entry.metaSha256,"Owned meta changed: "+entry.path);
                if(entry.kind!="folder")Require(planned.Contains(entry.path)&&FileHash(Absolute(entry.path))==entry.sha256,"Owned asset changed: "+entry.path);
            }
            foreach(string path in Directory.GetFiles(Absolute(AssetRoot),"*",SearchOption.AllDirectories))
            {
                string asset=ProjectRelative(path);if(asset.EndsWith(".meta",StringComparison.Ordinal))asset=asset.Substring(0,asset.Length-5);
                Require(asset==ManifestPath||planned.Contains(asset)&&recorded.Contains(asset)||Directory.Exists(Absolute(asset))&&recorded.Contains(asset),"Foreign/unrecorded asset in versioned folder: "+asset);
            }
            foreach(string path in planned)Require(path==ManifestPath||recorded.Contains(path),"Manifest omits a planned asset: "+path);
            return manifest;
        }
        static void Bake(TowerModelLibrary library,string fingerprint)
        {
            Require(!Directory.Exists(Absolute(AssetRoot)),"New versioned leaf must be absent.");
            EnsureFolder("Assets/Art");EnsureFolder("Assets/Art/OracleTowerLibrary");EnsureFolder(AssetRoot);
            var transient=new List<UnityEngine.Object>(64);
            try
            {
            // A failed bake intentionally leaves its partial owned leaf for review. Never recursively delete or repair it.
            File.WriteAllText(Absolute(OwnerPath),JsonUtility.ToJson(new Ownership{owner=Owner,version=Version,sourceFingerprint=fingerprint},true),new UTF8Encoding(false));
            AssetDatabase.ImportAsset(OwnerPath,ImportAssetOptions.ForceSynchronousImport);
            var entries=new List<Entry>();entries.Add(Record(AssetRoot,"folder"));entries.Add(Record(OwnerPath,"owner"));
            foreach(string folder in new[]{"Meshes","Materials","Palettes","Prefabs"}){EnsureFolder(AssetRoot+"/"+folder);entries.Add(Record(AssetRoot+"/"+folder,"folder"));}
            var materials=new Material[4];var records=new List<ModelRecord>(12);
            for(int type=0;type<4;type++)
            {
                Material source=library.GetMaterial(type);
                Texture2D palette=ReadTexture((Texture2D)source.GetTexture("_BaseMap"));transient.Add(palette);palette.name=Slugs[type]+" original eight-color palette";
                Texture2D emission=ReadTexture((Texture2D)source.GetTexture("_EmissionMap"));transient.Add(emission);emission.name=Slugs[type]+" original eight-color emission";
                ValidateFrozenPixels(palette,type,false);ValidateFrozenPixels(emission,type,true);
                string palettePath=AssetRoot+"/Palettes/"+Slugs[type]+"_base.asset",emissionPath=AssetRoot+"/Palettes/"+Slugs[type]+"_emission.asset";
                CreateAsset(palette,palettePath);Entry paletteEntry=Record(palettePath,"palette");paletteEntry.pixelHash=PixelHash(palette);entries.Add(paletteEntry);
                CreateAsset(emission,emissionPath);Entry emissionEntry=Record(emissionPath,"palette");emissionEntry.pixelHash=PixelHash(emission);entries.Add(emissionEntry);
                Material material=UnityEngine.Object.Instantiate(source);transient.Add(material);material.name=source.name+" saved";
                material.SetTexture("_BaseMap",palette);material.SetTexture("_EmissionMap",emission);
                ConfigureAuthoringEmission(material);
                string materialPath=AssetRoot+"/Materials/"+Slugs[type]+".mat";CreateAsset(material,materialPath);
                // Reconcile only this newly authored material after its URP import.
                ConfigureAuthoringEmission(material);AssetDatabase.SaveAssetIfDirty(material);
                entries.Add(Record(materialPath,"material"));materials[type]=material;
                for(int tier=1;tier<=3;tier++)
                {
                    var template=library.Get(type,tier);var meshes=new Mesh[6];
                    for(int group=0;group<6;group++)if(template.Groups[group]!=null)
                    {
                        meshes[group]=CopyMesh(template.Groups[group]);transient.Add(meshes[group]);string path=MeshPath(type,tier,group);CreateAsset(meshes[group],path);
                        Entry entry=Record(path,"mesh");entry.geometryHash=MeshHash(meshes[group]);entries.Add(entry);
                    }
                    string prefabPath=PrefabPath(type,tier);CreatePrefab(template.Recipe,meshes,material,prefabPath);
                    entries.Add(Record(prefabPath,"prefab"));records.Add(InspectPrefab(prefabPath,template.Recipe));
                }
            }
            var manifest=new Manifest{owner=Owner,version=Version,sourceFingerprint=fingerprint,createdUtc=DateTime.UtcNow.ToString("o"),shader="Universal Render Pipeline/Lit",neutralPrefabs=true,
                sourceFiles=SourceFiles,sourceSha256=SourceHashes,semanticKeys=new[]{"tower_construct","tower_builder_open","tower_confirm","tower_upgrade"},assets=entries.ToArray(),models=records.ToArray()};
            File.WriteAllText(Absolute(ManifestPath),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ManifestPath,ImportAssetOptions.ForceSynchronousImport);
            manifest.manifestGuid=AssetDatabase.AssetPathToGUID(ManifestPath);manifest.manifestMetaSha256=FileHash(Absolute(ManifestPath)+".meta");
            File.WriteAllText(Absolute(ManifestPath),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ManifestPath,ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                foreach(UnityEngine.Object item in transient)if(item!=null&&!AssetDatabase.Contains(item))UnityEngine.Object.DestroyImmediate(item);
            }
        }
        static ValidationReceipt Validate(TowerModelLibrary library,string fingerprint)
        {
            Manifest manifest=ReadAndVerifyOwnership(fingerprint);int meshCount=0,materials=0,palettes=0,prefabs=0,maxTriangles=0,highest=0;
            var modelChecks=new List<ModelRecord>(12);var materialChecks=new List<MaterialValidation>(4);
            foreach(Entry entry in manifest.assets)
            {
                if(entry.kind=="mesh"){Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(entry.path);Require(mesh!=null&&MeshHash(mesh)==entry.geometryHash,"Saved mesh hash mismatch.");meshCount++;}
                else if(entry.kind=="palette"){Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(entry.path);Require(texture!=null&&texture.width==8&&texture.height==1&&texture.mipmapCount==1
                    &&texture.filterMode==FilterMode.Point&&texture.wrapMode==TextureWrapMode.Clamp&&PixelHash(texture)==entry.pixelHash,"Saved palette mismatch.");palettes++;}
                else if(entry.kind=="material")
                {
                    Material material=AssetDatabase.LoadAssetAtPath<Material>(entry.path);
                    Require(material!=null&&material.shader!=null&&material.shader.name=="Universal Render Pipeline/Lit"&&material.shader.isSupported
                        &&!ShaderUtil.ShaderHasError(material.shader)&&material.IsKeywordEnabled("_EMISSION")
                        &&material.globalIlluminationFlags==ExpectedAuthoringGi(true),"Saved material/shader/emission validation failed.");
                    Require(IsOwnedPath(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")))&&IsOwnedPath(AssetDatabase.GetAssetPath(material.GetTexture("_EmissionMap"))),"Material references a foreign palette.");materials++;
                    materialChecks.Add(new MaterialValidation{path=entry.path,shader=material.shader.name,shaderSupported=material.shader.isSupported,shaderHasErrors=ShaderUtil.ShaderHasError(material.shader),emissionEnabled=material.IsKeywordEnabled("_EMISSION"),giFlags=(int)material.globalIlluminationFlags,
                        basePaletteGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap"))),emissionPaletteGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material.GetTexture("_EmissionMap"))),
                        baseColor=material.GetColor("_BaseColor"),emissionColor=material.GetColor("_EmissionColor"),metallic=material.GetFloat("_Metallic"),smoothness=material.GetFloat("_Smoothness")});
                }
            }
            for(int type=0;type<4;type++)for(int tier=1;tier<=3;tier++)
            {
                var template=library.Get(type,tier);ModelRecord record=InspectPrefab(PrefabPath(type,tier),template.Recipe);
                modelChecks.Add(record);
                maxTriangles=Math.Max(maxTriangles,record.triangles);if(tier==3)highest+=record.triangles;prefabs++;
                for(int group=0;group<6;group++)if(template.Groups[group]!=null)
                    Require(MeshHash(template.Groups[group])==MeshHash(AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(type,tier,group))),"Frozen family geometry differs from saved mesh.");
                Material saved=AssetDatabase.LoadAssetAtPath<Material>(AssetRoot+"/Materials/"+Slugs[type]+".mat"),source=library.GetMaterial(type);
                ValidateFrozenPixels((Texture2D)saved.GetTexture("_BaseMap"),type,false);ValidateFrozenPixels((Texture2D)saved.GetTexture("_EmissionMap"),type,true);
                Require(PixelHash((Texture2D)saved.GetTexture("_BaseMap"))==PixelHash((Texture2D)source.GetTexture("_BaseMap"))
                    &&PixelHash((Texture2D)saved.GetTexture("_EmissionMap"))==PixelHash((Texture2D)source.GetTexture("_EmissionMap")),"Frozen palette pixels changed during bake.");
                foreach(string color in new[]{"_BaseColor","_EmissionColor"})Require(saved.GetColor(color)==source.GetColor(color),"Material color drift.");
                foreach(string value in new[]{"_Metallic","_Smoothness"})Require(saved.GetFloat(value)==source.GetFloat(value),"Material surface drift.");
            }
            Require(meshCount==51&&materials==4&&palettes==8&&prefabs==12&&maxTriangles<=2500&&highest<=10000,"Frozen asset/count/budget validation failed.");
            return new ValidationReceipt{status="native-editor-asset-validated",version=Version,assetRoot=AssetRoot,sourceFingerprint=fingerprint,
                manifestSha256=FileHash(Absolute(ManifestPath)),neutralPrefabs=true,meshAssets=meshCount,materialAssets=materials,paletteAssets=palettes,prefabAssets=prefabs,
                maximumTriangles=maxTriangles,allTier3Triangles=highest,semanticKeys=manifest.semanticKeys,utc=DateTime.UtcNow.ToString("o"),
                models=modelChecks.ToArray(),shaderMaterials=materialChecks.ToArray(),
                warnings=new[]{"Preview and saved assets do not prove gameplay/authority integration or browser performance.","Authoring meshes/palettes remain readable; future delivery optimization must preserve their visual hashes."}};
        }
        static void CreatePrefab(TowerModelRecipe recipe,Mesh[] meshes,Material material,string path)
        {
            Scene scene=EditorSceneManager.NewPreviewScene();GameObject root=null;
            try
            {
                root=new GameObject(recipe.ArtName+" Tier "+recipe.Tier);SceneManager.MoveGameObjectToScene(root,scene);
                var groups=new Transform[6];
                for(int group=0;group<6;group++)
                {
                    var go=new GameObject(((TowerModelGroup)group).ToString());groups[group]=go.transform;
                    groups[group].SetParent(group>1?groups[1]:root.transform,false);
                    if(group==1)groups[group].localPosition=Vector(recipe.HeadPivot);
                    if(meshes[group]==null)continue;
                    go.AddComponent<MeshFilter>().sharedMesh=meshes[group];var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                    renderer.shadowCastingMode=group==0||group==1?ShadowCastingMode.On:ShadowCastingMode.Off;
                    renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                }
                Anchor("Muzzle",groups[2],Vector(recipe.Muzzle)-Vector(recipe.HeadPivot));
                Anchor("Interaction",root.transform,new Vector3(0,1.25f,.60f));
                Anchor("BuildSFX",root.transform,new Vector3(0,.18f,0));
                Anchor("CoreSFX",groups[3],Vector(recipe.CoreSocket)-Vector(recipe.HeadPivot));
                Require(!File.Exists(Absolute(path))&&!File.Exists(Absolute(path)+".meta")&&AssetDatabase.LoadMainAssetAtPath(path)==null,"Refusing existing prefab path.");
                PrefabUtility.SaveAsPrefabAsset(root,path,out bool success);Require(success,"Prefab save failed: "+path);
            }
            finally {if(root!=null)UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
        }
        static ModelRecord InspectPrefab(string path,TowerModelRecipe recipe)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab!=null,"Saved prefab missing.");
            foreach(string dependency in AssetDatabase.GetDependencies(path,true))
                Require((!dependency.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)&&!dependency.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))
                    || IsVerifiedUrpMaterialVersion(dependency,prefab),"Neutral prefab depends on an unapproved script/assembly asset: "+dependency);
            Require(prefab.transform.localPosition==Vector3.zero&&prefab.transform.localRotation==Quaternion.identity&&prefab.transform.localScale==Vector3.one,"Neutral root pivot/scale changed.");
            foreach(Component component in prefab.GetComponentsInChildren<Component>(true))
                Require(component!=null&&(component is Transform||component is MeshFilter||component is MeshRenderer),"Neutral prefab has a missing/runtime/physics/light/audio component.");
            int triangles=0,renderers=0;bool first=true;Bounds bounds=default;
            foreach(MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh=filter.sharedMesh;Require(mesh!=null&&mesh.subMeshCount==1&&IsOwnedPath(AssetDatabase.GetAssetPath(mesh)),"Foreign/invalid prefab mesh.");
                MeshRenderer renderer=filter.GetComponent<MeshRenderer>();Require(renderer!=null&&renderer.sharedMaterials.Length==1&&IsOwnedPath(AssetDatabase.GetAssetPath(renderer.sharedMaterial)),"Invalid neutral renderer material.");
                bool casts=filter.name=="Foundation"||filter.name=="Head";
                Require(renderer.shadowCastingMode==(casts?ShadowCastingMode.On:ShadowCastingMode.Off)&&renderer.receiveShadows
                    &&renderer.lightProbeUsage==LightProbeUsage.BlendProbes&&renderer.reflectionProbeUsage==ReflectionProbeUsage.Off,"Renderer settings differ from the frozen family.");
                triangles+=checked((int)mesh.GetIndexCount(0)/3);renderers++;Matrix4x4 matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;Bounds b=mesh.bounds;
                for(int bits=0;bits<8;bits++)
                {Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((bits&1)==0?-1:1,(bits&2)==0?-1:1,(bits&4)==0?-1:1));p=matrix.MultiplyPoint3x4(p);
                 if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            Require(!first&&triangles==recipe.Triangles&&renderers>=4&&renderers<=5&&Mathf.Abs(bounds.min.y)<.001f&&bounds.max.y>=4&&bounds.max.y<=6
                &&bounds.size.x<=2.001f&&bounds.size.z<=2.001f,"Neutral prefab bounds/counts failed.");
            Transform muzzle=UniqueAnchor(prefab,"Muzzle"),interaction=UniqueAnchor(prefab,"Interaction"),build=UniqueAnchor(prefab,"BuildSFX");
            Vector3 muzzleLocal=prefab.transform.InverseTransformPoint(muzzle.position);
            Require((muzzleLocal-Vector(recipe.Muzzle)).sqrMagnitude<.000001f,"Muzzle anchor differs from frozen family.");
            return new ModelRecord{type=recipe.Type,tier=recipe.Tier,path=path,name=recipe.ArtName,triangles=triangles,renderers=renderers,boundsCenter=bounds.center,boundsSize=bounds.size,
                muzzle=muzzleLocal,interaction=prefab.transform.InverseTransformPoint(interaction.position),buildSfx=prefab.transform.InverseTransformPoint(build.position)};
        }
        // # ARCHITECTURE SNAPSHOT
        // URP's installed Editor material importer adds one hidden AssetVersion
        // subasset. This exact path/type/assembly exception permits that metadata,
        // never a MonoBehaviour, runtime assembly or another package script.
        const string UrpVersionScript="Packages/com.unity.render-pipelines.universal/Editor/AssetVersion.cs";
        const string PartialArchiveRoot=AssetRoot+"_partial_first_native_bake_20261016";
        static bool IsVerifiedUrpMaterialVersion(string dependency,GameObject prefab)
        {
            if(dependency!=UrpVersionScript||prefab==null)return false;
            MonoScript script=AssetDatabase.LoadAssetAtPath<MonoScript>(dependency);
            Type metadata=script!=null?script.GetClass():null;
            if(metadata==null||metadata.FullName!="UnityEditor.Rendering.Universal.AssetVersion"||
                metadata.Assembly.GetName().Name!="Unity.RenderPipelines.Universal.Editor"||
                !typeof(ScriptableObject).IsAssignableFrom(metadata))return false;
            bool found=false;
            foreach(MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            foreach(Material material in renderer.sharedMaterials)
            {
                if(material==null)return false;string path=AssetDatabase.GetAssetPath(material);
                if(!IsOwnedPath(path)||!path.EndsWith(".mat",StringComparison.Ordinal)||
                    !EditorUtility.IsPersistent(material)||!AssetDatabase.Contains(material)||!AssetDatabase.IsMainAsset(material))return false;
                string owningGuid=AssetDatabase.AssetPathToGUID(path);
                if(String.IsNullOrEmpty(owningGuid)||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material,out string materialGuid,out long materialId)||
                    materialGuid!=owningGuid||materialId==0)return false;
                foreach(UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if(item==material)continue;
                    var version=item as ScriptableObject;
                    HideFlags flags=HideFlags.HideInHierarchy|HideFlags.HideInInspector|HideFlags.NotEditable;
                    // URP hidden version objects need not report IsSubAsset. Prove their
                    // persistent identity in this owned material instead; local IDs may be negative.
                    if(version==null||version.GetType()!=metadata||
                        !EditorUtility.IsPersistent(version)||!AssetDatabase.Contains(version)||AssetDatabase.IsMainAsset(version)||
                        AssetDatabase.GetAssetPath(version)!=path||
                        !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(version,out string versionGuid,out long versionId)||
                        versionGuid!=owningGuid||versionId==0||versionId==materialId||
                        (version.hideFlags&flags)!=flags||MonoScript.FromScriptableObject(version)!=script)return false;
                    found=true;
                }
            }
            return found;
        }
        [Serializable] sealed class PartialInventory
        {public string owner,version,sourceFingerprint,assetRoot,archiveRoot,capturedUtc;public Entry[] files;}

        /// <summary>Explicit recovery of the exact reviewed first failed leaf. Archives it, then bakes an absent leaf.</summary>
        public static string ArchiveVerifiedPartialAndExport(string inventoryPath,string receiptDirectory=null)
        {
            AssertEditorIdle();string fingerprint=VerifyFrozenSources(),setup=SceneSetupSignature();
            Require(!String.IsNullOrEmpty(inventoryPath)&&Path.IsPathRooted(inventoryPath),"An absolute reviewed partial inventory is required.");
            string inventoryFile=Path.GetFullPath(inventoryPath),assets=Path.GetFullPath(Application.dataPath);
            Require(!inventoryFile.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Recovery inventory must be outside Assets.");
            PartialInventory inventory=JsonUtility.FromJson<PartialInventory>(File.ReadAllText(inventoryFile));
            Require(inventory!=null&&inventory.owner==Owner&&inventory.version==Version&&inventory.sourceFingerprint==fingerprint&&
                inventory.assetRoot==AssetRoot&&inventory.archiveRoot==PartialArchiveRoot,"Partial inventory owner/version/source/path mismatch.");
            Require(Directory.Exists(Absolute(AssetRoot))&&!File.Exists(Absolute(ManifestPath))&&!File.Exists(Absolute(ManifestPath)+".meta"),"Only an owned, pre-manifest first failure is eligible; complete libraries use ordinary validation.");
            CheckNoReparse(Absolute(AssetRoot));CheckManagedTree();
            Ownership owner=JsonUtility.FromJson<Ownership>(File.ReadAllText(Absolute(OwnerPath)));
            Require(owner!=null&&owner.owner==Owner&&owner.version==Version&&owner.sourceFingerprint==fingerprint,"Partial leaf owner mismatch.");
            var planned=PlannedFiles();var recorded=new HashSet<string>(StringComparer.Ordinal);
            Require(inventory.files!=null&&inventory.files.Length>0,"Missing reviewed partial inventory.");
            foreach(Entry entry in inventory.files)
            {
                Require(entry!=null&&entry.path!=null&&recorded.Add(entry.path)&&
                    (IsOwnedPath(entry.path)||entry.path==AssetRoot+".meta"),"Invalid/duplicate partial inventory entry.");
                string asset=entry.path.EndsWith(".meta",StringComparison.Ordinal)?entry.path.Substring(0,entry.path.Length-5):entry.path;
                bool folder=asset==AssetRoot||asset==AssetRoot+"/Meshes"||asset==AssetRoot+"/Materials"||asset==AssetRoot+"/Palettes"||asset==AssetRoot+"/Prefabs";
                Require(folder||planned.Contains(asset),"Unplanned file in partial leaf.");
                Require(File.Exists(Absolute(entry.path))&&FileHash(Absolute(entry.path))==entry.sha256,"Reviewed partial changed; recovery refused: "+entry.path);
                if(entry.path.EndsWith(".meta",StringComparison.Ordinal))
                    Require((folder?Directory.Exists(Absolute(asset)):File.Exists(Absolute(asset)))&&!String.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(asset)),"Orphan/unimported partial metadata.");
            }
            string[] current=Directory.GetFiles(Absolute(AssetRoot),"*",SearchOption.AllDirectories);
            Require(current.Length+1==recorded.Count&&recorded.Contains(AssetRoot+".meta"),"Partial tree count differs from reviewed inventory.");
            foreach(string file in current)Require(recorded.Contains(ProjectRelative(file)),"Foreign file appeared in partial leaf.");
            // Prove canonical existing data before any move. Never overwrite or repair it.
            using(var reference=new RuntimeReference())ValidatePartialData(reference.Library);
            Require(!Directory.Exists(Absolute(PartialArchiveRoot))&&!File.Exists(Absolute(PartialArchiveRoot))&&
                !File.Exists(Absolute(PartialArchiveRoot)+".meta")&&!AssetDatabase.IsValidFolder(PartialArchiveRoot),"Recovery archive target is occupied; refusing overwrite.");
            string error=AssetDatabase.MoveAsset(AssetRoot,PartialArchiveRoot);
            Require(String.IsNullOrEmpty(error),"Could not archive reviewed partial leaf: "+error);
            foreach(Entry entry in inventory.files)
            {
                string archived=PartialArchiveRoot+entry.path.Substring(AssetRoot.Length);
                Require(FileHash(Absolute(archived))==entry.sha256,"Archive content changed during move; preserve archive and inspect before retry.");
            }
            Require(!Directory.Exists(Absolute(AssetRoot))&&!File.Exists(Absolute(AssetRoot)+".meta"),"Original leaf remains occupied after archive.");
            Require(setup==SceneSetupSignature(),"Recovery changed the scene setup.");
            WriteReceipt(receiptDirectory,"partial-archive-receipt.json",JsonUtility.ToJson(inventory,true));
            // This is one fresh bake after a reviewed reversible archive, not an
            // automatic retry/repair loop. Any further failure remains for review.
            return ExportVersionedLibrary(receiptDirectory);
        }
        static void ValidatePartialData(TowerModelLibrary library)
        {
            for(int type=0;type<4;type++)
            {
                string basePath=AssetRoot+"/Palettes/"+Slugs[type]+"_base.asset",emissionPath=AssetRoot+"/Palettes/"+Slugs[type]+"_emission.asset";
                foreach(string path in new[]{basePath,emissionPath})if(File.Exists(Absolute(path)))
                {
                    Texture2D saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);Require(saved!=null,"Partial palette type mismatch.");
                    ValidateFrozenPixels(saved,type,path==emissionPath);
                    Require(saved.width==8&&saved.height==1&&saved.mipmapCount==1&&saved.filterMode==FilterMode.Point&&saved.wrapMode==TextureWrapMode.Clamp,"Partial palette settings drift.");
                    Texture2D expected=(Texture2D)library.GetMaterial(type).GetTexture(path==emissionPath?"_EmissionMap":"_BaseMap");
                    Require(PixelHash(saved)==PixelHash(expected),"Partial palette differs from frozen source.");
                }
                string materialPath=AssetRoot+"/Materials/"+Slugs[type]+".mat";
                if(File.Exists(Absolute(materialPath)))
                {
                    Material saved=AssetDatabase.LoadAssetAtPath<Material>(materialPath),expected=library.GetMaterial(type);
                    Require(saved!=null&&saved.shader==expected.shader&&
                        AssetDatabase.GetAssetPath(saved.GetTexture("_BaseMap"))==basePath&&AssetDatabase.GetAssetPath(saved.GetTexture("_EmissionMap"))==emissionPath,"Partial material shader/palette drift.");
                    // The exact hash-pinned original partial has GI=None/keyword
                    // off after URP import. Accept that known authoring defect only
                    // for an unchanged archived partial; never for the new library.
                    bool originalImportedMetadata=saved.globalIlluminationFlags==MaterialGlobalIlluminationFlags.None&&!saved.IsKeywordEnabled("_EMISSION");
                    bool correctedMetadata=saved.globalIlluminationFlags==ExpectedAuthoringGi(true)&&saved.IsKeywordEnabled("_EMISSION");
                    Require(originalImportedMetadata||correctedMetadata,"Unexpected partial material emission metadata.");
                    foreach(string value in new[]{"_BaseColor","_EmissionColor"})Require(saved.GetColor(value)==expected.GetColor(value),"Partial material color drift.");
                    foreach(string value in new[]{"_Metallic","_Smoothness"})Require(saved.GetFloat(value)==expected.GetFloat(value),"Partial material surface drift.");
                }
                for(int tier=1;tier<=3;tier++)
                {
                    var template=library.Get(type,tier);
                    for(int group=0;group<6;group++)
                    {
                        string path=MeshPath(type,tier,group);if(!File.Exists(Absolute(path)))continue;
                        Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        Require(template.Groups[group]!=null&&saved!=null&&MeshHash(saved)==MeshHash(template.Groups[group]),"Partial mesh differs from frozen family.");
                    }
                    if(File.Exists(Absolute(PrefabPath(type,tier))))InspectPrefab(PrefabPath(type,tier),template.Recipe);
                }
            }
        }

        static Transform UniqueAnchor(GameObject root,string name)
        {Transform found=null;foreach(Transform item in root.GetComponentsInChildren<Transform>(true))if(item.name==name){Require(found==null,"Duplicated anchor.");found=item;}Require(found!=null,"Missing anchor: "+name);return found;}
        static void Anchor(string name,Transform parent,Vector3 position)
        {var anchor=new GameObject(name).transform;anchor.SetParent(parent,false);anchor.localPosition=position;}
        static Mesh CopyMesh(Mesh source)
        {
            Require(source!=null&&source.subMeshCount==1&&source.GetTopology(0)==MeshTopology.Triangles,"Frozen mesh layout changed.");
            using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(source))
            {
                var data=snapshot[0];using(var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))
                using(var normals=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))
                using(var uv=new NativeArray<Vector2>(data.vertexCount,Allocator.Temp))
                using(var indices=new NativeArray<int>(checked((int)source.GetIndexCount(0)),Allocator.Temp))
                {
                    data.GetVertices(vertices);data.GetNormals(normals);data.GetUVs(0,uv);data.GetIndices(indices,0,true);
                    var mesh=new Mesh{name=source.name+" saved",indexFormat=source.indexFormat};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices.ToArray(),0,false);mesh.bounds=source.bounds;return mesh;
                }
            }
        }
        static string MeshHash(Mesh mesh)
        {
            Require(mesh!=null&&mesh.subMeshCount==1,"Mesh hash requires frozen one-submesh layout.");
            using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh))
            using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory))
            {
                var data=snapshot[0];using(var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))
                using(var normals=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))using(var uv=new NativeArray<Vector2>(data.vertexCount,Allocator.Temp))
                using(var indices=new NativeArray<int>(checked((int)mesh.GetIndexCount(0)),Allocator.Temp))
                {
                    data.GetVertices(vertices);data.GetNormals(normals);data.GetUVs(0,uv);data.GetIndices(indices,0,true);
                    writer.Write(data.vertexCount);writer.Write(indices.Length);writer.Write((int)mesh.indexFormat);
                    for(int i=0;i<vertices.Length;i++){Write(writer,vertices[i]);Write(writer,normals[i]);writer.Write(uv[i].x);writer.Write(uv[i].y);}
                    for(int i=0;i<indices.Length;i++)writer.Write(indices[i]);Write(writer,mesh.bounds.center);Write(writer,mesh.bounds.size);writer.Flush();return Hash(memory.ToArray());
                }
            }
        }
        static Texture2D ReadTexture(Texture2D source)
        {
            Require(source!=null&&source.width==8&&source.height==1,"Frozen eight-cell atlas changed.");RenderTexture previous=RenderTexture.active;bool previousSrgb=GL.sRGBWrite;
            RenderTexture target=RenderTexture.GetTemporary(8,1,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Texture2D copy=null;
            try{GL.sRGBWrite=false;Graphics.Blit(source,target);RenderTexture.active=target;copy=new Texture2D(8,1,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                copy.ReadPixels(new Rect(0,0,8,1),0,0,false);copy.Apply(false,false);return copy;}
            catch{if(copy!=null)UnityEngine.Object.DestroyImmediate(copy);throw;}
            finally{RenderTexture.active=previous;GL.sRGBWrite=previousSrgb;RenderTexture.ReleaseTemporary(target);}
        }
        static string PixelHash(Texture2D texture)
        {Texture2D readable=ReadTexture(texture);try{return Hash(readable.GetRawTextureData<byte>().ToArray());}finally{UnityEngine.Object.DestroyImmediate(readable);}}
        static void ValidateFrozenPixels(Texture2D texture,int type,bool emission)
        {
            Texture2D readable=ReadTexture(texture);
            try
            {
                Color element=type==0?new Color(1,.36f,.12f):type==1?new Color(.18f,.68f,1):type==2?new Color(.62f,.78f,.29f):new Color(.66f,.91f,1);
                Color[] expected={new Color(.13f,.16f,.14f),new Color(.60f,.63f,.53f),new Color(.085f,.27f,.14f),new Color(.63f,.43f,.14f),new Color(.17f,.20f,.19f),new Color(.35f,.23f,.12f),element,element};
                Color32[] actual=readable.GetPixels32();
                for(int i=0;i<8;i++)
                {
                    Color32 cell=emission?(i==7?(Color32)element:(Color32)Color.black):(Color32)expected[i];
                    Require(Math.Abs(actual[i].r-cell.r)<=1&&Math.Abs(actual[i].g-cell.g)<=1&&Math.Abs(actual[i].b-cell.b)<=1&&actual[i].a==255,"Frozen atlas readback/color-space validation failed.");
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(readable);}
        }
        // Saved authoring materials use URP's documented public keyword policy.
        // Versioned GI flags preserve authored emission; this does not bake scene GI or create lights.
        // Runtime source materials/palette/colors stay the exact frozen originals.
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
        static void ConfigureAuthoringEmission(Material material)
        {
            Require(material!=null,"Missing newly authored material.");
            material.globalIlluminationFlags=ExpectedAuthoringGi(true);
            BaseShaderGUI.SetMaterialKeywords(material);
            Require(material.IsKeywordEnabled("_EMISSION")&&
                material.globalIlluminationFlags==ExpectedAuthoringGi(true),"URP rejected nonblack authoring emission.");
        }
        static void CreateAsset(UnityEngine.Object asset,string path)
        {Require(IsOwnedPath(path)&&!File.Exists(Absolute(path))&&!File.Exists(Absolute(path)+".meta")&&AssetDatabase.LoadMainAssetAtPath(path)==null,"Refusing asset overwrite: "+path);AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssetIfDirty(asset);}
        static Entry Record(string path,string kind)
        {return new Entry{path=path,kind=kind,guid=AssetDatabase.AssetPathToGUID(path),sha256=kind=="folder"?null:FileHash(Absolute(path)),metaSha256=FileHash(Absolute(path)+".meta")};}
        static string MeshPath(int type,int tier,int group)=>AssetRoot+"/Meshes/"+Slugs[type]+"_t"+tier+"_"+((TowerModelGroup)group).ToString().ToLowerInvariant()+".asset";
        static Vector3 Vector(TowerModelVector p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        static void Write(BinaryWriter writer,Vector3 p){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
        static bool IsOwnedPath(string path)=>path==AssetRoot||path!=null&&path.StartsWith(AssetRoot+"/",StringComparison.Ordinal)&&!path.Contains("..")&&!path.Contains("\\");
        static string ProjectRoot=>Directory.GetParent(Application.dataPath).FullName;
        static string Absolute(string path)
        {Require(path=="Assets"||path.StartsWith("Assets/",StringComparison.Ordinal),"Expected an Assets-relative path.");string result=Path.GetFullPath(Path.Combine(ProjectRoot,path));
         Require(result.StartsWith(Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||result==Path.GetFullPath(Application.dataPath),"Resolved asset path escaped Assets.");return result;}
        static string ProjectRelative(string path)=>Path.GetFullPath(path).Substring(ProjectRoot.Length+1).Replace('\\','/');
        static void CheckNoReparse(string path)
        {for(var directory=new DirectoryInfo(path);directory!=null&&directory.FullName.StartsWith(ProjectRoot,StringComparison.OrdinalIgnoreCase);directory=directory.Parent)
            if(directory.Exists)Require((directory.Attributes&FileAttributes.ReparsePoint)==0,"Reparse/symlink asset paths are not owned by this exporter.");}
        static void CheckManagedTree()
        {
            string root=Absolute(AssetRoot);if(!Directory.Exists(root))return;
            foreach(string directory in Directory.GetDirectories(root))
            {
                string name=Path.GetFileName(directory);Require(name=="Meshes"||name=="Materials"||name=="Palettes"||name=="Prefabs","Foreign directory inside versioned leaf.");
                CheckNoReparse(directory);Require(Directory.GetDirectories(directory).Length==0,"Unexpected nested asset directory.");
                foreach(string file in Directory.GetFiles(directory))Require((File.GetAttributes(file)&FileAttributes.ReparsePoint)==0,"Linked files are not owned assets.");
            }
            foreach(string file in Directory.GetFiles(root))Require((File.GetAttributes(file)&FileAttributes.ReparsePoint)==0,"Linked ownership/manifest files are not allowed.");
        }
        static void EnsureFolder(string path)
        {CheckNoReparse(Absolute(path));if(AssetDatabase.IsValidFolder(path))return;Require(!File.Exists(Absolute(path))&&!Directory.Exists(Absolute(path))&&!File.Exists(Absolute(path)+".meta"),"Folder path is occupied, unimported or has foreign orphan metadata.");
         int slash=path.LastIndexOf('/');Require(slash>0,"Invalid folder.");AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));Require(AssetDatabase.IsValidFolder(path),"Could not create owned folder.");}
        static string Hash(byte[] bytes){using(var sha=SHA256.Create()){var result=sha.ComputeHash(bytes);var text=new StringBuilder(result.Length*2);foreach(byte b in result)text.Append(b.ToString("x2"));return text.ToString();}}
        static string FileHash(string path){Require(File.Exists(path),"Missing owned file: "+path);Require((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,"Linked owned files are refused.");return Hash(File.ReadAllBytes(path));}
        static string SceneSetupSignature()
        {var text=new StringBuilder();foreach(var scene in EditorSceneManager.GetSceneManagerSetup())text.Append(scene.path).Append('|').Append(scene.isLoaded).Append('|').Append(scene.isActive).Append(';');return text.ToString();}
        internal static void WriteReceipt(string directory,string name,string json)
        {if(String.IsNullOrEmpty(directory))return;string absolute=Path.GetFullPath(directory),assets=Path.GetFullPath(Application.dataPath);
         Require(!absolute.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&absolute!=assets,"Receipts/previews must live outside Assets.");Directory.CreateDirectory(absolute);File.WriteAllText(Path.Combine(absolute,name),json,new UTF8Encoding(false));}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif

// # ARCHITECTURE SNAPSHOT
// Editor-only authored GI metadata; Unity metres/Y-up geometry and native memory/job owners remain unchanged.

// Coordinate Space: Existing authored Unity metres/Y-up; no geometry or pivot edits.
// Array Formats: Cold Editor material metadata and exact manifest hashes only.
// Dependencies/Pointers: Versioned URP11 emission policy; strict ownership/source/GUID checks remain; no runtime native owners changed.
