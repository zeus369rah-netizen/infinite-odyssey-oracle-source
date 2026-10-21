using System;
using OdysseyOracle.VisualDressing;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    [Serializable]
    public sealed class RoomReactionGlowBinding
    {
        [SerializeField] private Renderer first,second;
        [SerializeField] private bool fuelOnly;
        [SerializeField] private int count;
        [SerializeField] private int[] slots=new int[5];
        [SerializeField] private Color[] bases=new Color[5],emissions=new Color[5];
        [NonSerialized] private MaterialPropertyBlock block;
        [NonSerialized] private float applied=float.NaN;
        internal static RoomReactionGlowBinding Create(Renderer a,Renderer b,Material[] borrowed)
        {
            var owner=new RoomReactionGlowBinding{first=a,second=b};
            for(int i=0;i<borrowed.Length&&i<5;i++)
            {
                Material material=borrowed[i];if(material==null||!material.HasProperty("_EmissionColor"))continue;
                Color emission=material.GetColor("_EmissionColor");if(emission.maxColorComponent<=0)continue;
                int at=owner.count++;owner.slots[at]=i;owner.emissions[at]=emission;
                owner.bases[at]=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white;
            }
            return owner;
        }
        internal static RoomReactionGlowBinding CreateFuel(Renderer a)=>new RoomReactionGlowBinding{first=a,fuelOnly=true};
        internal void Apply(float gain)
        {
            if(gain==applied)return;applied=gain;if(block==null)block=new MaterialPropertyBlock();
            if(fuelOnly){block.Clear();block.SetFloat("_RoomTorchFuel",gain);if(first!=null)first.SetPropertyBlock(block);return;}
            for(int i=0;i<count;i++)
            {
                block.Clear();block.SetColor("_EmissionColor",emissions[i]*gain);block.SetColor("_BaseColor",bases[i]*gain);
                if(first!=null)first.SetPropertyBlock(block,slots[i]);if(second!=null)second.SetPropertyBlock(block,slots[i]);
            }
        }
    }
    public sealed partial class DungeonWorld
    {
        [SerializeField] private MeshRenderer roomReactionCandleRenderer;
        [SerializeField] private int[] roomReactionCandleNodes=new int[256];
        [SerializeField] private RefugeReactionFlower roomReactionFlower;
        private readonly Vector4[] roomReactionCandleGains=new Vector4[64];
        private MaterialPropertyBlock roomReactionCandleBlock;
        public bool RoomReactionFlowerReady=>roomReactionFlower!=null&&roomReactionFlower.Ready;
        private void RegisterRoomReactionCandles(MeshRenderer renderer,CandleFlameSeed[] seeds,int count)
        {
            roomReactionCandleRenderer=renderer;candleReactionCount=count;
            for(int i=0;i<count;i++)roomReactionCandleNodes[i]=RegisterRoomNode(geometryRoot.TransformPoint(seeds[i].WickLocal+Vector3.up*seeds[i].Height*.4f),.22f,RoomReactionKind.Candle,(uint)(300000+i),(uint)i);
        }
        private void BuildRoomReactionDiscoveries(Vector3 raykonFeet)
        {
            if(tutorialRefuge==null||geometryRoot!=tutorialRoot)return;
            Vector3 plant=new Vector3(-tutorialRefuge.OuterX+1.55f,raykonFeet.y,raykonFeet.z+.75f);
            ValidateRoomReactionPlantSite(plant,raykonFeet);
            roomReactionFlower=RefugeReactionFlower.TryCreate(geometryRoot,plant);
            if(roomReactionFlower!=null)RegisterRoomNode(geometryRoot.TransformPoint(plant+Vector3.up*.25f),.34f,RoomReactionKind.Plant,900000,flags:1);
            Vector3 cauldron=RaykonMeshes.CauldronColliderBounds.center;cauldron.y=0;
            Vector3 hearth=raykonFeet+Quaternion.Euler(0,90,0)*cauldron;
            LivingCampfirePresentation.TryCreate(geometryRoot,hearth,.15f,out LivingCampfirePresentation fire);
            RegisterRoomReactionLiving(fire,hearth,.15f,true);
        }
        private void ValidateRoomReactionPlantSite(Vector3 site,Vector3 raykonFeet)
        {
            Vector3 a=site+new Vector3(-.30f,0,-.30f),b=site+new Vector3(.30f,1.50f,.30f);bool fits=false;
            for(int i=0;i<tutorialRefuge.Rooms.Length;i++){var room=tutorialRefuge.Rooms[i];if(a.x>=room.MinU+.1f&&b.x<=room.MaxU-.1f&&a.z>=room.MinV+.1f&&b.z<=room.MaxV-.1f){fits=true;break;}}
            for(int i=0;i<tutorialRefuge.PropPlacements.Length;i++){var obstacle=RefugePlacementPolicy.At(tutorialRefuge.PropPlacements[i]);if(Overlap(a,b,new Vector3(obstacle.Min.X,obstacle.Min.Y,obstacle.Min.Z),new Vector3(obstacle.Max.X,obstacle.Max.Y,obstacle.Max.Z)))throw new InvalidOperationException("Reaction pot overlaps a saved prop.");}
            for(int i=0;i<2;i++){Vector3 shelf=new Vector3(-tutorialRefuge.OuterX+.45f,raykonFeet.y,raykonFeet.z+(i==0?-1.65f:1.65f));if(Overlap(a,b,shelf+new Vector3(-.25f,0,-.7f),shelf+new Vector3(.55f,2.8f,.7f)))throw new InvalidOperationException("Reaction pot overlaps a supported shelf.");}
            Vector3 table=raykonFeet+new Vector3(-.15f,0,1.55f);
            if(!fits||Overlap(a,b,table+new Vector3(-.75f,0,-.35f),table+new Vector3(.75f,1.29f,.35f))||
                Overlap(a,b,raykonFeet+new Vector3(-.325f,0,-.44f),raykonFeet+new Vector3(.325f,2.17f,.44f))||b.x>raykonFeet.x)
                throw new InvalidOperationException("Optional reaction pot does not clear the actual table/actor/talk route.");
            NativeArray<float4> rectangles=default;NativeArray<Foundation.World.RefugeFloorQuery32> queries=default;
            NativeArray<ulong> occupancy=default;NativeArray<Foundation.World.RefugeFloorResult32> results=default;JobHandle fence=default;
            try
            {
                rectangles=new NativeArray<float4>(tutorialRefuge.Rooms.Length,Allocator.TempJob);
                for(int i=0;i<rectangles.Length;i++){var room=tutorialRefuge.Rooms[i];rectangles[i]=new float4(room.MinU,room.MinV,room.MaxU,room.MaxV);}
                queries=new NativeArray<Foundation.World.RefugeFloorQuery32>(1,Allocator.TempJob);
                queries[0]=new Foundation.World.RefugeFloorQuery32{Origin=new float3(site.x,site.y+2,site.z),MaximumDistance=4,HalfFootprintXZ=new float2(.3f),Flags=Foundation.World.RefugeFloorSupportMath.CheckFootprintCorners,QueryId=0};
                fence=Foundation.World.RefugeFloorSupportCold.Schedule(rectangles,queries,Allocator.TempJob,out _,out occupancy,out results);fence.Complete();
                if(results[0].Status!=1||results[0].Position.y!=site.y)throw new InvalidOperationException("Reaction pot lost exact numeric floor support.");
            }
            finally{fence.Complete();if(results.IsCreated)results.Dispose();if(occupancy.IsCreated)occupancy.Dispose();if(queries.IsCreated)queries.Dispose();if(rectangles.IsCreated)rectangles.Dispose();}
        }
        private void ApplyRoomReactionPresentation()
        {
            bool candlesDirty=false;
            for(int node=0;node<roomReactionCount;node++)
            {
                float gain=RoomReactionGain(node);if(roomReactionApplied[node]==gain)continue;roomReactionApplied[node]=gain;
                RoomReactionNode32 descriptor=roomReactionNodes[node];
                if(descriptor.Kind==RoomReactionKind.Light)
                {if(roomReactionLights[node]!=null&&!roomReactionLightPulsed[node])roomReactionLights[node].intensity=roomReactionLightBases[node]*gain;}
                else if(descriptor.Kind==RoomReactionKind.FixtureLight){if(tutorialFireLights!=null)tutorialFireLights.SetRoomReactionFuel((int)descriptor.OwnerSlot,gain);}
                else if(descriptor.Kind==RoomReactionKind.Candle){int slot=(int)descriptor.OwnerSlot;if(slot<256){Vector4 group=roomReactionCandleGains[slot>>2];group[slot&3]=gain;roomReactionCandleGains[slot>>2]=group;candlesDirty=true;}}
                else if(descriptor.Kind==RoomReactionKind.VoxelFlame){if(roomReactionVoxel[node]!=null)roomReactionVoxel[node].SetRoomReactionFuel(gain);}
                else if(descriptor.Kind==RoomReactionKind.LivingFlame||descriptor.Kind==RoomReactionKind.Hearth){if(roomReactionLiving[node]!=null)roomReactionLiving[node].SetRoomReactionFuel(gain);if(roomReactionVoxel[node]!=null)roomReactionVoxel[node].SetRoomReactionFuel(gain);roomReactionGlow[node]?.Apply(gain);}
                else if(descriptor.Kind==RoomReactionKind.Lantern||descriptor.Kind==RoomReactionKind.TorchGlow)roomReactionGlow[node]?.Apply(gain);
            }
            if(candlesDirty&&roomReactionCandleRenderer!=null)
            {
                if(roomReactionCandleBlock==null)roomReactionCandleBlock=new MaterialPropertyBlock();
                roomReactionCandleBlock.SetFloat("_UseRoomReactions",1);roomReactionCandleBlock.SetVectorArray("_RoomReactionFlameGroups",roomReactionCandleGains);
                roomReactionCandleRenderer.SetPropertyBlock(roomReactionCandleBlock);
            }
            if(roomReactionFlower!=null)roomReactionFlower.ApplyGrowth(roomReactionState.Growth,(float)SpatialLightPulseTime);
        }
        internal void ClearRoomReactionsOnDetach()
        {
            roomReactionConnected=roomReactionAwaiting=false;roomReactionBoundEpoch=0;ResetRoomReactionScope(true);
            if(IsTutorialRefuge&&!IsCoopArena&&roomReactionState!=null)ApplyRoomReactionPresentation();
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual supported refuge local+Z/Y-up flower/hearth sites; original fixture pivots/structure remain. Pure support checks finish before binding; reference writes are bounded main-thread presentation only.
// Array Formats: Fixed512 adapters and64float4 packed candle MPB groups, cached per-material lantern blocks, one original flower and reused private-clock hearth. No Mono native-object initializer; MPBs created lazily on main-thread methods, never per frame.
// Dependencies/Pointers: Existing World owns all visual/lights/support lifetime; existing pulse/fire/campfire owners consume gains. Borrowed materials remain unchanged; original portal/shrine game state, audio/rewards and saves untouched.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: same saved brazier/room anchors and host-published gains.
// Array Formats: existing LivingFlame slot may bind the original Voxel fallback without changing node kind/layout hash.
// Dependencies/Pointers: existing World owns all presentation; cold brazier glow block prepared before gameplay; no target, physics, audio or award mutation in presentation loop.
