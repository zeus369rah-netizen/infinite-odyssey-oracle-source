namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        public void PauseMechanicsGuide015()
        {
            if(!mechanicsGuideVoiceOwned009||mechanicsGuidePaused015||voice==null||voice.clip!=mechanicsGuideClip009)return;
            voice.Pause();mechanicsGuidePaused015=true;session?.Hud?.SetMechanicsVoiceCaption009(this,false);
        }
        public bool ToggleMechanicsGuide015()
        {
            if(MechanicsGuidePlaying015){PauseMechanicsGuide015();return true;}
            if(!MechanicsVoiceContext009||(GuideAudioAdmission015.ReadState()&7)!=7)return false;
            if(mechanicsGuideVoiceOwned009&&mechanicsGuidePaused015&&voice!=null&&voice.clip==mechanicsGuideClip009)
            {
                if(voice.mute||voice.volume<=0||UnityEngine.AudioListener.volume<=0||UnityEngine.AudioListener.pause)return false;
                voice.UnPause();mechanicsGuidePaused015=false;session.Hud.SetMechanicsVoiceCaption009(this,true);return true;
            }
            return PlayMechanicsGuide();
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing explicit Mechanics Guide and exact original Activity voice source only.
// Array Formats: Existing source ownership plus one pause bool; AudioSource.Pause/UnPause preserve actual sample position.
// Dependencies/Pointers: Focus/mute suspend without autoplay; explicit actual UI Play alone resumes/replays. Menu exit retains original Stop; no new source/context/music call or reward mutation.
