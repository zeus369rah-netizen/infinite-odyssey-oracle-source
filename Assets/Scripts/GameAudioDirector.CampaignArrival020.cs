using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        AudioClip campaignArrivalClip020;
        DungeonWorld campaignArrivalAudioWorld020;
        uint campaignArrivalAudioRevision020;
        void PrepareCampaignArrival020()
        {campaignArrivalClip020=Resources.Load<AudioClip>("Audio/WorldTransitions020/portal_arrival020");Prime(campaignArrivalClip020);}
        public bool TryPlayCampaignArrival020(Vector3 position,uint generation,uint revision)
        {
            if(session==null||session.World==null||!session.CampaignHubActive020||generation==0||revision==0||
                generation!=session.World.CampaignHubGeneration020||revision!=session.World.WorldRevision020)return false;
            if(!ReferenceEquals(campaignArrivalAudioWorld020,session.World))
            {campaignArrivalAudioWorld020=session.World;campaignArrivalAudioRevision020=0;}
            if(revision<=campaignArrivalAudioRevision020)return false;
            campaignArrivalAudioRevision020=revision;
            if(!CanPlayCue()||campaignArrivalClip020==null||campaignArrivalClip020.loadState!=AudioDataLoadState.Loaded)return false;
            return PlayPrioritizedCue(campaignArrivalClip020,position,false,.18f,3,.1f);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Positive accepted offline hub revision/generation and borrowed World identity; listener-relative arrival cue.
// Array Formats: One cold clip reference, one borrowed World and one monotonic consumed revision; existing four prioritized audio voices.
// Dependencies/Pointers: Existing focus, gesture, pause, volume and loaded-clip gate remain. Missing/muted media consumes once without retries or extra sources; root accepts actual native/browser listening separately.
