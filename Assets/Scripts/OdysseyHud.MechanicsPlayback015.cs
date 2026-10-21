namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        UnityEngine.UI.Button mechanicsPlaybackButton015,mechanicsTranscriptButton015;
        UnityEngine.UI.Text mechanicsPlaybackLabel015,mechanicsTranscriptLabel015;
        bool mechanicsTranscriptExpanded015;
        void RefreshMechanicsPlayback015()
        {
            if(mechanicsPlaybackLabel015==null||mechanicsGuideOverview009==null)return;
            string playback=session.RefugeActivities!=null&&session.RefugeActivities.MechanicsGuidePlaying015?"PAUSE NARRATION":"PLAY NARRATION";
            if(mechanicsPlaybackLabel015.text!=playback)mechanicsPlaybackLabel015.text=playback;
            string transcript=mechanicsTranscriptExpanded015?"HIDE TRANSCRIPT":"SHOW TRANSCRIPT";
            if(mechanicsTranscriptLabel015.text!=transcript)mechanicsTranscriptLabel015.text=transcript;
            string caption=mechanicsTranscriptExpanded015?RefugeActivityDirector.MechanicsGuideCaption009:MechanicsGuideOverview009;
            if(mechanicsGuideOverview009.text!=caption)mechanicsGuideOverview009.text=caption;
            mechanicsGuideOverview009.fontSize=mechanicsTranscriptExpanded015?18:20;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing optional mechanics menu and prebuilt element cards, no automatic modal or audio source.
// Array Formats: Fixed cached control references and collapsed transcript flag, constant original caption strings.
// Dependencies/Pointers: Existing Activity source owns real Pause/UnPause. No playlist/mix/context/pointer/phase authority changes.
