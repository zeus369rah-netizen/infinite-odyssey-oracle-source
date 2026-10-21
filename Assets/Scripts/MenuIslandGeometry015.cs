// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original catalog-local metres/Y-up; all new scenery stays below/outside the playable surface.
// Array Formats: cold native cells/greedy quad streams, bounded box streams; four owned combined Unity meshes.
// Dependencies/Pointers: numeric jobs finish before main-thread uploads; no collider, gameplay entity or per-voxel Object.
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public static class MenuIslandGeometry015
    {
        public const int CityBuildings=64,CloudClusters=12,RainStreaks=96;
        public static float CellSize(Bounds bounds)=>Mathf.Max(bounds.size.x,bounds.size.z)/32f;
        public static Bounds FullIslandBounds(Bounds map,float groundY)
        {
            float unit=CellSize(map);var result=map;
            result.Encapsulate(new Vector3(map.min.x,groundY-MenuIslandMath015.Depth*unit-.025f,map.min.z));
            result.Encapsulate(new Vector3(map.max.x,groundY-.025f,map.max.z));return result;
        }
        public static Mesh BuildIsland(Bounds map,float groundY,uint seed)
        {
            float unit=CellSize(map);
            using(var chunk=new VoxelChunkData(new Vector3Int(32,MenuIslandMath015.Depth,32),unit))
            {
                new MenuIslandFillJob015{Cells=chunk.Cells,Seed=seed}.Schedule().Complete();
                using(var output=VoxelGreedyMesher.Build(chunk))
                {
                    var offset=new float3(map.center.x-16*unit,groundY-.025f-MenuIslandMath015.Depth*unit,map.center.z-16*unit);
                    new MenuIslandOffsetJob015{Vertices=output.Vertices,Offset=offset}.Schedule().Complete();
                    return Upload(output,"Original greedy-meshed floating island underside015",0);
                }
            }
        }
        public static Mesh BuildDistant(Bounds map,uint seed,int mode)
        {
            if(mode<1||mode>3)throw new ArgumentOutOfRangeException(nameof(mode));
            using(var output=new VoxelMeshBuffers())
            {
                new MenuIslandSceneryJob015{Vertices=output.Vertices,Normals=output.Normals,UVs=output.UVs,Indices=output.Indices,Groups=output.QuadGroups,
                    Center=new float3(map.center.x,map.min.y,map.center.z),Seed=seed,Mode=mode}.Schedule().Complete();
                return Upload(output,mode==1?"Original combined lower city015":mode==2?"Original combined distant clouds015":"Original bounded outdoor rain015",mode);
            }
        }
        static Mesh Upload(VoxelMeshBuffers output,string name,int mode)
        {
            int count=output.Vertices.Length;
            if(count==0||count>MenuIslandMath015.CellCount*24||output.Indices.Length>MenuIslandMath015.CellCount*36||output.QuadGroups.Length*4!=count)
                throw new InvalidOperationException("Bounded original scenery mesh contract failed.");
            var colors=new Color32[count];
            for(int i=0;i<count;i++)colors[i]=Tint(output.QuadGroups[i/4],mode);
            var mesh=new Mesh{name=name,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            try
            {
                mesh.vertices=output.Vertices.AsArray().ToArray();mesh.normals=output.Normals.AsArray().ToArray();mesh.uv=output.UVs.AsArray().ToArray();
                mesh.colors32=colors;mesh.triangles=output.Indices.AsArray().ToArray();mesh.RecalculateBounds();
                if(mode==2||mode==3){var bounds=mesh.bounds;bounds.Expand(mode==3?new Vector3(3,80,3):new Vector3(7,0,0));mesh.bounds=bounds;}
                // Fixed CPU/GPU buffer contents; motion uses only the material's private clock.
                mesh.UploadMeshData(true);return mesh;
            }
            catch{UnityEngine.Object.Destroy(mesh);throw;}
        }
        static Color32 Tint(byte group,int mode)
        {
            if(mode==3)return new Color32(139,179,190,255);
            if(mode==2)return group==1?new Color32(165,186,191,255):group==2?new Color32(137,159,170,255):new Color32(189,202,201,255);
            if(mode==1)return group==1?new Color32(52,69,84,255):group==2?new Color32(67,85,97,255):new Color32(87,101,108,255);
            return group==1?new Color32(56,67,68,255):group==2?new Color32(74,82,78,255):new Color32(89,94,84,255);
        }
    }
    [BurstCompile] public struct MenuIslandOffsetJob015:IJob
    {
        public NativeList<Vector3> Vertices;public float3 Offset;
        public void Execute(){for(int i=0;i<Vertices.Length;i++){Vector3 p=Vertices[i];Vertices[i]=new Vector3(p.x+Offset.x,p.y+Offset.y,p.z+Offset.z);}}
    }
    [BurstCompile] public struct MenuIslandSceneryJob015:IJob
    {
        public NativeList<Vector3> Vertices,Normals;public NativeList<Vector2> UVs;public NativeList<int> Indices;public NativeList<byte> Groups;
        public float3 Center;public uint Seed;public int Mode;
        float rainStart;
        public void Execute()
        {
            Vertices.Clear();Normals.Clear();UVs.Clear();Indices.Clear();Groups.Clear();
            if(Mode==1)
            {
                // Fixed ring addresses, no nearest-neighbor query or runtime spatial store.
                for(int i=0;i<MenuIslandGeometry015.CityBuildings;i++)
                {
                    float a=i*(2f*math.PI/MenuIslandGeometry015.CityBuildings),r=235f+MenuIslandMath015.Unit(Seed+(uint)i*13u)*100f;
                    float height=22f+MenuIslandMath015.Unit(Seed+(uint)i*71u)*64f;
                    float3 basePoint=Center+new float3(math.cos(a)*r,-155f,math.sin(a)*r);
                    float width=7f+MenuIslandMath015.Unit(Seed+(uint)i*31u)*10f;
                    Box(basePoint+new float3(0,height*.5f,0),new float3(width,height,width*.8f),(byte)(1+i%3));
                    Box(basePoint+new float3(0,height+2,0),new float3(width*.55f,4,width*.45f),(byte)(1+(i+1)%3));
                }
            }
            else if(Mode==2)
            {
                for(int i=0;i<MenuIslandGeometry015.CloudClusters;i++)
                {
                    float a=i*(2f*math.PI/MenuIslandGeometry015.CloudClusters)+.21f,r=185f+MenuIslandMath015.Unit(Seed+(uint)i*113u)*80f;
                    float3 p=Center+new float3(math.cos(a)*r,-35f-MenuIslandMath015.Unit(Seed+(uint)i*59u)*32f,math.sin(a)*r);
                    float width=28f+MenuIslandMath015.Unit(Seed+(uint)i*37u)*28f;
                    Box(p,new float3(width,4f,width*.45f),1);
                    Box(p+new float3(width*.08f,4f,0),new float3(width*.68f,4f,width*.34f),3);
                    Box(p-new float3(width*.12f,3f,width*.04f),new float3(width*.70f,3f,width*.32f),2);
                }
            }
            else if(Mode==3)
            {
                for(int i=0;i<MenuIslandGeometry015.RainStreaks;i++)
                {
                    uint id=Seed+(uint)i*199u;float a=MenuIslandMath015.Unit(id)*(2f*math.PI);
                    // Keep the original central visual hub dry; preview rain is outdoor-only, no world physics query.
                    float radius=48f+MenuIslandMath015.Unit(id+7u)*45f;
                    float3 p=Center+new float3(math.cos(a)*radius,6f+MenuIslandMath015.Unit(id+17u)*32f,math.sin(a)*radius);
                    Box(p,new float3(.055f,.9f,.055f),1);
                }
            }
        }
        void Box(float3 center,float3 size,byte group)
        {
            rainStart=center.y-Center.y;
            float3 h=size*.5f;
            Face(center,new float3(-h.x,-h.y,-h.z),new float3(-h.x,-h.y,h.z),new float3(-h.x,h.y,h.z),new float3(-h.x,h.y,-h.z),new float3(-1,0,0),group);
            Face(center,new float3(h.x,-h.y,h.z),new float3(h.x,-h.y,-h.z),new float3(h.x,h.y,-h.z),new float3(h.x,h.y,h.z),new float3(1,0,0),group);
            Face(center,new float3(-h.x,-h.y,-h.z),new float3(h.x,-h.y,-h.z),new float3(h.x,-h.y,h.z),new float3(-h.x,-h.y,h.z),new float3(0,-1,0),group);
            Face(center,new float3(-h.x,h.y,h.z),new float3(h.x,h.y,h.z),new float3(h.x,h.y,-h.z),new float3(-h.x,h.y,-h.z),new float3(0,1,0),group);
            Face(center,new float3(h.x,-h.y,-h.z),new float3(-h.x,-h.y,-h.z),new float3(-h.x,h.y,-h.z),new float3(h.x,h.y,-h.z),new float3(0,0,-1),group);
            Face(center,new float3(-h.x,-h.y,h.z),new float3(h.x,-h.y,h.z),new float3(h.x,h.y,h.z),new float3(-h.x,h.y,h.z),new float3(0,0,1),group);
        }
        void Face(float3 center,float3 a,float3 b,float3 c,float3 d,float3 normal,byte group)
        {
            int start=Vertices.Length;Add(center+a,normal,new float2(0,0));Add(center+b,normal,new float2(1,0));
            Add(center+c,normal,new float2(1,1));Add(center+d,normal,new float2(0,1));
            Indices.Add(start);Indices.Add(start+1);Indices.Add(start+2);Indices.Add(start);Indices.Add(start+2);Indices.Add(start+3);Groups.Add(group);
        }
        void Add(float3 p,float3 n,float2 uv){Vertices.Add(new Vector3(p.x,p.y,p.z));Normals.Add(new Vector3(n.x,n.y,n.z));UVs.Add(new Vector2(Mode==3?rainStart:uv.x,uv.y));}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: map-local metres/Y-up, top underside0.025m below accepted catalog HubAnchor support; lower skyline below−69m, clouds outside185m ring.
// Array Formats:32×12×32 cells, existing greedy face output;64×2 building boxes,12×3 cloud boxes,96 narrow rain prisms. Four fixed owned meshes, bounded cold native scratch.
// Dependencies/Pointers: completed fill/mesher/scenery jobs precede managed cold upload; TempJob/native chunk disposed in using scopes, immutable uploaded meshes owned by lease. No textures/downloads/generation service, colliders, particles, runtime Instantiate or global weather state.
