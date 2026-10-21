using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using OdysseyOracle.Foundation.World;

namespace OdysseyOracle
{
    // Shared instancing path deliberately works on WebGL without compute/structured buffers.
    sealed class LaneBuildHighlightBatch015:IDisposable
    {
        readonly Mesh mesh;readonly Material material;
        readonly MaterialPropertyBlock grid,blocked,good,bad;
        readonly Matrix4x4[] matrices=new Matrix4x4[LaneBuildMath015.HighlightCapacity],batch=new Matrix4x4[1023];
        NativeArray<Matrix4x4> nativeMatrices;NativeArray<int> nativeCounts;NativeArray<byte> cellFlags;JobHandle reader;int greenCount,redCount;
        public LaneBuildHighlightBatch015()
        {
            var shader=Resources.Load<Shader>("Shaders/OracleLaneBuild015");if(shader==null||!shader.isSupported||!SystemInfo.supportsInstancing)return;
            try
            {
            material=new Material(shader){name="Original lane building indicators",enableInstancing=true};
            grid=new MaterialPropertyBlock();blocked=new MaterialPropertyBlock();good=new MaterialPropertyBlock();bad=new MaterialPropertyBlock();
            grid.SetFloat("_Mode",0);grid.SetColor("_Tint",new Color(.25f,.85f,.52f,.48f));
            blocked.SetFloat("_Mode",0);blocked.SetColor("_Tint",new Color(1f,.18f,.12f,.48f));
            good.SetFloat("_Mode",1);good.SetColor("_Tint",new Color(.12f,1f,.28f,.82f));
            bad.SetFloat("_Mode",2);bad.SetColor("_Tint",new Color(1f,.18f,.12f,.95f));
            mesh=new Mesh{name="Original filled and outlined lane floor block"};
            mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
            mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();mesh.UploadMeshData(true);
            nativeMatrices=new NativeArray<Matrix4x4>(LaneBuildMath015.HighlightCapacity,Allocator.Persistent);nativeCounts=new NativeArray<int>(2,Allocator.Persistent);cellFlags=new NativeArray<byte>(LaneBuildMath015.TerrainCount,Allocator.Persistent);
            }
            catch{Dispose();throw;}
        }
        public bool Ready=>material!=null&&mesh!=null&&nativeMatrices.IsCreated&&nativeCounts.IsCreated&&cellFlags.IsCreated;
        public void Publish(NativeArray<LaneBuildPoint16> points,int length,float floorY)
        {
            if(!Ready)return;reader.Complete();Clear();
            if(!points.IsCreated||length<0||length>nativeMatrices.Length||length>points.Length||!math.isfinite(floorY))return;
            reader=new LaneHighlightMatrices015{Points=points,Matrices=nativeMatrices,Counts=nativeCounts,CellFlags=cellFlags,Count=length,FloorY=floorY}.Schedule();reader.Complete();
            greenCount=nativeCounts[0];redCount=nativeCounts[1];
            NativeArray<Matrix4x4>.Copy(nativeMatrices,0,matrices,0,greenCount);
            NativeArray<Matrix4x4>.Copy(nativeMatrices,length-redCount,matrices,greenCount,redCount);
        }
        public void Clear(){greenCount=redCount=0;}
        void DrawRange(int first,int count,MaterialPropertyBlock state)
        {
            for(int used=0;used<count;used+=1023)
            {int n=Math.Min(1023,count-used);Array.Copy(matrices,first+used,batch,0,n);Graphics.DrawMeshInstanced(mesh,0,material,batch,n,state,ShadowCastingMode.Off,false,2,null,LightProbeUsage.Off);}
        }
        public void Draw(bool showGrid,bool showMarker,Vector3 point,bool valid,float diameter,bool budget)
        {
            if(!Ready)return;
            if(showGrid){DrawRange(0,greenCount,budget?grid:blocked);DrawRange(greenCount,redCount,blocked);}
            if(showMarker&&math.isfinite(diameter)&&diameter>0&&diameter<=8)
                Graphics.DrawMesh(mesh,Matrix4x4.TRS(point+Vector3.up*LaneBuildPreviewRules020.DrawLift,Quaternion.identity,new Vector3(diameter,1,diameter)),material,2,null,0,valid?good:bad,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
        }
        public void Dispose()
        {reader.Complete();if(nativeMatrices.IsCreated)nativeMatrices.Dispose();if(nativeCounts.IsCreated)nativeCounts.Dispose();if(cellFlags.IsCreated)cellFlags.Dispose();if(mesh!=null)Release(mesh);if(material!=null)Release(material);Clear();}
        static void Release(UnityEngine.Object obj){if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
    }
    [BurstCompile]
    struct LaneHighlightMatrices015:IJob
    {
        [ReadOnly]public NativeArray<LaneBuildPoint16> Points;
        public NativeArray<Matrix4x4> Matrices;public NativeArray<int> Counts;public NativeArray<byte> CellFlags;public int Count;public float FloorY;
        public void Execute()
        {
            int good=0,bad=0;
            for(int i=0;i<CellFlags.Length;i++)CellFlags[i]=0;
            for(int i=0;i<Count;i++)
            {
                var point=Points[i];if(!LaneBuildPreviewRules020.TryDisplayCell(point.Position,out uint key))continue;
                CellFlags[(int)key]|=(byte)(point.Reason==0?1:2);
            }
            // A green block contains at least one existing legal canonical root; the full target stays exact.
            for(uint key=0;key<LaneBuildMath015.TerrainCount;key++)
            {
                byte flags=CellFlags[(int)key];if(flags==0)continue;
                int2 cell=LaneBuildMath015.Decode(key,8)-128;int at=(flags&1)!=0?good++:Count-1-bad++;
                Matrices[at]=new Matrix4x4(new Vector4(.98f,0,0,0),new Vector4(0,1,0,0),new Vector4(0,0,.98f,0),new Vector4(cell.x,FloorY+LaneBuildPreviewRules020.DrawLift,cell.y,1));
            }
            Counts[0]=good;Counts[1]=bad;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World support plane plus .016/.022m cosmetic separation only; tower physical roots remain exactly supportedY0.
// Array Formats: Fixed8192 native/managed64B matrices plus one1023 draw-copy batch; one borrowed-output quad and one owned instanced material, at most9grid draws+one marker.
// Dependencies/Pointers: Builder owns lifecycle and completes numeric matrix job before copy/reuse/disposal. Unity draw API copies submission data; no custom mutable GPU buffer/readback, per-cell objects, lights or compute path. Native allocation/performance acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted metre/Y-up/XZ support roots and HUD Canvas; cosmetic floor boxes do not establish build permission.
// Array Formats: Existing fixed8192 preview records, one ray job, four cached original tower choices, unchanged Frame64 pending/replay transaction.
// Dependencies/Pointers: Local picker owns no pause/cursor/economy; accepted host result alone closes successful transaction. Normal input/native/Web rendering remain separate acceptance gates.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World-supplied foundation plane+.012m, integer-centred.98m terrain-cell grid and full conservative envelope square.
// Array Formats: Existing8192 native/managed64B matrices/1023 draw-copy batch plus65536 native flag bytes/two ints and one cold red-grid property block; at most10 grid draws+one footprint.
// Dependencies/Pointers: Existing quad/material; no per-tile Objects, hot mesh creation, compute or shader change. Finite O(65536+8192) pack aggregates existing World root results; no new spatial/build predicate. Completed fence precedes copy/reuse/disposal; native/input/render acceptance remains root-owned.
