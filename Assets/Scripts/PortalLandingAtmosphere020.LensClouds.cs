using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class PortalLandingAtmosphere020
    {
        static readonly int CloudVisibilityId020 = Shader.PropertyToID("_CloudVisibility020");
        static readonly int CloudSeedId020 = Shader.PropertyToID("_CloudSeed020");
        static readonly int FlareAxisId020 = Shader.PropertyToID("_LensFlareAxis020");
        static readonly int FlareVisibilityId020 = Shader.PropertyToID("_LensFlareVisibility020");
        static readonly int CloudShadowStateId020 = Shader.PropertyToID("_CloudShadowState020");
        static readonly RaycastHit[] LensFlareHit020 = new RaycastHit[1];
        const float LensFlareRayDistance020 = 512f;

        Material cloudSurfaceOwner020;
        Vector4 previousCloudShadowState020, installedCloudShadowState020;
        uint cloudSeedGeneration020, cloudSeedRevision020;
        float cloudSeed020;
        bool cloudSurfaceStateCaptured020, cloudsEnabled020 = true, weatherEffectsEnabled020 = true;
        bool cloudShadowsEnabled020, callerPaused020;
        int cloudShadowQuality020;

        void CaptureCloudSurfaceState020(Material owner)
        {
            cloudSurfaceOwner020 = owner;
            cloudSurfaceStateCaptured020 = owner != null && owner.HasProperty(CloudShadowStateId020);
            if (!cloudSurfaceStateCaptured020) return;
            previousCloudShadowState020 = installedCloudShadowState020 = owner.GetVector(CloudShadowStateId020);
        }

        void ReadCloudPreferences020()
        {
            var preferences = session != null ? session.Graphics : null;
            cloudsEnabled020 = preferences == null || preferences.CloudsEnabled020;
            weatherEffectsEnabled020 = preferences == null || preferences.WeatherEffectsEnabled020;
            cloudShadowsEnabled020 = preferences != null && preferences.CloudShadowsEnabled020;
            cloudShadowQuality020 = preferences == null ? 0 : Mathf.Clamp(preferences.ShadowQuality020, 0, 3);

            if (cloudSeedGeneration020 == generation && cloudSeedRevision020 == worldRevision020) return;
            unchecked
            {
                uint hash = generation * 0x9E3779B9u + worldRevision020 * 0x85EBCA6Bu;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                cloudSeed020 = (hash & 0x00FFFFFFu) * (1f / 16777216f);
            }
            cloudSeedGeneration020 = generation;
            cloudSeedRevision020 = worldRevision020;
        }

        void SetLensFlarePause020(bool paused) => callerPaused020 = paused;

        void ApplyCloudPresentation020()
        {
            if (sky != null)
            {
                if (sky.HasProperty(CloudVisibilityId020)) sky.SetFloat(CloudVisibilityId020, cloudsEnabled020 ? 1f : 0f);
                if (sky.HasProperty(CloudSeedId020)) sky.SetFloat(CloudSeedId020, cloudSeed020);
            }

            if (!cloudSurfaceStateCaptured020 || surface == null || !ReferenceEquals(cloudSurfaceOwner020, surface) ||
                !surface.HasProperty(CloudShadowStateId020) || !surface.GetVector(CloudShadowStateId020).Equals(installedCloudShadowState020)) return;
            float strength = 0f;
            if (cloudsEnabled020 && weatherEffectsEnabled020 && cloudShadowsEnabled020 && cloudShadowQuality020 > 0 && cycleViewReady020)
            {
                float qualityStrength = cloudShadowQuality020 == 1 ? .22f : cloudShadowQuality020 == 2 ? .34f : .46f;
                strength = qualityStrength * Mathf.Clamp01(cycleView020.Daylight);
            }
            Vector4 state = new Vector4(strength, cloudSeed020, heldWind.x, heldWind.y);
            if (state.Equals(installedCloudShadowState020)) return;
            surface.SetVector(CloudShadowStateId020, state);
            cloudSurfaceOwner020 = surface;
            cloudSurfaceStateCaptured020 = true;
            installedCloudShadowState020 = state;
        }

        void UpdateSunLensFlare020(Camera camera)
        {
            if (sky == null || skyFallback || !sky.HasProperty(FlareAxisId020) || !sky.HasProperty(FlareVisibilityId020)) return;
            float visibility = 0f;
            Vector4 axisValue = Vector4.zero;
            if (camera != null && cycleViewReady020 && cycleKeyLight020 != null && cycleKeyLight020.enabled &&
                cycleKeyLight020.type == LightType.Directional && cycleView020.Night == 0 && cycleView020.Daylight > .001f &&
                !callerPaused020 && Application.isFocused && camera.gameObject.activeInHierarchy && (camera.isActiveAndEnabled || (captureCamera != null && ReferenceEquals(camera, captureCamera))) &&
                LaneVignetteRenderer.PassiveWindDescriptor015.w > .5f && !OracleBookAccessibility.PrefersReducedMotion)
            {
                Vector3 sun = -cycleKeyLight020.transform.forward;
                Vector3 cameraForward = camera.transform.forward;
                float forward = Vector3.Dot(cameraForward, sun);
                Vector3 projected = camera.transform.position + sun * 1024f;
                Vector3 viewport = camera.WorldToViewportPoint(projected);
                if (Finite(sun) && Finite(cameraForward) && Finite(viewport) && sun.y > .025f && forward > .025f && viewport.z > 0f)
                {
                    float edge = Mathf.Min(Mathf.Min(viewport.x, 1f - viewport.x), Mathf.Min(viewport.y, 1f - viewport.y));
                    float angleFade = SmoothStep(Mathf.InverseLerp(.025f, .24f, forward));
                    float edgeFade = SmoothStep(Mathf.InverseLerp(0f, .095f, edge));
                    if (angleFade > 0f && edgeFade > 0f)
                    {
                        int hits = Physics.RaycastNonAlloc(camera.transform.position + sun * .12f, sun, LensFlareHit020,
                            LensFlareRayDistance020, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        if (hits == 0)
                        {
                            Vector3 tangent = cameraForward - sun * forward;
                            if (tangent.sqrMagnitude < 1e-6f) tangent = camera.transform.right;
                            tangent.Normalize();
                            if (!Finite(tangent)) tangent = camera.transform.right;
                            if (!Finite(tangent)) tangent = Vector3.right;
                            tangent.Normalize();
                            axisValue = new Vector4(tangent.x, tangent.y, tangent.z, 0f);
                            visibility = Mathf.Clamp01(cycleView020.Daylight) * angleFade * edgeFade;
                        }
                    }
                }
            }
            sky.SetVector(FlareAxisId020, axisValue);
            sky.SetFloat(FlareVisibilityId020, visibility);
        }

        static float SmoothStep(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        #if UNITY_EDITOR
        public bool TryReadEditorSunLensFlare020(Camera ownedCaptureCamera,out float visibility)
        {
            visibility=0f;
            if(ownedCaptureCamera==null||!Bound020||!IsEditorCaptureLeaseOwned020(ownedCaptureCamera)||
                !ReferenceEquals(captureCamera,ownedCaptureCamera)||sky==null||skyFallback||!sky.HasProperty(FlareVisibilityId020))return false;
            float observed=sky.GetFloat(FlareVisibilityId020);
            if(float.IsNaN(observed)||float.IsInfinity(observed))return false;
            visibility=observed;
            return true;
        }
        #endif

        void RetireLensClouds020()
        {
            if (cloudSurfaceStateCaptured020 && cloudSurfaceOwner020 != null &&
                cloudSurfaceOwner020.HasProperty(CloudShadowStateId020) &&
                cloudSurfaceOwner020.GetVector(CloudShadowStateId020).Equals(installedCloudShadowState020))
                cloudSurfaceOwner020.SetVector(CloudShadowStateId020, previousCloudShadowState020);
            if (sky != null)
            {
                if (sky.HasProperty(CloudVisibilityId020)) sky.SetFloat(CloudVisibilityId020, 0f);
                if (sky.HasProperty(CloudSeedId020)) sky.SetFloat(CloudSeedId020, 0f);
                if (sky.HasProperty(FlareVisibilityId020)) sky.SetFloat(FlareVisibilityId020, 0f);
                if (sky.HasProperty(FlareAxisId020)) sky.SetVector(FlareAxisId020, Vector4.zero);
            }
            cloudSurfaceOwner020 = null;
            cloudSurfaceStateCaptured020 = false;
            previousCloudShadowState020 = installedCloudShadowState020 = Vector4.zero;
            cloudSeedGeneration020 = cloudSeedRevision020 = 0;
            cloudSeed020 = 0f;
            cloudsEnabled020 = weatherEffectsEnabled020 = true;
            cloudShadowsEnabled020 = callerPaused020 = false;
            cloudShadowQuality020 = 0;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One admitted world/session revision seed, normalized sky direction, exact camera viewport, and stationary world-metre floor projection.
// Array Formats: One cold RaycastHit[1], cached scalar preferences/seed, and one conditionally restored Vector4 on the borrowed surface material.
// Dependencies/Pointers: Existing atmosphere lease, retained directional key, held cycle/quality, GraphicsPreferences instance, private sky material, main-thread Physics query, and existing URP surface floor mask; an Editor-only exact-capture-camera getter reads the existing flare scalar without mutation. No new clock, GameObject, Light, camera, post-processing owner, or per-frame allocation.
