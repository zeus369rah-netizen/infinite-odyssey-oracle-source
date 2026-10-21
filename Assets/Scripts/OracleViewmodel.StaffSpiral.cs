using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace OdysseyOracle
{
    public sealed partial class OracleViewmodel
    {
        const int StaffSteps=16,StaffStrands=4;
        static readonly Vector3 StaffBase=new Vector3(0,-.332f,0),StaffLens=new Vector3(0,.450f,0);
        static readonly int SpiralAge=Shader.PropertyToID("_PulseAge"),SpiralDuration=Shader.PropertyToID("_PulseDuration"),SpiralMask=Shader.PropertyToID("_ElementMask"),SpiralStrength=Shader.PropertyToID("_Strength");
        static readonly int SpiralFire=Shader.PropertyToID("_FireColor"),SpiralWater=Shader.PropertyToID("_WaterColor"),SpiralEarth=Shader.PropertyToID("_EarthColor"),SpiralAir=Shader.PropertyToID("_AirColor");
        static readonly int EngulfAge020=Shader.PropertyToID("_EngulfAge"),EngulfPhase020=Shader.PropertyToID("_EngulfPhase"),EngulfMask020=Shader.PropertyToID("_EngulfMask"),EngulfStrength020=Shader.PropertyToID("_EngulfStrength");
        StaffSpiralState staffSpiralState;StaffAttackSwayState staffAttackSway;
        StaffEngulfState020 staffEngulf020;
        int staffSurfaceVertices020;
        public float DisplayedAttackSwayRate=>staffAttackSway.Rate;
        Mesh staffSpiralMesh;MeshRenderer staffSpiralRenderer;MaterialPropertyBlock staffSpiralBlock;
        public bool StaffSpiralShaderSupported=>staffSpiralRenderer!=null;
        public int DisplayedStaffPulseMask=>staffSpiralState.Active?staffSpiralState.PulseMask:0;
        public bool DisplayedStaffPulseAccepted=>staffSpiralState.AcceptedActive;
        public int DisplayedStaffEngulfMask020=>staffEngulf020.Active?staffEngulf020.Mask:0;
        public float DisplayedStaffEngulfStrength020=>staffEngulf020.Strength;
        public int StaffSurfaceVertexCount020=>staffSurfaceVertices020;
        void BuildStaffSpiral(Mesh bodyMesh,int staffVertexCount)
        {
            staffEngulf020.Clear();staffSurfaceVertices020=0;
            Shader shader=Resources.Load<Shader>("Shaders/OraclePlayerStaffSpiral");if(shader==null||!shader.isSupported)return;
            if(bodyMesh==null||staffVertexCount<=0||staffVertexCount>bodyMesh.vertexCount||staffVertexCount>100000)throw new System.InvalidOperationException("Finite authored staff prefix required.");
            Vector3[] source=bodyMesh.vertices,normals=bodyMesh.normals;
            if(normals.Length!=source.Length)throw new System.InvalidOperationException("Original staff normals required.");
            float low=float.PositiveInfinity,high=float.NegativeInfinity;
            for(int i=0;i<staffVertexCount;i++){low=Mathf.Min(low,source[i].y);high=Mathf.Max(high,source[i].y);}
            if(!float.IsFinite(low)||!float.IsFinite(high)||high-low<.1f)throw new System.InvalidOperationException("Finite original staff extent required.");
            var paint=new Material(shader){name="Owned original player staff rising helix"};owned.Add(paint);
            const int verticesPerStrand=(StaffSteps+1)*2;
            int surfaceStart=StaffStrands*verticesPerStrand;
            var vertices=new Vector3[surfaceStart+staffVertexCount];var uv=new List<Vector4>(vertices.Length);var indices=new List<int>(StaffStrands*StaffSteps*6+staffVertexCount*3);
            for(int strand=0;strand<StaffStrands;strand++)
            {
                for(int step=0;step<=StaffSteps;step++)
                {
                    float u=step/(float)StaffSteps;
                    for(int side=0;side<2;side++){int index=strand*verticesPerStrand+step*2+side;vertices[index]=Vector3.Lerp(StaffBase,StaffLens,u);uv.Add(new Vector4(u,side==0?-1f:1f,strand,0));}
                }
                for(int step=0;step<StaffSteps;step++)
                {int v=strand*verticesPerStrand+step*2;indices.Add(v);indices.Add(v+2);indices.Add(v+1);indices.Add(v+1);indices.Add(v+2);indices.Add(v+3);}
            }
            Bounds bounds=new Bounds((StaffBase+StaffLens)*.5f,new Vector3(.16f,.81f,.16f));
            for(int i=0;i<staffVertexCount;i++)
            {
                Vector3 p=source[i]+normals[i]*.00035f;
                if(!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z))throw new System.InvalidOperationException("Nonfinite staff surface rejected.");
                vertices[surfaceStart+i]=p;uv.Add(new Vector4(Mathf.Clamp01((source[i].y-low)/(high-low)),0,0,1));bounds.Encapsulate(p);
            }
            for(int slot=0;slot<bodyMesh.subMeshCount;slot++)
            {
                int[] original=bodyMesh.GetTriangles(slot);
                for(int i=0;i<original.Length;i+=3)
                {
                    int a=original[i],b=original[i+1],c=original[i+2];bool staff=a<staffVertexCount;
                    if(staff!=(b<staffVertexCount)||staff!=(c<staffVertexCount))throw new System.InvalidOperationException("Staff and hand must not share triangles.");
                    if(staff){indices.Add(surfaceStart+a);indices.Add(surfaceStart+b);indices.Add(surfaceStart+c);}
                }
            }
            staffSurfaceVertices020=staffVertexCount;
            staffSpiralMesh=new Mesh{name="Original player staff helix and staff-only surface020"};owned.Add(staffSpiralMesh);
            if(vertices.Length>65535)staffSpiralMesh.indexFormat=IndexFormat.UInt32;
            staffSpiralMesh.vertices=vertices;staffSpiralMesh.SetUVs(0,uv);staffSpiralMesh.SetTriangles(indices,0);
            staffSpiralMesh.bounds=bounds;staffSpiralMesh.UploadMeshData(true);
            var node=new GameObject("Bounded original player staff rising spiral");node.transform.SetParent(wandRoot,false);node.AddComponent<MeshFilter>().sharedMesh=staffSpiralMesh;
            staffSpiralRenderer=node.AddComponent<MeshRenderer>();ConfigureRenderer(staffSpiralRenderer,new[]{paint});
            staffSpiralBlock=new MaterialPropertyBlock();staffSpiralBlock.SetColor(SpiralFire,ElementAccents[0]);staffSpiralBlock.SetColor(SpiralWater,ElementAccents[1]);staffSpiralBlock.SetColor(SpiralEarth,ElementAccents[2]);staffSpiralBlock.SetColor(SpiralAir,ElementAccents[3]);
            staffSpiralState.Clear();staffAttackSway.Clear();SyncStaffSpiral();
        }
        void NotifyStaffSelection(int mask)
        {
            staffEngulf020.Select(mask);bool pulse=staffSpiralState.Select(mask);SyncStaffSpiral();
            if(!pulse)return;
            var session=GameSession.Instance;if(staffSpiralRenderer!=null&&session!=null)session.Audio?.PlayStaffSpiral(false);
        }
        void PulseAcceptedStaff(int mask)
        {
            if(!staffSpiralState.Launch(mask))return;
            staffEngulf020.Launch(mask);
            SyncStaffSpiral();
            var session=GameSession.Instance;if(staffSpiralRenderer!=null&&session!=null)session.Audio?.PlayStaffSpiral(true);
        }
        void AdvanceStaffSpiral(float dt){staffAttackSway.Step(dt);if(!staffSpiralState.Active&&!staffEngulf020.Active)return;staffSpiralState.Step(dt);staffEngulf020.Step(dt);SyncStaffSpiral();}
        void SyncStaffSpiral()
        {
            if(staffSpiralRenderer==null||staffSpiralBlock==null)return;
            staffSpiralBlock.SetFloat(SpiralAge,staffSpiralState.Age);staffSpiralBlock.SetFloat(SpiralDuration,staffSpiralState.Duration);
            staffSpiralBlock.SetFloat(SpiralMask,staffSpiralState.PulseMask);staffSpiralBlock.SetFloat(SpiralStrength,staffSpiralState.Strength);
            staffSpiralBlock.SetFloat(EngulfAge020,staffEngulf020.Age);staffSpiralBlock.SetFloat(EngulfPhase020,staffEngulf020.Phase);
            staffSpiralBlock.SetFloat(EngulfMask020,staffEngulf020.Mask);staffSpiralBlock.SetFloat(EngulfStrength020,staffEngulf020.Strength);
            staffSpiralRenderer.SetPropertyBlock(staffSpiralBlock);
        }
        void ResetStaffSpiral(){staffAttackSway.Clear();if(staffSpiralState.PulseMask==0&&staffSpiralState.SelectionMask==0&&staffEngulf020.Mask==0&&staffEngulf020.SelectionMask==0)return;staffSpiralState.Clear();staffEngulf020.Clear();SyncStaffSpiral();}
        Color StaffFacetAccent(int mask,Color fallback)
        {
            if(mask==15){int phase=(int)Mathf.Repeat(clock*3.5f,4f);return ElementAccents[phase];}
            if(mask!=0&&(mask&(mask-1))!=0)
            {int dominant=staffSpiralState.AcceptedActive?lastSpell:selectedElement;for(int i=0;i<4;i++)if(i!=dominant&&(mask&(1<<i))!=0)return ElementAccents[i];}
            return Color.Lerp(fallback,new Color(.025f,.035f,.025f),.60f);
        }
        void OnDisable(){ResetStaffSpiral();ResetNativeHands015();}
        void ReleaseStaffSpiralReferences(){staffSpiralState.Clear();staffAttackSway.Clear();staffEngulf020.Clear();staffSurfaceVertices020=0;staffSpiralMesh=null;staffSpiralRenderer=null;staffSpiralBlock=null;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original wand-local base(0,-.332,0) to exact existing lens(0,.450,0), under unchanged camera-rig wall/near-plane clearance.
// Array Formats: one cold136vertex/384index static owned mesh and material; four constant colors, scalar age/mask/strength in one reusable MPB; no hot vertex uploads/instantiation/colliders/lights.
// Dependencies/Pointers: same existing viewmodel ownership/disposal list; selection is quiet, accepted launch stronger and mask-snapshotted; pause freezes private age, hidden/reset/disable clears it. No Raykon/World/HP/host/cadence/protocol mutation or native acceptance claim.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Oracle accepted world roots and camera/child-local poses only; no authority geometry changes.
// Array Formats: Fixed31shared body slots/2prewarmed player graphs and original palette/pose/feedback buffers; no wire ABI growth.
// Dependencies/Pointers: Player/Rooms/Combat remain sole owners; root integrates default-OFF original native player pilot and verifies real bindings/peer/clearance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original staff-only BuildWand prefix plus0.00035m normal offset; normalized height spans every copied shaft/frame/lens vertex and bounds enclose both surface and helix.
// Array Formats: existing one effect renderer/material/MPB, one cold mesh extension, four extra scalar uploads and one bounded value-state; no new GameObject/renderer/light/audio source or active-frame mesh allocation/upload.
// Dependencies/Pointers: original spiral/audio admissions and accepted spring unchanged. Prefix guards exclude all hand/cuff triangles; original body palettes/geometry and proper grip/pickup remain. Root verifies imported shader and actual pixels.
