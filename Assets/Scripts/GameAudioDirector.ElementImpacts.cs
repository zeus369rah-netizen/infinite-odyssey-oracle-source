using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        static readonly string[] ElementImpactResources={"Audio/SpellImpact009/fire_impact","Audio/SpellImpact009/water_impact","Audio/SpellImpact009/earth_impact","Audio/SpellImpact009/air_impact"};
        readonly AudioClip[] elementImpactClips=new AudioClip[4];
        public int ElementImpactPlaybackAttempts{get;private set;}
        public int ElementImpactPlaybackAccepted{get;private set;}
        void LoadElementImpactClips()
        {for(int i=0;i<4;i++){elementImpactClips[i]=Resources.Load<AudioClip>(ElementImpactResources[i]);Prime(elementImpactClips[i]);}PreparePairCues020();}
        public bool PlayElementImpact(int element,Vector3 position)
        {
            if(element<0||element>3||!CanPlayCue()||session==null||!session.SimulationActive||!session.CombatUnlocked||!Near(position,30f))return false;
            if(ElementImpactPlaybackAttempts<int.MaxValue)ElementImpactPlaybackAttempts++;
            AudioClip clip=elementImpactClips[element];
            if(clip==null||clip.loadState!=AudioDataLoadState.Loaded)return false;
            bool played=PlayPrioritizedCue(clip,position,true,.075f,2,.015f);if(played&&ElementImpactPlaybackAccepted<int.MaxValue)ElementImpactPlaybackAccepted++;return played;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: accepted World-space contact -> existing spatial four-voice prioritized cue pool; existing30m listener bound/focus/gesture/mute policy.
// Array Formats: four borrowed imported original PCM clips loaded/primed once cold, two diagnostic counters; no new runtime source/PCM/clip/provider/formatting.
// Dependencies/Pointers: GameAudioDirector retains mix/voice/focus lifetime. Missing/unloaded clips and saturated/protected voices reject explicitly; counters do not prove listener audibility or browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world contacts, projectile-local three-part visuals and cached shot identity; physical collision and gameplay semantics unchanged.
// Array Formats: Existing48/12 bolt pools,20 three-part impact slots, fixed six numeric/cue recipes and one extra local combo integer per accepted-shot receipt;64B/256B wire formats unchanged.
// Dependencies/Pointers: Existing host acceptance/dedupe/expiry and audio voice policies remain canonical. Presentation failures grant no damage, status, recovery or authority; root owns actual renderer/listening/browser/peer/performance acceptance.
