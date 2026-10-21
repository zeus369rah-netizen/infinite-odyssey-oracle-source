using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        static readonly string[] ElementUpgradeKeys011 = { "fire", "water", "earth", "air" };
        readonly AudioClip[] elementUpgradeClips011 = new AudioClip[4];
        readonly float[] elementUpgradeNext011 = new float[4];
        bool elementUpgradePrepared011;

        void PrepareElementUpgrades011()
        {
            if (!initialized || elementUpgradePrepared011) return;
            elementUpgradePrepared011 = true;
            for (int i = 0; i < 4; i++) {
                AudioClip clip = Resources.Load<AudioClip>("Audio/ElementUpgrades011/" + ElementUpgradeKeys011[i]);
                elementUpgradeClips011[i] = clip; Prime(clip);
            }
        }
        public int ElementUpgradeReadyMask011
        {
            get {
                int mask = 0;
                for (int i = 0; i < 4; i++) if (ElementUpgradeLoaded011(elementUpgradeClips011[i])) mask |= 1 << i;
                return mask;
            }
        }
        static bool ElementUpgradeLoaded011(AudioClip clip) => clip != null && clip.loadState == AudioDataLoadState.Loaded &&
            clip.samples > 0 && clip.frequency > 0 && clip.length > 0 && clip.length <= 2f;

        // Caller observes one accepted local tier delta or a successfully stored local purchase.
        // No admission/replay/ownership grant is created by this cosmetic method.
        public bool TryPlayElementUpgrade011(int element)
        {
            if (element < 0 || element > 3 || !isActiveAndEnabled || !CanPlayCue() || session == null ||
                !session.HasStarted || session.IsDead || session.IsDowned || session.Victory) return false;
            AudioClip clip = elementUpgradeClips011[element];
            float now = Time.unscaledTime;
            if (!ElementUpgradeLoaded011(clip) || now < elementUpgradeNext011[element]) return false;
            // Same priority/gain/cadence as the existing generic UI upgrade; no extra source.
            if (!PlayPrioritizedCue(clip, ListenerPosition(), false, .05f, 1, .18f)) return false;
            elementUpgradeNext011[element] = now + .18f;
            return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing listener-centred2D accepted UI feedback; no world/gameplay/network mutation.
// Array Formats: Four cold borrowed clips/four scalar next-times, existing protected4 cue voices/eight acoustic leases; no new AudioSource or bank entries.
// Dependencies/Pointers: Existing focus/real gesture/Master/SFX/duck/stop owner; caller deduplicates accepted local state/commit. Cold Resources/Prime only, no hot load/synthesis. Four root-generated/imported assets and actual listening remain pending; fixed38 browser bank unchanged.
