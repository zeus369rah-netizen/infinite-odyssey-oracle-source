// PSEUDOCODE: cold-borrow16clips and fixed native storage; consume only the retained player's actual grounded stride/support; validate a private numeric support result; emit one idle-only LRU cue or drop quietly.
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace OdysseyOracle
{
    public enum SupportedStepState020:byte { Uninitialized,InactiveHub,Held,SupportUnavailable,Unproven,LandingRebase,MissingClip,Busy,Played }
    public struct SupportedStepSnapshot020
    {
        public SupportedStepState020 State;
        public SupportedStepFamily020 Family;
        public uint Attempts,Accepted,MissingMask,Sequence,SupportKey;
        public ulong Epoch,Collider;
        public float SupportHeight,FeetDelta,SupportDelta;
        public int Take;
    }
    public sealed partial class GameAudioDirector
    {
        static readonly string[] SupportedStepResources020={
            "CampaignHub020/Weather/woodStep01","CampaignHub020/Weather/woodStep02","CampaignHub020/Weather/woodStep03","CampaignHub020/Weather/woodStep04",
            "CampaignHub020/Weather/woodCreak01","CampaignHub020/Weather/woodCreak02","CampaignHub020/Weather/woodCreak03","CampaignHub020/Weather/woodCreak04",
            "CampaignHub020/Weather/stairUp01","CampaignHub020/Weather/stairUp02","CampaignHub020/Weather/stairUp03","CampaignHub020/Weather/stairUp04",
            "CampaignHub020/Weather/stairDown01","CampaignHub020/Weather/stairDown02","CampaignHub020/Weather/stairDown03","CampaignHub020/Weather/stairDown04"};
        readonly AudioClip[] supportedStepClips020=new AudioClip[16],supportedStepVoiceClips020=new AudioClip[4];
        readonly uint[] supportedStepVoiceGenerations020=new uint[4];
        readonly ulong[] supportedStepFamilySerial020=new ulong[4];
        NativeArray<SupportedStepRow020> supportedStepPrivateRows020;
        NativeArray<SupportedStepResult020> supportedStepResult020;
        JobHandle supportedStepFence020;
        SupportedStepScope020 supportedStepScope020;
        SupportedStepSnapshot020 supportedStepReadout020;
        DungeonWorld supportedStepWorld020;
        Transform supportedStepRoot020;
        SupportedStepResult020 supportedStepPrevious020;
        ulong supportedStepEpochHigh020,supportedStepWoodEvents020;
        uint supportedStepSequence020,supportedStepSeed020;
        float supportedStepPreviousFeet020;
        int supportedStepFrame020=-1;
        bool supportedStepPrepared020,supportedStepKnown020;
        public SupportedStepSnapshot020 SupportedFootstepReadout020=>supportedStepReadout020;
        void PrepareSupportedFootsteps020()
        {
            if(supportedStepPrepared020)return;supportedStepPrepared020=true;
            supportedStepPrivateRows020=new NativeArray<SupportedStepRow020>(SupportedFootstepRules020.Capacity,Allocator.Persistent);
            supportedStepResult020=new NativeArray<SupportedStepResult020>(1,Allocator.Persistent);
            for(int i=0;i<16;i++){supportedStepClips020[i]=Resources.Load<AudioClip>(SupportedStepResources020[i]);Prime(supportedStepClips020[i]);if(supportedStepClips020[i]==null)supportedStepReadout020.MissingMask|=1u<<i;}
        }
        // Explicit cold optional injection; no ownership of clips or PCM is transferred.
        public bool SetSupportedFootstepClip020(SupportedStepFamily020 family,int take,AudioClip clip)
        {
            int row=(int)family-1;if(row<0||row>=4||take<0||take>=4)return false;
            StopSupportedFootstepPlayback020();int index=row*4+take;supportedStepClips020[index]=clip;Prime(clip);
            if(clip==null)supportedStepReadout020.MissingMask|=1u<<index;else supportedStepReadout020.MissingMask&=~(1u<<index);return true;
        }
        bool ReadSupportedStepAdmission020(out DungeonWorld world,out Transform root,out uint generation,out uint revision)
        {
            world=null;root=null;generation=revision=0;
            if(session==null||!ReferenceEquals(GameSession.Instance,session)||!session.isActiveAndEnabled||!session.CampaignHubActive020)return false;
            world=session.World;if(world==null||!world.isActiveAndEnabled||!world.CampaignHubActive020)return false;
            generation=world.CampaignHubGeneration020;revision=world.WorldRevision020;
            if(generation==0||revision==0||!world.TryGetPortalLandingPrototype020(generation,out var snapshot)||snapshot.Generation!=generation)return false;
            root=snapshot.Root;return root!=null&&root.gameObject.activeInHierarchy&&world.transform.localToWorldMatrix.Equals(Matrix4x4.identity)&&root.localToWorldMatrix.Equals(Matrix4x4.identity);
        }
        bool SupportedStepCurrent020(in SupportedStepScope020 scope)
        {
            return scope.Same(supportedStepScope020)&&ReadSupportedStepAdmission020(out var world,out var root,out uint generation,out uint revision)&&
                ReferenceEquals(world,supportedStepWorld020)&&ReferenceEquals(root,supportedStepRoot020)&&generation==scope.Generation&&revision==scope.Revision&&
                session.Player!=null&&EntityId.ToULong(session.Player.GetEntityId())==scope.Player&&
                EntityId.ToULong(session.GetEntityId())==scope.Session&&EntityId.ToULong(world.GetEntityId())==scope.World&&EntityId.ToULong(root.GetEntityId())==scope.Root;
        }
        bool SupportedStepAudible020()=>CanPlayCue()&&GuideGestureObserved015&&session!=null&&session.IsPlaying&&!session.Paused&&!session.IsDead&&!session.IsDowned&&!session.Victory&&session.Player!=null&&session.Player.isActiveAndEnabled;
        bool BindSupportedStepScope020(DungeonWorld world,Transform root,uint generation,uint revision)
        {
            RetireSupportedFootsteps020();
            if(!supportedStepPrepared020||supportedStepEpochHigh020==ulong.MaxValue||!world.TryCopySupportedFootstepRows020(supportedStepPrivateRows020,root,generation,revision))return false;
            supportedStepWorld020=world;supportedStepRoot020=root;
            supportedStepScope020=new SupportedStepScope020{Session=EntityId.ToULong(session.GetEntityId()),World=EntityId.ToULong(world.GetEntityId()),Root=EntityId.ToULong(root.GetEntityId()),Player=EntityId.ToULong(session.Player.GetEntityId()),Epoch=++supportedStepEpochHigh020,Generation=generation,Revision=revision};
            supportedStepSequence020=0;supportedStepWoodEvents020=0;supportedStepFrame020=-1;
            for(int i=0;i<4;i++)supportedStepFamilySerial020[i]=0;
            supportedStepSeed020=SupportedFootstepRules020.Mix((uint)supportedStepScope020.Root^(uint)(supportedStepScope020.Root>>32)^generation^revision);
            return SupportedStepCurrent020(supportedStepScope020);
        }
        internal void ResetSupportedFootstepTravel020(OracleFoley source)
        {if(source!=null&&ReferenceEquals(source,foley))supportedStepKnown020=false;}

        // true consumes the real hub stride even if silence is required; false preserves the legacy non-hub path.
        internal bool TryPlaySupportedFootstep020(OracleFoley source,CharacterController body,Vector3 feet,bool continuous,float speed,bool alternate)
        {
            if(session==null||!session.CampaignHubActive020)return false;
            if(source==null||!ReferenceEquals(source,foley)||body==null||!ReferenceEquals(body.gameObject,source.gameObject)||
                session.Player==null||!ReferenceEquals(source.transform,session.Player.transform))return true;
            if(!supportedStepPrepared020||!body.enabled||!body.isGrounded||!Finite(feet)||!SupportedStepAudible020())
            {supportedStepKnown020=false;supportedStepReadout020.State=SupportedStepState020.Held;return true;}
            if(!ReadSupportedStepAdmission020(out var world,out var root,out uint generation,out uint revision))
            {RetireSupportedFootsteps020();supportedStepReadout020.State=SupportedStepState020.SupportUnavailable;return true;}
            if(!SupportedStepCurrent020(supportedStepScope020)&&!BindSupportedStepScope020(world,root,generation,revision))
            {supportedStepReadout020.State=SupportedStepState020.SupportUnavailable;return true;}
            if(supportedStepFrame020==Time.frameCount||supportedStepSequence020==uint.MaxValue)return true;
            supportedStepFrame020=Time.frameCount;uint sequence=++supportedStepSequence020;
            if(supportedStepReadout020.Attempts<uint.MaxValue)supportedStepReadout020.Attempts++;
            supportedStepReadout020.Sequence=sequence;supportedStepReadout020.Epoch=supportedStepScope020.Epoch;
            if(!SupportedFootstepRules020.StepValid(speed,continuous))
            {supportedStepKnown020=false;supportedStepReadout020.State=SupportedStepState020.LandingRebase;return true;}
            if(!source.TryReadSupportedHit020(feet,out var hit)||!(hit.collider is BoxCollider box)||box.isTrigger||!box.enabled||!box.gameObject.activeInHierarchy||
                !ReferenceEquals(box.transform.parent,supportedStepRoot020)||box.center!=Vector3.zero||box.transform.localScale!=Vector3.one)
            {supportedStepKnown020=false;supportedStepReadout020.State=SupportedStepState020.Unproven;return true;}
            var query=new SupportedStepQuery020{Scope=supportedStepScope020,Collider=EntityId.ToULong(box.GetEntityId()),Sequence=sequence,Point=(float3)hit.point,Feet=(float3)feet,
                Center=(float3)box.transform.localPosition,Size=(float3)box.size,Rotation=box.transform.localRotation,NormalY=hit.normal.y};
            supportedStepFence020=new SupportedStepQueryJob020{Rows=supportedStepPrivateRows020,Result=supportedStepResult020,Query=query}.Schedule();supportedStepFence020.Complete();
            var result=supportedStepResult020[0];
            if(result.Valid!=1||result.Sequence!=sequence||result.Collider!=query.Collider||!result.Scope.Same(query.Scope)||!SupportedStepCurrent020(result.Scope)||!SupportedStepAudible020()||
                !body.isGrounded||EntityId.ToULong(box.GetEntityId())!=query.Collider||!ReferenceEquals(box.transform.parent,supportedStepRoot020)||
                box.transform.localPosition!=(Vector3)query.Center||box.size!=(Vector3)query.Size||box.transform.localRotation!=(Quaternion)query.Rotation)
            {supportedStepKnown020=false;supportedStepReadout020.State=SupportedStepState020.Unproven;return true;}
            var family=SupportedFootstepRules020.Family(result.Wood,result.Flight,result.Key,result.Top,feet.y,supportedStepKnown020,supportedStepPrevious020.Flight,supportedStepPrevious020.Key,supportedStepPrevious020.Top,supportedStepPreviousFeet020);
            supportedStepReadout020.SupportDelta=supportedStepKnown020?result.Top-supportedStepPrevious020.Top:0;supportedStepReadout020.FeetDelta=supportedStepKnown020?feet.y-supportedStepPreviousFeet020:0;
            supportedStepPrevious020=result;supportedStepPreviousFeet020=feet.y;supportedStepKnown020=true;
            if(family==SupportedStepFamily020.WoodStep){if((supportedStepWoodEvents020++&3)==3)family=SupportedStepFamily020.WoodCreak;}
            supportedStepReadout020.Family=family;supportedStepReadout020.SupportKey=result.Key;supportedStepReadout020.Collider=result.Collider;supportedStepReadout020.SupportHeight=result.Top;
            AudioClip clip;float gain=.1f;int take=-1;
            if(family==SupportedStepFamily020.Stone)namedClips.TryGetValue(alternate?"footstep_stone_01":"footstep_stone_02",out clip);
            else
            {
                int row=(int)family-1;
                if(supportedStepFamilySerial020[row]==ulong.MaxValue){supportedStepReadout020.State=SupportedStepState020.Busy;return true;}
                take=SupportedFootstepRules020.Take(supportedStepFamilySerial020[row]++,supportedStepSeed020^(uint)family);int index=row*4+take;clip=supportedStepClips020[index];
                if(clip==null||clip.loadState!=AudioDataLoadState.Loaded||!SupportedFootstepRules020.ClipValid(clip.length))
                {supportedStepReadout020.MissingMask|=1u<<index;supportedStepReadout020.State=SupportedStepState020.MissingClip;supportedStepReadout020.Take=take;return true;}
                supportedStepReadout020.MissingMask&=~(1u<<index);if(family==SupportedStepFamily020.WoodCreak)gain=.065f;
            }
            supportedStepReadout020.Take=take;
            if(clip==null||clip.loadState!=AudioDataLoadState.Loaded){supportedStepReadout020.State=SupportedStepState020.MissingClip;return true;}
            bool played=PlayIdleSupportedStep020(clip,feet,gain);
            supportedStepReadout020.State=played?SupportedStepState020.Played:SupportedStepState020.Busy;
            if(played&&supportedStepReadout020.Accepted<uint.MaxValue)supportedStepReadout020.Accepted++;
            return true;
        }
        bool PlayIdleSupportedStep020(AudioClip clip,Vector3 feet,float gain)
        {
            int selected=-1;float oldest=float.PositiveInfinity,now=Time.unscaledTime;
            for(int i=0;i<4;i++)
            {
                var voice=cuePool[i];if(voice==null||voice.isPlaying||AcousticVoiceBusy015(i)||now<cueNext[i])continue;
                if(cueStarted[i]<oldest){selected=i;oldest=cueStarted[i];}
            }
            if(selected<0||!cueAdmission.Commit(selected,now,0,Mathf.Min(.25f,clip.length),.18f))return false;
            var source=cuePool[selected];source.Stop();source.transform.position=feet;source.spatialBlend=1;source.clip=clip;source.pitch=1;
            cueGains[selected]=gain;source.volume=sfxVolume*gain;cueStarted[selected]=now;cueNext[selected]=now+.18f;PlayAcousticVoice015(selected,source);
            if(TryReadAcousticVoice015(selected,out var frame)&&frame.Generation!=0){supportedStepVoiceGenerations020[selected]=frame.Generation;supportedStepVoiceClips020[selected]=clip;}
            return true;
        }
        void GuardSupportedFootsteps020()
        {
            if(!supportedStepScope020.Valid)return;
            if(!SupportedStepCurrent020(supportedStepScope020)){RetireSupportedFootsteps020();supportedStepReadout020.State=SupportedStepState020.InactiveHub;return;}
            if(!SupportedStepAudible020()){StopSupportedFootstepPlayback020();supportedStepKnown020=false;supportedStepReadout020.State=SupportedStepState020.Held;}
        }
        void StopSupportedFootstepPlayback020()
        {
            supportedStepKnown020=false;
            supportedStepFence020.Complete();
            for(int i=0;i<4;i++)
            {
                if(supportedStepVoiceClips020[i]!=null&&cuePool[i]!=null&&cuePool[i].clip==supportedStepVoiceClips020[i]&&
                    TryReadAcousticVoice015(i,out var frame)&&frame.Generation==supportedStepVoiceGenerations020[i])
                {cuePool[i].Stop();StopExternalVoice015(i);}
                supportedStepVoiceGenerations020[i]=0;supportedStepVoiceClips020[i]=null;
            }
        }
        void RetireSupportedFootsteps020()
        {
            StopSupportedFootstepPlayback020();supportedStepKnown020=false;supportedStepScope020=default;supportedStepWorld020=null;supportedStepRoot020=null;supportedStepReadout020.Epoch=0;
        }
        void DisposeSupportedFootsteps020()
        {RetireSupportedFootsteps020();if(supportedStepPrivateRows020.IsCreated)supportedStepPrivateRows020.Dispose();if(supportedStepResult020.IsCreated)supportedStepResult020.Dispose();}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual current admitted local-player root feet and exact cold-published support collider; independent of WeatherEffects/Clouds/weather volume.
// Array Formats:16shared clips,72private native row copy,1result,4owned voice generation/clip receipts and four finite variation counters; no hot managed allocation/new source.
// Dependencies/Pointers: OracleFoley consumes one real stride; World owns immutable metadata/native collider creation. Existing Master/SFX/gesture/focus, priority0idle-only LRU and acoustic adapters retain authority. Missing/unproven/busy events drop once without fallback/replay; native import, traversal, sound mix and WebGL acceptance remain pending.
