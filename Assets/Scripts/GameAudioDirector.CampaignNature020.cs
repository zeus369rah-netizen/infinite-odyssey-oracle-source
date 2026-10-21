using UnityEngine;
using OracleNature020;

namespace OdysseyOracle
{
    public struct CampaignBrushToken020
    {
        public uint OwnerEpoch,Generation,WorldRevision,GeometryRevision,FoliageRevision,Step;
        public ulong Serial,Plant,Player;
        public Vector3 Position;
        public float Strength;
        public int Variant;
    }
    public sealed partial class GameAudioDirector
    {
        const string CampaignWindCue020="campaign_garden_wind020";
        readonly AudioClip[] natureBrush020=new AudioClip[4];
        AudioClip natureWind020,natureOwnedClip020;
        AmbientEmitter natureRecord020;
        DungeonWorld natureWorld020;
        Transform natureRoot020;
        uint natureEpoch020,natureEpochHigh020,natureGeneration020,natureRevision020,natureGeometry020,natureFoliage020,natureStep020,natureVoiceGeneration020;
        int natureId020=-1,natureSlot020=-1,natureFrame020=-1;
        ulong natureSerial020;
        double natureClock020,natureNext020;
        bool natureResources020;
        float natureFade020;
        Wind020 natureState020;
        internal uint CampaignNatureAudioEpoch020=>natureEpoch020;

        void PrepareCampaignNatureResources020()
        {
            if(natureResources020)return;natureResources020=true;
            natureWind020=Resources.Load<AudioClip>("CampaignHub020/Nature/gardenWind01");Prime(natureWind020);
            natureBrush020[0]=Resources.Load<AudioClip>("CampaignHub020/Nature/brush01");Prime(natureBrush020[0]);
            natureBrush020[1]=Resources.Load<AudioClip>("CampaignHub020/Nature/brush02");Prime(natureBrush020[1]);
            natureBrush020[2]=Resources.Load<AudioClip>("CampaignHub020/Nature/brush03");Prime(natureBrush020[2]);
            natureBrush020[3]=Resources.Load<AudioClip>("CampaignHub020/Nature/brush04");Prime(natureBrush020[3]);
            namedClips[CampaignWindCue020]=natureWind020;namedNext[CampaignWindCue020]=0;
        }
        bool NatureScope020(GameSession source,DungeonWorld world,Transform root,uint generation,uint revision,uint geometry)
        {
            return source!=null&&object.ReferenceEquals(source,session)&&source.isActiveAndEnabled&&source.CampaignHubActive020&&
                world!=null&&object.ReferenceEquals(source.World,world)&&root!=null&&root.gameObject.activeInHierarchy&&
                generation!=0&&revision!=0&&geometry!=0&&generation==world.CampaignHubGeneration020&&revision==world.WorldRevision020&&
                world.CampaignHubActive020&&world.TryGetPortalLandingPrototype020(generation,out var snapshot)&&
                snapshot.Generation==generation&&object.ReferenceEquals(snapshot.Root,root);
        }
        bool NatureCurrent020()=>natureEpoch020!=0&&natureFoliage020!=0&&natureGeneration020!=0&&NatureScope020(session,natureWorld020,natureRoot020,natureGeneration020,natureRevision020,natureGeometry020);
        bool NatureAudible020()=>CanPlayCue()&&GuideGestureObserved015&&session!=null&&session.IsPlaying&&!session.Paused&&
            !session.IsDead&&!session.IsDowned&&!session.Victory&&session.Player!=null&&session.Player.isActiveAndEnabled;

        // Cold only, after the real geometry/foliage/boundary publisher has been accepted.
        internal bool PrepareCampaignNatureAudio020(GameSession source,DungeonWorld world,Transform root,uint generation,uint revision,uint geometry,uint foliage)
        {
            RetireCampaignNatureAudio020();
            if(!initialized||!acousticReady015||foliage==0||natureEpochHigh020==uint.MaxValue||!NatureScope020(source,world,root,generation,revision,geometry))return false;
            PrepareCampaignNatureResources020();
            int selected=-1;
            if(natureWind020!=null&&ambientEmitters.Count<MaximumAmbientEmitters)
                for(int i=0;i<8;i++)if(!ambientEmitters.ContainsKey(2147483200+i)){selected=2147483200+i;break;}
            natureEpoch020=++natureEpochHigh020;natureWorld020=world;natureRoot020=root;natureGeneration020=generation;natureRevision020=revision;natureGeometry020=geometry;natureFoliage020=foliage;
            natureState020=WindMath020.Create();natureStep020=0;natureSerial020=0;natureClock020=natureNext020=0;
            if(selected>=0)
            {
                natureId020=selected;natureRecord020=new AmbientEmitter{Cue=CampaignWindCue020,Range=30,Gain=0,Position=ListenerPosition()};
                ambientEmitters.Add(selected,natureRecord020);nextAmbientCheck=0;
            }
            // Missing wind/capacity may still permit accepted brush tokens; neither result invents a clip.
            return true;
        }

        // Main-thread scalar sink only. The caller owns exact completed outer-boundary and contact job authority.
        internal bool TickCampaignNatureAudio020(GameSession source,DungeonWorld world,Transform root,uint epoch,uint generation,uint revision,uint geometry,uint foliage,
            uint step,float outerDistance,float delta,bool boundaryReady,bool held)
        {
            natureFrame020=-1;
            if(!NatureCurrent020()||!object.ReferenceEquals(source,session)||!object.ReferenceEquals(world,natureWorld020)||
                !object.ReferenceEquals(root,natureRoot020)||epoch!=natureEpoch020||generation!=natureGeneration020||revision!=natureRevision020||geometry!=natureGeometry020||foliage!=natureFoliage020)
            {RetireCampaignNatureAudio020();return false;}
            if(step==0||step<=natureStep020||step==uint.MaxValue||float.IsNaN(delta)||float.IsInfinity(delta)||delta<0)
            {StopCampaignNaturePlayback020();return false;}
            natureStep020=step;
            if(held||!NatureAudible020()){StopCampaignNaturePlayback020();return true;}
            float dt=Mathf.Min(delta,.1f);
            natureClock020+=dt;
            if(double.IsNaN(natureClock020)||double.IsInfinity(natureClock020)){RetireCampaignNatureAudio020();return false;}
            // Contact can still be accepted when a missing boundary withholds only wind exposure.
            natureFrame020=Time.frameCount;
            if(!boundaryReady||!WindMath020.Advance(in natureState020,outerDistance,dt,false,out var next))
            {natureFade020=0;if(natureRecord020!=null)natureRecord020.Gain=0;return true;}
            natureState020=next;
            if(natureRecord020!=null&&ambientEmitters.TryGetValue(natureId020,out var record)&&object.ReferenceEquals(record,natureRecord020))
            {natureFade020=Mathf.MoveTowards(natureFade020,natureState020.Gain,WindMath020.Rate*dt);natureRecord020.Position=ListenerPosition();natureRecord020.Gain=natureFade020;}
            return true;
        }

        internal bool TryPlayCampaignBrush020(GameSession source,in CampaignBrushToken020 token)
        {
            if(!NatureCurrent020()||!object.ReferenceEquals(source,session)||natureFrame020!=Time.frameCount||token.Step!=natureStep020||
                token.OwnerEpoch!=natureEpoch020||token.Generation!=natureGeneration020||token.WorldRevision!=natureRevision020||token.GeometryRevision!=natureGeometry020||token.FoliageRevision!=natureFoliage020||
                token.Serial==0||token.Serial<=natureSerial020||token.Serial==ulong.MaxValue||token.Plant==0||
                (token.Plant>>32)==0||(uint)token.Plant<1||(uint)token.Plant>8||token.Player==0||token.Variant<0||token.Variant>3||
                !Finite(token.Position)||float.IsNaN(token.Strength)||float.IsInfinity(token.Strength)||token.Strength<0||token.Strength>1)return false;
            natureSerial020=token.Serial; // Every well-scoped attempt is terminal, including mute/clip/pool rejection.
            if(!NatureAudible020()||OracleBookAccessibility.PrefersReducedMotion||session.Player==null||
                token.Player!=EntityId.ToULong(session.Player.GetEntityId())||!Near(token.Position,12f)||natureClock020<natureNext020)return false;
            natureNext020=natureClock020+.22;
            AudioClip clip=natureBrush020[token.Variant];
            if(clip==null||clip.loadState!=AudioDataLoadState.Loaded||clip.length<=0||clip.length>2||NatureOneShotBusy020())return false;
            int selected=-1;float oldest=float.PositiveInfinity;float now=Time.unscaledTime;
            // Four bounded main-thread AudioSource adapters; all spatial/plant querying stays in the numeric worker.
            for(int i=0;i<4;i++)
            {
                AudioSource item=cuePool[i];
                if(item==null||item.isPlaying||AcousticVoiceBusy015(i)||now<cueNext[i])continue;
                if(cueStarted[i]<oldest){oldest=cueStarted[i];selected=i;}
            }
            if(selected<0)return false;
            if(!cueAdmission.Commit(selected,now,0,Mathf.Min(clip.length,2),.22))return false;
            AudioSource voice=cuePool[selected];voice.Stop();voice.transform.position=token.Position;
            voice.spatialBlend=1;voice.clip=clip;voice.pitch=1;
            cueGains[selected]=Mathf.Lerp(.020f,.045f,token.Strength);voice.volume=sfxVolume*cueGains[selected];
            cueStarted[selected]=now;cueNext[selected]=now+.22f;
            PlayAcousticVoice015(selected,voice);
            if(!TryReadAcousticVoice015(selected,out var frame)||frame.Generation==0)return true;
            natureSlot020=selected;natureVoiceGeneration020=frame.Generation;natureOwnedClip020=clip;
            return true;
        }
        bool NatureOneShotBusy020()
        {
            if(natureSlot020<0)return false;
            if(!TryReadAcousticVoice015(natureSlot020,out var frame)||frame.Generation!=natureVoiceGeneration020||
                cuePool[natureSlot020]==null||cuePool[natureSlot020].clip!=natureOwnedClip020)
            {natureSlot020=-1;natureVoiceGeneration020=0;natureOwnedClip020=null;return false;}
            return cuePool[natureSlot020].isPlaying||AcousticVoiceBusy015(natureSlot020);
        }
        bool IsOwnedCampaignWind020(int id)=>id==natureId020&&natureRecord020!=null&&ambientEmitters.TryGetValue(id,out var record)&&
            object.ReferenceEquals(record,natureRecord020)&&record.Cue==CampaignWindCue020;
        void GuardCampaignNatureAudio020()
        {
            if(natureGeneration020==0)return;
            if(!NatureCurrent020()){RetireCampaignNatureAudio020();return;}
            if(!NatureAudible020())StopCampaignNaturePlayback020();
        }
        void StopCampaignNaturePlayback020()
        {
            natureFrame020=-1;natureFade020=0;
            if(natureRecord020!=null)natureRecord020.Gain=0;
            if(NatureOneShotBusy020())
            {cuePool[natureSlot020].Stop();StopExternalVoice015(natureSlot020);}
            natureSlot020=-1;natureVoiceGeneration020=0;natureOwnedClip020=null;
            for(int i=0;i<4;i++)if(IsOwnedCampaignWind020(ambientIds[i])&&ambientPool[i]!=null&&ambientPool[i].clip==natureWind020)
            {ambientPool[i].Stop();ambientIds[i]=-1;ambientGains[i]=0;}
        }
        internal void RetireCampaignNatureAudio020()
        {
            StopCampaignNaturePlayback020();
            if(IsOwnedCampaignWind020(natureId020))ambientEmitters.Remove(natureId020);
            natureRecord020=null;natureWorld020=null;natureRoot020=null;natureId020=-1;
            natureEpoch020=natureGeneration020=natureRevision020=natureGeometry020=natureFoliage020=natureStep020=0;natureSerial020=0;natureClock020=natureNext020=0;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact current GameSession/World/published root, current offline local player and caller-certified outer-edge distance/plant contact tokens.
// Array Formats: Five cold borrowed Resources clips, one of32 ambient records, one held wind state and existing4cue/4ambient voices; no new source/context or native owner here.
// Dependencies/Pointers: Required guarded owner hunks expose the wind cue/2D pool restore and stop hooks. Root must supply validated outer boundary and stable128-clump contact worker, stage/serial dispatch and all native/import/browser acceptance; new unmapped clips use existing Unity backend fallback.
