#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Root-only, read-only native fixture. Require the authored material/plane in actual rendered meshes,
    // allowing a nearer prop. Public MeshData snapshots support non-readable combined meshes.
    // Positions are tutorial-root local metres; no camera, save, gameplay, physics or light mutation.
    public static class TutorialTorchMountChecks
    {
        [Serializable] public sealed class Result { public string status; public int passed, failed, triangles, livingFires, emitters; public Row[] checks; public string illumination; }
        [Serializable] public sealed class Row { public string name, status, exception; public int matches; public float expectedDistance, measuredDistance; }
        struct Triangle { public Vector3 a,b,c; public int material; }
        static readonly float[] Rows = { -11.8f,-6.2f,3f,13f,22f,27.3f };

        public static string Run()
        {
            var result=new Result(); var checks=new List<Row>(100);
            try {
                GameSession session=GameSession.Instance;
                Require(session!=null && session.World!=null && !session.World.IsCoopArena,"An initialized tutorial world is required.");
                DungeonWorld world=session.World;
                Transform root=null;
                TutorialFireLightPool pool=world.GetComponentInChildren<TutorialFireLightPool>(false);
                Require(pool!=null,"Tutorial emitter registry must exist."); root=pool.transform;
                List<Triangle> triangles=Read(world); result.triangles=triangles.Count;
                Require(triangles.Count>0,"Actual stone, iron and wood triangles must exist.");
                VoxelFireEffect[] fires=world.GetComponentsInChildren<VoxelFireEffect>(false);
                result.livingFires=fires.Length;result.emitters=pool.RegisteredEmitterCount;
                Check(checks,"Exactly13 living fires",()=>Require(fires.Length==13,"Expected12 wall torches and one campfire."));
                Check(checks,"Exactly23 registered emitters",()=>Require(pool.RegisteredEmitterCount==23,"No duplicated chandelier-flame registrations."));
                foreach(float z in Rows) foreach(float side in new[]{-1f,1f}) {
                    float y=z>23?3.1f:2.65f,inward=-side,face=side*4.2125f,shaft=side*3.6825f;
                    string id=side+"/"+z;
                    Probe(checks,triangles,root,"Pier panel "+id,1,new Vector3(side*3.9f,y-.1f,z+.195f),Vector3.right*side,.3125f);
                    Probe(checks,triangles,root,"Embedded iron backplate "+id,2,new Vector3(side*3.9f,y-.02f,z),Vector3.right*side,.2375f);
                    Probe(checks,triangles,root,"Continuous iron arm "+id,2,new Vector3(face+inward*.27f,y-.60f,z),Vector3.up,.205f);
                    Vector3 axis=new Vector3(inward*.51f,.22f,0).normalized;
                    Vector3 normal=new Vector3(axis.y,-axis.x,0);
                    Vector3 center=new Vector3(face+inward*.275f,y-.46f,z);
                    Probe(checks,triangles,root,"Diagonal iron brace "+id,2,center+normal*.12f,-normal,.0825f);
                    Probe(checks,triangles,root,"Supported oak shaft "+id,3,new Vector3(side*3.25f,y-.1f,z),Vector3.right*side,.3675f);
                    Probe(checks,triangles,root,"Iron flame cup "+id,2,new Vector3(shaft,y+.55f,z+.10f),Vector3.down,.205f);
                    Vector3 flame=root.TransformPoint(new Vector3(shaft,y+.35f,z));
                    Check(checks,"Living flame on cup "+id,()=>{
                        int found=0;foreach(VoxelFireEffect fire in fires) if(fire.name=="Living wall torch flame and stepped smoke" && (fire.transform.position-flame).sqrMagnitude<.000001f)found++;
                        Require(found==1,"Exactly one live flame root must sit5mm above the authored cup top.");
                    });
                    Vector3 emitter=new Vector3(shaft,y+.57f,z);
                    Check(checks,"Registered warm spill "+id,()=>{
                        int found=0;for(int i=0;i<pool.RegisteredEmitterCount;i++)if(pool.TryGetEmitterPosition(i,out Vector3 p)&&(p-emitter).sqrMagnitude<.000001f)found++;
                        Require(found==1,"Exactly one registered emitter must follow its mounted flame.");
                    });
                }
                result.illumination="Geometry/registry checks do not measure wall brightness. For surface contribution, root captures existing light on/off render requests after a native frame; no nearest-light assignment is misreported as all12 illuminated.";
            } catch(Exception exception) { checks.Add(new Row{name="Native prerequisites",status="failed",exception=exception.ToString()}); }
            foreach(Row row in checks)if(row.status=="passed")result.passed++;else result.failed++;
            result.status=result.failed==0&&result.passed>0?"passed":"failed";result.checks=checks.ToArray();
            return JsonUtility.ToJson(result,true);
        }
        static void Check(List<Row> rows,string name,Action action) {
            var row=new Row{name=name,status="failed"};try{action();row.status="passed";}catch(Exception e){row.exception=e.ToString();}rows.Add(row);
        }
        static void Probe(List<Row> rows,List<Triangle> triangles,Transform root,string name,int material,Vector3 localOrigin,Vector3 localDirection,float distance) {
            var row=new Row{name=name,status="failed",expectedDistance=distance,measuredDistance=-1};
            try {
                Require((root.lossyScale-Vector3.one).sqrMagnitude<.000001f,"Fixture requires the authored unit-scale geometry root.");
                Vector3 origin=root.TransformPoint(localOrigin),direction=root.TransformDirection(localDirection).normalized;
                foreach(Triangle t in triangles) {
                    if(t.material!=material)continue;
                    Vector3 e1=t.b-t.a,e2=t.c-t.a,n=Vector3.Cross(e1,e2);
                    float alignment=Vector3.Dot(n,direction);
                    if(n.sqrMagnitude<.000000001f||alignment*alignment<n.sqrMagnitude*.998001f)continue;
                    Vector3 p=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,p);if(Mathf.Abs(det)<.000001f)continue;
                    float inv=1/det;Vector3 offset=origin-t.a;float u=Vector3.Dot(offset,p)*inv;if(u<-.00001f||u>1.00001f)continue;
                    Vector3 q=Vector3.Cross(offset,e1);float v=Vector3.Dot(direction,q)*inv;if(v<-.00001f||u+v>1.00001f)continue;
                    float hit=Vector3.Dot(e2,q)*inv;
                    if(hit>0&&Mathf.Abs(hit-distance)<=.003f){row.matches++;row.measuredDistance=hit;}
                }
                Require(row.matches>0,"No actual triangle with matching material/normal at the authored distance; nearby props cannot satisfy this test.");row.status="passed";
            }catch(Exception e){row.exception=e.ToString();}rows.Add(row);
        }
        static List<Triangle> Read(DungeonWorld world) {
            var output=new List<Triangle>(20000);
            foreach(MeshFilter filter in world.GetComponentsInChildren<MeshFilter>(false)) {
                Mesh mesh=filter.sharedMesh;MeshRenderer renderer=filter.GetComponent<MeshRenderer>();if(mesh==null||renderer==null||!renderer.enabled)continue;
                Material[] materials=renderer.sharedMaterials;Matrix4x4 matrix=filter.transform.localToWorldMatrix;
                using(Mesh.MeshDataArray snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh)) {
                    Mesh.MeshData data=snapshot[0];var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp);
                    try {
                        data.GetVertices(vertices);
                        for(int sub=0;sub<data.subMeshCount;sub++) {
                            if(sub>=materials.Length||materials[sub]==null)continue;
                            string name=materials[sub].name;int tag=name=="Green slate recesses"?1:name=="Black wrought iron"?2:name=="Ancient oak grain"?3:0;
                            var descriptor=data.GetSubMesh(sub);if(tag==0||descriptor.topology!=MeshTopology.Triangles)continue;
                            var indices=new NativeArray<int>(descriptor.indexCount,Allocator.Temp);
                            try{data.GetIndices(indices,sub,true);for(int i=0;i+2<indices.Length;i+=3)output.Add(new Triangle{material=tag,a=matrix.MultiplyPoint3x4(vertices[indices[i]]),b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]),c=matrix.MultiplyPoint3x4(vertices[indices[i+2]])});}
                            finally{if(indices.IsCreated)indices.Dispose();}
                        }
                    }finally{if(vertices.IsCreated)vertices.Dispose();}
                }
            }return output;
        }
        static void Require(bool value,string message){Assert.IsTrue(value,message);if(!value)throw new InvalidOperationException(message);}
    }
}
#endif
