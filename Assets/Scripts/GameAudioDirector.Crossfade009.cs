using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        float musicFadeDuration009,musicFadeOutgoingStart015=1f;
        int musicFadeStartSample009,musicFadeFrequency009;
        bool musicStartupFade009,musicFadeSameAsset009,playlistHasGesture009;
        void StartMusicCrossfade009(float outgoingRemaining,AudioClip incoming,bool sameAsset)
        {
            musicFadeOutgoingStart015=Mathf.Clamp01(musicGain);musicStartupFade009=false;musicFadeElapsed=0;musicFadeDuration009=MusicCrossfadeMath009.Duration(outgoingRemaining,MusicCrossfadeMath009.Remaining(incoming.samples,musicNext.timeSamples,incoming.frequency));musicFadeSameAsset009=sameAsset;
            musicFadeStartSample009=musicNext.timeSamples;musicFadeFrequency009=incoming.frequency;
        }
        void StartMusicFadeIn009(AudioClip incoming)
        {
            musicStartupFade009=true;musicFadeElapsed=0;musicFadeDuration009=MusicCrossfadeMath009.StartupDuration(MusicCrossfadeMath009.Remaining(incoming.samples,music.timeSamples,incoming.frequency));musicFadeSameAsset009=false;
            musicFadeStartSample009=music.timeSamples;musicFadeFrequency009=incoming.frequency;musicGain=0;musicNextGain=0;
        }
        void AdvanceMusicCrossfade009()
        {
            float elapsed=MusicCrossfadeMath009.SampleElapsed(musicNext.timeSamples,musicFadeStartSample009,musicFadeFrequency009);
            // A stopped incoming clip cannot hold this scalar lifetime indefinitely after a long engine stall.
            if(!musicNext.isPlaying)elapsed=musicFadeDuration009;
            musicFadeElapsed=Mathf.Max(musicFadeElapsed,elapsed);var gains=MusicCrossfadeMath009.Gains(musicFadeElapsed,musicFadeDuration009,musicFadeSameAsset009);musicGain=musicFadeOutgoingStart015*gains.Outgoing;musicNextGain=gains.Incoming;
        }
        bool TickMusicFadeIn009()
        {
            if(!musicStartupFade009)return false;
            if(music==null||music.clip==null){CancelMusicFade009();return false;}
            float elapsed=MusicCrossfadeMath009.SampleElapsed(music.timeSamples,musicFadeStartSample009,musicFadeFrequency009);
            if(!music.isPlaying)elapsed=musicFadeDuration009;
            musicFadeElapsed=Mathf.Max(musicFadeElapsed,elapsed);musicGain=MusicCrossfadeMath009.Gains(musicFadeElapsed,musicFadeDuration009,false).Incoming;musicNextGain=0;
            if(musicFadeElapsed>=musicFadeDuration009){musicStartupFade009=false;musicGain=1;}
            return true;
        }
        void CancelMusicFade009(){musicStartupFade009=false;musicFadeElapsed=musicFadeDuration009=0;musicFadeStartSample009=musicFadeFrequency009=0;musicFadeSameAsset009=false;}
        float MusicRemaining009(AudioSource source)
        {return source!=null&&source.clip!=null?MusicCrossfadeMath009.Remaining(source.clip.samples,source.timeSamples,source.clip.frequency):0;}
        void ApplyMusicIntroOffset009(AudioSource source,AudioClip clip)
        {
            float offset=MusicCrossfadeMath009.IntroOffset(clip.length,playlist009.Current>=0,playlistShortIntros009);
            if(offset>0&&clip.frequency>0&&clip.samples>1)source.timeSamples=Mathf.Clamp(Mathf.RoundToInt(offset*clip.frequency),0,clip.samples-1);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing2D two-source music only; public timeSamples/frequency read actual playback position, never audio PCM/global Time mutation.
// Array Formats: Fixed sample/frequency/elapsed/duration/gesture scalars plus pure two-float gains; no new sources/objects/arrays or repeated allocations.
// Dependencies/Pointers: Existing focus gate pauses sources before these methods; gains retain Master/Music/narration duck. Root native listening/performance/browser/import gates remain. Music Off/user pause hold scalar fade and sample positions; lifecycle stop alone cancels. A forced opening advance retains the outgoing startup gain without a jump.
