using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeNpcRuntimeOwner
    {
        sealed class GuardTapBinding009
        {
            public Transform Arm,Weapon;public Quaternion ArmRest;public float TipRootY,FloorY;public int TipVertex;
            public Mesh OwnedSpearCap;public uint Serial;
        }
        GuardTapBinding009[] guardTapBindings009;
        GuardFloorTapLease009 guardTap009;
        void InitializeGuardTap009(){guardTapBindings009=new GuardTapBinding009[2];guardTap009=new GuardFloorTapLease009();}
        void DisposeGuardTap009()
        {
            guardTap009?.Dispose();guardTap009=null;
            if(guardTapBindings009!=null){ReleaseGuardSpearCap009(guardTapBindings009[0]);ReleaseGuardSpearCap009(guardTapBindings009[1]);}
            guardTapBindings009=null;
        }
        static void ReleaseGuardSpearCap009(GuardTapBinding009 binding)
        {if(binding==null||binding.OwnedSpearCap==null)return;if(Application.isPlaying)Object.Destroy(binding.OwnedSpearCap);else Object.DestroyImmediate(binding.OwnedSpearCap);binding.OwnedSpearCap=null;}
        void InitializeGuardTapActor009(int role,Actor actor)
        {
            if(role<2||role>3||actor==null||guardTapBindings009==null)return;
            Transform root=actor.RootBasis,weapon=root.Find(RefugeNpcAssetCatalog.PartPath(role,7));
            if(weapon==null||(root.lossyScale-Vector3.one).sqrMagnitude>1e-8f||Vector3.Dot(root.up,Vector3.up)<.99999f)return;
            var filter=weapon.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null||!filter.sharedMesh.isReadable)return;
            Matrix4x4 m=root.worldToLocalMatrix*weapon.localToWorldMatrix;
            var matrix=new float4x4(new float4(m.m00,m.m10,m.m20,m.m30),new float4(m.m01,m.m11,m.m21,m.m31),new float4(m.m02,m.m12,m.m22,m.m32),new float4(m.m03,m.m13,m.m23,m.m33));
            GuardToolSupport009 support;
            using(var vertices=new NativeArray<Vector3>(filter.sharedMesh.vertices,Allocator.TempJob))
            using(var result=new NativeArray<GuardToolSupport009>(1,Allocator.TempJob)){
                new GuardToolSupportJob009{Vertices=vertices.Reinterpret<float3>(12),ToolToRoot=matrix,Result=result}.Schedule().Complete();support=result[0];}
            if(support.Valid!=1||!Finite(support.RootY)||Mathf.Abs(support.RootY)>.12f)return;
            var binding=new GuardTapBinding009{Arm=role==2?actor.Right:actor.Left,Weapon=weapon,
                ArmRest=role==2?actor.RightRest:actor.LeftRest,TipRootY=support.RootY,TipVertex=support.Vertex,FloorY=actor.Feet.position.y};
            guardTapBindings009[role-2]=binding;
            if(role==2){
                GameObject cap=null;
                try{binding.OwnedSpearCap=OriginalRuneSpearhead009.Build();
                    cap=new GameObject("Original rune-guard spear point009",typeof(MeshFilter),typeof(MeshRenderer));cap.transform.SetParent(weapon,false);
                    cap.GetComponent<MeshFilter>().sharedMesh=binding.OwnedSpearCap;cap.GetComponent<MeshRenderer>().sharedMaterial=catalog.SharedPaint;}
                catch(System.Exception){if(cap!=null){if(Application.isPlaying)Object.Destroy(cap);else Object.DestroyImmediate(cap);}ReleaseGuardSpearCap009(binding);Status="Ilyra's optional spear point could not be prepared; her original staff/lore are retained.";}
            }
        }
        public bool TryGetGuardTapSupport009(int role,out Transform weapon,out float floorY,out int vertex)
        {
            weapon=null;floorY=0;vertex=-1;if(role<2||role>3||guardTapBindings009==null)return false;
            var b=guardTapBindings009[role-2];if(b==null||b.Weapon==null||role==2&&b.OwnedSpearCap==null)return false;
            weapon=b.Weapon;floorY=b.FloorY;vertex=b.TipVertex;return true;
        }
        void BeginGuardTap009(int role)
        {if(guardTapBindings009==null||role<2||role>3)return;var binding=guardTapBindings009[role-2];if(binding!=null)binding.Serial++;}
        void CancelGuardTaps009()
        {
            guardTap009?.Reset();if(guardTapBindings009==null)return;
            if(guardTapBindings009[0]!=null)guardTapBindings009[0].Serial=0;
            if(guardTapBindings009[1]!=null)guardTapBindings009[1].Serial=0;
        }
        void OnApplicationFocus(bool focused){if(!focused)CancelGuardTaps009();}
        void TickGuardTaps009()
        {
            if(guardTap009==null||session==null)return;
            bool visible=session.Hud.IsRefugeNpcConversationVisible(this)&&Application.isFocused;
            CaptureGuardTap009(0,visible);CaptureGuardTap009(1,visible);
            if(!guardTap009.Evaluate(Time.unscaledDeltaTime))return;
            ApplyGuardTap009(0,guardTap009.Read(0));ApplyGuardTap009(1,guardTap009.Read(1));
        }
        void CaptureGuardTap009(int index,bool visible)
        {
            Actor a=actors[index];var binding=guardTapBindings009[index];
            if(a==null||binding==null||conversationRole!=index+2||!visible||binding.Serial==0){guardTap009.Set(index,default);return;}
            Quaternion current=binding.Arm.localRotation,rest=binding.ArmRest;
            guardTap009.Set(index,new GuardFloorTapInput009{Enabled=1,Serial=binding.Serial,FloorY=binding.FloorY,RootY=a.RootBasis.position.y,
                TipRestRootY=binding.TipRootY,TorsoRestY=a.TorsoRest.y,StartTorsoY=a.Torso.localPosition.y,
                ArmCurrent=new quaternion(current.x,current.y,current.z,current.w),ArmRest=new quaternion(rest.x,rest.y,rest.z,rest.w)});
        }
        void ApplyGuardTap009(int index,GuardFloorTapOutput009 pose)
        {
            if(pose.Active==0)return;Actor a=actors[index];var binding=guardTapBindings009[index];if(a==null||binding==null)return;
            a.Torso.localPosition=new Vector3(a.TorsoRest.x,pose.TorsoY,a.TorsoRest.z);
            binding.Arm.localRotation=new Quaternion(pose.Arm.value.x,pose.Arm.value.y,pose.Arm.value.z,pose.Arm.value.w);
            if(pose.Contact==1){Vector3 contact=binding.Weapon.position;contact.y=binding.FloorY;session.Audio?.PlayCue("footstep_stone_01",contact);}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original supported root/feet and exact cold measured spear rest vertex; coherent rigid upper body moves, feet never do.
// Array Formats: Fixed2 numeric gesture records, cold per-tool TempJob support result and one owned24triangle Ilyra spearhead; fixed cached renderer writes.
// Dependencies/Pointers: Existing accepted lore serial/visible modal and existing pooled stone cue/typewriting owner; all jobs complete before transforms/disposal. No quest/economy/body prefab/borrowed palette mutation; native support/clearance/voice/browser proof pending.
