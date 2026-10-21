using System;
using OdysseyOracle.VisualDressing;
using UnityEngine;
#if UNITY_EDITOR
using RoomReactionReload=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using RoomReactionReload=System.Action;
#endif

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        [SerializeField] private RoomReactionNode32[] roomReactionNodes=new RoomReactionNode32[ElementalRoomReactionState.Capacity];
        [SerializeField] private Light[] roomReactionLights=new Light[ElementalRoomReactionState.Capacity];
        [SerializeField] private float[] roomReactionLightBases=new float[ElementalRoomReactionState.Capacity];
        [SerializeField] private bool[] roomReactionLightPulsed=new bool[ElementalRoomReactionState.Capacity];
        [SerializeField] private VoxelFireEffect[] roomReactionVoxel=new VoxelFireEffect[ElementalRoomReactionState.Capacity];
        [SerializeField] private LivingCampfirePresentation[] roomReactionLiving=new LivingCampfirePresentation[ElementalRoomReactionState.Capacity];
        [SerializeField] private RoomReactionGlowBinding[] roomReactionGlow=new RoomReactionGlowBinding[ElementalRoomReactionState.Capacity];
        [SerializeField] private int[] roomReactionPulseNodes=new int[MaximumSpatialPulseLights];
        [SerializeField] private int roomReactionCount,roomReactionRejected,candleReactionCount,roomWorldLightOrdinal,roomFlameOrdinal,roomLanternOrdinal;
        [SerializeField] private uint roomReactionGeneration=1;
        private ElementalRoomReactionState roomReactionState;
        private RoomReactionReload roomReactionReload;
        private bool roomReactionSubscribed,roomReactionWasRefuge,roomReactionConnected,roomReactionAwaiting;
        private uint roomReactionBoundEpoch;
        private readonly float[] roomReactionApplied=new float[ElementalRoomReactionState.Capacity];
        private int roomReactionTickFrame=-1;
        public int RoomReactionNodeCount=>roomReactionCount;
        public int RoomReactionRejectedRegistrations=>roomReactionRejected;
        public bool RoomReactionReady=>roomReactionState!=null&&roomReactionState.IsCreated&&roomReactionRejected==0;
        public float RoomReactionPlantGrowth=>roomReactionState!=null?roomReactionState.Growth:0;
        public uint RoomReactionRevision=>roomReactionState!=null?roomReactionState.Revision:0;
        public ulong RoomReactionLayoutHash=>roomReactionState!=null?roomReactionState.LayoutHash:0;
        public uint RoomReactionSceneGeneration=>roomReactionState!=null?roomReactionState.Generation:0;
        public bool RoomReactionHasSnapshot=>roomReactionState!=null&&roomReactionState.HasSnapshot;
        public bool TryGetRoomReactionNode(int index,out RoomReactionNode32 node,out float gain,out bool lit){node=default;gain=0;lit=false;if(index<0||index>=roomReactionCount||roomReactionState==null)return false;node=roomReactionNodes[index];gain=RoomReactionGain(index);lit=roomReactionState.Target(index);return true;}
        public float CampfireReactionFuel
        {
            get{for(int i=0;i<roomReactionCount;i++)if(roomReactionNodes[i].Kind==RoomReactionKind.LivingFlame)return RoomReactionGain(i);return 1;}
        }
        private int RegisterRoomNode(Vector3 center,float radius,RoomReactionKind kind,uint stable,uint ownerSlot=0,uint flags=0)
        {
            for(int i=0;i<roomReactionCount;i++)if(roomReactionNodes[i].StableId==stable&&roomReactionNodes[i].Kind==kind)return i;
            if(roomReactionCount>=ElementalRoomReactionState.Capacity||roomReactionState!=null){roomReactionRejected++;return -1;}
            flags=RefugeFlameFlags020(center,kind,flags);
            int slot=roomReactionCount++;roomReactionNodes[slot]=new RoomReactionNode32{Center=center,Radius=radius,Kind=kind,StableId=stable,OwnerSlot=ownerSlot,Flags=flags};
            return slot;
        }
        private void RegisterRoomReactionPointLight(Light source,Vector3 local)
        {
            if(source==null||geometryRoot!=tutorialRoot||tutorialRefuge==null)return;
            int node=RegisterRoomNode(geometryRoot.TransformPoint(local),.48f,RoomReactionKind.Light,(uint)(100000+roomWorldLightOrdinal++));
            if(node<0)return;roomReactionLights[node]=source;roomReactionLightBases[node]=source.intensity;
        }
        internal int RegisterRoomReactionExternalLight(Light source,uint stable)
        {
            if(source==null||tutorialRefuge==null||IsCoopArena||!IsTutorialRefuge)return -1;
            int node=RegisterRoomNode(source.transform.position,.55f,RoomReactionKind.Light,stable);
            if(node>=0){roomReactionLights[node]=source;roomReactionLightBases[node]=source.intensity;roomReactionLightPulsed[node]=true;}return node;
        }
        internal void RegisterRoomReactionFixture(int index,Vector3 local)
        {
            if(tutorialRoot==null||tutorialRefuge==null)return;
            RegisterRoomNode(tutorialRoot.TransformPoint(local),.45f,RoomReactionKind.FixtureLight,(uint)(200000+index),(uint)index);
        }
        private void RegisterRoomReactionPulseSlot(int pulseSlot,Light source)
        {
            if((uint)pulseSlot>=roomReactionPulseNodes.Length)return;roomReactionPulseNodes[pulseSlot]=0;
            for(int i=0;i<roomReactionCount;i++)if(roomReactionLights[i]==source)
            {roomReactionPulseNodes[pulseSlot]=i+1;roomReactionLightPulsed[i]=true;return;}
        }
        private float RoomReactionPulseGain(int slot)=>slot>=0&&slot<roomReactionPulseNodes.Length&&roomReactionPulseNodes[slot]>0?RoomReactionGain(roomReactionPulseNodes[slot]-1):1;
        internal float RoomReactionGain(int node)
        {
            if(IsCoopArena||!IsTutorialRefuge||node<0||node>=roomReactionCount)return 1;
            if(roomReactionAwaiting)return 0;
            return roomReactionState!=null&&roomReactionState.IsCreated?roomReactionState.GainAt(node):(roomReactionNodes[node].Flags&1u)==0?1:0;
        }
        private void RegisterRoomReactionVoxel(VoxelFireEffect visual,Vector3 local,float height)
        {
            if(geometryRoot!=tutorialRoot||tutorialRefuge==null)return;
            int node=RegisterRoomNode(geometryRoot.TransformPoint(local+Vector3.up*height*.35f),Mathf.Clamp(height*.6f,.2f,.7f),RoomReactionKind.VoxelFlame,(uint)(400000+roomFlameOrdinal++));
            if(node>=0)roomReactionVoxel[node]=visual;
        }
        private void RegisterRoomReactionLiving(LivingCampfirePresentation visual,Vector3 local,float height,bool hearth=false)
        {
            if(visual==null||geometryRoot!=tutorialRoot||tutorialRefuge==null)return;
            int node=RegisterRoomNode(geometryRoot.TransformPoint(local+Vector3.up*height*.35f),Mathf.Clamp(height*.6f,.2f,.7f),hearth?RoomReactionKind.Hearth:RoomReactionKind.LivingFlame,(uint)(hearth?700000:500000),flags:hearth?1u:0u);
            if(node>=0){roomReactionLiving[node]=visual;if(hearth)cauldronHearthAudioNodePlusOne009=node+1;if(visual!=null)visual.SetRoomReactionFuel(hearth?0:1);}
        }
        internal void RegisterRoomReactionLantern(int ordinal,Vector3 worldCenter,Renderer fixedRenderer,Renderer movingRenderer,Material[] materials)
        {
            int node=RegisterRoomNode(worldCenter,.65f,RoomReactionKind.Lantern,(uint)(600000+roomLanternOrdinal++),(uint)ordinal);
            if(node>=0)roomReactionGlow[node]=RoomReactionGlowBinding.Create(fixedRenderer,movingRenderer,materials);
        }
        private void RegisterRoomReactionTorch(Renderer renderer,Vector3 local,uint ordinal)
        {
            int node=RegisterRoomNode(geometryRoot.TransformPoint(local),.35f,RoomReactionKind.TorchGlow,800000+ordinal);
            if(node>=0)roomReactionGlow[node]=RoomReactionGlowBinding.CreateFuel(renderer);
        }
        private void EnsureRoomReactionState()
        {
            if(roomReactionState!=null||roomReactionCount==0)return;
            roomReactionState=new ElementalRoomReactionState(roomReactionNodes,roomReactionCount);
            roomReactionState.Reset(roomReactionGeneration,true);
            for(int i=0;i<roomReactionApplied.Length;i++)roomReactionApplied[i]=float.NaN;
            if(roomReactionReload==null)roomReactionReload=DisposeRoomReactionNative;
            if(!roomReactionSubscribed){LightPoolLifecycle.SubscribeBeforeReload(roomReactionReload);roomReactionSubscribed=true;}
        }
        private void TickElementalRoomReactions()
        {
            if(roomReactionTickFrame==Time.frameCount)return;roomReactionTickFrame=Time.frameCount;
            if(!IsTutorialRefuge||IsCoopArena)
            {
                if(roomReactionWasRefuge){ResetRoomReactionScope(true);roomReactionWasRefuge=false;}return;
            }
            EnsureRoomReactionState();if(roomReactionState==null)return;
            if(!roomReactionWasRefuge){ResetRoomReactionScope(true);roomReactionWasRefuge=true;}
            var rooms=GameSession.Instance!=null?GameSession.Instance.Rooms:null;
            bool connected=rooms!=null&&rooms.Connected;uint epoch=connected?rooms.RoomReactionEpoch:0;
            if(connected!=roomReactionConnected||epoch!=roomReactionBoundEpoch)
            {
                roomReactionConnected=connected;roomReactionBoundEpoch=epoch;roomReactionAwaiting=connected&&!rooms.IsHost;
                ResetRoomReactionScope(!roomReactionAwaiting);
            }
            bool authority=!connected||rooms.IsHost;
            roomReactionState.Tick(Time.unscaledDeltaTime,authority);
            ApplyRoomReactionPresentation();
        }
        private void ResetRoomReactionScope(bool defaults)
        {
            ResetRefugeFlameReceipt020();
            roomReactionGeneration++;if(roomReactionGeneration==0)roomReactionGeneration=1;
            roomReactionState?.Reset(roomReactionGeneration,defaults);
            for(int i=0;i<roomReactionApplied.Length;i++)roomReactionApplied[i]=float.NaN;
        }
        internal bool TryRoomReactionContact(int mask,Vector3 origin,Vector3 direction,float distance,float radius,out float contact)
        {
            contact=distance;if(!IsTutorialRefuge||IsCoopArena)return false;TickElementalRoomReactions();
            return roomReactionState!=null&&roomReactionState.Query(mask,origin,direction,distance,radius,out contact);
        }
        internal bool AcceptRoomReactionHit(ulong caster,uint shot,int mask,Vector3 point)
        {
            if(!IsTutorialRefuge||IsCoopArena)return false;TickElementalRoomReactions();
            var rooms=GameSession.Instance!=null?GameSession.Instance.Rooms:null;
            bool authority=rooms==null||!rooms.Connected||rooms.IsHost&&rooms.HasAcceptedHubSpawn&&rooms.IsCampaignMemberRegistered(caster);
            if(roomReactionState==null||!roomReactionState.AcceptHit(authority,caster,shot,mask,point,out var accepted))return false;
            PublishRefugeFlameTransition020(caster,shot,mask,point,accepted);return true;
        }
        internal bool CaptureRoomReactions(uint epoch,out RoomReactionFrame112 frame)
        {
            frame=default;if(!IsTutorialRefuge||IsCoopArena)return false;TickElementalRoomReactions();
            if(!RoomReactionReady||epoch==0)return false;frame=roomReactionState.Capture(epoch);return true;
        }
        internal bool ApplyRoomReactions(in RoomReactionFrame112 frame,uint epoch)
        {
            if(!IsTutorialRefuge||IsCoopArena)return false;TickElementalRoomReactions();
            if(roomReactionState==null||!roomReactionState.Apply(frame,epoch))return false;
            roomReactionAwaiting=false;ApplyRoomReactionPresentation();return true;
        }
        private void DisposeRoomReactionNative()
        {
            roomReactionState?.Dispose();roomReactionState=null;ResetRefugeFlameReceipt020();
            if(roomReactionSubscribed&&roomReactionReload!=null)LightPoolLifecycle.UnsubscribeBeforeReload(roomReactionReload);
            roomReactionSubscribed=false;roomReactionTickFrame=-1;roomReactionWasRefuge=false;
        }
        private void OnDisable(){try{HideCampaignAssetPresentation020();}finally{ResetPassiveWind015();DisposeRoomReactionNative();}}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Cold actual supported refuge fixture world centres; query math only in native store, existing local geometry/pivots/colliders unchanged.
// Array Formats: Fixed512 serialized cold Node32/reference adapters plus disposable native store/gains/8word targets/48receipts;32pulse slot mappings. Mono field initializers allocate only managed arrays/data, never native Objects/MPBs.
// Dependencies/Pointers: Existing World owns fixtures/materials; current accepted-shot host and binary room epoch supply authority. Native state is initialized on main-thread Tick and disposed on disable/reload/destroy; arena/connection scope resets invalidate state without save or portal/shrine mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual refuge/world audio anchors only; no geometry/gameplay mutation.
// Array Formats: O(1) read-only cauldron frame,2 cold sources, existing4 ambient voices; cached narration bool.
// Dependencies/Pointers: Existing Audio/World/Activity lifetimes/accepted state remain canonical; work-only SFX integration, native/listening pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing anchored presentation receives the same bounded passive world-XZ field; geometry/physics/accepted room fuel stay canonical.
// Array Formats: existing fixed mesh/vertex/material owner and one shared float4; no hot collections, per-window objects or mutable gameplay alias.
// Dependencies/Pointers: work-only shared-breeze adaptation; original shaders and fallback lifetimes remain. Root alone imports/tests actual native/WebGL visuals, culling, allocation and lifecycle.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original accepted refuge world contacts and cold main-room membership.
// Array Formats: existing Node32 flags add one eligibility bit; stack Accepted24 and scalar event receipt; no new native allocation.
// Dependencies/Pointers: original accepted host shot, scope reset and completed job fences remain canonical; snapshot apply never publishes achievement/audio, rejected/duplicate/no-op hits produce no new event.
