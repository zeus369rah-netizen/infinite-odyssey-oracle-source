using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        // Only updates an existing cold-registered record. External clear stays
        // fail-closed; a presentation frame never allocates a replacement record.
        internal bool UpdateSmithFurnaceAmbient015(int id,Vector3 position,float fuel)
        {
            if(!Finite(position)||float.IsNaN(fuel)||float.IsInfinity(fuel)||!ambientEmitters.TryGetValue(id,out var emitter))return false;
            float gain=.065f*Mathf.Clamp01(fuel);
            if((emitter.Position-position).sqrMagnitude>1e-8f||Mathf.Abs(emitter.Gain-gain)>.0001f){emitter.Position=position;emitter.Gain=gain;nextAmbientCheck=0f;}
            return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Cached actual furnace world audio anchor and canonical host-fuel scalar, not raw PCM/source clocks.
// Array Formats: One borrowed existing32-emitter dictionary entry and existing four nearest ambient voices; no new AudioSource/hot allocation.
// Dependencies/Pointers: Verified torch_crackle, existing range/voice/focus/pause/Master/SFX/narration ducking. Cold Smith owner registers/retires; no music/voice/HRTF backend or previous sound-record mutation.
