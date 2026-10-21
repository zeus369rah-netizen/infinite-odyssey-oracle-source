using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        static readonly string[] CampaignSoundPaths020={
            "CampaignIntro020/Sfx/rain020","CampaignIntro020/Sfx/blood_storm020","CampaignIntro020/Sfx/portal020",
            "CampaignIntro020/Sfx/crystal_shatter020","CampaignIntro020/Sfx/page020","CampaignIntro020/Sfx/ward020","CampaignIntro020/Sfx/heartstone020"
        };
        readonly AudioClip[] campaignSounds020=new AudioClip[7];
        AudioSource campaignVoice020,campaignRain020,campaignStorm020,campaignShot020;
        bool campaignAudioPrepared020,campaignAudioOwned020,campaignAudioPaused020,campaignVoicePaused020,campaignVoicePending020,campaignVoiceStarted020;
        bool campaignEffectFirst020,campaignEffectSecond020;
        int campaignAudioPage020;
        float campaignRainGain020,campaignStormGain020,campaignShotGain020;
        double campaignVoiceDeadline020;
        public bool CampaignIntroVoicePending020=>campaignVoicePending020;
        public bool CampaignIntroAudioPaused020=>campaignAudioPaused020;
        public bool CampaignIntroVoicePlaying020=>campaignAudioOwned020&&!campaignAudioPaused020&&campaignVoice020!=null&&campaignVoice020.isPlaying;
        public bool CampaignIntroAudioSuspended020=>applicationPaused||!Application.isFocused||!isActiveAndEnabled;
        bool CampaignIntroSpeechDucking020=>CampaignIntroVoicePlaying020&&campaignVoice020.volume>0f&&!campaignVoice020.mute;
        bool CampaignIntroAudioContext020=>initialized&&campaignAudioOwned020&&session!=null&&session.Hud!=null&&
            session.Hud.CampaignBookVisible020&&!session.HasStarted&&!campaignAudioPaused020&&
            isActiveAndEnabled&&!applicationPaused&&Application.isFocused&&gestureAuthorized&&GuideGestureObserved015;
        bool CampaignIntroNarrationReady020=>CampaignIntroAudioContext020&&masterVolume>0f&&AudioListener.volume>0f&&!AudioListener.pause&&
            session.Tutorial!=null&&session.Tutorial.VoiceVolume>0f&&(GuideAudioAdmission015.ReadState()&7)==7;

        public void PrepareCampaignIntroAudio020()
        {
            if(campaignAudioPrepared020||!initialized)return;
            campaignAudioPrepared020=true;
            campaignVoice020=Source("Campaign prologue narration020",false,32);
            campaignRain020=Source("Campaign prologue rain020",false,240);
            campaignStorm020=Source("Campaign prologue storm020",false,240);
            campaignShot020=Source("Campaign prologue one shot020",false,176);
            campaignRain020.loop=campaignStorm020.loop=true;
            for(int i=0;i<campaignSounds020.Length;i++){campaignSounds020[i]=Resources.Load<AudioClip>(CampaignSoundPaths020[i]);Prime(campaignSounds020[i]);}
            campaignRain020.clip=campaignSounds020[0];campaignStorm020.clip=campaignSounds020[1];
        }
        public void BeginCampaignIntroAudio020()
        {
            PrepareCampaignIntroAudio020();StopCampaignIntroAudio020();
            if(!campaignAudioPrepared020)return;
            campaignAudioOwned020=true;campaignAudioPaused020=false;
            campaignRainGain020=campaignStormGain020=0f;
        }
        public void SetCampaignIntroPage020(int page,AudioClip clip,bool paused)
        {
            if(!campaignAudioOwned020||campaignVoice020==null||page<0||page>=10)return;
            campaignVoice020.Stop();campaignVoice020.clip=clip;campaignVoicePaused020=campaignVoiceStarted020=false;
            campaignAudioPage020=page;campaignEffectFirst020=campaignEffectSecond020=false;
            campaignAudioPaused020=paused;
            campaignVoicePending020=!paused&&clip!=null&&session!=null&&session.Tutorial!=null&&session.Tutorial.VoiceVolume>0f&&masterVolume>0f;
            campaignVoiceDeadline020=Time.realtimeSinceStartupAsDouble+3d;
            if(!paused)PlayCampaignShot020(4,.025f);
            TickCampaignIntroAudio020(0f,0f);
        }
        public void TickCampaignIntroAudio020(float delta,float pageTime)
        {
            if(!campaignAudioOwned020)return;
            if(CampaignIntroAudioSuspended020){PauseCampaignIntroAudio020();return;}
            if(campaignVoicePending020&&Time.realtimeSinceStartupAsDouble>campaignVoiceDeadline020)campaignVoicePending020=false;
            if(!CampaignIntroAudioContext020)return;
            if(campaignVoicePending020)
            {
                if(CampaignIntroNarrationReady020&&campaignVoice020.clip!=null&&campaignVoice020.clip.loadState==AudioDataLoadState.Loaded)
                {campaignVoicePending020=false;if(campaignVoicePaused020)campaignVoice020.UnPause();else campaignVoice020.Play();campaignVoicePaused020=false;campaignVoiceStarted020=true;}
            }
            float blend=1f-Mathf.Exp(-Mathf.Clamp(delta,0f,.1f)/.8f);
            campaignRainGain020=Mathf.Lerp(campaignRainGain020,campaignAudioPage020<5?.035f:0f,blend);
            campaignStormGain020=Mathf.Lerp(campaignStormGain020,campaignAudioPage020>=5?.027f:0f,blend);
            if(CanPlayCue()&&(GuideAudioAdmission015.ReadState()&7)==7)
            {
                if(campaignRainGain020>.0001f&&campaignRain020.clip!=null&&!campaignRain020.isPlaying)campaignRain020.Play();
                if(campaignStormGain020>.0001f&&campaignStorm020.clip!=null&&!campaignStorm020.isPlaying)campaignStorm020.Play();
            }
            if(campaignRainGain020<=.0001f&&campaignRain020.isPlaying)campaignRain020.Stop();
            if(campaignStormGain020<=.0001f&&campaignStorm020.isPlaying)campaignStorm020.Stop();
            if(!campaignEffectFirst020&&pageTime>=(campaignAudioPage020==9?2f:1.8f))
            {
                campaignEffectFirst020=true;
                if(campaignAudioPage020==0)PlayCampaignShot020(2,.045f);
                else if(campaignAudioPage020==5)PlayCampaignShot020(3,.035f);
                else if(campaignAudioPage020==7)PlayCampaignShot020(5,.035f);
                else if(campaignAudioPage020==9)PlayCampaignShot020(6,.04f);
            }
            float shatterTail=1.8f+(campaignSounds020[3]!=null?campaignSounds020[3].length:2.68f)+.25f;
            if(campaignAudioPage020==5&&!campaignEffectSecond020&&pageTime>=shatterTail)
            {campaignEffectSecond020=true;PlayCampaignShot020(5,.035f);}
            ApplyCampaignIntroVolumes020();
        }
        void PlayCampaignShot020(int key,float gain)
        {
            if(!CampaignIntroAudioContext020||!CanPlayCue()||(GuideAudioAdmission015.ReadState()&7)!=7||key<0||key>=campaignSounds020.Length||campaignSounds020[key]==null||campaignSounds020[key].loadState!=AudioDataLoadState.Loaded)return;
            campaignShot020.Stop();campaignShot020.clip=campaignSounds020[key];campaignShotGain020=gain;campaignShot020.volume=sfxVolume*gain;campaignShot020.Play();
        }
        public void PauseCampaignIntroAudio020()
        {
            if(!campaignAudioOwned020||campaignAudioPaused020)return;
            campaignAudioPaused020=true;campaignVoicePending020=false;
            campaignVoicePaused020=campaignVoice020!=null&&campaignVoice020.isPlaying;
            if(campaignVoicePaused020)campaignVoice020.Pause();
            if(campaignRain020!=null)campaignRain020.Pause();if(campaignStorm020!=null)campaignStorm020.Pause();if(campaignShot020!=null)campaignShot020.Stop();
        }
        public void ResumeCampaignIntroAudio020()
        {
            if(!campaignAudioOwned020||!Application.isFocused||applicationPaused)return;
            campaignAudioPaused020=false;
            if(campaignVoice020!=null&&campaignVoice020.clip!=null&&!campaignVoice020.isPlaying&&(!campaignVoiceStarted020||campaignVoicePaused020))
            {campaignVoicePending020=true;campaignVoiceDeadline020=Time.realtimeSinceStartupAsDouble+3d;}
            if(campaignRain020!=null)campaignRain020.UnPause();if(campaignStorm020!=null)campaignStorm020.UnPause();
        }
        void ApplyCampaignIntroVolumes020()
        {
            if(!campaignAudioPrepared020)return;
            if(campaignVoice020!=null)campaignVoice020.volume=session!=null&&session.Tutorial!=null?session.Tutorial.VoiceVolume:0f;
            if(campaignRain020!=null)campaignRain020.volume=sfxVolume*campaignRainGain020*duck;
            if(campaignStorm020!=null)campaignStorm020.volume=sfxVolume*campaignStormGain020*duck;
            if(campaignShot020!=null)campaignShot020.volume=sfxVolume*campaignShotGain020;
        }
        public void StopCampaignIntroAudio020()
        {
            campaignAudioOwned020=campaignAudioPaused020=campaignVoicePaused020=campaignVoicePending020=campaignVoiceStarted020=false;
            campaignRainGain020=campaignStormGain020=0f;
            if(campaignVoice020!=null){campaignVoice020.Stop();campaignVoice020.clip=null;}
            if(campaignRain020!=null)campaignRain020.Stop();if(campaignStorm020!=null)campaignStorm020.Stop();if(campaignShot020!=null)campaignShot020.Stop();
        }
        void DisposeCampaignIntroAudio020()
        {
            StopCampaignIntroAudio020();
            if(campaignVoice020!=null)Destroy(campaignVoice020.gameObject);
            if(campaignRain020!=null)Destroy(campaignRain020.gameObject);
            if(campaignStorm020!=null)Destroy(campaignStorm020.gameObject);
            if(campaignShot020!=null)Destroy(campaignShot020.gameObject);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing main-menu audio/mixer/gesture context; four cold nonspatial sources, no world or authoritative state.
// Array Formats: Seven shared SFX clips, one narrator/two ambience/one one-shot channel and scalar admission/cue/fade state.
// Dependencies/Pointers: Existing Master listener, SFX/narration gain and music duck remain canonical. Focus/application pause cancels pending intent; only explicit Resume restarts a real paused lease. Resources remain root-owned.
