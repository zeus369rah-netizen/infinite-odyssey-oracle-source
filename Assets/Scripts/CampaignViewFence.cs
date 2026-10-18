namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure accepted-view highwater; scalar fields only. This is a replay fence,
    // never authentication. Sender identity comes from Room's NGO callback.
    public struct CampaignViewFence
    {
        string challenge,campaign;
        int packet,stage,epoch,revision,phase;
        public void Begin(string localChallenge)
        {this=default;challenge=CampaignQuestState.ValidNonce(localChallenge)?localChallenge:null;}
        public void Clear(){this=default;}
        public bool TryAccept(ulong sender,ulong server,int version,string hello,int sequence,
            CampaignSnapshot state,int capacity,ulong localPeer)
        {
            if(sender!=server||version!=10||challenge==null||hello!=challenge||sequence<=packet||
                !CampaignQuestState.ValidSnapshot(state,capacity))return false;
            bool member=false;
            for(int i=0;i<state.peers.Length;i++)member|=state.peers[i].id==localPeer;
            if(!member)return false;
            if(campaign!=null)
            {
                if(campaign!=state.campaign)
                {
                    if(phase!=(int)CampaignPhase.Complete&&phase!=(int)CampaignPhase.Failed)return false;
                }
                else
                {
                    if(state.stage<stage||state.stage>stage+1||state.epoch<epoch||state.revision<revision)return false;
                    if(state.stage==stage&&state.phase<phase&&
                        !(phase==(int)CampaignPhase.Preparing&&state.phase==(int)CampaignPhase.Refuge))return false;
                    if(state.stage>stage&&(phase!=(int)CampaignPhase.StageClear||state.phase!=(int)CampaignPhase.Refuge))return false;
                }
            }
            campaign=state.campaign;stage=state.stage;epoch=state.epoch;revision=state.revision;phase=state.phase;packet=sequence;
            return true;
        }
    }
}
