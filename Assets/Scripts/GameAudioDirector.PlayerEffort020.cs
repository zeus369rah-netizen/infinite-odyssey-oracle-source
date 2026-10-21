using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        private AudioClip exhaustedClip020;
        public int PlayerEffortPlaybackAttempts020 { get; private set; }
        public int PlayerEffortPlaybackAccepted020 { get; private set; }

        private void PreparePlayerEffort020()
        {
            exhaustedClip020 = Resources.Load<AudioClip>("Audio/PlayerEffort020/exhausted020");
            Prime(exhaustedClip020);
        }
        public bool PlayPlayerExhausted020()
        {
            if (!CanPlayCue() || session == null || !session.IsPlaying || exhaustedClip020 == null ||
                exhaustedClip020.loadState != AudioDataLoadState.Loaded) return false;
            if (PlayerEffortPlaybackAttempts020 < int.MaxValue) PlayerEffortPlaybackAttempts020++;
            bool played = PlayPrioritizedCue(exhaustedClip020, ListenerPosition(), false, .12f, 2, .1f);
            if (played && PlayerEffortPlaybackAccepted020 < int.MaxValue) PlayerEffortPlaybackAccepted020++;
            return played;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Listener-relative2D one-shot at the existing player listener position; no world emitter or loop.
// Array Formats: One cold Resources clip reference and two saturating counters; same existing four shared prioritized voices and Web native-fallback budget.
// Dependencies/Pointers: Player exhaustion edge calls once; focus/gesture/mute/pause/loaded-clip admission stays canonical. No new source, bank entry, authority or retry loop; root owns media import and browser listening.
