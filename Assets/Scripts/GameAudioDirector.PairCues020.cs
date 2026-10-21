using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        static readonly string[] PairCueResources020={"Audio/ComboFeedback020/combustion020","Audio/ComboFeedback020/mud020","Audio/ComboFeedback020/steam020","Audio/Elements/element_crystal","Audio/Elements/element_magma","Audio/Elements/element_ice"};
        readonly AudioClip[] pairCueClips020=new AudioClip[6];
        void PreparePairCues020()
        {for(int i=0;i<6;i++){if(pairCueClips020[i]==null)pairCueClips020[i]=Resources.Load<AudioClip>(PairCueResources020[i]);Prime(pairCueClips020[i]);}}
        public bool SetPairCue020(int combo,AudioClip clip)
        {if(combo<1||combo>6)return false;pairCueClips020[combo-1]=clip;Prime(clip);return true;}
        AudioClip PairCue020(int element,int combo)
        {if(!SpellPairPresentation020.Allows(combo,element))return null;var clip=pairCueClips020[combo-1];return clip!=null&&clip.loadState==AudioDataLoadState.Loaded?clip:null;}
        public void PlayPairCast020(int element,int combo)
        {
            AudioClip clip=PairCue020(element,combo);if(clip==null){PlayCast(element);return;}
            if(!CanPlayCue()||session==null||!session.IsPlaying||!session.CombatUnlocked||Time.unscaledTime<nextCastAt)return;
            nextCastAt=Time.unscaledTime+.10f;cast.Stop();cast.clip=clip;cast.pitch=1;activeCastGain=.075f;cast.volume=sfxVolume*activeCastGain;cast.Play();
        }
        public void PlayPairRemoteCast020(int element,int combo,Vector3 position)
        {
            AudioClip clip=PairCue020(element,combo);if(clip==null){PlayRemoteCast(element,position);return;}
            if(!CanPlayCue()||session==null||!session.SimulationActive||!session.CombatUnlocked||!Near(position,30f)||Time.unscaledTime<nextRemoteSpell)return;
            if(PlayPrioritizedCue(clip,position,true,.055f,1,.05f))nextRemoteSpell=Time.unscaledTime+.06f;
        }
        public bool PlayPairImpact020(int element,int combo,Vector3 position)
        {
            AudioClip clip=PairCue020(element,combo);if(clip==null)return PlayElementImpact(element,position);
            if(!CanPlayCue()||session==null||!session.SimulationActive||!session.CombatUnlocked||!Near(position,30f))return false;
            if(ElementImpactPlaybackAttempts<int.MaxValue)ElementImpactPlaybackAttempts++;
            bool played=PlayPrioritizedCue(clip,position,true,.075f,2,.015f);if(played&&ElementImpactPlaybackAccepted<int.MaxValue)ElementImpactPlaybackAccepted++;return played;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing own 2D cast source and30m world spatial cues from accepted casts/landings.
// Array Formats: Six cold borrowed clip references: three root-generated ComboFeedback020 cues and three retained Element cues; existing four prioritized cue voices, same gains/priorities/cadences and counters.
// Dependencies/Pointers: Missing/unloaded/invalid pair data falls back to the original single-element owner. Protected-pool rejection is terminal. Root imports/verifies new clips and native/browser delivery; original38 catalog entries and media remain unchanged here.
