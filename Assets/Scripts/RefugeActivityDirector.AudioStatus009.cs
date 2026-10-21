using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        // Covers all real clips on the sole Activity voice owner, including
        // NPC greetings, mechanics guide and retained optional practice cues.
        public bool IsNarrationPlaying009 => isActiveAndEnabled && voice!=null && voice.isPlaying &&
            !voice.mute && voice.volume>0f && AudioListener.volume>0f &&
            !AudioListener.pause && Application.isFocused;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Activity voice/source state only; no scene/screen position.
// Array Formats: One computed bool from cached source, no clip/sample copies or runtime allocation.
// Dependencies/Pointers: Existing Activity remains sole NPC/mechanics voice owner; Audio only reads this flag to duck its current sources, without starting/stopping narration.

