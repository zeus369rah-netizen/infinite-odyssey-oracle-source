using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    internal sealed class EnemyStatusRenderer009:IDisposable
    {
        NativeArray<EnemyStatusInput009> inputs;
        NativeArray<uint> generations;
        NativeArray<float4x4> matrices;
        NativeArray<int> counts;
        NativeArray<EnemyStatusBounds009> bounds;
        readonly Mesh[] meshes=new Mesh[9];
        Material material;
        ulong occupied;
        JobHandle fence;
        bool ready,disposed;
        internal bool Ready=>ready&&!disposed;
        internal int LastSubmittedGlyphs{get;private set;}
        internal int LastSubmittedBatches{get;private set;}
        internal EnemyStatusRenderer009()
        {
            try
            {
                Shader shader=Resources.Load<Shader>("Shaders/OracleEnemyStatusIcons009");
                material=Resources.Load<Material>("Materials/EnemyStatusIcons009");
                if(shader==null||!shader.isSupported||material==null||material.shader!=shader||!material.enableInstancing||material.GetColor("_Tint")!=Color.white)return;
                if(UnsafeUtility.SizeOf<float4x4>()!=64||UnsafeUtility.SizeOf<Matrix4x4>()!=64||UnsafeUtility.SizeOf<EnemyStatusInput009>()!=32)
                    throw new InvalidOperationException("Status matrix/input layout differs from the reviewed contract.");
                inputs=new NativeArray<EnemyStatusInput009>(48,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                generations=new NativeArray<uint>(48,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                matrices=new NativeArray<float4x4>(9*48,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
                counts=new NativeArray<int>(9,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                bounds=new NativeArray<EnemyStatusBounds009>(9,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                for(int k=0;k<9;k++)
                {
                    meshes[k]=BuildGlyph(k);
                }
                ready=true;
            }
            catch{Dispose();throw;}
        }
        internal EnemyStatusHandle009 Register()
        {
            if(!Ready)return default;
            ulong available=(~occupied)&((1UL<<48)-1);
            if(available==0)return default;
            int slot=math.tzcnt(available);uint generation=generations[slot]+1;
            if(generation==0)return default;
            generations[slot]=generation;occupied|=1UL<<slot;inputs[slot]=default;
            return new EnemyStatusHandle009{Index=slot,Generation=generation};
        }
        bool Owns(EnemyStatusHandle009 h)=>Ready&&h.Generation!=0&&(uint)h.Index<48&&(occupied&(1UL<<h.Index))!=0&&generations[h.Index]==h.Generation;
        internal void Publish(EnemyStatusHandle009 h,Vector3 anchor,uint status,int theme,bool active)
        {
            if(!Owns(h))return;
            inputs[h.Index]=new EnemyStatusInput009{Anchor=new float3(anchor.x,anchor.y,anchor.z),LiveStatus=status,Theme=theme,Generation=h.Generation,Active=active?1u:0u};
        }
        internal void Release(EnemyStatusHandle009 h)
        {if(!Owns(h))return;inputs[h.Index]=default;occupied&=~(1UL<<h.Index);}
        internal void Draw(Camera camera)
        {
            LastSubmittedGlyphs=LastSubmittedBatches=0;
            if(!Ready||camera==null||!SystemInfo.supportsInstancing||camera.orthographic)return;
            Transform eye=camera.transform;Vector3 p=eye.position,r=eye.right,u=eye.up,f=eye.forward;
            fence=new EnemyStatusLayoutJob009{Inputs=inputs,Eye=new float3(p.x,p.y,p.z),Right=new float3(r.x,r.y,r.z),Up=new float3(u.x,u.y,u.z),Forward=new float3(f.x,f.y,f.z),
                TanHalfFov=math.tan(math.radians(camera.fieldOfView*.5f)),PixelHeight=camera.pixelHeight,Near=camera.nearClipPlane,Far=camera.farClipPlane,
                Matrices=matrices,Counts=counts,Bounds=bounds}.Schedule();fence.Complete();
            LastSubmittedGlyphs=counts[0]+counts[1]+counts[2]+counts[3]+counts[4]+counts[5]+counts[6]+counts[7]+counts[8];
            DrawKind(0,camera);DrawKind(1,camera);DrawKind(2,camera);DrawKind(3,camera);DrawKind(4,camera);
            DrawKind(5,camera);DrawKind(6,camera);DrawKind(7,camera);DrawKind(8,camera);
        }
        void DrawKind(int kind,Camera camera)
        {
            int count=counts[kind];if(count==0)return;
            EnemyStatusBounds009 b=bounds[kind];float3 c=(b.Min+b.Max)*.5f,s=b.Max-b.Min;
            var rp=new RenderParams(material){camera=camera,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false,
                worldBounds=new UnityEngine.Bounds(new Vector3(c.x,c.y,c.z),new Vector3(s.x,s.y,s.z))};
            Graphics.RenderMeshInstanced(rp,meshes[kind],0,matrices.Reinterpret<Matrix4x4>(),count,kind*48);
            LastSubmittedBatches++;
        }
        public void Dispose()
        {
            if(disposed)return;fence.Complete();disposed=true;ready=false;occupied=0;
            if(inputs.IsCreated)inputs.Dispose();if(generations.IsCreated)generations.Dispose();if(matrices.IsCreated)matrices.Dispose();
            if(counts.IsCreated)counts.Dispose();if(bounds.IsCreated)bounds.Dispose();
            for(int i=0;i<9;i++){DestroyOwned(meshes[i]);meshes[i]=null;}
            material=null;
        }
        static void DestroyOwned(UnityEngine.Object value)
        {if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        static Color Tint(int kind)
        {
            switch(kind){case 0:return new Color(1,.35f,.10f);case 1:return new Color(.15f,.75f,1);case 2:return new Color(.65f,.90f,.28f);case 3:return new Color(.90f,.86f,1);
                case 4:return new Color(1,.42f,.06f);case 5:return new Color(.12f,.85f,1);case 6:return new Color(1,.91f,.12f);case 7:return new Color(1,.68f,.18f);default:return new Color(.85f,.47f,1);}
        }
        static readonly string[][] Glyphs={
            new[]{"0001000","0011000","0010100","0110110","0101110","0111110","0011100"},
            new[]{"0001000","0011100","0011100","0111110","1111111","0111110","0011100"},
            new[]{"0011100","0111110","1111111","1101011","1111111","0111110","0001000"},
            new[]{"0000000","0011110","1100011","0111100","0000011","1111110","0000000"},
            new[]{"1010001","0011000","0010100","0110110","0101110","0111110","0011100"},
            new[]{"0001000","0011100","0101010","0001000","0011100","0001000","0001000"},
            new[]{"1001001","0101010","0011100","1111111","0011100","0101010","1001001"},
            new[]{"1110111","1100011","1101011","1101011","0101010","0010100","0001000"},
            new[]{"1100011","0110110","0011100","0001000","0010100","0100010","1000001"}};
        static string[] Letter(char ch)
        {
            switch(ch){case '+':return new[]{"010","010","111","010","010"};case '-':return new[]{"000","000","111","000","000"};case '2':return new[]{"111","001","111","100","111"};
                case '4':return new[]{"101","101","111","001","001"};case '5':return new[]{"111","100","111","001","111"};default:return new[]{"101","001","010","100","101"};}
        }
        static Mesh BuildGlyph(int kind)
        {
            var vertices=new List<Vector3>(600);var colors=new List<Color>(600);var indices=new List<int>(900);
            float width=EnemyStatusMath009.Width(kind);Quad(vertices,colors,indices,0,0,width,1,new Color(.045f,.045f,.045f,1));
            Quad(vertices,colors,indices,0,.47f,width,.035f,Color.white);Quad(vertices,colors,indices,0,-.47f,width,.035f,Color.white);
            Quad(vertices,colors,indices,-width*.5f+.02f,0,.035f,.94f,Color.white);Quad(vertices,colors,indices,width*.5f-.02f,0,.035f,.94f,Color.white);
            float x=kind>=7?-.9f:0;
            Pattern(vertices,colors,indices,Glyphs[kind],x,0,.10f);
            if(kind>=7)
            {
                string text=kind==7?"+25%":"-45%";
                for(int letter=0;letter<4;letter++)Pattern(vertices,colors,indices,Letter(text[letter]),-.12f+letter*.30f,0,.075f);
            }
            Color tint=Tint(kind);
            for(int i=0;i<colors.Count;i++)colors[i]=colors[i]*tint;
            Mesh mesh=null;
            try{mesh=new Mesh{name="Original live status009 glyph "+kind};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;}
            catch{DestroyOwned(mesh);throw;}
        }
        static void Pattern(List<Vector3> v,List<Color> colors,List<int> indices,string[] rows,float cx,float cy,float unit)
        {for(int y=0;y<rows.Length;y++)for(int x=0;x<rows[y].Length;x++)if(rows[y][x]=='1')Quad(v,colors,indices,cx+(x-(rows[y].Length-1)*.5f)*unit,cy+((rows.Length-1)*.5f-y)*unit,unit*.86f,unit*.86f,Color.white);}
        static void Quad(List<Vector3> v,List<Color> colors,List<int> indices,float cx,float cy,float w,float h,Color color)
        {
            int n=v.Count;float z=color.r<.1f?-.002f:0;
            v.Add(new Vector3(cx-w*.5f,cy-h*.5f,z));v.Add(new Vector3(cx-w*.5f,cy+h*.5f,z));v.Add(new Vector3(cx+w*.5f,cy+h*.5f,z));v.Add(new Vector3(cx+w*.5f,cy-h*.5f,z));
            colors.Add(color);colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);
        }
    }

    public sealed partial class OracleCombat
    {
        EnemyStatusRenderer009 statusRenderer009;
        public bool EnemyStatusIconsReady009=>statusRenderer009!=null&&statusRenderer009.Ready;
        public int EnemyStatusGlyphsSubmitted009=>statusRenderer009!=null?statusRenderer009.LastSubmittedGlyphs:0;
        public int EnemyStatusBatchesSubmitted009=>statusRenderer009!=null?statusRenderer009.LastSubmittedBatches:0;
        void InitializeEnemyStatus009(){if(statusRenderer009==null)statusRenderer009=new EnemyStatusRenderer009();}
        internal EnemyStatusHandle009 RegisterEnemyStatus009()=>statusRenderer009!=null?statusRenderer009.Register():default;
        internal void PublishEnemyStatus009(EnemyStatusHandle009 h,Vector3 anchor,uint status,int theme,bool active)
        {statusRenderer009?.Publish(h,anchor,status,theme,active);}
        internal void ReleaseEnemyStatus009(EnemyStatusHandle009 h){statusRenderer009?.Release(h);}
        void LateUpdate(){if(initialized&&player!=null)statusRenderer009?.Draw(player.ViewCamera);}
        void DisposeEnemyStatus009(){statusRenderer009?.Dispose();statusRenderer009=null;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World-metre actual healthbar anchors, camera billboard right/up and finite distance/depth pixel sizing; no targeting/damage/Physics.
// Array Formats:48x32Binput/generations,9x48x64B column-major matrices,9counts/24Bbounds; max288live glyphs,9cold original shared meshes and one borrowed saved material.
// Dependencies/Pointers: Renderer owns native memory/fence; pure Burst loop then NativeArray Matrix4x4 reinterpret for explicit supported-instancing adapters; no per-frame heap or per-enemy Canvas, native/Web/render acceptance pending.
