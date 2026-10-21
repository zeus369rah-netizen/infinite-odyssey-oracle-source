using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        AudioClip npcBram009,npcIlyra009,npcSmith009,npcSpeakingClip009;
        UnityEngine.Object npcVoiceOwner009;
        int npcVoiceRole009,npcVoiceContext009,npcVoiceFrame009=-1;
        float npcCaptionUntil009;
        Text npcCaption009;
        public static string NpcGreetingCaption009(int role)=>role==4?
            "I am Raykon, keeper of this cauldron. Come closer, Oracle. Your staff is a focus, not a decoration. Take the practice staff near the shrine. Learn one spell, then a pair. Choose all four elements for the great Nova. When you are ready, the violet portal will carry your party onward.":role==3?
            "I'm Bram. I watch the portal. Gather your party here, or head through alone when you're ready.":role==2?
            "I'm Ilyra, keeper of these runes. Explore the training alcoves and try your elements. Come back when you're ready to test what you've learned.":
            "Welcome to my forge. Inspect the choices here. If an upgrade is available, its cost is shown before you confirm.";
        void InitializeNpcVoices009(Canvas canvas)
        {
            InitializeMechanicsVoice009();
            InitializeNpcBanter015();
            npcBram009=Resources.Load<AudioClip>("Tutorial/npc_bram_greeting009");
            npcIlyra009=Resources.Load<AudioClip>("Tutorial/npc_ilyra_greeting009");
            npcSmith009=Resources.Load<AudioClip>("Tutorial/Smith015/greeting015");
            if(npcSmith009!=null)npcSmith009.LoadAudioData();
            if(npcIlyra009!=null)npcIlyra009.LoadAudioData();
            if(npcBram009!=null)npcBram009.LoadAudioData();
            if(raykonVoice!=null)raykonVoice.LoadAudioData();
            InitializeNpcSubtitle020(canvas);
        }
        bool NpcVoiceContext009(int role,UnityEngine.Object owner,int context)
        {
            if(owner==null||session==null||session.Hud==null||session.World==null||!session.World.IsTutorialRefuge||session.World.IsCoopArena||!session.HasStarted||session.IsDead||session.IsDowned||session.Victory||!Application.isFocused)return false;
            if(context==1)return (role==2||role==3)&&object.ReferenceEquals(owner,session.RefugeNpcs)&&session.Hud.IsRefugeNpcConversationVisible(owner);
            if(context==4)return role==4&&object.ReferenceEquals(owner,this)&&session.Hud.IsRaykonMentorVisible(this);
            if(role!=1)return false;
            return context==2?object.ReferenceEquals(owner,session.FieldSmith)&&session.FieldSmith!=null&&session.FieldSmith.Visible:
                context==3&&object.ReferenceEquals(owner,session.Shop)&&session.Shop!=null&&session.Shop.Visible;
        }
        public bool TryPlayNpcGreeting009(int role,UnityEngine.Object owner,int context)
        {
            if(Time.frameCount==npcVoiceFrame009||!NpcVoiceContext009(role,owner,context)||voice==null)return false;
            // A newly accepted different speaker interrupts the one old lease,
            // even if that new speaker's repeat-line cooldown is still running.
            if(npcVoiceOwner009!=null&&(!object.ReferenceEquals(owner,npcVoiceOwner009)||role!=npcVoiceRole009))
                StopNpcGreeting009(null);
            if(!TryChooseNpcLine015(role,out AudioClip clip,out string caption))return false;
            StopMechanicsGuide();StopNpcGreeting009(null);
            npcVoiceFrame009=Time.frameCount;npcVoiceOwner009=owner;npcVoiceRole009=role;npcVoiceContext009=context;
            npcVoiceWorld020=session.World;npcSpeakingClip009=clip;
            voice.Stop();voice.clip=clip;
            npcCaptionUntil009=Time.unscaledTime+(clip!=null?clip.length+1f:Mathf.Clamp(caption.Length/12f,6f,30f));
            // The exact one selected resource/caption is retained; no dialogue queue.
            bool audible=clip!=null&&!voice.mute&&voice.volume>0&&AudioListener.volume>0&&!AudioListener.pause;
            if(audible)voice.Play();
            BeginNpcSubtitle020(caption);
            if(NpcDialogueSpeaking020)session.Tutorial?.PauseForExternalNarration009();
            return audible&&voice.isPlaying;
        }
        public bool IsNpcGreetingSpeaking009(int role,UnityEngine.Object owner)=>npcVoiceRole009==role&&object.ReferenceEquals(npcVoiceOwner009,owner)&&
            voice!=null&&npcSpeakingClip009!=null&&voice.clip==npcSpeakingClip009&&voice.isPlaying&&!voice.mute&&voice.volume>0&&AudioListener.volume>0&&!AudioListener.pause&&Application.isFocused;
        public void StopNpcGreeting009(UnityEngine.Object owner)
        {
            if(owner!=null&&!object.ReferenceEquals(owner,npcVoiceOwner009))return;
            RecordCampaignNpcStop020();
            if(voice!=null&&npcSpeakingClip009!=null&&voice.clip==npcSpeakingClip009)voice.Stop();
            npcSpeakingClip009=null;npcVoiceOwner009=null;npcVoiceRole009=npcVoiceContext009=0;npcVoiceWorld020=null;
            HideNpcSubtitle020();
        }
        void TickNpcVoices009()
        {
            TickMechanicsVoice009();
            if(object.ReferenceEquals(npcVoiceOwner009,null))return;
            // Closing the read modal does not revoke the accepted NPC utterance.
            // A destroyed Unity owner still reaches this validity check and clears captions.
            if(!NpcVoiceLeaseValid020()){StopNpcGreeting009(null);return;}
            if(Time.unscaledTime>=npcCaptionUntil009){CompleteCampaignNpcLine020();StopNpcGreeting009(null);return;}
            if(voice!=null&&npcSpeakingClip009!=null&&voice.clip!=npcSpeakingClip009){StopNpcGreeting009(null);return;}
            if(voice!=null&&(voice.mute||voice.volume<=0||AudioListener.volume<=0||AudioListener.pause))voice.Stop();
            TickNpcSubtitle020();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted actor/dialogue context with one cold screen-space subtitle; no world/support change.
// Array Formats: Three cold optional clip refs/exact captions, one owner/context/frame fence; existing single Activity AudioSource reused without sample arrays or per-E allocation.
// Dependencies/Pointers: Only existing accepted lore/Forge/shop hooks play; exit/focus/mute stop without autoplay; missing voices remain subtitle-only. Root alone imports stock licensed/provenance-checked clips; no generated audio, cloned identity or rights guarantee here.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted original NPC conversation or explicit paused guide UI; no actor/root/authority writes.
// Array Formats: Eight cold banter clips with exact captions and four bounded role counters/cooldowns; one existing voice source.
// Dependencies/Pointers: Stock Kokoro/established Branok performances retain provenance; interaction only, accepted speech survives panel close; focus/mute/world/disable stop, no automatic replay.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted refuge/world and existing compact screen-space subtitle.
// Array Formats: One voice/clip/owner/world lease and one exact cached utterance; no queue or runtime line construction.
// Dependencies/Pointers: Strict visible-panel admission; continuation is separate, new speaker/world/disable preempts. Existing licensed resources/captions unchanged; root owns native acceptance.
