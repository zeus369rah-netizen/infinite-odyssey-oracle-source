using System;
using Unity.Mathematics;
using UnityEngine;
using OracleNatureBoundary020;

namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        CampaignNatureBindingLease020 campaignNatureBinding020;
        CampaignGardenFoliageScope020 campaignNatureBindingScope020;
        DungeonWorld campaignNatureBindingWorld020;
        Transform campaignNatureBindingRoot020;
        CampaignGardenFoliageLease020 campaignNatureBindingFoliage020;
        PortalLandingAtmosphere020 campaignNatureBindingAtmosphere020;
        GameAudioDirector campaignNatureBindingAudio020;
        uint campaignNatureBoundaryEpochHighWater020,campaignNatureStep020;
        ulong campaignNatureSequence020;
        float campaignNatureWindStrength020;
        double campaignNaturePendingElapsed020,campaignNatureLastElapsed020;
        float campaignNaturePendingDelta020;
        ulong campaignNaturePendingPlayer020;
        bool campaignNatureAttempted020,campaignNatureRetireFailed020,campaignNatureDispatchPrepared020,campaignNaturePendingHeld020,campaignNatureClockSampled020,campaignNaturePendingInterrupted020,campaignNatureForceSilentRebase020,campaignNatureFailureSuppressed020;
        string campaignNatureFailure020;

        // PSEUDOCODE: use the accepted foliage scope and shared atmosphere held clock;
        // build one exact layout boundary and copied contact index; schedule at most
        // one bounded current capsule frame; verify references/revisions after both
        // job fences; synchronously dispatch only current results to existing owners.
        float TickCampaignNatureBinding020(in CampaignGardenFoliageScope020 actual,Vector4 brush,Vector2 vertical)
        {
            if(!actual.Positive||!CampaignHubActive020||World==null||campaignAtmosphere020==null||!campaignAtmosphere020.Bound020||
                campaignFoliage020==null||!campaignFoliage020.Ready020||!campaignFoliage020.Scope020.Matches(actual))
            {
                RetireCampaignNatureBinding020();
                return 0f;
            }
            if(campaignNatureFailureSuppressed020)
            {
                if(campaignNatureBindingScope020.Matches(actual))return 0f;
                campaignNatureFailureSuppressed020=false;campaignNatureAttempted020=false;campaignNatureBindingScope020=default;
            }
            if(!NatureBindingReferencesCurrent020(actual,false))
            {
                RetireCampaignNatureBinding020();
                return 0f;
            }
            if(campaignNatureRetireFailed020)return 0f;
            if(campaignNatureAttempted020&&!campaignNatureBindingScope020.Matches(actual))
            {
                if(!RetireCampaignNatureBinding020())return 0f;
                campaignNatureAttempted020=false;
            }
            if(!campaignNatureAttempted020)
            {
                campaignNatureAttempted020=true;
                campaignNatureBindingScope020=actual;
                campaignNatureBindingWorld020=World;
                campaignNatureBindingRoot020=World.TryGetPortalLandingPrototype020(actual.Generation,out var accepted)?accepted.Root:null;
                campaignNatureBindingFoliage020=campaignFoliage020;
                campaignNatureBindingAtmosphere020=campaignAtmosphere020;
                campaignNatureBindingAudio020=Audio;
                campaignNatureSequence020=0;campaignNatureStep020=0;campaignNatureWindStrength020=0f;
                campaignNatureClockSampled020=false;campaignNatureLastElapsed020=0d;
                if(campaignNatureBindingRoot020==null||campaignNatureBoundaryEpochHighWater020==uint.MaxValue)
                    return FailNatureBinding020("No positive current root or boundary epoch remains.");
                campaignNatureBoundaryEpochHighWater020++;
                var boundaryScope=new BoundaryScope020
                {
                    Session=EntityId.ToULong(gameObject.GetEntityId()),
                    World=EntityId.ToULong(World.gameObject.GetEntityId()),
                    Root=EntityId.ToULong(campaignNatureBindingRoot020.GetEntityId()),
                    Epoch=campaignNatureBoundaryEpochHighWater020,Generation=actual.Generation,
                    WorldRevision=actual.WorldRevision,GeometryRevision=actual.GeometryRevision
                };
                campaignNatureBinding020=new CampaignNatureBindingLease020();
                if(!campaignNatureBinding020.BeginBoundary020(boundaryScope))
                    return FailNatureBinding020(campaignNatureBinding020.Failure020);
            }
            if(campaignNatureBinding020==null||campaignNatureRetireFailed020)return 0f;

            if(!NatureBindingReferencesCurrent020(actual,campaignNatureDispatchPrepared020))
                return FailNatureBinding020("Session, World, root, publisher, or atmosphere identity changed.");

            if(!ReadNatureClock020(out double elapsed,out uint clockGeneration,
                out uint clockRevision,out bool clockHeld)||double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0d||
                clockGeneration!=actual.Generation||clockRevision!=actual.WorldRevision)
                return FailNatureBinding020("The shared atmosphere clock no longer matches the admitted nature scope.");
            bool reduced=OracleBookAccessibility.PrefersReducedMotion;
            bool held=clockHeld||!IsPlaying||Paused||!Application.isFocused||IsDead||IsDowned||Victory;
            double elapsedDelta=campaignNatureClockSampled020?elapsed-campaignNatureLastElapsed020:0d;
            if(double.IsNaN(elapsedDelta)||double.IsInfinity(elapsedDelta)||elapsedDelta<0d||elapsedDelta>float.MaxValue)
                return FailNatureBinding020("The shared atmosphere clock delta is invalid.");
            float delta=(float)elapsedDelta;
            held=held||delta==0f;
            if(campaignNatureBinding020.FramePending020&&!campaignNaturePendingHeld020&&(held||reduced))
                campaignNaturePendingInterrupted020=true;

            int boundaryStatus=campaignNatureBinding020.PollBoundaryBuild020(CurrentBoundaryScope020());
            if(boundaryStatus<0)return FailNatureBinding020(campaignNatureBinding020.Failure020);
            if(boundaryStatus==0)return 0f;

            if(!campaignNatureDispatchPrepared020)
            {
                if(!PrepareCampaignNatureDispatch020(campaignNatureBindingRoot020,actual.GeometryRevision,actual.FoliageRevision))
                    return FailNatureBinding020("The accepted existing audio owner refused the current nature publishers.");
                campaignNatureAudioEpoch020=CampaignNatureDispatchEpoch020;
                if(campaignNatureAudioEpoch020==0)return FailNatureBinding020("The audio owner returned no positive dispatch epoch.");
                campaignNatureDispatchPrepared020=true;
                if(!campaignNatureBinding020.BeginContacts020(campaignNatureBindingFoliage020,actual,campaignNatureAudioEpoch020))
                    return FailNatureBinding020(campaignNatureBinding020.Failure020);
            }

            int indexStatus=campaignNatureBinding020.PollContactIndex020(actual,campaignNatureAudioEpoch020);
            if(indexStatus<0)return FailNatureBinding020(campaignNatureBinding020.Failure020);
            if(indexStatus==0)return 0f;

            if(campaignNatureBinding020.FramePending020)
            {
                int frameStatus=campaignNatureBinding020.PollFrame020(out var frame,out var edge);
                if(frameStatus<0)return FailNatureBinding020(campaignNatureBinding020.Failure020);
                if(frameStatus==0)return campaignNatureWindStrength020;
                if(!NatureBindingReferencesCurrent020(actual,true))
                    return FailNatureBinding020("A completed nature frame lost its exact source or revision.");
                bool interrupted=campaignNaturePendingInterrupted020;
                bool fresh=!interrupted&&elapsed>=campaignNaturePendingElapsed020&&elapsed-campaignNaturePendingElapsed020<=.10d;
                bool edgeReady=fresh&&!campaignNaturePendingHeld020&&edge.Status==BoundaryStatus020.Ready&&
                    WindBoundaryMath020.Same(edge.Scope,CurrentBoundaryScope020())&&edge.Step==frame.Step&&
                    edge.Player==campaignNaturePendingPlayer020&&FiniteNature020(edge.Distance)&&edge.Distance>=0f;
                var audioFrame=new CampaignNatureAudioFrame020
                {
                    Epoch=campaignNatureAudioEpoch020,Generation=actual.Generation,WorldRevision=actual.WorldRevision,
                    GeometryRevision=actual.GeometryRevision,FoliageRevision=actual.FoliageRevision,Step=frame.Step,
                    Delta=interrupted?0f:campaignNaturePendingDelta020,BoundaryReady=edgeReady,Held=campaignNaturePendingHeld020||interrupted,
                    OuterDistance=edgeReady?edge.Distance:0f,TokenCount=(byte)(fresh&&!campaignNaturePendingHeld020&&!interrupted?frame.TokenCount:0)
                };
                if(audioFrame.TokenCount>0)audioFrame.First=ToBrushToken020(in frame.First);
                if(audioFrame.TokenCount>1)audioFrame.Second=ToBrushToken020(in frame.Second);
                if(!DispatchCampaignNatureAudio020(in audioFrame))
                    return FailNatureBinding020("The completed current frame was refused by the existing audio owner.");
                float normalized=Audio.CampaignNatureWindStrength020;
                campaignNatureWindStrength020=FiniteNature020(normalized)?Mathf.Clamp01(normalized):0f;
                if(interrupted)campaignNatureForceSilentRebase020=!held&&!reduced;
                campaignNaturePendingInterrupted020=false;
            }

            if(campaignNatureBinding020.FramePending020)return campaignNatureWindStrength020;
            if(campaignNatureSequence020>=ulong.MaxValue-1UL||campaignNatureStep020>=uint.MaxValue-1U)
                return FailNatureBinding020("Nature sequence or step exhausted; retire before wrap.");
            campaignNatureSequence020++;campaignNatureStep020++;

            CampaignGardenContactPlayer020 playerValue=default;
            if(brush.w>0f&&Player!=null&&campaignFoliageController020!=null)
            {
                ulong playerId=EntityId.ToULong(Player.GetEntityId());
                if(playerId!=0)playerValue=new CampaignGardenContactPlayer020
                {
                    PlayerId=playerId,FeetWorld=new float3(brush.x,vertical.x,brush.y),
                    Radius=brush.z,Height=vertical.y,Accepted=1
                };
            }
            bool forceSilentRebase=campaignNatureForceSilentRebase020;
            bool scheduleHeld=held||forceSilentRebase;
            float scheduleDelta=scheduleHeld?0f:delta;
            bool allowEvents=!scheduleHeld&&!reduced&&playerValue.Accepted==1;
            bool allowMotion=!reduced&&!IsDead&&!IsDowned&&playerValue.Accepted==1;
            var query=default(BoundaryQuery020);
            if(!campaignNatureBinding020.ScheduleFrame020(in playerValue,in query,scheduleDelta,scheduleHeld,allowEvents,allowMotion,
                campaignNatureSequence020,campaignNatureStep020))
                return FailNatureBinding020(campaignNatureBinding020.Failure020);
            campaignNaturePendingElapsed020=elapsed;campaignNaturePendingDelta020=scheduleDelta;
            campaignNaturePendingPlayer020=playerValue.Accepted==1?playerValue.PlayerId:0UL;
            campaignNaturePendingHeld020=scheduleHeld;
            campaignNaturePendingInterrupted020=false;
            campaignNatureLastElapsed020=elapsed;campaignNatureClockSampled020=true;
            campaignNatureForceSilentRebase020=false;
            return campaignNatureWindStrength020;
        }

        static CampaignBrushToken020 ToBrushToken020(in CampaignGardenContactCue020 cue)=>
            new CampaignBrushToken020
            {
                OwnerEpoch=cue.OwnerEpoch,Generation=cue.Generation,WorldRevision=cue.WorldRevision,
                GeometryRevision=cue.GeometryRevision,FoliageRevision=cue.FoliageRevision,Step=cue.Step,
                Serial=cue.Serial,Plant=cue.Plant,Player=cue.Player,
                Position=new Vector3(cue.Position.x,cue.Position.y,cue.Position.z),Strength=cue.Strength,Variant=cue.Variant
            };

        BoundaryScope020 CurrentBoundaryScope020()
        {
            if(campaignNatureBinding020==null||World==null||campaignNatureBindingRoot020==null)return default;
            var expected=campaignNatureBinding020.Scope020;
            if(!World.TryGetPortalLandingPrototype020(World.CampaignHubGeneration020,out var snapshot)||snapshot.Root==null)return default;
            expected.Session=EntityId.ToULong(gameObject.GetEntityId());
            expected.World=EntityId.ToULong(World.gameObject.GetEntityId());
            expected.Root=EntityId.ToULong(snapshot.Root.GetEntityId());
            expected.Generation=World.CampaignHubGeneration020;expected.WorldRevision=World.WorldRevision020;
            expected.GeometryRevision=CampaignLandingObstacleSet020.Revision;
            return expected;
        }

        bool NatureBindingReferencesCurrent020(in CampaignGardenFoliageScope020 expected,bool requireAudio)
        {
            if(!isActiveAndEnabled||!object.ReferenceEquals(GameSession.Instance,this)||!CampaignHubActive020||World==null||
                campaignAtmosphere020==null||!campaignAtmosphere020.Bound020||campaignFoliage020==null||
                !campaignFoliage020.Ready020||!campaignFoliage020.Scope020.Matches(expected)||
                (campaignNatureAttempted020&&!object.ReferenceEquals(World,campaignNatureBindingWorld020))||
                (campaignNatureAttempted020&&!object.ReferenceEquals(campaignFoliage020,campaignNatureBindingFoliage020))||
                (campaignNatureAttempted020&&!object.ReferenceEquals(campaignAtmosphere020,campaignNatureBindingAtmosphere020))||
                (campaignNatureAttempted020&&!object.ReferenceEquals(Audio,campaignNatureBindingAudio020))||
                World.CampaignHubGeneration020!=expected.Generation||World.WorldRevision020!=expected.WorldRevision||
                CampaignLandingObstacleSet020.Revision!=expected.GeometryRevision||
                !World.TryGetPortalLandingPrototype020(expected.Generation,out var snapshot)||snapshot.Root==null||
                EntityId.ToULong(snapshot.Root.GetEntityId())!=expected.WorldToken||
                campaignNatureAttempted020&&!object.ReferenceEquals(snapshot.Root,campaignNatureBindingRoot020))return false;
            if(!campaignAtmosphere020.TryReadPresentationClock020(out _,out uint generation,out uint revision,out _)||
                generation!=expected.Generation||revision!=expected.WorldRevision)return false;
            return !requireAudio||campaignNatureDispatchPrepared020&&Audio!=null&&
                object.ReferenceEquals(Audio,campaignNatureBindingAudio020)&&CampaignNatureDispatchEpoch020==campaignNatureAudioEpoch020;
        }

        float FailNatureBinding020(string reason)
        {
            campaignNatureWindStrength020=0f;
            campaignNatureFailure020=reason;
            RetireCampaignNatureBinding020(true);
            return 0f;
        }

        bool RetireCampaignNatureBinding020(bool suppressSameScopeFailure=false)
        {
            // A failed fence/disposal is retried only through the explicit recovery entry point.
            if(campaignNatureRetireFailed020)return false;
            campaignNatureWindStrength020=0f;
            if(campaignNatureDispatchPrepared020||CampaignNatureDispatchEpoch020!=0)RetireCampaignNatureDispatch020();
            campaignNatureDispatchPrepared020=false;campaignNatureAudioEpoch020=0;
            if(campaignNatureBinding020!=null)
            {
                if(!campaignNatureBinding020.TryRetire020(out var failure))
                {campaignNatureRetireFailed020=true;Debug.LogError("Nature job retirement retained: "+failure);return false;}
                campaignNatureBinding020=null;
            }
            campaignNatureRetireFailed020=false;campaignNatureBindingWorld020=null;campaignNatureBindingRoot020=null;
            campaignNatureBindingFoliage020=null;campaignNatureBindingAtmosphere020=null;campaignNatureBindingAudio020=null;
            campaignNaturePendingPlayer020=0;campaignNaturePendingHeld020=false;
            campaignNaturePendingInterrupted020=campaignNatureForceSilentRebase020=false;
            campaignNatureSequence020=0;campaignNatureStep020=0;campaignNaturePendingElapsed020=campaignNatureLastElapsed020=0d;
            campaignNaturePendingDelta020=0f;campaignNatureClockSampled020=false;
            campaignNatureAttempted020=suppressSameScopeFailure;campaignNatureFailureSuppressed020=suppressSameScopeFailure;
            if(!suppressSameScopeFailure)campaignNatureBindingScope020=default;
            return true;
        }

        internal bool RetryCampaignNatureBindingRetirement020()
        {
            campaignNatureRetireFailed020=false;
            if(!RetireCampaignNatureBinding020())return false;
            campaignNatureAttempted020=false;campaignNatureBindingScope020=default;campaignNatureFailureSuppressed020=false;return true;
        }

        bool ReadNatureClock020(out double elapsed, out uint generation, out uint revision, out bool held)
        {
#if UNITY_EDITOR
            return campaignAtmosphere020.TryReadEditorNatureClock020(out elapsed, out generation, out revision, out held);
#else
            return campaignAtmosphere020.TryReadPresentationClock020(out elapsed, out generation, out revision, out held);
#endif
        }

        static bool FiniteNature020(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted version-2 Campaign World/root; current real local CharacterController capsule; exact 69-floor world-XZ boundary and 128 immutable foliage roots.
// Array Formats: One retained nature job lease; one current contact frame/edge query, two bounded cue tokens, one shared smoothed wind scalar, monotonic IDs only.
// Dependencies/Pointers: Called inside TickCampaignFoliage020 after current capsule capture and before its material update; normal retirement clears stale scope, while a separate failed-scope latch is checked before retired references. Atmosphere owns the only presentation clock; Audio owns wind smoothing and existing 4 ambient/4 cue pools. No gameplay, progression, voice, network, object discovery loop, or native integration is added.





// Editor phase probes use their saved normal timeline; production clock reads and all rewind/scope/freshness fences are unchanged.
