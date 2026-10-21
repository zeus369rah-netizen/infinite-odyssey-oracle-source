#if UNITY_EDITOR
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class PortalLandingAtmosphere020
    {
        Camera editorPhaseCamera020;
        uint editorPhaseToken020, editorPhaseTokenSequence020, editorPhaseGeneration020, editorPhaseRevision020;
        double editorPhaseSavedElapsed020, editorPhaseLastWrittenElapsed020;
        float editorPhaseSavedShaderClock020, editorPhaseLastWrittenShaderClock020;
        bool editorPhaseSavedHeld020;
        int editorPhaseSavedRain020, editorPhaseSavedSplashes020, editorPhaseSavedQuality020;

        public bool IsEditorCaptureLeaseOwned020(Camera ownedCamera) => ownedCamera != null && ReferenceEquals(captureCamera, ownedCamera);
        public bool IsEditorPhaseProbeTicketRetained020(uint ticket) => ticket != 0 && ticket == editorPhaseToken020;

        public bool TryReadEditorCycleKey020(out Light key, out Color originalColor, out Quaternion originalRotation)
        {
            key = null;
            originalColor = default;
            originalRotation = default;
            if (!Bound020 || !OwnsAdmission() || cycleKeyLight020 == null ||
                !session.TryGetCampaignDirectionalLight020(out Light current) || !ReferenceEquals(current, cycleKeyLight020))
                return false;
            key = cycleKeyLight020;
            originalColor = originalKeyColor020;
            originalRotation = originalKeyRotation020;
            return true;
        }

        public bool TryReadEditorCycleUniforms020(out float daylight, out float night, out float storm, out float twilight,
            out float aurora, out Vector3 sunDirection)
        {
            daylight = night = storm = twilight = aurora = 0f;
            sunDirection = default;
            if (!Bound020 || !OwnsAdmission() || skyFallback || sky == null || !cycleViewReady020 ||
                !sky.HasProperty(CycleDaylightId020) || !sky.HasProperty(CycleNightId020) || !sky.HasProperty(CycleStormId020) ||
                !sky.HasProperty(CycleTwilightId020) || !sky.HasProperty(CycleAuroraId020) || !sky.HasProperty(CycleSunDirectionId020))
                return false;
            daylight = sky.GetFloat(CycleDaylightId020);
            night = sky.GetFloat(CycleNightId020);
            storm = sky.GetFloat(CycleStormId020);
            twilight = sky.GetFloat(CycleTwilightId020);
            aurora = sky.GetFloat(CycleAuroraId020);
            Vector4 direction = sky.GetVector(CycleSunDirectionId020);
            sunDirection = new Vector3(direction.x, direction.y, direction.z);
            return FiniteEditorCycle020(daylight) && FiniteEditorCycle020(night) && FiniteEditorCycle020(storm) && FiniteEditorCycle020(twilight) && FiniteEditorCycle020(aurora) && Finite(sunDirection);
        }

        // Editor capture changes visual elapsed temporarily; Nature keeps the saved normal timeline.
        public bool TryReadEditorNatureClock020(out double elapsedSeconds, out uint clockGeneration, out uint clockRevision, out bool held)
        {
            if (editorPhaseToken020 == 0)
                return TryReadPresentationClock020(out elapsedSeconds, out clockGeneration, out clockRevision, out held);
            elapsedSeconds = 0d; clockGeneration = clockRevision = 0; held = false;
            uint ticket = editorPhaseToken020;
            if (!OwnsEditorPhaseProbe020(ticket) || !presentationHeld020 ||
                presentationElapsedSeconds020 != editorPhaseLastWrittenElapsed020 ||
                presentationShaderClock020 != editorPhaseLastWrittenShaderClock020 ||
                double.IsNaN(editorPhaseSavedElapsed020) || double.IsInfinity(editorPhaseSavedElapsed020) || editorPhaseSavedElapsed020 < 0d)
                return false;
            elapsedSeconds = editorPhaseSavedElapsed020;
            clockGeneration = editorPhaseGeneration020;
            clockRevision = editorPhaseRevision020;
            held = editorPhaseSavedHeld020 || presentationHeld020;
            return clockGeneration != 0 && clockRevision != 0;
        }

        public bool TryBeginEditorPhaseProbe020(Camera ownedCaptureCamera, out uint ticket)
        {
            ticket = 0;
            if (!Application.isPlaying || ownedCaptureCamera == null || !ownedCaptureCamera.gameObject.activeInHierarchy ||
                captureCamera == null || !ReferenceEquals(ownedCaptureCamera, captureCamera) || editorPhaseToken020 != 0 ||
                !Bound020 || !OwnsAdmission() || !cycleViewReady020 ||
                !session.TryGetCampaignDirectionalLight020(out Light currentKey) || !ReferenceEquals(currentKey, cycleKeyLight020))
                return false;

            editorPhaseTokenSequence020++;
            if (editorPhaseTokenSequence020 == 0) editorPhaseTokenSequence020++;
            editorPhaseToken020 = editorPhaseTokenSequence020;
            editorPhaseCamera020 = ownedCaptureCamera;
            editorPhaseGeneration020 = generation;
            editorPhaseRevision020 = worldRevision020;
            editorPhaseSavedElapsed020 = presentationElapsedSeconds020;
            editorPhaseLastWrittenElapsed020 = presentationElapsedSeconds020;
            editorPhaseSavedShaderClock020 = presentationShaderClock020;
            editorPhaseLastWrittenShaderClock020 = presentationShaderClock020;
            editorPhaseSavedHeld020 = presentationHeld020;
            editorPhaseSavedRain020 = rainCount;
            editorPhaseSavedSplashes020 = splashCount;
            editorPhaseSavedQuality020 = quality;
            presentationHeld020 = true;
            ticket = editorPhaseToken020;
            SetPresentationUniforms();
            return true;
        }

        public bool TrySetEditorPhaseProbe020(uint ticket, double elapsedSeconds)
        {
            if (!OwnsEditorPhaseProbe020(ticket) ||
                !CampaignCycleRules020.TryView(elapsedSeconds, editorPhaseGeneration020, editorPhaseRevision020, out _))
                return false;

            float shaderClock = CampaignCycleRules020.ProjectShaderClock(elapsedSeconds);
            if (!FiniteEditorCycle020(shaderClock)) return false;
            presentationElapsedSeconds020 = elapsedSeconds;
            editorPhaseLastWrittenElapsed020 = elapsedSeconds;
            presentationShaderClock020 = shaderClock;
            editorPhaseLastWrittenShaderClock020 = presentationShaderClock020;
            presentationHeld020 = true;
            RefreshCampaignCycleView020();
            if (!cycleViewReady020) return false;

            Vector4 policy = LaneVignetteRenderer.PassiveWindDescriptor015;
            bool motion = policy.w > .5f && !OracleBookAccessibility.PrefersReducedMotion;
            float particleFactor = session.Graphics != null ? session.Graphics.EffectiveParticleFactor : .125f;
            int nextRain = motion && CampaignCycleRules020.AllowsBloodstorm(cycleView020)
                ? PortalLandingWeatherRules020.RainCount(quality, particleFactor) : 0;
            int nextSplashes = Mathf.Min(SplashCapacity, (nextRain + 3) / 4);
            bool changed = nextRain != rainCount || nextSplashes != splashCount;
            rainCount = nextRain;
            splashCount = nextSplashes;
            if (changed && hasSeeds) Recount();
            SetPresentationUniforms();
            return cycleViewReady020 && presentationHeld020 && presentationElapsedSeconds020 == elapsedSeconds;
        }

        public bool TryEndEditorPhaseProbe020(uint ticket)
        {
            if (ticket == 0 || ticket != editorPhaseToken020) return false;
            bool owns = OwnsEditorPhaseProbe020(ticket) && presentationHeld020 &&
                presentationElapsedSeconds020 == editorPhaseLastWrittenElapsed020 &&
                presentationShaderClock020 == editorPhaseLastWrittenShaderClock020;
            double savedElapsed = editorPhaseSavedElapsed020;
            float savedShaderClock = editorPhaseSavedShaderClock020;
            bool savedHeld = editorPhaseSavedHeld020;
            int savedRain = editorPhaseSavedRain020, savedSplashes = editorPhaseSavedSplashes020, savedQuality = editorPhaseSavedQuality020;
            if (!owns)
            {
                ClearEditorPhaseProbe020();
                if (bound) Retire();
                return false;
            }

            try
            {
                presentationElapsedSeconds020 = savedElapsed;
                presentationShaderClock020 = savedShaderClock;
                presentationHeld020 = savedHeld;
                quality = savedQuality;
                rainCount = savedRain;
                splashCount = savedSplashes;
                RefreshCampaignCycleView020();
                if (hasSeeds) Recount();
                SetPresentationUniforms();
                bool restored = cycleViewReady020 && presentationElapsedSeconds020 == savedElapsed &&
                    presentationShaderClock020 == savedShaderClock && presentationHeld020 == savedHeld;
                ClearEditorPhaseProbe020();
                if (!restored && bound) Retire();
                return restored;
            }
            catch
            {
                ClearEditorPhaseProbe020();
                if (bound) Retire();
                throw;
            }
        }
        bool OwnsEditorPhaseProbe020(uint ticket) => ticket != 0 && ticket == editorPhaseToken020 &&
            ReferenceEquals(editorPhaseCamera020, captureCamera) && editorPhaseCamera020 != null &&
            editorPhaseCamera020.gameObject.activeInHierarchy && editorPhaseGeneration020 == generation &&
            editorPhaseRevision020 == worldRevision020 && Bound020 && OwnsAdmission();

        static bool FiniteEditorCycle020(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void ClearEditorPhaseProbe020()
        {
            editorPhaseToken020 = 0;
            editorPhaseCamera020 = null;
            editorPhaseGeneration020 = editorPhaseRevision020 = 0;
            editorPhaseSavedElapsed020 = editorPhaseLastWrittenElapsed020 = 0d;
            editorPhaseSavedShaderClock020 = editorPhaseLastWrittenShaderClock020 = 0f;
            editorPhaseSavedHeld020 = false;
            editorPhaseSavedRain020 = editorPhaseSavedSplashes020 = editorPhaseSavedQuality020 = 0;
        }
    }
}
#endif
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing admitted atmosphere cycle in unwrapped presentation seconds, pinned to the one borrowed private capture camera and exact World generation/revision.
// Array Formats: Fixed scalar ticket/unwrapped-clock/shader-projection/weather snapshots and one existing cycle view; no collections or new time integrator.
// Dependencies/Pointers: Editor-only phase writes update elapsed cycle time and cached shader projection together; cleanup restores both only while the same capture/session/world/root scope and both last-written values still match. The ticket query distinguishes a still-live token from a terminally cleared token after failed/stale restore, so callers retry only live cleanup and still fail capture on lost restoration.


// Editor-only Nature reads borrow the saved normal elapsed during the exact owned phase token; no clock is advanced or reset.
