namespace OdysseyOracle
{
    public sealed partial class TutorialDirector
    {
        public void PauseForExternalNarration009()
        {
            if(session==null)return;
            bool npc=session.RefugeActivities!=null&&session.RefugeActivities.NpcDialogueSpeaking020;
            bool mechanics=session.Hud!=null&&session.Hud.IsMechanicsGuideVisible&&session.Paused;
            if(!npc&&!mechanics)return;
            if(guideSourceOwned015)
            {
                bool paused=voicePaused,playing=voice!=null&&voice.isPlaying;
                PauseGuideNarration015();voicePaused=paused||playing;return;
            }
            if(voice!=null&&voice.isPlaying){voice.Pause();voicePaused=true;}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Tutorial voice/paused guide context, no world or lesson progression changes.
// Array Formats: Existing source and pause bool only; no new audio source, clip load or automatic narration.
// Dependencies/Pointers: Explicit mechanics playback pauses the separate narrator immediately; original SyncVoice owns later resumption through real UI/gameplay state, never a new guide autoplay path.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted NPC/guide audio ownership and immutable guide note metadata.
// Array Formats: Existing fixed guide clip/caption/sample stores; no new frame storage or caption generation.
// Dependencies/Pointers: All-role NPC lease pauses automatic guide intent; explicit eligible Play alone preempts/resumes. Root v5 resource/voiced captions remain exact; root owns integration and native/browser listening.
