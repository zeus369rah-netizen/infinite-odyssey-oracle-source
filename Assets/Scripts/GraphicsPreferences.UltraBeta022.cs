// PSEUDOCODE
// 1. Treat preset index 4 as opt-in; preserve desktop behavior and admit browser enhancement only when runtime GraphicsDeviceType is WebGPU.
// 2. Preserve existing shadow tiers unless Beta, High shadows, owned URP clone, runtime shadow support, shadow-map format, and 4096 max texture size all agree.
// 3. Use High SMAA only on the owned desktop or desktop-WebGPU URP path; otherwise retain the prior FXAA route.
// 4. Raise saturation by eight points and the existing screen-space flare to 0.025 with one sample.
// 5. Capture parameter baselines before writes and restore only values still equal to this owner's last write.
// 6. Keep all profile searches and dictionaries in existing cold settings refresh/teardown paths.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle
{
    public sealed partial class GraphicsPreferences
    {
        private const int UltraBetaPreset022 = 4;
        private const float UltraBetaSaturationLift022 = 8f;
        private const float UltraBetaLensFlareIntensity022 = .025f;
        private readonly Dictionary<ColorAdjustments, UltraBetaParameterState022> ultraBetaSaturation022 =
            new Dictionary<ColorAdjustments, UltraBetaParameterState022>();
        private readonly Dictionary<ScreenSpaceLensFlare, UltraBetaParameterState022> ultraBetaFlare022 =
            new Dictionary<ScreenSpaceLensFlare, UltraBetaParameterState022>();
        private readonly Dictionary<Camera, UltraBetaCameraAaState022> ultraBetaCameraAa022 =
            new Dictionary<Camera, UltraBetaCameraAaState022>();

        private struct UltraBetaCameraAaState022
        {
            public UniversalAdditionalCameraData Data;
            public AntialiasingMode BaselineMode;
            public AntialiasingQuality BaselineQuality;
            public AntialiasingMode LastMode;
            public AntialiasingQuality LastQuality;
            public bool OwnsMode;
            public bool OwnsQuality;
            public bool HasModeWrite;
            public bool HasQualityWrite;
        }

        private struct UltraBetaParameterState022
        {
            public float Baseline;
            public bool BaselineOverride;
            public float LastWrite;
            public bool LastOverride;
            public bool HasLastWrite;
        }

        // Internal compatibility name also admits non-mobile WebGLPlayer when Unity reports WebGPU.
        private bool IsUltraBetaDesktop022 => Preset == UltraBetaPreset022 &&
            !Application.isMobilePlatform &&
            (Application.platform != RuntimePlatform.WebGLPlayer ||
             SystemInfo.graphicsDeviceType == GraphicsDeviceType.WebGPU);

        private void ResolveUltraBetaShadowBudget022(ref float distance, ref int resolution)
        {
            if (!IsUltraBetaDesktop022 || ShadowQuality020 != 3 || !OwnsCurrentQualityPipeline020() ||
                !SystemInfo.supportsShadows || SystemInfo.maxTextureSize < 4096 ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Shadowmap)) return;
            distance = 54f;
            resolution = 4096;
        }

        private void ApplyUltraBetaCameraQuality022(Camera camera, UniversalAdditionalCameraData data)
        {
            if (camera == null || data == null) return;
            if (!IsUltraBetaDesktop022 || !OwnsCurrentQualityPipeline020())
            {
                // ApplyCamera has already installed the regular preset values.
                // Retiring here prevents teardown from undoing that newer choice.
                ultraBetaCameraAa022.Remove(camera);
                return;
            }

            bool found = ultraBetaCameraAa022.TryGetValue(camera, out var state);
            if (found && !ReferenceEquals(state.Data, data))
            {
                // Do not adopt a replacement AA component during this Beta lease.
                // A normal preset or teardown retires the stale camera entry.
                return;
            }
            if (!found)
            {
                state = new UltraBetaCameraAaState022
                {
                    Data = data,
                    BaselineMode = data.antialiasing,
                    BaselineQuality = data.antialiasingQuality,
                    OwnsMode = true,
                    OwnsQuality = true
                };
            }
            else
            {
                // The Beta path does not rewrite a base AA value before this
                // ownership check, so any changed field is a foreign write.
                if (state.OwnsMode && state.HasModeWrite &&
                    data.antialiasing != state.LastMode) state.OwnsMode = false;
                if (state.OwnsQuality && state.HasQualityWrite &&
                    data.antialiasingQuality != state.LastQuality) state.OwnsQuality = false;
                if (!state.OwnsMode && !state.OwnsQuality)
                {
                    // Keep a tombstone so a later refresh cannot mistake foreign
                    // AA values for a fresh baseline and overwrite them.
                    ultraBetaCameraAa022[camera] = state;
                    return;
                }
            }

            if (state.OwnsMode)
            {
                state.LastMode = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                state.HasModeWrite = true;
                ultraBetaCameraAa022[camera] = state;
                data.antialiasing = state.LastMode;
            }
            if (state.OwnsQuality)
            {
                state.LastQuality = AntialiasingQuality.High;
                state.HasQualityWrite = true;
                ultraBetaCameraAa022[camera] = state;
                data.antialiasingQuality = state.LastQuality;
            }
        }

        private void RestoreUltraBetaCameraQuality022()
        {
            bool complete = true;
            foreach (var pair in ultraBetaCameraAa022)
            {
                Camera camera = pair.Key;
                UltraBetaCameraAaState022 state = pair.Value;
                if (camera == null || state.Data == null) continue;
                try
                {
                    // A replaced component is a different owner's state. Never
                    // restore its fields through the replacement component.
                    var attached = camera.GetComponent<UniversalAdditionalCameraData>();
                    if (!ReferenceEquals(attached, state.Data)) continue;

                    if (state.OwnsMode && state.HasModeWrite && state.Data.antialiasing == state.LastMode)
                        state.Data.antialiasing = state.BaselineMode;
                    if (state.OwnsQuality && state.HasQualityWrite &&
                        state.Data.antialiasingQuality == state.LastQuality)
                        state.Data.antialiasingQuality = state.BaselineQuality;
                }
                catch
                {
                    // Keep the full ledger for retry; already-restored fields no
                    // longer match LastWrite and are naturally skipped next time.
                    complete = false;
                }
            }
            if (complete) ultraBetaCameraAa022.Clear();
        }

        private void ApplyUltraBetaVolumeRefinements022(VolumeProfile profile, ColorAdjustments grade)
        {
            if (grade != null)
            {
                if (IsUltraBetaDesktop022) ApplyUltraBetaSaturation022(grade);
                else RestoreUltraBetaSaturation022(grade);
            }

            if (profile == null || !profile.TryGet<ScreenSpaceLensFlare>(out var flare)) return;
            bool enabled = IsUltraBetaDesktop022 && OwnsCurrentQualityPipeline020() && runtimePipeline != null &&
                runtimePipeline.supportScreenSpaceLensFlare && flare.samples.value == 1;
            if (enabled) ApplyUltraBetaFlare022(flare);
            else RestoreUltraBetaFlare022(flare);
        }

        private void ApplyUltraBetaSaturation022(ColorAdjustments grade)
        {
            var parameter = grade.saturation;
            if (!ultraBetaSaturation022.TryGetValue(grade, out var state))
            {
                state = new UltraBetaParameterState022
                {
                    Baseline = parameter.value,
                    BaselineOverride = parameter.overrideState
                };
            }
            else if (state.HasLastWrite &&
                (!Mathf.Approximately(parameter.value, state.LastWrite) && !Mathf.Approximately(parameter.value, state.Baseline) ||
                 parameter.overrideState != state.LastOverride && parameter.overrideState != state.BaselineOverride))
            {
                ultraBetaSaturation022.Remove(grade);
                return;
            }

            state.LastWrite = Mathf.Clamp(state.Baseline + UltraBetaSaturationLift022, -100f, 100f);
            state.LastOverride = true;
            state.HasLastWrite = true;
            ultraBetaSaturation022[grade] = state;
            parameter.overrideState = true;
            parameter.value = state.LastWrite;
        }

        private void ApplyUltraBetaFlare022(ScreenSpaceLensFlare flare)
        {
            var parameter = flare.intensity;
            if (!ultraBetaFlare022.TryGetValue(flare, out var state))
            {
                state = new UltraBetaParameterState022
                {
                    Baseline = parameter.value,
                    BaselineOverride = parameter.overrideState
                };
            }
            else if (state.HasLastWrite &&
                (!Mathf.Approximately(parameter.value, state.LastWrite) && !Mathf.Approximately(parameter.value, state.Baseline) ||
                 parameter.overrideState != state.LastOverride && parameter.overrideState != state.BaselineOverride))
            {
                ultraBetaFlare022.Remove(flare);
                return;
            }

            state.LastWrite = UltraBetaLensFlareIntensity022;
            state.LastOverride = true;
            state.HasLastWrite = true;
            ultraBetaFlare022[flare] = state;
            parameter.overrideState = true;
            parameter.value = state.LastWrite;
        }

        private void RestoreUltraBetaSaturation022(ColorAdjustments grade)
        {
            if (grade == null || !ultraBetaSaturation022.TryGetValue(grade, out var state)) return;
            var parameter = grade.saturation;
            bool ownsValue = state.HasLastWrite && Mathf.Approximately(parameter.value, state.LastWrite);
            bool ownsOverride = state.HasLastWrite && parameter.overrideState == state.LastOverride;
            if (!ownsValue && !ownsOverride) { ultraBetaSaturation022.Remove(grade); return; }
            try
            {
                if (ownsValue) parameter.value = state.Baseline;
                if (ownsOverride) parameter.overrideState = state.BaselineOverride;
                ultraBetaSaturation022.Remove(grade);
            }
            catch { }
        }

        private void RestoreUltraBetaFlare022(ScreenSpaceLensFlare flare)
        {
            if (flare == null || !ultraBetaFlare022.TryGetValue(flare, out var state)) return;
            var parameter = flare.intensity;
            bool ownsValue = state.HasLastWrite && Mathf.Approximately(parameter.value, state.LastWrite);
            bool ownsOverride = state.HasLastWrite && parameter.overrideState == state.LastOverride;
            if (!ownsValue && !ownsOverride) { ultraBetaFlare022.Remove(flare); return; }
            try
            {
                if (ownsValue) parameter.value = state.Baseline;
                if (ownsOverride) parameter.overrideState = state.BaselineOverride;
                ultraBetaFlare022.Remove(flare);
            }
            catch { }
        }

        private void RestoreUltraBetaVolumeRefinements022()
        {
            bool complete = true;
            foreach (var pair in ultraBetaSaturation022)
            {
                if (pair.Key == null || !pair.Value.HasLastWrite) continue;
                var parameter = pair.Key.saturation;
                bool ownsValue = Mathf.Approximately(parameter.value, pair.Value.LastWrite);
                bool ownsOverride = parameter.overrideState == pair.Value.LastOverride;
                if (!ownsValue && !ownsOverride) continue;
                try
                {
                    if (ownsValue) parameter.value = pair.Value.Baseline;
                    if (ownsOverride) parameter.overrideState = pair.Value.BaselineOverride;
                }
                catch { complete = false; }
            }
            foreach (var pair in ultraBetaFlare022)
            {
                if (pair.Key == null || !pair.Value.HasLastWrite) continue;
                var parameter = pair.Key.intensity;
                bool ownsValue = Mathf.Approximately(parameter.value, pair.Value.LastWrite);
                bool ownsOverride = parameter.overrideState == pair.Value.LastOverride;
                if (!ownsValue && !ownsOverride) continue;
                try
                {
                    if (ownsValue) parameter.value = pair.Value.Baseline;
                    if (ownsOverride) parameter.overrideState = pair.Value.BaselineOverride;
                }
                catch { complete = false; }
            }
            if (!complete) return;
            ultraBetaSaturation022.Clear();
            ultraBetaFlare022.Clear();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Camera AA and URP volume parameters; no scene transforms or render-target ownership.
// Array Formats: Cold camera-keyed tombstones and profile-component baseline/last-write dictionaries; no frame-loop collections.
// Dependencies/Pointers: GraphicsPreferences private pipeline gate; exact Camera/data identity is required for selective AA ownership and restore.
