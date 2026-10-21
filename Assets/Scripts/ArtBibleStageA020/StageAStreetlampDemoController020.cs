/*
PSEUDOCODE
1. On enable, finish retained cleanup, capture the optional serialized amber Point Light baseline, and validate caps of 64 renderers and 8 slots per row.
2. Make one private runtime clone; snapshot rows before replacing source slots. A failed setup rolls back selectively and retains rows still referencing the clone.
3. Each frame, preserve the held 4096-second clock and focus/application/manual/reduced-motion holds; update changed shader floats and the single assigned light from one bounded core-emission multiplier.
4. On disable/retry, restore only current slots referencing this clone and restore light intensity only when it still equals the last value written by this owner. Preserve foreign changes and retain failed cleanup state for retry.
5. WebGL follows the browser preference getter; native builds use an explicit serialized preview switch because the native getter is always false, not an OS reduced-motion query.
*/
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>Demo-only Stage A lighting and wind preview for one authored streetlamp asset.</summary>
    [DisallowMultipleComponent]
    public sealed class StageAStreetlampDemoController020 : MonoBehaviour
    {
        const int MaximumRendererCount = 64;
        const int MaximumMaterialSlots = 8;
        const float ClockWrapSeconds = 4096f;
        const float MaximumFrameStep = 0.05f;
        const float TwoPi = 6.28318530718f;
        const float BaseWetness = 0.32f;
        const float BaseWindStrength = 0.07f;
        const float GustWindStrength = 0.07f;
        const float MinimumCoreEmissionScale = 0.9f;
        const float MaximumCoreEmissionScale = 1.1f;

        static readonly int WindClockId = Shader.PropertyToID("_WindClock020");
        static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength020");
        static readonly int WetnessId = Shader.PropertyToID("_Wetness020");
        static readonly int EmissionScaleId = Shader.PropertyToID("_EmissionScale020");

        [SerializeField] Material sourceMaterial;
        [SerializeField] Renderer[] assetRenderers;
        [SerializeField] Light optionalAmberPointLight;
        [SerializeField] bool startPaused;
#if !UNITY_WEBGL || UNITY_EDITOR
        [SerializeField] bool nativeReducedMotion;
#endif

        Renderer[] boundRenderers;
        Material[][] originalMaterialRows;
        Material runtimeMaterial;
        Material runtimeSourceMaterial;
        Light boundAmberPointLight;
        float amberPointLightBaselineIntensity;
        float lastAmberPointLightIntensity = float.NaN;
        float attemptedAmberPointLightIntensity = float.NaN;
        float currentCoreEmissionMultiplier = 1f;
        float heldClockSeconds;
        float lastWindClock = float.NaN;
        float lastWindStrength = float.NaN;
        float lastWetness = float.NaN;
        float lastEmissionScale = float.NaN;
        bool pauseRequested;
        bool applicationPaused;
        bool cleanupBlocked;
        bool hasAmberPointLightSnapshot;
        bool hasAmberPointLightWrite;
        bool hasAmberPointLightWriteAttempt;

        public bool IsPaused => pauseRequested;
        public bool IsCleanupBlocked => cleanupBlocked;
        public bool IsReducedMotionActive => ShouldUseReducedMotion();
        public bool HasAmberPointLightBinding => hasAmberPointLightSnapshot && boundAmberPointLight != null;
        public bool OwnsAmberPointLightWrite => hasAmberPointLightWrite;
        public float AmberPointLightBaselineIntensity => hasAmberPointLightSnapshot ? amberPointLightBaselineIntensity : 0f;
        public float CoreEmissionMultiplier => currentCoreEmissionMultiplier;

        void OnEnable()
        {
            if (HasOwnedRuntimeState())
            {
                try
                {
                    ReleaseRuntimeState();
                }
                catch (System.Exception exception)
                {
                    cleanupBlocked = true;
                    Debug.LogError("Stage A demo re-enable blocked until owned-state cleanup succeeds: " + exception.Message);
                    return;
                }
            }

            cleanupBlocked = false;
            pauseRequested = startPaused;
            applicationPaused = false;
            try
            {
                CaptureOptionalAmberPointLight();
                PrepareRuntimeState();
            }
            catch
            {
                try
                {
                    ReleaseRuntimeState();
                    cleanupBlocked = false;
                }
                catch (System.Exception cleanupException)
                {
                    cleanupBlocked = true;
                    Debug.LogError("Stage A demo setup rollback is pending explicit cleanup retry: " + cleanupException.Message);
                }
                throw;
            }
        }

        void PrepareRuntimeState()
        {
            if (sourceMaterial == null || assetRenderers == null || assetRenderers.Length == 0)
                return;
            if (assetRenderers.Length > MaximumRendererCount)
                throw new System.InvalidOperationException("Stage A demo renderer list exceeds its cold capacity.");
            if (!sourceMaterial.HasProperty(WindClockId) ||
                !sourceMaterial.HasProperty(WindStrengthId) ||
                !sourceMaterial.HasProperty(WetnessId) ||
                !sourceMaterial.HasProperty(EmissionScaleId))
                return;

            Material clone = new Material(sourceMaterial);
            runtimeSourceMaterial = sourceMaterial;
            runtimeMaterial = clone;
            clone.name = sourceMaterial.name + " (Stage A demo runtime)";
            clone.hideFlags = HideFlags.DontSave;
            boundRenderers = new Renderer[assetRenderers.Length];
            originalMaterialRows = new Material[assetRenderers.Length][];

            bool reboundAnySlot = false;
            for (int i = 0; i < assetRenderers.Length; i++)
            {
                Renderer target = assetRenderers[i];
                if (target == null || IsEarlierDuplicate(target, i))
                    continue;

                Material[] originalRow = target.sharedMaterials;
                if (originalRow == null)
                    throw new System.InvalidOperationException("Renderer returned a null shared-material row.");
                if (originalRow.Length > MaximumMaterialSlots)
                    throw new System.InvalidOperationException("Stage A demo renderer row exceeds its cold slot capacity.");

                bool containsSource = false;
                for (int slot = 0; slot < originalRow.Length; slot++)
                    containsSource |= System.Object.ReferenceEquals(originalRow[slot], sourceMaterial);
                if (!containsSource)
                    continue;

                // Record the exact row before attempting the setter; rollback can handle a setter that throws after partial mutation.
                boundRenderers[i] = target;
                originalMaterialRows[i] = originalRow;
                Material[] replacementRow = (Material[])originalRow.Clone();
                for (int slot = 0; slot < replacementRow.Length; slot++)
                {
                    if (System.Object.ReferenceEquals(replacementRow[slot], sourceMaterial))
                        replacementRow[slot] = clone;
                }
                target.sharedMaterials = replacementRow;
                reboundAnySlot = true;
            }

            if (!reboundAnySlot)
            {
                if (Application.isPlaying)
                    Destroy(clone);
                else
                    DestroyImmediate(clone);
                ClearMaterialRuntimeState();
                return;
            }

            heldClockSeconds = 0f;
            lastWindClock = float.NaN;
            lastWindStrength = float.NaN;
            lastWetness = float.NaN;
            lastEmissionScale = float.NaN;
            bool reducedMotion = ShouldUseReducedMotion();
            float initialWind = reducedMotion ? 0f : BaseWindStrength + GustWindStrength * 0.5f;
            ApplyValues(0f, initialWind, BaseWetness, 1f);
            ApplyOptionalAmberPointLightEmission(1f);
        }

        void CaptureOptionalAmberPointLight()
        {
            Light candidate = optionalAmberPointLight;
            if (candidate == null)
                return;
            if (candidate.type != LightType.Point)
                throw new System.InvalidOperationException("The optional demo light must be an existing Point Light.");
            if (candidate.shadows != LightShadows.None)
                throw new System.InvalidOperationException("The optional demo Point Light must already be shadowless.");

            float baseline = candidate.intensity;
            if (float.IsNaN(baseline) || float.IsInfinity(baseline))
                throw new System.InvalidOperationException("The demo amber light has a non-finite baseline intensity.");

            boundAmberPointLight = candidate;
            amberPointLightBaselineIntensity = baseline;
            lastAmberPointLightIntensity = float.NaN;
            attemptedAmberPointLightIntensity = float.NaN;
            currentCoreEmissionMultiplier = 1f;
            hasAmberPointLightSnapshot = true;
            hasAmberPointLightWrite = false;
            hasAmberPointLightWriteAttempt = false;
        }

        void Update()
        {
            if (cleanupBlocked)
                return;

            Material material = runtimeMaterial;
            bool hasMaterial = material != null;
            bool hasLight = hasAmberPointLightSnapshot && boundAmberPointLight != null;
            if (!hasMaterial && !hasLight)
                return;

            bool reducedMotion = ShouldUseReducedMotion();
            bool holdClock = pauseRequested || applicationPaused || !Application.isFocused || reducedMotion;
            if (!holdClock)
            {
                float step = Mathf.Clamp(Time.unscaledDeltaTime, 0f, MaximumFrameStep);
                heldClockSeconds = Mathf.Repeat(heldClockSeconds + step, ClockWrapSeconds);
            }

            if (reducedMotion)
            {
                currentCoreEmissionMultiplier = 1f;
                if (hasMaterial)
                    ApplyValues(heldClockSeconds, 0f, BaseWetness, 1f);
                ApplyOptionalAmberPointLightEmission(1f);
                return;
            }

            float phase = heldClockSeconds * TwoPi;
            float gust = 0.5f + 0.5f * Mathf.Sin(phase * 0.125f);
            float wind = Mathf.Clamp(BaseWindStrength + GustWindStrength * gust, 0f, 0.14f);
            float wetness = Mathf.Clamp(BaseWetness + 0.04f * Mathf.Sin(phase * 0.0625f), 0f, 0.5f);
            float emission = Mathf.Clamp(1f + 0.06f * Mathf.Sin(phase * 0.75f),
                MinimumCoreEmissionScale, MaximumCoreEmissionScale);
            currentCoreEmissionMultiplier = emission;
            if (hasMaterial)
                ApplyValues(heldClockSeconds, wind, wetness, emission);
            ApplyOptionalAmberPointLightEmission(emission);
        }

        public void SetPaused(bool value)
        {
            pauseRequested = value;
        }

        /// <summary>Retries retained renderer detachment; if active, resumes setup only after old ownership is fully released.</summary>
        public bool RetryPendingCleanup()
        {
            if (!HasOwnedRuntimeState())
            {
                cleanupBlocked = false;
                return true;
            }

            try
            {
                ReleaseRuntimeState();
                cleanupBlocked = false;
            }
            catch (System.Exception exception)
            {
                cleanupBlocked = true;
                Debug.LogError("Stage A demo cleanup retry remains blocked: " + exception.Message);
                return false;
            }

            if (isActiveAndEnabled)
            {
                try
                {
                    CaptureOptionalAmberPointLight();
                    PrepareRuntimeState();
                }
                catch (System.Exception setupException)
                {
                    try
                    {
                        ReleaseRuntimeState();
                        cleanupBlocked = false;
                    }
                    catch (System.Exception cleanupException)
                    {
                        cleanupBlocked = true;
                        Debug.LogError("Stage A demo retry setup rollback remains pending: " + cleanupException.Message);
                    }
                    Debug.LogError("Stage A demo retry setup failed: " + setupException.Message);
                    return false;
                }
            }
            return true;
        }

        void OnApplicationPause(bool isPaused)
        {
            applicationPaused = isPaused;
        }

        bool IsEarlierDuplicate(Renderer candidate, int endExclusive)
        {
            for (int i = 0; i < endExclusive; i++)
            {
                if (boundRenderers != null && boundRenderers[i] == candidate)
                    return true;
            }
            return false;
        }

        bool ShouldUseReducedMotion()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return OracleBookAccessibility.PrefersReducedMotion;
#else
            return nativeReducedMotion;
#endif
        }

        void ApplyValues(float clock, float wind, float wetness, float emission)
        {
            Material material = runtimeMaterial;
            if (material == null)
                return;

            SetFloatIfChanged(material, WindClockId, clock, ref lastWindClock);
            SetFloatIfChanged(material, WindStrengthId, wind, ref lastWindStrength);
            SetFloatIfChanged(material, WetnessId, wetness, ref lastWetness);
            SetFloatIfChanged(material, EmissionScaleId, emission, ref lastEmissionScale);
        }

        void ApplyOptionalAmberPointLightEmission(float emissionMultiplier)
        {
            if (!hasAmberPointLightSnapshot)
                return;

            Light target = boundAmberPointLight;
            if (target == null)
                return;

            float boundedMultiplier = Mathf.Clamp(emissionMultiplier, MinimumCoreEmissionScale, MaximumCoreEmissionScale);
            float desiredIntensity = amberPointLightBaselineIntensity * boundedMultiplier;
            if (hasAmberPointLightWrite && desiredIntensity == lastAmberPointLightIntensity)
                return;

            attemptedAmberPointLightIntensity = desiredIntensity;
            hasAmberPointLightWriteAttempt = true;
            try
            {
                target.intensity = desiredIntensity;
            }
            catch
            {
                cleanupBlocked = true;
                throw;
            }
            lastAmberPointLightIntensity = desiredIntensity;
            hasAmberPointLightWrite = true;
            attemptedAmberPointLightIntensity = float.NaN;
            hasAmberPointLightWriteAttempt = false;
        }

        static void SetFloatIfChanged(Material material, int propertyId, float value, ref float previous)
        {
            if (float.IsNaN(previous) || Mathf.Abs(value - previous) > 0.000001f)
            {
                material.SetFloat(propertyId, value);
                previous = value;
            }
        }

        void OnDisable()
        {
            try
            {
                ReleaseRuntimeState();
                cleanupBlocked = false;
            }
            catch (System.Exception exception)
            {
                cleanupBlocked = true;
                Debug.LogError("Stage A demo retained owned-state cleanup for explicit retry: " + exception.Message);
            }
        }

        void ReleaseRuntimeState()
        {
            Material clone = runtimeMaterial;
            System.Exception firstFailure = null;
            bool cloneReferenceAvailable = !System.Object.ReferenceEquals(clone, null);
            int rendererCount = boundRenderers == null || originalMaterialRows == null
                ? 0
                : System.Math.Min(boundRenderers.Length, originalMaterialRows.Length);
            if (!cloneReferenceAvailable && HasPendingRendererRows())
                firstFailure = new System.InvalidOperationException("Owned material reference is missing while renderer snapshots remain.");

            for (int i = 0; cloneReferenceAvailable && i < rendererCount; i++)
            {
                Renderer target = boundRenderers[i];
                Material[] originalRow = originalMaterialRows[i];
                if (originalRow == null)
                {
                    if (boundRenderers != null)
                        boundRenderers[i] = null;
                    continue;
                }
                if (target == null)
                {
                    ClearRendererEntry(i);
                    continue;
                }

                try
                {
                    Material[] currentRow = target.sharedMaterials;
                    if (!ContainsCloneReference(currentRow, clone))
                    {
                        ClearRendererEntry(i);
                        continue;
                    }

                    Material[] restoredRow = (Material[])currentRow.Clone();
                    for (int slot = 0; slot < restoredRow.Length; slot++)
                    {
                        if (!System.Object.ReferenceEquals(restoredRow[slot], clone))
                            continue;

                        Material replacement = slot < originalRow.Length ? originalRow[slot] : runtimeSourceMaterial;
                        if (System.Object.ReferenceEquals(replacement, clone))
                            replacement = runtimeSourceMaterial;
                        restoredRow[slot] = replacement;
                    }
                    target.sharedMaterials = restoredRow;
                    if (ContainsCloneReference(target.sharedMaterials, clone))
                        throw new System.InvalidOperationException("Renderer setter left the private material clone bound.");
                    ClearRendererEntry(i);
                }
                catch (System.Exception exception)
                {
                    if (firstFailure == null)
                        firstFailure = exception;
                }
            }

            if (HasPendingRendererRows())
            {
                if (firstFailure == null)
                    firstFailure = new System.InvalidOperationException("Some renderer rows remain bound to the private clone.");
            }

            try
            {
                ReleaseOptionalAmberPointLight();
            }
            catch (System.Exception exception)
            {
                if (firstFailure == null)
                    firstFailure = exception;
            }

            if (HasPendingRendererRows())
                throw firstFailure;

            if (cloneReferenceAvailable)
            {
                if (Application.isPlaying)
                    Destroy(clone);
                else
                    DestroyImmediate(clone);
            }
            ClearMaterialRuntimeState();

            if (firstFailure != null)
                throw firstFailure;
        }

        void ReleaseOptionalAmberPointLight()
        {
            if (!hasAmberPointLightSnapshot)
            {
                ClearAmberPointLightState();
                return;
            }

            Light target = boundAmberPointLight;
            if (target != null)
            {
                float currentIntensity = target.intensity;
                bool stillOwnedValue = hasAmberPointLightWrite && currentIntensity == lastAmberPointLightIntensity;
                bool partiallyAppliedAttempt = hasAmberPointLightWriteAttempt && currentIntensity == attemptedAmberPointLightIntensity;
                if (stillOwnedValue || partiallyAppliedAttempt)
                {
                    target.intensity = amberPointLightBaselineIntensity;
                    if (target.intensity != amberPointLightBaselineIntensity)
                        throw new System.InvalidOperationException("Amber light baseline restoration did not persist.");
                }
            }
            ClearAmberPointLightState();
        }

        bool HasOwnedRuntimeState()
        {
            return !System.Object.ReferenceEquals(runtimeMaterial, null) ||
                   !System.Object.ReferenceEquals(runtimeSourceMaterial, null) ||
                   boundRenderers != null || originalMaterialRows != null || hasAmberPointLightSnapshot;
        }

        bool HasPendingRendererRows()
        {
            if (originalMaterialRows == null)
                return false;
            for (int i = 0; i < originalMaterialRows.Length; i++)
            {
                if (originalMaterialRows[i] != null)
                    return true;
            }
            return false;
        }

        static bool ContainsCloneReference(Material[] row, Material clone)
        {
            if (row == null)
                return false;
            for (int slot = 0; slot < row.Length; slot++)
            {
                if (System.Object.ReferenceEquals(row[slot], clone))
                    return true;
            }
            return false;
        }

        void ClearRendererEntry(int index)
        {
            if (boundRenderers != null && index < boundRenderers.Length)
                boundRenderers[index] = null;
            if (originalMaterialRows != null && index < originalMaterialRows.Length)
                originalMaterialRows[index] = null;
        }

        void ClearMaterialRuntimeState()
        {
            runtimeMaterial = null;
            runtimeSourceMaterial = null;
            boundRenderers = null;
            originalMaterialRows = null;
        }

        void ClearAmberPointLightState()
        {
            boundAmberPointLight = null;
            amberPointLightBaselineIntensity = 0f;
            lastAmberPointLightIntensity = float.NaN;
            attemptedAmberPointLightIntensity = float.NaN;
            currentCoreEmissionMultiplier = 1f;
            hasAmberPointLightSnapshot = false;
            hasAmberPointLightWrite = false;
            hasAmberPointLightWriteAttempt = false;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Demo-only local asset presentation; no transforms, vertices, or world-state ownership.
// Array Formats: Cold Renderer[] target list and captured Material[][] source bindings; one private shared Material clone and one optional serialized Light snapshot.
// Dependencies/Pointers: OdysseyOracle.OracleBookAccessibility.PrefersReducedMotion; main-thread Unity lifecycle, four cached shader float IDs, and one assigned amber light.
