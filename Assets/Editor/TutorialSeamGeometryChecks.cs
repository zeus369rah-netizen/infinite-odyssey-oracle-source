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
    // Staged root-only native check. Read actual opaque rendered triangles, not existing solid colliders.
    // Public read-only mesh snapshots support UploadMeshData(true); every temporary buffer is disposed.
    // No scene creation, gameplay mutation, private reflection, mesh upload or physics cooking.
    public static class TutorialSeamGeometryChecks
    {
        [Serializable] public sealed class Result { public string status; public int passed, failed, triangles; public Row[] checks; }
        [Serializable] public sealed class Row { public string name, status, exception; public float distance, nearestDistance; public int authoredMatches; }
        struct Triangle { public Vector3 a,b,c; }

        public static string Run()
        {
            var report = new Result();
            var rows = new List<Row>(48);
            try {
                Demand(Application.isPlaying, "Run only in root's stable native tutorial/menu frame.");
                GameSession session = GameSession.Instance;
                Demand(session != null && session.World != null && !session.World.IsCoopArena, "An initialized tutorial world is required.");
                List<Triangle> triangles = ReadOpaque(session.World);
                report.triangles = triangles.Count;
                Demand(triangles.Count > 0 && triangles.Count < 1000000, "Opaque tutorial mesh data must exist and remain bounded.");

                // Current mortar heights intentionally miss the front bricks: backing must close these rays.
                foreach (float side in new[] { -1f, 1f }) {
                    Probe(rows, triangles, "Hall low mortar " + side, new Vector3(0,.605f,-9.70f), Vector3.right*side, 6.3f, true, 5.78f);
                    Probe(rows, triangles, "Hall upper mortar " + side, new Vector3(0,4.265f,4.0f), Vector3.right*side, 6.3f, true, 5.78f);
                }
                foreach (float center in new[] { -3f, 8f, 18f }) {
                    foreach (float side in new[] { -1f, 1f }) {
                        foreach (float end in new[] { -1f, 1f })
                            Probe(rows, triangles, "Alcove side mortar " + side + "/" + center + "/" + end,
                                new Vector3(side*9.2f,.56f,center), Vector3.forward*end, 2.9f, true, 2.15f);
                        Probe(rows, triangles, "Alcove entrance kept open " + side + "/" + center,
                            new Vector3(side*5.0f,1.6f,center), Vector3.right*side, 1.1f, false);
                    }
                    Probe(rows, triangles, "Crypt outer mortar " + center, new Vector3(-9f,.56f,center), Vector3.left, 1.3f, true, .82f);
                    Probe(rows, triangles, "Window lower masonry " + center, new Vector3(9f,.56f,center), Vector3.right, 1.3f, true, .82f);
                    // Start beyond the intentional bars; do not mistake bars for an occluded window aperture.
                    Probe(rows, triangles, "Window view aperture " + center, new Vector3(9.6f,3f,center+.25f), Vector3.right, 1.5f, false);
                }
                Probe(rows, triangles, "Arrival end mortar behind closed door", new Vector3(0,.605f,-14f), Vector3.back, .8f, true, .10f);
                Probe(rows, triangles, "Rear end mortar beyond steps", new Vector3(0,.605f,27.95f), Vector3.forward, .8f, true, .20f);
                foreach (float z in new[] { -9.85f, -.05f, 4.55f, 11.05f, 14.75f, 24.2f })
                    foreach (float side in new[] { -1f, 1f }) {
                        float y = z > 23f ? 3.1f : 2.65f;
                        // Top outside iron plate: require the authored stone corbel face at x±5.41.
                        Probe(rows, triangles, "Rendered torch corbel " + side + "/" + z,
                            new Vector3(side*5.2f,y+.11f,z+.24f), Vector3.right*side, .5f, true, .21f);
                    }
            } catch (Exception exception) {
                rows.Add(new Row { name="native mesh prerequisites", status="failed", exception=exception.ToString(), distance=-1 });
            }
            foreach (Row row in rows) { if(row.status=="passed")report.passed++;else report.failed++; }
            report.status = report.failed == 0 && report.passed > 0 ? "passed" : "failed";
            report.checks = rows.ToArray();
            return JsonUtility.ToJson(report, true);
        }

        static List<Triangle> ReadOpaque(DungeonWorld world)
        {
            var output = new List<Triangle>(40000);
            foreach (MeshFilter filter in world.GetComponentsInChildren<MeshFilter>(false)) {
                Mesh mesh = filter.sharedMesh;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if(mesh==null || renderer==null || !renderer.enabled)continue;
                Material[] materials=renderer.sharedMaterials;
                Matrix4x4 matrix=filter.transform.localToWorldMatrix;
                using(Mesh.MeshDataArray snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh)) {
                    Mesh.MeshData data=snapshot[0];
                    var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp);
                    try {
                        data.GetVertices(vertices);
                        for(int submesh=0;submesh<data.subMeshCount;submesh++) {
                            if(submesh>=materials.Length || materials[submesh]==null || materials[submesh].renderQueue>=2500)continue;
                            var descriptor=data.GetSubMesh(submesh);
                            if(descriptor.topology!=MeshTopology.Triangles)continue;
                            var indices=new NativeArray<int>(descriptor.indexCount,Allocator.Temp);
                            try {
                                data.GetIndices(indices,submesh,true);
                                for(int i=0;i+2<indices.Length;i+=3)
                                    output.Add(new Triangle { a=matrix.MultiplyPoint3x4(vertices[indices[i]]),
                                        b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]),c=matrix.MultiplyPoint3x4(vertices[indices[i+2]]) });
                            } finally { if(indices.IsCreated)indices.Dispose(); }
                        }
                    } finally { if(vertices.IsCreated)vertices.Dispose(); }
                }
            }
            return output;
        }

        static void Probe(List<Row> rows,List<Triangle> triangles,string name,Vector3 origin,Vector3 direction,float limit,bool expected,float expectedDistance=-1)
        {
            var row=new Row { name=name,status="running",distance=-1,nearestDistance=-1 };
            try {
                float closest=float.PositiveInfinity, authored=float.PositiveInfinity;
                foreach(Triangle triangle in triangles) {
                    Vector3 edge1=triangle.b-triangle.a,edge2=triangle.c-triangle.a;
                    Vector3 p=Vector3.Cross(direction,edge2);
                    float determinant=Vector3.Dot(edge1,p);
                    if(Mathf.Abs(determinant)<.000001f)continue;
                    float inverse=1f/determinant;
                    Vector3 offset=origin-triangle.a;
                    float u=Vector3.Dot(offset,p)*inverse;
                    if(u<-.00001f || u>1.00001f)continue;
                    Vector3 q=Vector3.Cross(offset,edge1);
                    float v=Vector3.Dot(direction,q)*inverse;
                    if(v<-.00001f || u+v>1.00001f)continue;
                    float hit=Vector3.Dot(edge2,q)*inverse;
                    if(hit<=.0001f || hit>limit)continue;
                    if(hit<closest)closest=hit;
                    // Props may legitimately occlude the backing. Require its plane at the authored
                    // distance, rather than accepting an arbitrary nearer hit or a distant bare wall.
                    // Parallel plane normals reject a slanted prop crossing the same distance band.
                    if(expectedDistance>=0 && Mathf.Abs(hit-expectedDistance)<.02f) {
                        Vector3 normal=Vector3.Cross(edge1,edge2);
                        float squareLength=normal.sqrMagnitude;
                        float alignment=Vector3.Dot(normal,direction);
                        if(squareLength>.00000001f && alignment*alignment>=squareLength*.998001f) {
                            row.authoredMatches++;
                            if(Mathf.Abs(hit-expectedDistance)<Mathf.Abs(authored-expectedDistance))authored=hit;
                        }
                    }
                }
                bool found=!float.IsPositiveInfinity(closest);
                if(found)row.nearestDistance=closest;
                row.distance=expectedDistance>=0 && row.authoredMatches>0 ? authored : row.nearestDistance;
                Demand(found==expected,"Opaque rendered mesh aperture/seam expectation failed: "+name);
                if(expectedDistance>=0)Demand(row.authoredMatches>0,"An opaque axis-facing triangle must exist at the authored backing/corbel distance, even when a nearer prop occludes it; a near prop alone or distant bare wall cannot satisfy this check.");
                row.status="passed";
            } catch(Exception exception) { row.status="failed";row.exception=exception.ToString(); }
            rows.Add(row);
        }
        static void Demand(bool condition,string message)
        {
            Assert.IsTrue(condition,message);
            if(!condition)throw new InvalidOperationException(message);
        }
    }
}
#endif
