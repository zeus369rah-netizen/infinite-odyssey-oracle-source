using System;
using OdysseyOracle.Foundation.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        // # ARCHITECTURE SNAPSHOT
        // Coordinate Space: actual current World publishes one global world-XZ cosmetic field; source-backed window cache uses supported unit/yaw refuge space.
        // Array Formats: one float4 uniform, double clock, three cached anchors/seven rooms and one optional36-quad existing dressing batch.
        // Dependencies/Pointers: existing World.Update/disable/destroy and resources own lifetime. No second updater, audio, actor physics, random state or accepted room-reaction mutation.
        static readonly int PassiveWindId015=Shader.PropertyToID("_OraclePassiveWind015");
        static DungeonWorld passiveWindOwner015;
        double passiveWindClock015;
        Vector4 passiveWindField015;
        uint passiveWindRevision015;
        readonly Vector3[] refugeWindAnchors015=new Vector3[3];
        readonly Rect[] refugeWindRooms015=new Rect[7];
        int refugeWindWindows015,refugeWindRoomCount015,refugeWindStatus015;
        Matrix4x4 refugeWindToLocal015,refugeWindToWorld015;
        float refugeWindCeiling015;
        bool passiveWindWasRefuge015;
        public Vector4 PassiveWindField015=>passiveWindField015;
        public uint PassiveWindRevision015=>passiveWindRevision015;
        bool RefugeWindContext015=>isActiveAndEnabled&&IsTutorialRefuge&&geometryRoot!=null&&geometryRoot.localToWorldMatrix==refugeWindToWorld015;
        public int RefugeWindowWindCount015=>RefugeWindContext015?refugeWindWindows015:0;
        public int RefugeWindRoomCount015=>RefugeWindContext015?refugeWindRoomCount015:0;
        public int RefugeWindowWindStatus015=>refugeWindStatus015;
        public bool TryGetRefugeWindowWindAnchor015(int index,out Vector3 worldAnchor)
        {worldAnchor=default;if(index<0||index>=RefugeWindowWindCount015)return false;worldAnchor=refugeWindAnchors015[index];return true;}
        public bool TryReadRefugeWindSpace015(out Matrix4x4 worldToLocal,out float ceilingY)
        {worldToLocal=Matrix4x4.identity;ceilingY=0;if(RefugeWindowWindCount015==0)return false;worldToLocal=refugeWindToLocal015;ceilingY=refugeWindCeiling015;return true;}
        public bool TryGetRefugeWindRoom015(int index,out Rect localXZ)
        {localXZ=default;if(index<0||index>=RefugeWindRoomCount015)return false;localXZ=refugeWindRooms015[index];return true;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPassiveWindSubsystem015(){passiveWindOwner015=null;Shader.SetGlobalVector(PassiveWindId015,Vector4.zero);}
        void OnEnable(){passiveWindClock015=0;unchecked{passiveWindRevision015++;}}
        void TickPassiveWind015()
        {
            var owner=GameSession.Instance;if(owner==null||owner.World!=this||!isActiveAndEnabled)return;
            if(passiveWindOwner015!=this){passiveWindOwner015=this;passiveWindClock015=0;unchecked{passiveWindRevision015++;}}
            bool context=RefugeWindContext015;if(context!=passiveWindWasRefuge015){passiveWindWasRefuge015=context;unchecked{passiveWindRevision015++;}}
            Vector4 policy=LaneVignetteRenderer.PassiveWindDescriptor015;
            bool motion=policy.w>.5f&&!OracleBookAccessibility.PrefersReducedMotion;
            bool held=!motion||!Application.isFocused||owner.HasStarted&&owner.Paused;
            passiveWindClock015=PassiveWindMath015.Advance(passiveWindClock015,Time.unscaledDeltaTime,held);
            float4 next=PassiveWindMath015.Sample((float)passiveWindClock015,new float2(policy.x,policy.y),policy.z,motion);
            Vector4 value=new Vector4(next.x,next.y,next.z,next.w);
            if(passiveWindField015!=value){passiveWindField015=value;Shader.SetGlobalVector(PassiveWindId015,value);}
        }
        void ResetPassiveWind015()
        {
            passiveWindClock015=0;passiveWindField015=Vector4.zero;unchecked{passiveWindRevision015++;}
            if(passiveWindOwner015!=this)return;passiveWindOwner015=null;Shader.SetGlobalVector(PassiveWindId015,Vector4.zero);
        }
        static float4x4 WindMatrix015(Matrix4x4 m)=>new float4x4(new float4(m.m00,m.m10,m.m20,m.m30),new float4(m.m01,m.m11,m.m21,m.m31),new float4(m.m02,m.m12,m.m22,m.m32),new float4(m.m03,m.m13,m.m23,m.m33));
        void BuildRefugeWindowWind015()
        {
            refugeWindWindows015=refugeWindRoomCount015=0;refugeWindStatus015=0;unchecked{passiveWindRevision015++;}
            if(!IsTutorialRefuge||tutorialRefuge.AlcoveCenters.Length!=3||tutorialRefuge.Rooms.Length!=7)return;
            // These three sources are the actual existing +X RoomV2 clear openings; rail gap3.5..4.65 and centre-Z gap between bars±.3.
            // The pinned geometry/layout source contract is checked by the work packet; no hole, collider or barred-window exemption changes.
            var matrix=geometryRoot.localToWorldMatrix;refugeWindToWorld015=matrix;refugeWindToLocal015=geometryRoot.worldToLocalMatrix;refugeWindCeiling015=tutorialRefuge.CeilingY;
            NativeArray<float3> anchors=default,worldAnchors=default;NativeArray<PassiveWindVertex32> vertices=default;NativeArray<ushort> indices=default;JobHandle fence=default;
            Mesh mesh=null;Material material=null;bool meshRegistered=false,materialRegistered=false;
            try
            {
                anchors=new NativeArray<float3>(3,Allocator.TempJob);worldAnchors=new NativeArray<float3>(3,Allocator.TempJob);
                vertices=new NativeArray<PassiveWindVertex32>(144,Allocator.TempJob);indices=new NativeArray<ushort>(216,Allocator.TempJob);
                for(int i=0;i<3;i++)anchors[i]=new float3(tutorialRefuge.OuterX-.24f,4.075f,tutorialRefuge.AlcoveCenters[i]);
                fence=new PassiveWindWindowMesh015{Anchors=anchors,WorldAnchors=worldAnchors,Vertices=vertices,Indices=indices,LocalToWorld=WindMatrix015(matrix)}.Schedule();fence.Complete();
                for(int i=0;i<3;i++){float3 p=worldAnchors[i];refugeWindAnchors015[i]=new Vector3(p.x,p.y,p.z);}
                for(int i=0;i<7;i++){var r=tutorialRefuge.Rooms[i];refugeWindRooms015[i]=new Rect(r.MinU,r.MinV,r.MaxU-r.MinU,r.MaxV-r.MinV);}
                refugeWindWindows015=3;refugeWindRoomCount015=7;refugeWindStatus015=1;
                Shader shader=Resources.Load<Shader>("Shaders/OracleWindowBreeze015");if(shader==null||!shader.isSupported){refugeWindStatus015=2;return;}
                mesh=new Mesh{name="Original three real window breeze trails015",indexFormat=IndexFormat.UInt16};
                mesh.SetVertexBufferParams(144,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,3));
                mesh.SetVertexBufferData(vertices,0,0,144);mesh.SetIndexBufferParams(216,IndexFormat.UInt16);mesh.SetIndexBufferData(indices,0,0,216);mesh.subMeshCount=1;mesh.SetSubMesh(0,new SubMeshDescriptor(0,216));
                // Includes the full signed2m path, all fixed grille-gap seeds, wave and camera-facing streak extent. Never recomputed per frame.
                float minZ=tutorialRefuge.AlcoveCenters[0]-1.05f,maxZ=tutorialRefuge.AlcoveCenters[2]+1.05f;
                mesh.bounds=new Bounds(new Vector3(tutorialRefuge.OuterX-.85f,4.075f,(minZ+maxZ)*.5f),new Vector3(2.8f,.7f,maxZ-minZ));mesh.UploadMeshData(true);
                material=new Material(shader){name="Original shared restrained window breeze015"};resources.Add(material);materialRegistered=true;
                var renderer=AttachRefugeDressingBatch("Batched breeze through three existing windows015",mesh,material);meshRegistered=true;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;refugeWindStatus015=3;
            }
            catch{refugeWindStatus015=4;throw;}
            finally
            {
                fence.Complete();if(indices.IsCreated)indices.Dispose();if(vertices.IsCreated)vertices.Dispose();if(worldAnchors.IsCreated)worldAnchors.Dispose();if(anchors.IsCreated)anchors.Dispose();
                if(!meshRegistered&&mesh!=null)Destroy(mesh);if(!materialRegistered&&material!=null)Destroy(material);
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: current World publishes world-XZ velocity/unscaled held time/gust; three source-backed local +X windows expose immutable world anchors and actual seven-room local union.
// Array Formats: one float4 shader write at most/frame; cached3Vector3/7Rect/matrix data, cold TempJob3+3 anchors/144Vertex32/216ushort, one existing batch renderer/material/mesh.
// Dependencies/Pointers: clock/scene/reset ownership is World only. No hot allocation/spatial query/audio/persistence/light/physics or accepted Wind snuff changes. Source shader/layout and numeric/culling checks precede root native/browser acceptance; unsupported shader keeps real audio anchors and explicitly omits optional trails.
