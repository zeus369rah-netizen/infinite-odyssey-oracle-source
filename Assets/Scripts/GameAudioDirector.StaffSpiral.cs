using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        const string StaffSpiralResource="Audio/WorldCue/player_staff_spiral";
        AudioClip staffSpiralClip;float nextStaffSpiral;
        void LoadStaffSpiralClip(){staffSpiralClip=Resources.Load<AudioClip>(StaffSpiralResource);if(staffSpiralClip!=null)staffSpiralClip.LoadAudioData();}
        public bool PlayStaffSpiral(bool accepted)
        {
            if(!CanPlayCue()||session==null||!session.IsPlaying||!session.CombatUnlocked||session.Victory||session.IsDead||
                Time.unscaledTime<nextStaffSpiral||staffSpiralClip==null)return false;
            if(!PlayPrioritizedCue(staffSpiralClip,ListenerPosition(),false,accepted?.045f:.025f,1,.08f))return false;
            nextStaffSpiral=Time.unscaledTime+.12f;return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing nonspatial local cue pool; no new AudioSource, ambient emitter or spatial/global mix change.
// Array Formats: one cold-loaded shared original mono PCM clip and scalar.12s admission timestamp; existing four prioritized voices and focus/gesture/mute gates reused.
// Dependencies/Pointers: only cosmetic selection/accepted-own feedback calls; missing resource stays silent, existing accepted cast sounds remain. Root imports exact provenance-recorded oscillator WAV; no runtime synthesis/paid service/foreign recording.
