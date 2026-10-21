using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        public const string WorkshopAssemblyResource015 = "Audio/Workshop015/workshop_assembly015";
        const float WorkshopConstructGain015 = .25f; // Existing cue gain cap; offline stereo/downmix metrics retained.
        AudioClip workshopAssemblyClip015;
        bool workshopPrepared015;
        float workshopNextWorld015, workshopNextPreview015;

        // Cold Resources ownership: borrowed clip, never destroyed or loaded in a cue callback.
        public void PrepareWorkshopAudio015()
        {
            if (!initialized || workshopPrepared015) return;
            workshopPrepared015 = true;
            workshopAssemblyClip015 = Resources.Load<AudioClip>(WorkshopAssemblyResource015);
            Prime(workshopAssemblyClip015);
        }

        // Caller has already accepted/deduplicated (world lease, building ID, revision).
        // Revision0 is valid and distinct buildings can share a revision; it is not a global audio key.
        public bool TryPlayWorkshopAssembly015(Vector3 position, uint revision, bool quietPreview = false)
        {
            bool construction = !quietPreview && HasProductionCue(ProductionCue.TowerConstruct);
            AudioClip clip = construction ? productionClips[(int)ProductionCue.TowerConstruct] : workshopAssemblyClip015;
            if (!isActiveAndEnabled || !CanPlayCue() || session == null || !session.HasStarted || session.IsDead || session.Victory ||
                clip == null || clip.loadState != AudioDataLoadState.Loaded || !Finite(position)) return false;
            if (!quietPreview && (!session.SimulationActive || !Near(position, 30f))) return false;
            float now = Time.unscaledTime;
            if (now < (quietPreview ? workshopNextPreview015 : workshopNextWorld015)) return false;
            float cadence = quietPreview ? .25f : .10f;
            if (!PlayPrioritizedCue(clip, quietPreview ? ListenerPosition() : position, !quietPreview,
                quietPreview ? .022f : construction ? WorkshopConstructGain015 : .05f, 0, cadence)) return false;
            if (quietPreview) workshopNextPreview015 = now + cadence;
            else workshopNextWorld015 = now + cadence;
            return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted finite world anchor in metres/Y-up, or existing listener-centred2D preview.
// Array Formats: Existing borrowed licensed construction cue or cold0.7s original fallback/quiet preview, two cadence clocks; existing protected4 cue voices and8 acoustic leases.
// Dependencies/Pointers: Same accepted caller dedup/focus/gesture/volume/duck/stop owner. World prefers retained catalog21 recording at existing gain cap; offline stereo/downmix metrics and listening limit explicit. Quiet preview/fallback unchanged; no new event/source/media/bank or hot load.
