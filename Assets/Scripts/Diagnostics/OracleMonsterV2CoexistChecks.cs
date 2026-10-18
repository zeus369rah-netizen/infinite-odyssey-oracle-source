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
using UnityEngine.SceneManagement;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Root-run finite Editor fixture, compiled in Assembly-CSharp OUTSIDE an
    // Editor folder. Same-assembly access uses the real factory/lease/motion
    // types without reflection or changes to fingerprinted sources. No Update,
    // queue, actor component, HP/network/prefs/asset mutation or timing claim.
    public static class OracleMonsterV2CoexistChecks
    {
        const string V1="Assets/Art/OracleMonsterLibrary/v1_0_0";
        const string V2="Assets/Art/OracleMonsterLibrary/v2_0_0";
        static readonly string[] Slugs={"guardian_golem","stalker","shade","spider"};
        static readonly int[] Parts={11,11,13,17},V1Triangles={408,372,564,528},V2Triangles={636,600,792,708},Overrides={4,4,5,2};
        static readonly Vector3 Origin=new Vector3(10000f,1000f,10000f);
        [Serializable] public sealed class StyleReceipt
        {
            public string style;
            public int parts,v1Triangles,v2Triangles,overriddenMeshes,groundFrames,flyingFrames;
            public float maximumPartBoundsError,maximumPoseError,maximumSoleError,minimumFlyingClearance;
            public bool publicFactoriesUsed,oneV2DisposePreservesOthers,lastV2DisposePreservesV1,savedReadableMeshRetained;
        }
        [Serializable] public sealed class Receipt
        {
            public string status,utc,error,v1ManifestSha256,v2ManifestSha256,triangleCountMethod;
            public string scenesBeforeSha256,scenesAfterSha256,roomsBeforeSha256,roomsAfterSha256;
            public int assertions,failures,distinctRuntimeCacheMeshes,distinctRuntimeCacheTriangles;
            public int oneV1FamilyDrawnTriangles,oneV2FamilyDrawnTriangles,coexistFixtureDrawnTriangles;
            public int v2CoreParts,extraFlyingFinsPerModel,extraFlyingTrianglesPerModel;
            public bool unityExecuted,mainScenesPreserved,roomStatePreserved,borrowedAssetsPreserved,temporarySceneClosed;
            public bool hpWritten,networkWritten,prefsWritten,assetsWritten,performanceClaim;
            public StyleReceipt[] styles;
            public string[] limitations;
        }
        sealed class Neutral:IDisposable
        {
            public GameObject Root;IDisposable lease;
            public Neutral(int kind,bool detail,Material[] paints,Scene scene)
            {
                if(detail){var value=MonsterNeutralModelAuthoringV2.Create((MonsterNeutralModelAuthoringV2.Kind)kind,paints[0],paints[1],paints[2],paints[3],scene);Root=value.Root;lease=value;}
                else{var value=MonsterNeutralModelAuthoring.Create((MonsterNeutralModelAuthoring.Kind)kind,paints[0],paints[1],paints[2],paints[3],scene);Root=value.Root;lease=value;}
                Root.hideFlags=HideFlags.HideAndDontSave;Root.transform.position=Origin;
            }
            public void Dispose(){var owned=lease;lease=null;owned?.Dispose();Root=null;}
        }
        sealed class Rig:IDisposable
        {
            public GameObject Root;public Transform Body,Left,Right;
            public MonsterVisualLease Lease;public GroundedGolemMotion Motion;
            public MeshFilter[] Filters;public PresentationFootprint Ground;
            public readonly List<Renderer> Flashes=new List<Renderer>(17);
            public Rig(int kind,bool detail,Material[] paints,Scene scene)
            {
                try
                {
                    Root=new GameObject("Finite real-motion rig"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(Root,scene);Root.transform.position=Origin;
                    Body=new GameObject("Body").transform;Body.SetParent(Root.transform,false);Body.localScale=Vector3.one*1.20f;
                    Left=new GameObject("Left arm pivot").transform;Left.SetParent(Body,false);Left.localPosition=new Vector3(-.53f,1.36f,0);
                    Right=new GameObject("Right arm pivot").transform;Right.SetParent(Body,false);Right.localPosition=new Vector3(.53f,1.36f,0);
                    Lease=MonsterVisualFactory.Create((MonsterVoxelCosmetics.Style)kind,Body,Left,Right,paints[0],paints[1],kind==2?paints[3]:paints[2],Flashes,detail?MonsterArtVersion.DetailV2:MonsterArtVersion.OriginalV1);
                    Motion=new GroundedGolemMotion(Root.transform,Body,Lease.LeftLeg,Lease.RightLeg,Left,Right,Lease.OriginalForMotion);
                    Filters=Root.GetComponentsInChildren<MeshFilter>();Ground=PresentationFootprint.Measure(Root.transform,Body,Left,Right);
                }
                catch{Dispose();throw;}
            }
            public float Sole()
            {
                if(Lease.OriginalForMotion.SpiderLegs!=null)return Lease.OriginalForMotion.LowestSpiderSole();
                return Mathf.Min(SoleOf(Lease.LeftLeg),SoleOf(Lease.RightLeg));
            }
            public void AddFins(Material paint)
            {
                // Exact existing ConfigureFlying geometry. Only the temporary
                // primitive collider is removed immediately in stopped Editor.
                for(int side=-1;side<=1;side+=2)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Flying guardian aether fin";go.transform.SetParent(Body,false);
                    go.transform.localPosition=new Vector3(side*.81f,1.2f,-.05f);go.transform.localScale=new Vector3(.72f,.075f,.42f);
                    go.transform.localRotation=Quaternion.Euler(0,0,side*18f);UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<MeshRenderer>().sharedMaterial=paint;
                }
            }
            public void Dispose(){Lease?.Dispose();Lease=null;if(Root!=null)UnityEngine.Object.DestroyImmediate(Root);Root=null;}
        }
        static void Check(Receipt result,bool passed,string label)
        {result.assertions++;if(!passed){result.failures++;throw new InvalidOperationException(label);}}
        public static Receipt Run(string absoluteWorkReceiptDirectory)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Run once on the idle Editor main thread with Play/build/import stopped.");
            string output=WorkDirectory(absoluteWorkReceiptDirectory);
            foreach(var room in UnityEngine.Object.FindObjectsByType<RoomConnection>(FindObjectsInactive.Include))
                if(room.Connected||room.Busy)throw new InvalidOperationException("Stop connected/busy rooms before the finite isolated fixture.");
            var result=new Receipt{utc=DateTime.UtcNow.ToString("o"),unityExecuted=true,styles=new StyleReceipt[4],extraFlyingFinsPerModel=2,extraFlyingTrianglesPerModel=24,
                triangleCountMethod="Drawn means indexed triangles referenced by active enabled MeshRenderers, excluding cameras/culling/overdraw; resident means distinct live runtime cache meshes. No GPU draw measurement.",
                limitations=new[]{"Finite stopped-Editor geometry/lifetime/actual-motion check only; no browser,FPS,frame-time,network,HP or combat acceptance.",
                    "Grounded samples use a temporary far-away collider in the shared default physics scene; no simulation step is run.",
                    "Runtime factory meshes intentionally use UploadMeshData(true); only saved master mesh assets are asserted readable.",
                    "Run with no unrelated authoring factory leases. A pre-existing external lease makes final cache-release assertions fail honestly; private caches are never reset.",
                    "Borrowed saved V1 materials/mesh assets are loaded and never authored; the fixture saves only this external receipt."}};
            string beforeScenes=SceneSignature(),beforeRooms=RoomSignature();Scene originalActive=SceneManager.GetActiveScene(),scene=default;
            result.scenesBeforeSha256=beforeScenes;result.roomsBeforeSha256=beforeRooms;
            var beforeAssets=AssetFiles();var originals=new Neutral[4];var first=new Neutral[4];var last=new Neutral[4];
            var v1Meshes=new HashSet<Mesh>();var overrides=new HashSet<Mesh>();var all=new HashSet<Mesh>();var borrowed=new List<Mesh>(4);
            Rig a=null,b=null;GameObject floor=null;
            try
            {
                var paints=BorrowedPaints();result.v1ManifestSha256=Hash(File.ReadAllBytes(Absolute(V1+"/manifest.json")));result.v2ManifestSha256=Hash(File.ReadAllBytes(Absolute(V2+"/manifest.json")));
                scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                Check(result,scene.GetPhysicsScene()==Physics.defaultPhysicsScene,"The finite ground fixture requires default physics; no alternate probing fallback.");
                for(int kind=0;kind<4;kind++)
                {
                    result.styles[kind]=new StyleReceipt{style=Slugs[kind]};
                    originals[kind]=new Neutral(kind,false,paints,scene);first[kind]=new Neutral(kind,true,paints,scene);last[kind]=new Neutral(kind,true,paints,scene);
                    var old=originals[kind].Root.GetComponentsInChildren<MeshFilter>();var one=first[kind].Root.GetComponentsInChildren<MeshFilter>();var two=last[kind].Root.GetComponentsInChildren<MeshFilter>();
                    var row=result.styles[kind];row.publicFactoriesUsed=true;row.parts=one.Length;row.v1Triangles=Triangles(old);row.v2Triangles=Triangles(one);
                    Check(result,old.Length==Parts[kind]&&one.Length==Parts[kind]&&two.Length==Parts[kind],"Actual original part/renderer count");
                    Check(result,row.v1Triangles==V1Triangles[kind]&&row.v2Triangles==V2Triangles[kind],"Actual original/detail triangles");
                    for(int i=0;i<old.Length;i++)
                    {
                        Check(result,old[i].name==one[i].name&&one[i].name==two[i].name,"Canonical public-factory traversal labels");
                        Check(result,one[i].sharedMesh==two[i].sharedMesh,"Same-style V2 leases share exact meshes");
                        Check(result,old[i].GetComponent<MeshRenderer>().sharedMaterial==one[i].GetComponent<MeshRenderer>().sharedMaterial,"Original borrowed palette unchanged");
                        float error=BoundsError(old[i].sharedMesh.bounds,one[i].sharedMesh.bounds);row.maximumPartBoundsError=Mathf.Max(row.maximumPartBoundsError,error);
                        Check(result,error<.00002f,"Part mesh extrema preserved");
                        Check(result,Vector3.Distance(old[i].transform.localPosition,one[i].transform.localPosition)<.00002f&&Quaternion.Angle(old[i].transform.localRotation,one[i].transform.localRotation)<.0002f,"Original part pivots preserved");
                        v1Meshes.Add(old[i].sharedMesh);all.Add(old[i].sharedMesh);all.Add(one[i].sharedMesh);
                        if(old[i].sharedMesh!=one[i].sharedMesh){overrides.Add(one[i].sharedMesh);row.overriddenMeshes++;}
                        if(old[i].name=="Left leg"||old[i].name=="Right leg"||old[i].name.StartsWith("Spider leg ",StringComparison.Ordinal))
                            Check(result,old[i].sharedMesh==one[i].sharedMesh,"Original leg and sole meshes are identical shared objects");
                    }
                    Check(result,row.overriddenMeshes==Overrides[kind],"Actual bounded override count");
                    Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(V1+"/Meshes/"+Slugs[kind]+"/part_00.asset");
                    Check(result,saved!=null&&saved.isReadable&&AssetDatabase.Contains(saved),"Original saved master remains readable/persistent");
                    Check(result,MeshDigest(saved)==MeshDigest(old[0].sharedMesh),"Borrowed saved mesh matches the exact live V1 part");
                    old[0].sharedMesh=saved;borrowed.Add(saved); // Temporary renderer reference only; never alter an asset.
                    result.oneV1FamilyDrawnTriangles+=row.v1Triangles;result.oneV2FamilyDrawnTriangles+=row.v2Triangles;result.v2CoreParts+=row.parts;
                }
                result.distinctRuntimeCacheMeshes=all.Count;foreach(var mesh in all)result.distinctRuntimeCacheTriangles+=checked((int)mesh.GetIndexCount(0)/3);
                result.coexistFixtureDrawnTriangles=result.oneV1FamilyDrawnTriangles+2*result.oneV2FamilyDrawnTriangles;
                Check(result,v1Meshes.Count==52&&overrides.Count==15&&all.Count==67&&result.distinctRuntimeCacheTriangles==3492,"Observed drawn versus resident cache counts");
                Check(result,result.oneV1FamilyDrawnTriangles==1872&&result.oneV2FamilyDrawnTriangles==2736&&result.v2CoreParts==52&&result.coexistFixtureDrawnTriangles==7344,"Actual simultaneous fixture draw counts");
                for(int kind=0;kind<4;kind++)
                {
                    var remaining=last[kind].Root.GetComponentsInChildren<MeshFilter>();first[kind].Dispose();first[kind].Dispose();
                    foreach(var filter in remaining)Check(result,filter!=null&&filter.sharedMesh!=null,"Disposing one V2 lease preserves another");
                    result.styles[kind].oneV2DisposePreservesOthers=true;
                }
                for(int kind=0;kind<4;kind++){last[kind].Dispose();last[kind].Dispose();result.styles[kind].lastV2DisposePreservesV1=true;}
                foreach(var mesh in overrides)Check(result,mesh==null,"Last V2 lease releases only owned overrides");
                foreach(var mesh in v1Meshes)Check(result,mesh!=null,"All original cached base meshes survive last V2 disposal");
                for(int kind=0;kind<4;kind++)
                {
                    Check(result,Triangles(originals[kind].Root.GetComponentsInChildren<MeshFilter>())==V1Triangles[kind],"V1 geometry survives both V2 disposals");
                    Check(result,borrowed[kind]!=null&&borrowed[kind].isReadable,"Borrowed readable saved master survives both V2 disposals");
                    result.styles[kind].savedReadableMeshRetained=true;originals[kind].Dispose();originals[kind].Dispose();
                }
                foreach(var mesh in v1Meshes)Check(result,mesh==null,"Final V1 lease releases transient original base cache");
                foreach(var paint in paints)Check(result,paint!=null&&AssetDatabase.Contains(paint),"All borrowed original paints survive every lease");
                foreach(var mesh in borrowed)Check(result,mesh!=null&&mesh.isReadable&&AssetDatabase.Contains(mesh),"Readable saved masters survive final V1 disposal");

                var occupied=new Collider[8];Check(result,Physics.OverlapBoxNonAlloc(Origin+Vector3.up,new Vector3(8,4,8),occupied,Quaternion.identity,~0,QueryTriggerInteraction.Ignore)==0,"Far-away ground fixture volume is empty");
                floor=new GameObject("Owned finite fixture floor"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(floor,scene);
                floor.transform.position=Origin-Vector3.up*.1f;var box=floor.AddComponent<BoxCollider>();box.size=new Vector3(12,.2f,12);Physics.SyncTransforms();
                for(int kind=0;kind<4;kind++)
                {
                    var row=result.styles[kind];a=new Rig(kind,false,paints,scene);b=new Rig(kind,true,paints,scene);CompareFootprint(result,a.Ground,b.Ground);
                    for(int frame=0;frame<96;frame++)
                    {
                        var phase=frame<24?OracleEnemy.GuardianAttackPhase.Ready:frame<48?OracleEnemy.GuardianAttackPhase.Windup:frame<72?OracleEnemy.GuardianAttackPhase.Strike:OracleEnemy.GuardianAttackPhase.Recovery;
                        float progress=(frame%24)/23f,hurt=frame%9==0?.1f:0;
                        a.Root.transform.position+=Vector3.forward*.012f;b.Root.transform.position=a.Root.transform.position;
                        a.Motion.Tick(1f/60,true,false,false,false,phase,progress,hurt);b.Motion.Tick(1f/60,true,false,false,false,phase,progress,hurt);
                        ComparePose(result,row,a,b);float soleError=Mathf.Abs(a.Sole()-b.Sole());row.maximumSoleError=Mathf.Max(row.maximumSoleError,soleError);
                        Check(result,soleError<.001f&&Mathf.Abs(a.Sole()-Origin.y-.004f)<.001f&&Mathf.Abs(b.Sole()-Origin.y-.004f)<.001f,"Actual grounded sole support and V1/V2 parity");
                        if(frame%24==23)CompareFootprint(result,PresentationFootprint.Measure(a.Root.transform,a.Body,a.Left,a.Right),PresentationFootprint.Measure(b.Root.transform,b.Body,b.Left,b.Right));row.groundFrames++;
                    }
                    a.Dispose();b.Dispose();Check(result,a.Flashes.Count==0&&b.Flashes.Count==0,"All lease-owned flash entries removed");a=b=null;
                    a=new Rig(kind,false,paints,scene);b=new Rig(kind,true,paints,scene);a.AddFins(paints[2]);b.AddFins(paints[2]);
                    for(int frame=0;frame<48;frame++)
                    {
                        var phase=frame<24?OracleEnemy.GuardianAttackPhase.Windup:OracleEnemy.GuardianAttackPhase.Strike;float progress=(frame%24)/23f;
                        Vector3 position=Origin+new Vector3(0,1.1f+Mathf.Sin(frame*.13f)*.08f,frame*.012f);a.Root.transform.position=b.Root.transform.position=position;
                        a.Motion.Tick(1f/60,true,true,false,false,phase,progress,0);b.Motion.Tick(1f/60,true,true,false,false,phase,progress,0);ComparePose(result,row,a,b);
                        float ca=MinimumVisualY(a.Root)-Origin.y,cb=MinimumVisualY(b.Root)-Origin.y;
                        Check(result,!float.IsNaN(ca)&&!float.IsInfinity(ca)&&!float.IsNaN(cb)&&!float.IsInfinity(cb),"Flying bounds produce finite clearance");
                        row.minimumFlyingClearance=frame==0?Mathf.Min(ca,cb):Mathf.Min(row.minimumFlyingClearance,Mathf.Min(ca,cb));
                        Check(result,Mathf.Abs(ca-cb)<.001f&&ca>.05f&&cb>.05f,"Actual folded/flying body clears fixture floor equally");
                        if(frame%24==23)CompareFootprint(result,PresentationFootprint.Measure(a.Root.transform,a.Body,a.Left,a.Right,a.Ground.NavigationRadius),PresentationFootprint.Measure(b.Root.transform,b.Body,b.Left,b.Right,b.Ground.NavigationRadius));row.flyingFrames++;
                    }
                    a.Dispose();b.Dispose();a=b=null;
                }
            }
            catch(Exception error){result.error=error.ToString();if(result.failures==0)result.failures=1;}
            finally
            {
                Cleanup(result,()=>a?.Dispose(),"V1 rig");Cleanup(result,()=>b?.Dispose(),"V2 rig");
                for(int i=0;i<4;i++)
                {
                    int slot=i;Cleanup(result,()=>first[slot]?.Dispose(),"First V2 neutral");
                    Cleanup(result,()=>last[slot]?.Dispose(),"Last V2 neutral");Cleanup(result,()=>originals[slot]?.Dispose(),"V1 neutral");
                }
                Cleanup(result,()=>{if(floor!=null)UnityEngine.Object.DestroyImmediate(floor);},"Owned floor");
                Cleanup(result,()=>{if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);},"Owned additive scene");
                Cleanup(result,()=>{if(originalActive.IsValid()&&originalActive.isLoaded)SceneManager.SetActiveScene(originalActive);Physics.SyncTransforms();},"Active scene restore");
                result.temporarySceneClosed=!scene.IsValid()||!scene.isLoaded;
                Cleanup(result,()=>{result.scenesAfterSha256=SceneSignature();result.mainScenesPreserved=beforeScenes==result.scenesAfterSha256;},"Scene preservation receipt");
                Cleanup(result,()=>{result.roomsAfterSha256=RoomSignature();result.roomStatePreserved=beforeRooms==result.roomsAfterSha256;},"Room preservation receipt");
                Cleanup(result,()=>result.borrowedAssetsPreserved=Equal(beforeAssets,AssetFiles()),"Asset preservation receipt");
                if(!result.mainScenesPreserved||!result.roomStatePreserved||!result.borrowedAssetsPreserved||!result.temporarySceneClosed)result.failures++;
            }
            result.status=result.failures==0?"PASSED_NATIVE_V1_V2_COEXIST_DISPOSE_GAIT":"FAILED_NATIVE_V1_V2_COEXIST_DISPOSE_GAIT";
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"native-v1-v2-coexist.json"),JsonUtility.ToJson(result,true),new UTF8Encoding(false));return result;
        }
        static void Cleanup(Receipt result,Action action,string label)
        {try{action();}catch(Exception error){result.failures++;result.error=(result.error??string.Empty)+"\n"+label+": "+error;}}
        static void ComparePose(Receipt result,StyleReceipt row,Rig a,Rig b)
        {
            Check(result,a.Filters.Length==b.Filters.Length,"Actual motion part count");
            for(int i=0;i<a.Filters.Length;i++)
            {
                var x=a.Filters[i].transform;var y=b.Filters[i].transform;
                float error=Mathf.Max(Vector3.Distance(x.localPosition,y.localPosition),Quaternion.Angle(x.localRotation,y.localRotation),Vector3.Distance(x.localScale,y.localScale));
                row.maximumPoseError=Mathf.Max(row.maximumPoseError,error);Check(result,a.Filters[i].name==b.Filters[i].name&&error<.001f,"Actual rigid gait/attack local pose unchanged");
            }
        }
        static void CompareFootprint(Receipt result,PresentationFootprint a,PresentationFootprint b)
        {
            Check(result,BoundsError(a.Hitbox,b.Hitbox)<.0002f,"Actual attack-sweep bounds preserved");
            Check(result,Mathf.Abs(a.NavigationRadius-b.NavigationRadius)<.0002f&&Mathf.Abs(a.CrowdRadius-b.CrowdRadius)<.0002f&&Mathf.Abs(a.LowerBodyRadius-b.LowerBodyRadius)<.0002f,"Actual navigation/crowd/lower-body radii preserved");
            Check(result,Mathf.Abs(a.CapsuleBottom-b.CapsuleBottom)<.0002f&&Mathf.Abs(a.CapsuleTop-b.CapsuleTop)<.0002f&&Mathf.Abs(a.MinimumVisualY-b.MinimumVisualY)<.0002f,"Actual vertical capsule/sole envelope preserved");
        }
        static float SoleOf(Transform leg)
        {Matrix4x4 m=leg.localToWorldMatrix;Bounds b=leg.GetComponent<MeshFilter>().sharedMesh.bounds;Vector3 e=b.extents;return m.MultiplyPoint3x4(b.center).y-(Mathf.Abs(m.m10)*e.x+Mathf.Abs(m.m11)*e.y+Mathf.Abs(m.m12)*e.z);}
        static float MinimumVisualY(GameObject root)
        {
            float min=float.PositiveInfinity;foreach(var f in root.GetComponentsInChildren<MeshFilter>())
            {Matrix4x4 m=f.transform.localToWorldMatrix;Bounds b=f.sharedMesh.bounds;Vector3 e=b.extents;min=Mathf.Min(min,m.MultiplyPoint3x4(b.center).y-Mathf.Abs(m.m10)*e.x-Mathf.Abs(m.m11)*e.y-Mathf.Abs(m.m12)*e.z);}return min;
        }
        static float BoundsError(Bounds a,Bounds b)=>Mathf.Max(Vector3.Distance(a.min,b.min),Vector3.Distance(a.max,b.max));
        static int Triangles(MeshFilter[] filters)
        {
            int value=0;foreach(var f in filters)
            {
                var renderer=f.GetComponent<MeshRenderer>();
                if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy||f.sharedMesh==null||f.sharedMesh.subMeshCount!=1)
                    throw new InvalidOperationException("Expected one active original mesh renderer/submesh per core part.");
                value+=checked((int)f.sharedMesh.GetIndexCount(0)/3);
            }return value;
        }
        static Material[] BorrowedPaints()
        {
            string[] names={"stone","joints","cyan","shade_mint"};var value=new Material[4];
            for(int i=0;i<4;i++){string path=V1+"/Materials/"+names[i]+".mat";value[i]=AssetDatabase.LoadAssetAtPath<Material>(path);if(value[i]==null||!AssetDatabase.Contains(value[i])||AssetDatabase.GetAssetPath(value[i])!=path)throw new InvalidOperationException("Missing exact borrowed V1 palette: "+path);}return value;
        }
        static string MeshDigest(Mesh mesh)
        {
            using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh))using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                var data=snapshot[0];using(var v=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))using(var n=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))
                using(var uv=new NativeArray<Vector2>(data.vertexCount,Allocator.Temp))using(var ix=new NativeArray<int>(checked((int)mesh.GetIndexCount(0)),Allocator.Temp))
                {
                    data.GetVertices(v);data.GetNormals(n);data.GetUVs(0,uv);data.GetIndices(ix,0,true);writer.Write(v.Length);writer.Write(ix.Length);
                    for(int i=0;i<v.Length;i++){Write(writer,v[i]);Write(writer,n[i]);writer.Write(uv[i].x);writer.Write(uv[i].y);}for(int i=0;i<ix.Length;i++)writer.Write(ix[i]);Write(writer,mesh.bounds.center);Write(writer,mesh.bounds.size);writer.Flush();return Hash(stream.ToArray());
                }
            }
        }
        static void Write(BinaryWriter writer,Vector3 p){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
        static string SceneSignature()
        {
            var text=new StringBuilder();text.Append(SceneManager.GetActiveScene().handle.GetRawData().ToString());
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                Scene scene=SceneManager.GetSceneAt(i);text.Append('|').Append(scene.handle.GetRawData().ToString()).Append(':').Append(scene.path).Append(':').Append(scene.isLoaded).Append(':').Append(scene.isDirty);
                if(!scene.isLoaded)continue;int count=0;foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {if(++count>20000)throw new InvalidOperationException("Scene signature exceeds the finite20k-transform fixture guard.");text.Append('|').Append(t.GetEntityId().ToString()).Append(':').Append(t.parent!=null?t.parent.GetEntityId().ToString():"root").Append(':').Append(t.localPosition.ToString("R")).Append(':').Append(t.localRotation.ToString("R")).Append(':').Append(t.localScale.ToString("R")).Append(':').Append(t.gameObject.activeSelf);}
            }
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        static string RoomSignature()
        {
            var text=new StringBuilder();var rooms=UnityEngine.Object.FindObjectsByType<RoomConnection>(FindObjectsInactive.Include);
            Array.Sort(rooms,(a,b)=>string.CompareOrdinal(a.GetEntityId().ToString(),b.GetEntityId().ToString()));foreach(var r in rooms)
                text.Append(r.GetEntityId().ToString()).Append(':').Append(r.Connected).Append(':').Append(r.Busy).Append(':').Append(r.IsHost).Append(':').Append(r.PlayerCount).Append(':').Append(r.CampaignActive).Append(':').Append(r.LocalReady).Append(':').Append(r.ReadyPlayerCount).Append('|');
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        static SortedDictionary<string,string> AssetFiles()
        {
            var values=new SortedDictionary<string,string>(StringComparer.Ordinal);
            foreach(string root in new[]{V1,V2})
            {string directory=Absolute(root);if(!Directory.Exists(directory))throw new InvalidOperationException("Bake and validate both immutable libraries before this fixture.");foreach(string f in Directory.GetFiles(directory,"*",SearchOption.AllDirectories))values.Add(f,Hash(File.ReadAllBytes(f)));values.Add(directory+".meta",Hash(File.ReadAllBytes(directory+".meta")));}return values;
        }
        static bool Equal(SortedDictionary<string,string> a,SortedDictionary<string,string> b)
        {if(a.Count!=b.Count)return false;foreach(var row in a)if(!b.TryGetValue(row.Key,out var value)||row.Value!=value)return false;return true;}
        static string Absolute(string asset)=>Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,asset));
        static string WorkDirectory(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path))throw new ArgumentException("An absolute receipt directory under this task's work folder is required.");
            string value=Path.GetFullPath(path),work=Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"../../work"));
            if(!value.StartsWith(work+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("The fixture writes receipts only below the task work folder.");return value;
        }
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",string.Empty).ToLowerInvariant();}
    }
}
#endif
