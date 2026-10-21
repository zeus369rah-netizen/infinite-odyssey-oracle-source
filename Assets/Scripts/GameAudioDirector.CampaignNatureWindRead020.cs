using UnityEngine;
using OracleNature020;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        // PSEUDOCODE: expose the already smoothed ambient-wind envelope as a bounded
        // scalar for the retained foliage shader; preserve one existing wind owner.
        internal float CampaignNatureWindStrength020=>Mathf.Clamp01(natureFade020/WindMath020.Maximum);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One already accepted world-XZ boundary wind state; scalar is normalized against the frozen .095 maximum.
// Array Formats: One float read from the existing smoothed wind owner; no new timer, job, audio source, or allocation.
// Dependencies/Pointers: Read only after DispatchCampaignNatureAudio020 accepted the matching completed frame. GameAudioDirector remains the sole ambient wind smoother and owner of its existing four ambient/four cue pools.
