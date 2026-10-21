using UnityEngine;
namespace OdysseyOracle
{
    public struct CampaignNatureAudioFrame020
    {
        public uint Epoch,Generation,WorldRevision,GeometryRevision,FoliageRevision,Step;
        public float OuterDistance,Delta;
        public bool BoundaryReady,Held;
        public byte TokenCount;
        public CampaignBrushToken020 First,Second;
    }
    public sealed partial class GameSession
    {
        GameAudioDirector campaignNatureAudioOwner020;
        DungeonWorld campaignNatureAudioWorld020;
        Transform campaignNatureAudioRoot020;
        uint campaignNatureAudioEpoch020,campaignNatureAudioGeneration020,campaignNatureAudioWorldRevision020,
            campaignNatureAudioGeometry020,campaignNatureAudioFoliage020;
        internal uint CampaignNatureDispatchEpoch020=>campaignNatureAudioEpoch020;

        // Called only after the real accepted boundary/foliage publisher is ready, never from steady Tick.
        internal bool PrepareCampaignNatureDispatch020(Transform root,uint geometry,uint foliage)
        {
            RetireCampaignNatureDispatch020();
            if(!isActiveAndEnabled||!CampaignHubActive020||World==null||Audio==null||root==null||geometry==0||foliage==0)return false;
            uint generation=World.CampaignHubGeneration020,revision=World.WorldRevision020;
            if(!World.TryGetPortalLandingPrototype020(generation,out var snapshot)||!object.ReferenceEquals(snapshot.Root,root)||
                !Audio.PrepareCampaignNatureAudio020(this,World,root,generation,revision,geometry,foliage))return false;
            uint epoch=Audio.CampaignNatureAudioEpoch020;
            if(epoch==0){Audio.RetireCampaignNatureAudio020();return false;}
            campaignNatureAudioOwner020=Audio;campaignNatureAudioWorld020=World;campaignNatureAudioRoot020=root;
            campaignNatureAudioEpoch020=epoch;campaignNatureAudioGeneration020=generation;campaignNatureAudioWorldRevision020=revision;
            campaignNatureAudioGeometry020=geometry;campaignNatureAudioFoliage020=foliage;
            return true;
        }
        // Caller has completed a value-only boundary/contact job, retained its exact epoch/revisions,
        // and compared its current foliage publisher reference before reaching this scalar sink.
        internal bool DispatchCampaignNatureAudio020(in CampaignNatureAudioFrame020 frame)
        {
            if(campaignNatureAudioEpoch020==0||!isActiveAndEnabled||!CampaignHubActive020||
                !object.ReferenceEquals(Audio,campaignNatureAudioOwner020)||!object.ReferenceEquals(World,campaignNatureAudioWorld020)||
                frame.Epoch!=campaignNatureAudioEpoch020||frame.Generation!=campaignNatureAudioGeneration020||
                frame.WorldRevision!=campaignNatureAudioWorldRevision020||frame.GeometryRevision!=campaignNatureAudioGeometry020||
                frame.FoliageRevision!=campaignNatureAudioFoliage020||frame.TokenCount>2)
            {RetireCampaignNatureDispatch020();return false;}
            if(!campaignNatureAudioOwner020.TickCampaignNatureAudio020(this,campaignNatureAudioWorld020,campaignNatureAudioRoot020,
                frame.Epoch,frame.Generation,frame.WorldRevision,frame.GeometryRevision,frame.FoliageRevision,frame.Step,
                frame.OuterDistance,frame.Delta,frame.BoundaryReady,frame.Held))
            {RetireCampaignNatureDispatch020();return false;}
            if(frame.TokenCount>0)campaignNatureAudioOwner020.TryPlayCampaignBrush020(this,in frame.First);
            if(frame.TokenCount>1)campaignNatureAudioOwner020.TryPlayCampaignBrush020(this,in frame.Second);
            return true;
        }
        internal void RetireCampaignNatureDispatch020()
        {
            if(campaignNatureAudioOwner020!=null)campaignNatureAudioOwner020.RetireCampaignNatureAudio020();
            campaignNatureAudioOwner020=null;campaignNatureAudioWorld020=null;campaignNatureAudioRoot020=null;
            campaignNatureAudioEpoch020=campaignNatureAudioGeneration020=campaignNatureAudioWorldRevision020=
                campaignNatureAudioGeometry020=campaignNatureAudioFoliage020=0;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact accepted session/World/root/actual Audio reference and completed numeric epoch/generation/world/geometry/foliage scope.
// Array Formats: One cached audio binding, one bounded value frame and at most2transient brush tokens; no per-frame allocation or second update component.
// Dependencies/Pointers: Root must invoke Prepare only after actual publishers, Dispatch once after the owned job fence, and Retire on every invalidation/session-only disable before native authority release; no placeholder boundary/contact caller is supplied.
