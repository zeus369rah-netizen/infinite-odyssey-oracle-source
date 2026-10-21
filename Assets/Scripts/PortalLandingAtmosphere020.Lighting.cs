using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public sealed partial class PortalLandingAtmosphere020
    {
        static readonly int CycleSunDirectionId020 = Shader.PropertyToID("_CycleSunDirection020");
        static readonly Color DayKeyColor020 = new Color(.92f, .84f, .70f);
        static readonly Color DayAmbientSky020 = new Color(.36f, .42f, .50f);
        static readonly Color DayAmbientEquator020 = new Color(.23f, .28f, .34f);
        static readonly Color DayAmbientGround020 = new Color(.13f, .15f, .18f);
        static readonly Color DayProbeColor020 = new Color(.242f, .286f, .352f);
        static readonly Color NightProbeColor020 = new Color(.16f, .115f, .18f);
        static readonly Color DayFogColor020 = new Color(.075f, .10f, .14f);
        const float DayFogDensity020 = .0023f;

        Light cycleKeyLight020;
        float originalKeyIntensity020, installedKeyIntensity020;
        Color originalKeyColor020, installedKeyColor020;
        Quaternion originalKeyRotation020, installedKeyRotation020;
        bool cycleKeyCaptured020;
        Color installedSkyColor020, installedEquatorColor020, installedGroundColor020, installedFogColor020;
        float installedAmbientIntensity020, installedFogDensity020;
        bool installedFog020;
        FogMode installedFogMode020;

        void CaptureCycleLighting020(Light key)
        {
            cycleKeyLight020 = key;
            if (key == null)
            {
                cycleKeyCaptured020 = false;
                return;
            }
            originalKeyIntensity020 = installedKeyIntensity020 = key.intensity;
            originalKeyColor020 = installedKeyColor020 = key.color;
            originalKeyRotation020 = installedKeyRotation020 = key.transform.rotation;
            cycleKeyCaptured020 = true;
        }

        void RecordInstalledAmbient020()
        {
            installedSkyColor020 = RenderSettings.ambientSkyColor;
            installedEquatorColor020 = RenderSettings.ambientEquatorColor;
            installedGroundColor020 = RenderSettings.ambientGroundColor;
            installedAmbientIntensity020 = RenderSettings.ambientIntensity;
            installedProbe = RenderSettings.ambientProbe;
            installedFog020 = RenderSettings.fog;
            installedFogMode020 = RenderSettings.fogMode;
            installedFogColor020 = RenderSettings.fogColor;
            installedFogDensity020 = RenderSettings.fogDensity;
        }

        void ApplyCycleLighting020()
        {
            if (!cycleViewReady020 || !cycleKeyCaptured020 || cycleKeyLight020 == null) return;
            float daylight = Mathf.Clamp01(cycleView020.Daylight);
            float elevation = CampaignCycleRules020.SunElevationDegrees(cycleView020);
            float azimuth = CampaignCycleRules020.SunAzimuthDegrees(cycleView020);
            Quaternion dayRotation = Quaternion.Euler(elevation, azimuth, 0f);
            float dayArc = CampaignCycleRules020.KeyArcBlend(cycleView020);
            Quaternion targetRotation = Quaternion.Slerp(originalKeyRotation020, dayRotation, dayArc);
            Color targetColor = Color.Lerp(originalKeyColor020, DayKeyColor020, daylight);

            cycleKeyLight020.intensity = cycleView020.DirectionalIntensity;
            cycleKeyLight020.color = targetColor;
            cycleKeyLight020.transform.rotation = targetRotation;
            installedKeyIntensity020 = cycleKeyLight020.intensity;
            installedKeyColor020 = cycleKeyLight020.color;
            installedKeyRotation020 = cycleKeyLight020.transform.rotation;

            Vector3 sunDirection = -cycleKeyLight020.transform.forward;
            if (sky != null && sky.HasProperty(CycleSunDirectionId020))
                sky.SetVector(CycleSunDirectionId020, new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));

            if (RenderSettings.skybox != sky || RenderSettings.ambientMode != AmbientMode.Trilight) return;
            if (RenderSettings.ambientSkyColor == installedSkyColor020)
            {
                installedSkyColor020 = Color.Lerp(SkyColor, DayAmbientSky020, daylight);
                RenderSettings.ambientSkyColor = installedSkyColor020;
            }
            if (RenderSettings.ambientEquatorColor == installedEquatorColor020)
            {
                installedEquatorColor020 = Color.Lerp(EquatorColor, DayAmbientEquator020, daylight);
                RenderSettings.ambientEquatorColor = installedEquatorColor020;
            }
            if (RenderSettings.ambientGroundColor == installedGroundColor020)
            {
                installedGroundColor020 = Color.Lerp(GroundColor, DayAmbientGround020, daylight);
                RenderSettings.ambientGroundColor = installedGroundColor020;
            }
            if (RenderSettings.ambientIntensity == installedAmbientIntensity020)
            {
                installedAmbientIntensity020 = cycleView020.AmbientScale;
                RenderSettings.ambientIntensity = installedAmbientIntensity020;
            }
            if (SameProbe(RenderSettings.ambientProbe, installedProbe))
            {
                SphericalHarmonicsL2 nextProbe = default;
                nextProbe.AddAmbientLight(Color.Lerp(NightProbeColor020, DayProbeColor020, daylight));
                installedProbe = nextProbe;
                RenderSettings.ambientProbe = installedProbe;
            }
            if (RenderSettings.fogColor == installedFogColor020)
            {
                installedFogColor020 = Color.Lerp(FogColor, DayFogColor020, daylight);
                RenderSettings.fogColor = installedFogColor020;
            }
            if (RenderSettings.fogDensity == installedFogDensity020)
            {
                installedFogDensity020 = Mathf.Lerp(FogDensity, DayFogDensity020, daylight);
                RenderSettings.fogDensity = installedFogDensity020;
            }
        }

        void RestoreCycleLighting020()
        {
            if (bound && ReferenceEquals(globalLease, this))
            {
                if (cycleKeyCaptured020 && cycleKeyLight020 != null)
                {
                    if (cycleKeyLight020.intensity == installedKeyIntensity020) cycleKeyLight020.intensity = originalKeyIntensity020;
                    if (cycleKeyLight020.color == installedKeyColor020) cycleKeyLight020.color = originalKeyColor020;
                    if (cycleKeyLight020.transform.rotation == installedKeyRotation020) cycleKeyLight020.transform.rotation = originalKeyRotation020;
                }
            }
            cycleKeyLight020 = null;
            cycleKeyCaptured020 = false;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted CampaignHub lease, one retained session key, world Y-up ambient/fog settings, and normalized sky direction.
// Array Formats: Scalar/Color/Quaternion structs and one existing Light pointer; no arrays, per-frame allocations, new GameObjects, or per-frame component discovery.
// Dependencies/Pointers: Borrows only GameSession's existing directional Light, preserves its enabled/type/shadow settings, and drives its direction into the current sky. Ambient/probe/fog updates are guarded by current ownership and each field restores only when unchanged from its last write. No flare post-process or second global owner.
