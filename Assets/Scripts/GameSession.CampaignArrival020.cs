using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        DungeonWorld campaignArrivalWorld020;
        CampaignArrivalReceipt020 campaignArrivalReceipt020;
        CampaignArrivalPresentation020 campaignArrivalPresentation020;
        public uint CampaignArrivalAcceptedRevision020=>campaignArrivalReceipt020.Revision;
        public CampaignArrivalPresentation020 CampaignArrivalPresentation020=>campaignArrivalPresentation020;
        partial void CampaignHubArrivalAccepted020(uint generation,uint revision)
        {
            if(!ReferenceEquals(Instance,this)||!isActiveAndEnabled||!HasStarted||World==null||!CampaignHubActive020||!World.CampaignHubActive020||
                generation==0||generation!=World.CampaignHubGeneration020||revision==0||revision!=World.WorldRevision020)return;
            if(!ReferenceEquals(campaignArrivalWorld020,World)){campaignArrivalWorld020=World;campaignArrivalReceipt020.Reset();}
            if(!campaignArrivalReceipt020.Accept(generation,revision))return;
            // Receipt belongs to session, surviving presentation-component loss or missing resources.
            try
            {
                if(campaignArrivalPresentation020==null)campaignArrivalPresentation020=gameObject.AddComponent<CampaignArrivalPresentation020>();
                campaignArrivalPresentation020.PlayAccepted(this,generation,revision);
            }
            finally{Audio?.TryPlayCampaignArrival020(World.PlayerSpawn,generation,revision);}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact accepted offline session/world generation/revision and authoritative teleported spawn used only for pooled cue position.
// Array Formats: One World reference,8byte receipt and cached presentation adapter; one cold first-acceptance component allocation, reused afterward.
// Dependencies/Pointers: Refreshed router declares/calls this optional partial only after successful commit/teleport/lease/Resume. Root owns GameAudioDirector's prioritized method/resource. No Tick/menu/focus/rebind replay, reward/save/input or baseline atmosphere mutation.
