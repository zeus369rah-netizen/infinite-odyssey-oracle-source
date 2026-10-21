using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        bool raykonGreetingPlayed;
        int raykonOpenedFrame=-1;
        public bool RaykonGreetingAvailable=>raykonVoice!=null;
        public bool RaykonGreetingPlayed=>raykonGreetingPlayed;
        public bool RaykonGreetingSpeaking=>voice!=null&&voice.isPlaying&&
            (raykonVoice!=null&&voice.clip==raykonVoice||npcVoiceRole009==4&&npcSpeakingClip009!=null&&voice.clip==npcSpeakingClip009);
        public void StopRaykonGreetingForGuide(){StopNpcGreeting009(null);SyncRaykonMouth();}
        private void SyncRaykonMouth()
        {
            bool speaking=RaykonGreetingSpeaking&&!voice.mute&&voice.volume>0f&&AudioListener.volume>0f&&!AudioListener.pause&&Application.isFocused;
            session?.World?.SetRaykonSpeaking(speaking);
        }
        public void UseGuideStationNarration()
        {
            // The guide replaces the two automatic station intros. Raykon's real
            // interaction retains its independent once-per-session actor greeting.
            voicesPlayed|=3;StopNpcGreeting009(null);if(voice!=null)voice.Stop();
        }
        public bool CanTalkToRaykon()
        {
            if(session==null||!session.HasStarted||!session.IsPlaying||!Application.isFocused||Cursor.lockState!=CursorLockMode.Locked||
                session.IsDead||session.IsDowned||session.Victory||session.Player==null||session.Player.ViewCamera==null||session.World==null||
                AbilityBindings.IsRebinding||GameActionBindings.IsRebinding||(session.Builder!=null&&session.Builder.IsPlacing)||
                (session.Revival!=null&&session.Revival.IsHelping)||(session.Shop!=null&&session.Shop.Visible)||
                (session.FieldSmith!=null&&session.FieldSmith.Visible)||(session.Defense!=null&&session.Defense.Running)||
                !session.World.TryGetRaykonInteractionTarget(out var root,out var feet,out _))return false;
            Vector3 delta=session.Player.transform.position-feet;
            if(delta.sqrMagnitude>3.25f*3.25f)return false;
            Transform eye=session.Player.ViewCamera.transform;
            // First actual collider must belong to the supported Raykon actor. No
            // through-wall dot-only interaction, invisible Ilyra target or scene search.
            return Physics.Raycast(eye.position,eye.forward,out RaycastHit hit,3.65f,~(1<<2),QueryTriggerInteraction.Ignore)&&
                (hit.transform==root||hit.transform.IsChildOf(root));
        }
        public bool TryTalkToRaykon(bool playGreeting=true)
        {
            if(Time.frameCount==raykonOpenedFrame||!CanTalkToRaykon()||!session.Hud.TryShowRaykonMentor(this))return false;
            raykonOpenedFrame=Time.frameCount;session.World.BeginRaykonTalk();
            if(playGreeting&&TryPlayNpcGreeting009(4,this,4))raykonGreetingPlayed=true;
            // Existing canonical role2, unordered relative to Smith. Only an
            // accepted host snapshot changes visit/skill permission; no local bits.
            session.FieldSmith?.BoundCampaign?.RequestRaykonMentorVisit();
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual supported Raykon world root/feet, metre/Y-up local reach and first-hit collider gate on main thread.
// Array Formats: One once-per-session greeting flag/frame fence and existing-source speaking bool; cached World/body MPB edge adapter, no sample array or phoneme data.
// Dependencies/Pointers: World owns actor, FieldSmith borrows canonical campaign, host independently validates role2 pose/facing/LOS. No local quest, currency, native or combat mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted original NPC conversation or explicit paused guide UI; no actor/root/authority writes.
// Array Formats: Eight cold banter clips with exact captions and four bounded role counters/cooldowns; one existing voice source.
// Dependencies/Pointers: Stock Kokoro/established Branok performances retain provenance; interaction only, close/focus/mute stop, no automatic replay.
