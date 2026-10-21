using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        NibwickRuntime020 campaignNpcResultOwner020;
        uint campaignNpcToken020;
        NibwickVoiceResult020 campaignNpcResult020;

        bool CampaignNpcLeaseValid020()=>npcVoiceContext009==5&&npcVoiceRole009==0&&
            npcVoiceOwner009 is NibwickRuntime020 owner&&session!=null&&session.Nibwick020==owner&&
            owner.LeaseCurrent020(owner.Generation020)&&object.ReferenceEquals(session.World,npcVoiceWorld020);
        public bool TryBeginCampaignNpcLine020(NibwickRuntime020 owner,uint generation,uint token,int line,AudioClip clip,string caption)
        {
            if(owner==null||session==null||session.Nibwick020!=owner||!session.CampaignHubActive020||
                !isActiveAndEnabled||voice==null||!Application.isFocused||Time.frameCount==npcVoiceFrame009||
                token==0||!owner.LeaseCurrent020(generation)||!owner.CanAdmitLine020(token,line)||
                caption!=NibwickLines020.Caption(line)||string.IsNullOrEmpty(caption))return false;
            // Positive Campaign context is disjoint from every tutorial role path.
            // Existing guide pause cancels pending playback before this new lease.
            session.Tutorial?.PauseGuideNarration015();StopMechanicsGuide();StopNpcGreeting009(null);
            npcVoiceFrame009=Time.frameCount;npcVoiceOwner009=owner;npcVoiceRole009=0;npcVoiceContext009=5;
            npcVoiceWorld020=session.World;npcSpeakingClip009=clip;
            campaignNpcResultOwner020=owner;campaignNpcToken020=token;campaignNpcResult020=NibwickVoiceResult020.Accepted;
            voice.Stop();voice.clip=clip;
            npcCaptionUntil009=Time.unscaledTime+(clip!=null?clip.length+.4f:Mathf.Clamp(caption.Length/12f,6f,30f));
            if(clip!=null&&!voice.mute&&voice.volume>0&&AudioListener.volume>0&&!AudioListener.pause)voice.Play();
            BeginNpcSubtitle020(caption);
            if(NpcDialogueSpeaking020)session.Tutorial?.PauseForExternalNarration009();
            return true; // Accepted audio OR exact subtitle-only lease; no queue.
        }
        public NibwickVoiceResult020 CampaignNpcResult020(NibwickRuntime020 owner,uint token)
        {return token!=0&&campaignNpcResultOwner020==owner&&campaignNpcToken020==token?campaignNpcResult020:NibwickVoiceResult020.None;}
        public bool IsCampaignNpcSpeaking020(NibwickRuntime020 owner,uint token)=>
            CampaignNpcResult020(owner,token)==NibwickVoiceResult020.Accepted&&object.ReferenceEquals(npcVoiceOwner009,owner)&&NpcDialogueSpeaking020;
        void RecordCampaignNpcStop020()
        {if(npcVoiceContext009==5&&campaignNpcResult020==NibwickVoiceResult020.Accepted)campaignNpcResult020=NibwickVoiceResult020.Interrupted;}
        void CompleteCampaignNpcLine020()
        {if(npcVoiceContext009==5&&campaignNpcResult020==NibwickVoiceResult020.Accepted)campaignNpcResult020=NibwickVoiceResult020.Completed;}
        public void ReleaseCampaignNpcResult020(NibwickRuntime020 owner)
        {if(campaignNpcResultOwner020!=owner)return;campaignNpcResultOwner020=null;campaignNpcToken020=0;campaignNpcResult020=NibwickVoiceResult020.None;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Strict positive current Campaign hub/Nibwick owner admission and existing compact screen-space subtitle; tutorial role/catalog paths remain narrow.
// Array Formats: One existing Activity AudioSource, one exact borrowed clip/caption, one owner/token/result record; no queue, voice clone, sample arrays or new mixer voice.
// Dependencies/Pointers: New accepted line/explicit guide preempts common lease; natural deadline and interruption are distinct. Reading close alone does not revoke. Root owns actual resource import/listening; absence/mute uses bounded exact subtitle-only continuation.
