using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class TutorialDirector
    {
        readonly GuidePlaybackState015 guidePlayback015=new GuidePlaybackState015();
        readonly AudioClip[] guideClips015=new AudioClip[GuidePlaybackState015.StepCapacity];
        readonly string[] guideCaptions015=new string[GuidePlaybackState015.StepCapacity];
        bool guideSourceOwned015,guideEverPlayed015;
        int staffHintBinding015=int.MinValue;bool staffHintCollected015,staffHintReady015;
        public uint GuidePresentationRevision015=>guidePlayback015.Revision;
        public bool HasGuidePresentation015=>active&&!Finished&&!SuppressAutomaticTutorialPresentation&&guidePlayback015.HasView;
        public bool InstructionMenuBlocksGameplay015=>session!=null&&session.Hud!=null&&session.Hud.IsTutorialInstructionVisible;
        public bool GuideReview015=>guidePlayback015.IsReview;
        public bool GuidePlaying015=>guideSourceOwned015&&guidePlayback015.Status==GuidePlaybackStatus015.Playing&&voice!=null&&voice.isPlaying;
        public bool GuideAudioAvailable015=>guidePlayback015.HasView&&guideClips015[guidePlayback015.ViewKey]!=null;
        public bool GuidePreviousAvailable015=>guidePlayback015.CanReview(-1);
        public bool GuideNextAvailable015=>GuideReview015?guidePlayback015.CanReview(1)||guidePlayback015.LiveKey>=0:HasPendingInstruction;
        public string GuideTitle015=>guidePlayback015.ViewKey>=0&&guidePlayback015.ViewKey<12?TutorialInstructionCards.Title(guidePlayback015.ViewKey):Heading;
        public string GuideCaption015=>guidePlayback015.HasView?guideCaptions015[guidePlayback015.ViewKey]??"":"";
        public string GuideNote015=>guidePlayback015.ViewKey>=0&&guidePlayback015.ViewKey<12?TutorialInstructionCards.Note(guidePlayback015.ViewKey):Instruction;
        public bool NeedsPracticeStaff015=>active&&!Finished&&CurrentLesson==Lesson.Shrine&&session!=null&&session.World!=null&&!session.World.TrainingStaffCollected015;

        void UpdatePracticeStaffHint015()
        {
            bool collected=session.World.TrainingStaffCollected015;int binding=GameActionBindings.LastChangeFrame;
            if(staffHintReady015&&staffHintCollected015==collected&&staffHintBinding015==binding)return;
            staffHintReady015=true;staffHintCollected015=collected;staffHintBinding015=binding;
            Heading=collected?"The green shrine":"Take the practice staff";
            Instruction=collected?"Practice staff collected. Approach the glowing green shrine to meet Raykon.":
                "Find the practice staff beside the green shrine. Look at it and press ["+GameActionBindings.GetLabel("Interact")+"]. Collect it before Raykon's spell practice.";
        }

        void PrepareGuideStep015(int key,AudioClip clip,string caption)
        {
            if(key<0||key>=GuidePlaybackState015.StepCapacity||voice==null)return;
            StopVoice();guideClips015[key]=clip;guideCaptions015[key]=caption;
            guidePlayback015.Activate(key,Time.realtimeSinceStartupAsDouble);
            voice.clip=clip;guideSourceOwned015=true;guideEverPlayed015=false;
        }
        void ResetGuidePlayback015()
        {
            ForgetGuideSource015();guidePlayback015.Reset();staffHintReady015=false;
            System.Array.Clear(guideClips015,0,guideClips015.Length);System.Array.Clear(guideCaptions015,0,guideCaptions015.Length);
        }
        void ForgetGuideSource015(){guideSourceOwned015=guideEverPlayed015=false;}
        bool GuideAudioReady015
        {
            get
            {
                AudioClip clip=GuideAudioAvailable015?guideClips015[guidePlayback015.ViewKey]:null;
                if(!HasGuidePresentation015||voice==null||clip==null||clip.loadState!=AudioDataLoadState.Loaded||voice.mute||narrationVolume<=0||
                    AudioListener.pause||AudioListener.volume<=0||session==null||!session.HasStarted||session.IsDead||session.IsDowned||session.Victory||
                    applicationPaused||session.Audio==null||!session.Audio.GuideGestureObserved015||(GuideAudioAdmission015.ReadState()&7)!=7)return false;
                if(session.RefugeActivities!=null&&session.RefugeActivities.NpcDialogueSpeaking020)return false;
                if(session.Hud!=null&&session.Hud.IsMechanicsGuideVisible)return false;
                return !session.Paused||session.Hud!=null&&session.Hud.IsTutorialInstructionVisible;
            }
        }
        bool SyncGuideVoice015()
        {
            if(!guideSourceOwned015)return false;
            voice.volume=narrationVolume;
            // An accepted NPC lease cancels automatic intent; its end alone cannot resume the guide.
            if(session!=null&&session.RefugeActivities!=null&&session.RefugeActivities.NpcDialogueSpeaking020)
            {PauseGuideNarration015();return true;}
            bool ready=GuideAudioReady015;
            if(guidePlayback015.Status==GuidePlaybackStatus015.Playing)
            {
                if(!ready){PauseGuideNarration015();return true;}
                if(voice.isPlaying)guideEverPlayed015=true;
                if(guideEverPlayed015&&!voice.isPlaying){guidePlayback015.End();voicePaused=false;return true;}
            }
            if(guidePlayback015.TryAdmit(Time.realtimeSinceStartupAsDouble,ready))
            {
                session.RefugeActivities?.StopRaykonGreetingForGuide();
                int samples=guidePlayback015.SavedSamples;
                if(voicePaused)voice.UnPause();
                else {voice.Play();if(samples>0&&samples<voice.clip.samples)voice.timeSamples=samples;}
                voicePaused=false;guideEverPlayed015=voice.isPlaying;
                if(HasPendingInstruction&&!GuideReview015)instructionState.TryStartNarration();
            }
            return true;
        }
        public void PauseGuideNarration015()
        {
            if(!guideSourceOwned015||voice==null)return;
            if(guidePlayback015.Status==GuidePlaybackStatus015.Playing)
            {guidePlayback015.Pause(voice.timeSamples);voice.Pause();voicePaused=true;}
            else guidePlayback015.CancelPending();
        }
        public bool ToggleGuideNarration015()
        {
            if(!HasGuidePresentation015||!GuideAudioAvailable015)return false;
            bool npcSpeaking=session!=null&&session.RefugeActivities!=null&&session.RefugeActivities.NpcDialogueSpeaking020;
            if(GuidePlaying015||guidePlayback015.Status==GuidePlaybackStatus015.Pending&&!npcSpeaking){PauseGuideNarration015();return true;}
            // Explicit UI Play can resume/replay. Merely regaining focus never makes this request.
            if(!Application.isFocused||applicationPaused||(GuideAudioAdmission015.ReadState()&3)!=3)return false;
            session.RefugeActivities?.StopRaykonGreetingForGuide(); // Explicit eligible Play preempts the one NPC lease before readiness.
            if(!guideSourceOwned015){voice.clip=guideClips015[guidePlayback015.ViewKey];guideSourceOwned015=true;guideEverPlayed015=false;}
            guidePlayback015.Request(false,Time.realtimeSinceStartupAsDouble);SyncGuideVoice015();return true;
        }
        public bool ReviewPreviousGuide015()=>ReviewGuide015(-1);
        bool ReviewGuide015(int direction)
        {
            if(!HasGuidePresentation015||!guidePlayback015.TryReviewTarget(direction,out int key))return false;
            PauseGuideNarration015();voice.Stop();voicePaused=false;guidePlayback015.View(key);voice.clip=guideClips015[key];guideSourceOwned015=true;guideEverPlayed015=false;return true;
        }
        public void NextGuide015()
        {
            if(!HasGuidePresentation015)return;
            if(GuideReview015)
            {
                if(ReviewGuide015(1))return;
                PauseGuideNarration015();voice.Stop();voicePaused=false;guidePlayback015.ReturnLive();voice.clip=guideClips015[guidePlayback015.ViewKey];guideSourceOwned015=true;guideEverPlayed015=false;return;
            }
            PauseGuideNarration015();ContinueInstructionCard();
        }
        public void ReturnToLiveGuide015()
        {
            if(!guidePlayback015.IsReview)return;
            PauseGuideNarration015();voice.Stop();voicePaused=false;guidePlayback015.ReturnLive();voice.clip=guideClips015[guidePlayback015.ViewKey];guideSourceOwned015=true;guideEverPlayed015=false;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing original guide source and real accepted tutorial-step presentation; no world/clock/progression writes.
// Array Formats: Cold fixed32clip/caption/sample slots,12card history and32bit once-admission mask; scalar state only in Sync.
// Dependencies/Pointers: Canonical practice acknowledgement/reward state remains separate. Focus/menu/mute pause actual samples; explicit Play alone resumes. Browser bridge only observes real activation/context, no unlock or simulated consent.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted NPC/guide audio ownership and immutable guide note metadata.
// Array Formats: Existing fixed guide clip/caption/sample stores; no new frame storage or caption generation.
// Dependencies/Pointers: All-role NPC lease pauses automatic guide intent; explicit eligible Play alone preempts/resumes. Root v5 resource/voiced captions remain exact; root owns integration and native/browser listening.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted guide/menu Canvas and actual guide view/audio state.
// Array Formats: Existing bounded UI/state stores; cold immutable65status labels, no frame formatting.
// Dependencies/Pointers: Original restart/portal/settings/narration/voice/input/achievement owners remain. Root owns source apply and full compile/native/browser/listening acceptance.
