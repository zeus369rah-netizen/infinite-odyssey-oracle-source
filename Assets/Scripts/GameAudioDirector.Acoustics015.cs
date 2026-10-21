using System;
using System.Collections.Generic;
using OdysseyOracle.Foundation.Audio;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    // Main-thread backend command only. No Object reference or provider resource enters native jobs.
    public struct AudioVoiceFrame015
    {
        public int Slot; public ulong ClipIdentity;
        public uint Generation, OwnerRevision, GeometryRevision, ProfileRevision;
        public Vector3 Position; public float Gain, Cutoff, Pitch, Duration; public bool Spatial;
    }
    public sealed partial class GameAudioDirector
    {
        public Func<AudioVoiceFrame015, bool> ExternalVoiceStart015;
        public Action<AudioVoiceFrame015> ExternalVoiceUpdate015;
        public Action<int, uint, uint> ExternalVoiceStop015;
        public Action ExternalVoiceUnlockRequested015;
        readonly AudioSource[] acousticSources015 = new AudioSource[8];
        readonly AudioLowPassFilter[] acousticFilters015 = new AudioLowPassFilter[8];
        readonly uint[] acousticGeneration015 = new uint[8];
        readonly uint[] acousticStatus015 = new uint[8];
        readonly float[] acousticGain015 = new float[8], acousticCutoff015 = new float[8], acousticNext015 = new float[8];
        readonly float[] acousticTargetGain015 = new float[8], acousticTargetCutoff015 = new float[8], externalUntil015 = new float[8];
        readonly bool[] acousticSpatial015 = new bool[8], externalVoice015 = new bool[8];
        readonly ulong[] acousticListenerCell015 = new ulong[8], acousticEmitterCell015 = new ulong[8];
        readonly ulong[] acousticTowerIds015 = new ulong[8];
        readonly uint[] acousticTestGenerations015 = new uint[8];
        readonly List<Collider> acousticRegistration015 = new List<Collider>(4096);
        readonly RaycastHit[] acousticHits015 = new RaycastHit[16];
        readonly ProductionCueAdmission towerAdmission015 = new ProductionCueAdmission();
        NativeParallelHashMap<ulong, AudioBlocker32> acousticBlockers015;
        NativeArray<AudioBlocker32> acousticTowers015;
        NativeArray<AudioQuery80> acousticQueries015;
        NativeArray<AudioHit16> acousticHitValues015;
        NativeArray<AudioResult48> acousticResults015;
        JobHandle acousticReader015;
        static uint nextAcousticOwner015;
        uint acousticOwner015, acousticGeometry015 = 1, acousticProfileRevision015 = 1;
        int acousticPending015, acousticCursor015, acousticKind015;
        bool acousticReady015, acousticRegistryKnown015, acousticTowerUnknown015, acousticEnabled015 = true, acousticSpatializer015;
        bool acousticPluginAvailable015;
        bool acousticExternalReady015, acousticExternalHrtf015, acousticExternalLowpass015;
        uint acousticUi015 = 1;
        float acousticCustom015 = .7f, acousticTestNext015;
        int acousticTestStep015 = -1;
        int acousticTestMask015;
        bool acousticTestCapture015;
        int acousticRays015, acousticPublished015, acousticStale015, acousticUnknown015, acousticBlocked015;
        string acousticBackendLabel015 = "Positional stereo; no HRTF plugin is configured.";
        string acousticNativeLabel015;
        public uint AcousticUiRevision015 => acousticUi015;
        public uint AcousticOwnerRevision015 => acousticOwner015;
        public uint AcousticGeometryRevision015 => acousticGeometry015;
        public int AcousticRayCount015 => acousticRays015;
        public int AcousticPublishedCount015 => acousticPublished015;
        public int AcousticStaleCount015 => acousticStale015;
        public int AcousticUnknownCount015 => acousticUnknown015;
        public int AcousticBlockedCount015 => acousticBlocked015;
        public bool AcousticEnabled015 => acousticEnabled015;
        public int AcousticProfile015 => acousticKind015;
        public float AcousticStrength015 => acousticCustom015;
        public bool AcousticSpatializerRequested015 => acousticSpatializer015;
        public bool AcousticNativePluginAvailable015 => acousticPluginAvailable015;
        public string AcousticBackendLabel015 => acousticBackendLabel015;
        public bool AcousticRegistryKnown015 => acousticRegistryKnown015;
        AudioProfile32 CurrentAcousticProfile015 => AudioAcousticMath015.Profile(acousticKind015, acousticCustom015, acousticSpatializer015);

        void InitializeAcoustics015()
        {
            nextAcousticOwner015 = AudioAcousticMath015.Next(nextAcousticOwner015); acousticOwner015 = nextAcousticOwner015;
            acousticBlockers015 = new NativeParallelHashMap<ulong, AudioBlocker32>(AudioAcousticMath015.Blockers, Allocator.Persistent);
            acousticTowers015 = new NativeArray<AudioBlocker32>(8, Allocator.Persistent);
            acousticQueries015 = new NativeArray<AudioQuery80>(2, Allocator.Persistent);
            acousticHitValues015 = new NativeArray<AudioHit16>(32, Allocator.Persistent);
            acousticResults015 = new NativeArray<AudioResult48>(2, Allocator.Persistent);
            acousticReady015 = true;
            acousticEnabled015 = PlayerPrefs.GetInt(PreferencePrefix + "Occlusion015", 1) != 0;
            acousticKind015 = Mathf.Clamp(PlayerPrefs.GetInt(PreferencePrefix + "Profile015", 0), 0, 3);
            acousticCustom015 = ReadVolume("OcclusionStrength015", .7f);
            acousticSpatializer015 = PlayerPrefs.GetInt(PreferencePrefix + "Spatializer015", 0) != 0;
#if !UNITY_WEBGL || UNITY_EDITOR
            // Cold capability query only. A configured plugin name is not proof of HRTF quality/support.
            string plugin = AudioSettings.GetSpatializerPluginName();
            acousticPluginAvailable015 = !string.IsNullOrEmpty(plugin);
            if (acousticPluginAvailable015) acousticBackendLabel015 = "Configured spatializer available; device listening test required.";
#else
            acousticBackendLabel015 = "Browser positional/gain fallback; enhanced browser backend not yet accepted.";
#endif
            acousticNativeLabel015 = acousticBackendLabel015;
            for (int slot = 0; slot < 8; slot++)
            {
                AudioSource source = slot < 4 ? cuePool[slot] : towerPool[slot - 4]; acousticSources015[slot] = source;
                acousticGain015[slot] = acousticTargetGain015[slot] = 1; acousticCutoff015[slot] = acousticTargetCutoff015[slot] = 22000;
#if !UNITY_WEBGL || UNITY_EDITOR
                if (source != null) { var filter = source.gameObject.AddComponent<AudioLowPassFilter>(); filter.lowpassResonanceQ = 1; filter.enabled = false; acousticFilters015[slot] = filter; }
#endif
            }
            RebuildAcousticWorld015(session != null && session.World != null ? session.World.transform : null);
        }
        // Cold topology boundary. Existing collider-free actors are not implicitly added to physics.
        public void RebuildAcousticWorld015(Transform world)
        {
            if (!acousticReady015) return; CompleteAcousticReader015(); acousticBlockers015.Clear();
            acousticRegistryKnown015 = world != null && !acousticTowerUnknown015; acousticRegistration015.Clear();
            if (world != null) world.GetComponentsInChildren(false, acousticRegistration015);
            for (int index = 0; index < acousticRegistration015.Count; index++)
            {
                Collider collider = acousticRegistration015[index];
                if (collider == null || !collider.enabled || collider.isTrigger || collider.attachedRigidbody != null) continue;
                Bounds bounds = collider.bounds;
                if (acousticBlockers015.Count() >= AudioAcousticMath015.Blockers ||
                    !acousticBlockers015.TryAdd(EntityId.ToULong(collider.GetEntityId()), Box015(bounds))) acousticRegistryKnown015 = false;
            }
            if (acousticBlockers015.Count() == 0) acousticRegistryKnown015 = false;
            acousticRegistration015.Clear(); InvalidateAcousticGeometry015();
        }
        public void RefreshAcousticCollider015(Collider collider)
        {
            if (!acousticReady015 || collider == null) return; CompleteAcousticReader015();
            ulong identity = EntityId.ToULong(collider.GetEntityId());
            bool active = collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger && collider.attachedRigidbody == null;
            if (!active) acousticBlockers015.Remove(identity);
            else if (acousticBlockers015.ContainsKey(identity)) acousticBlockers015[identity] = Box015(collider.bounds);
            else if (acousticBlockers015.Count() >= AudioAcousticMath015.Blockers || !acousticBlockers015.TryAdd(identity, Box015(collider.bounds))) acousticRegistryKnown015 = false;
            InvalidateAcousticGeometry015();
        }
        public bool SetAcousticTower015(ulong identity, Bounds bounds, bool active)
        {
            if (!acousticReady015 || identity == 0) return false; CompleteAcousticReader015();
            if (active && !AudioAcousticMath015.ValidBox(Box015(bounds))) { acousticTowerUnknown015 = true; acousticRegistryKnown015 = false; InvalidateAcousticGeometry015(); return false; }
            int selected = -1;
            for (int index = 0; index < 8; index++) if (acousticTowerIds015[index] == identity) { selected = index; break; }
            if (selected < 0 && active) for (int index = 0; index < 8; index++) if (acousticTowerIds015[index] == 0) { selected = index; break; }
            if (selected < 0) { if (active) { acousticTowerUnknown015 = true; acousticRegistryKnown015 = false; InvalidateAcousticGeometry015(); } return !active; }
            acousticTowerIds015[selected] = active ? identity : 0;
            acousticTowers015[selected] = active ? Box015(bounds) : default;
            InvalidateAcousticGeometry015(); return true;
        }
        static AudioBlocker32 Box015(Bounds value) => new AudioBlocker32 { Minimum = F015(value.min), Maximum = F015(value.max), Active = 1 };
        static float3 F015(Vector3 value) => new float3(value.x, value.y, value.z);
        void InvalidateAcousticGeometry015()
        {
            acousticGeometry015 = AudioAcousticMath015.Next(acousticGeometry015);
            for (int slot = 0; slot < 8; slot++) { acousticStatus015[slot] = AudioAcousticMath015.Unknown; acousticNext015[slot] = 0; acousticTargetGain015[slot] = .85f; acousticTargetCutoff015[slot] = 12000; }
        }
        void CompleteAcousticReader015() { if (acousticPending015 != 0) { acousticReader015.Complete(); acousticPending015 = 0; } }
        bool AcousticVoiceBusy015(int slot) => acousticReady015 && slot >= 0 && slot < 8 && externalVoice015[slot] && Time.unscaledTime < externalUntil015[slot];
        void PlayAcousticVoice015(int slot, AudioSource source)
        {
            if (!acousticReady015 || source == null) { if (source != null) source.Play(); return; }
            StopExternalVoice015(slot); acousticGeneration015[slot] = AudioAcousticMath015.Next(acousticGeneration015[slot]);
            acousticStatus015[slot] = AudioAcousticMath015.Unknown;
            acousticSpatial015[slot] = source.spatialBlend > .5f; acousticNext015[slot] = 0;
            acousticGain015[slot] = acousticTargetGain015[slot] = acousticSpatial015[slot] && acousticEnabled015 ? .85f : 1;
            acousticCutoff015[slot] = acousticTargetCutoff015[slot] = acousticSpatial015[slot] && acousticEnabled015 ? 12000 : 22000;
            source.volume *= AcousticGain015(slot);
#if !UNITY_WEBGL || UNITY_EDITOR
            if (acousticFilters015[slot] != null) { acousticFilters015[slot].enabled = acousticEnabled015 && acousticSpatial015[slot]; acousticFilters015[slot].cutoffFrequency = acousticCutoff015[slot]; }
#endif
            if (acousticTestCapture015) { acousticTestMask015 |= 1 << slot; acousticTestGenerations015[slot] = acousticGeneration015[slot]; }
#if !UNITY_WEBGL || UNITY_EDITOR
            source.spatialize = acousticSpatial015[slot] && acousticSpatializer015 && acousticPluginAvailable015;
#endif
            AudioVoiceFrame015 frame = VoiceFrame015(slot);
            bool accepted = frame.Duration > 0 && frame.Duration <= 120 && ExternalVoiceStart015 != null && ExternalVoiceStart015(frame);
            externalVoice015[slot] = accepted;
            externalUntil015[slot] = accepted ? Time.unscaledTime + frame.Duration / Mathf.Max(.1f, frame.Pitch) : 0;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (accepted && acousticFilters015[slot] != null) acousticFilters015[slot].enabled = false;
#endif
            if (!accepted) source.Play(); // Exactly one backend per lease; no mid-lease migration.
#if UNITY_WEBGL && !UNITY_EDITOR
            // Observation follows the actual branch; it cannot admit or replay a voice.
            if (webAudio015 != null) webAudio015.ObserveDecision015(frame, accepted);
#endif
        }
        void StopExternalVoice015(int slot)
        {
            if (externalVoice015[slot]) ExternalVoiceStop015?.Invoke(slot, acousticGeneration015[slot], acousticOwner015);
            externalVoice015[slot] = false; externalUntil015[slot] = 0;
        }
        AudioVoiceFrame015 VoiceFrame015(int slot)
        {
            AudioSource source = acousticSources015[slot]; AudioClip clip = source != null ? source.clip : null;
            return new AudioVoiceFrame015 { Slot = slot, ClipIdentity = clip != null ? EntityId.ToULong(clip.GetEntityId()) : 0,
                Generation = acousticGeneration015[slot], OwnerRevision = acousticOwner015, GeometryRevision = acousticGeometry015,
                ProfileRevision = acousticProfileRevision015, Position = source != null ? source.transform.position : Vector3.zero,
                Gain = CanPlayCue() && (slot < 4 || session != null && session.SimulationActive) ? masterVolume * sfxVolume * (slot < 4 ? cueGains[slot] : towerGains[slot - 4]) * acousticGain015[slot] * AcousticSpeechGain015 : 0,
                Cutoff = acousticCutoff015[slot], Pitch = source != null ? source.pitch : 1, Duration = clip != null ? clip.length : 0, Spatial = acousticSpatial015[slot] };
        }
        public bool TryReadAcousticVoice015(int slot, out AudioVoiceFrame015 frame)
        { frame = default; if (!acousticReady015 || slot < 0 || slot >= 8) return false; frame = VoiceFrame015(slot); return frame.Generation != 0; }
        public bool TryReadAcousticStatus015(int slot, out uint status)
        { status = 0; if (!acousticReady015 || slot < 0 || slot >= 8) return false; status = acousticStatus015[slot]; return true; }
        float AcousticSpeechGain015 => IsNarrationDucking009 ? CurrentAcousticProfile015.SpeechGain : 1f;
        float AcousticGain015(int slot) => acousticReady015 ? (acousticSpatial015[slot] ? acousticGain015[slot] : 1) * AcousticSpeechGain015 : 1;
        public void SetExternalAcousticCapability015(bool ready, bool hrtfRequested, bool lowpassAvailable)
        {
            if (ready == acousticExternalReady015 && hrtfRequested == acousticExternalHrtf015 && lowpassAvailable == acousticExternalLowpass015) return;
            acousticExternalReady015 = ready; acousticExternalHrtf015 = hrtfRequested; acousticExternalLowpass015 = lowpassAvailable;
            acousticBackendLabel015 = !ready ? acousticNativeLabel015 : hrtfRequested && lowpassAvailable ? "Browser HRTF panner and low-pass requested; listening test required." :
                lowpassAvailable ? "Browser positional low-pass available; HRTF is not requested." : "Browser positional gain fallback; low-pass is unavailable.";
            acousticUi015 = AudioAcousticMath015.Next(acousticUi015);
        }
        void TickAcoustics015(float dt)
        {
            if (!acousticReady015) return;
            bool context = CanPlayCue() && session != null && !session.IsDead && !session.Victory;
            if (!context) { StopAcousticVoices015(); return; }
            Vector3 listener = ListenerPosition(); float now = Time.unscaledTime; AudioProfile32 profile = CurrentAcousticProfile015;
            if (acousticPending015 > 0 && acousticReader015.IsCompleted)
            {
                acousticReader015.Complete();
                for (int row = 0; row < acousticPending015; row++)
                {
                    AudioResult48 result = acousticResults015[row]; int slot = result.Slot;
                    if (slot < 0 || slot >= 8 || !AudioAcousticMath015.ValidResult(result) || !AudioAcousticMath015.Matches(result, slot, acousticGeneration015[slot], acousticGeometry015, acousticProfileRevision015, acousticOwner015)) { CountAcoustic015(ref acousticStale015); continue; }
                    AudioSource source = acousticSources015[slot]; AudioQuery80 query = acousticQueries015[row];
                    if (source == null || (listener - V015(query.Listener)).sqrMagnitude > .25f || (source.transform.position - V015(query.Emitter)).sqrMagnitude > .25f) { CountAcoustic015(ref acousticStale015); continue; }
                    acousticTargetGain015[slot] = result.Gain; acousticTargetCutoff015[slot] = result.Cutoff;
                    acousticStatus015[slot] = result.Status; CountAcoustic015(ref acousticPublished015);
                    if (result.Status == AudioAcousticMath015.Unknown) CountAcoustic015(ref acousticUnknown015);
                    if (result.Status == AudioAcousticMath015.Blocked) CountAcoustic015(ref acousticBlocked015);
                }
                acousticPending015 = 0;
            }
            int count = 0;
            for (int attempt = 0; attempt < 8 && acousticPending015 == 0 && count < 2; attempt++)
            {
                int slot = acousticCursor015; acousticCursor015 = (acousticCursor015 + 1) & 7;
                AudioSource source = acousticSources015[slot];
                if (externalVoice015[slot] && now >= externalUntil015[slot]) StopExternalVoice015(slot);
                if (source == null || !acousticSpatial015[slot] || !source.isPlaying && !AcousticVoiceBusy015(slot)) continue;
                if (!acousticEnabled015) { acousticTargetGain015[slot] = 1; acousticTargetCutoff015[slot] = 22000; continue; }
                Vector3 emitter = source.transform.position;
                ulong listenerCell = 0, emitterCell = 0;
                bool keys = AudioAcousticMath015.TryCell(F015(listener), out listenerCell) && AudioAcousticMath015.TryCell(F015(emitter), out emitterCell);
                if (keys && now < acousticNext015[slot] && listenerCell == acousticListenerCell015[slot] && emitterCell == acousticEmitterCell015[slot]) continue;
                acousticNext015[slot] = now + .10f; acousticListenerCell015[slot] = listenerCell; acousticEmitterCell015[slot] = emitterCell;
                Vector3 delta = emitter - listener; float length = delta.magnitude;
                var query = new AudioQuery80 { Listener = F015(listener), Emitter = F015(emitter), Slot = slot,
                    ListenerCell = listenerCell, EmitterCell = emitterCell, Length = length, Generation = acousticGeneration015[slot],
                    GeometryRevision = acousticGeometry015, ProfileRevision = acousticProfileRevision015, OwnerRevision = acousticOwner015,
                    Flags = keys && length > .25f && length <= 30 ? AudioAcousticMath015.QueryValid : 0 };
                if (!acousticRegistryKnown015) query.Flags |= AudioAcousticMath015.RegistryUnknown;
                if (query.Flags == AudioAcousticMath015.QueryValid)
                {
                    Vector3 direction = delta / length;
                    query.HitCount = Physics.RaycastNonAlloc(listener + direction * .12f, direction, acousticHits015, length - .24f, ~(1 << 2), QueryTriggerInteraction.Ignore);
                    CountAcoustic015(ref acousticRays015);
                    if (query.HitCount == 16) query.Flags |= AudioAcousticMath015.QuerySaturated;
                    for (int hit = 0; hit < query.HitCount; hit++) acousticHitValues015[count * 16 + hit] = new AudioHit16 {
                        Identity = EntityId.ToULong(acousticHits015[hit].colliderEntityId), Distance = acousticHits015[hit].distance };
                }
                acousticQueries015[count++] = query;
            }
            if (count > 0) { acousticPending015 = count; acousticReader015 = new AudioAcousticJob015 { Queries = acousticQueries015, Hits = acousticHitValues015, Blockers = acousticBlockers015, Towers = acousticTowers015, Profile = profile, Results = acousticResults015 }.Schedule(count, 1); }
            for (int slot = 0; slot < 8; slot++)
            {
                acousticGain015[slot] = AudioAcousticMath015.Approach(acousticGain015[slot], acousticEnabled015 && acousticSpatial015[slot] ? acousticTargetGain015[slot] : 1, dt, acousticTargetGain015[slot] < acousticGain015[slot] ? profile.Attack : profile.Release);
                acousticCutoff015[slot] = AudioAcousticMath015.Approach(acousticCutoff015[slot], acousticEnabled015 && acousticSpatial015[slot] ? acousticTargetCutoff015[slot] : 22000, dt, profile.Release);
                var filter = acousticFilters015[slot]; if (filter != null) { filter.enabled = acousticEnabled015 && acousticSpatial015[slot] && acousticSources015[slot] != null && acousticSources015[slot].isPlaying && !externalVoice015[slot] && acousticCutoff015[slot] < 21000; if (filter.enabled) filter.cutoffFrequency = acousticCutoff015[slot]; }
                if (externalVoice015[slot]) ExternalVoiceUpdate015?.Invoke(VoiceFrame015(slot));
            }
            TickAcousticTest015(now);
        }
        static Vector3 V015(float3 value) => new Vector3(value.x, value.y, value.z);
        static void CountAcoustic015(ref int value) { if (value < int.MaxValue) value++; }
        public void SetAcousticProfile015(int kind, bool enabled, float customStrength, bool requestSpatializer)
        {
            if (kind < 0 || kind > 3 || float.IsNaN(customStrength) || float.IsInfinity(customStrength)) return;
            CompleteAcousticReader015(); SuspendExternalAudio015(); acousticKind015 = kind; acousticEnabled015 = enabled; acousticCustom015 = Mathf.Clamp01(customStrength); acousticSpatializer015 = requestSpatializer;
            acousticProfileRevision015 = AudioAcousticMath015.Next(acousticProfileRevision015);
            acousticUi015 = AudioAcousticMath015.Next(acousticUi015);
            PlayerPrefs.SetInt(PreferencePrefix + "Occlusion015", enabled ? 1 : 0); PlayerPrefs.SetInt(PreferencePrefix + "Profile015", kind);
            PlayerPrefs.SetFloat(PreferencePrefix + "OcclusionStrength015", acousticCustom015); PlayerPrefs.SetInt(PreferencePrefix + "Spatializer015", requestSpatializer ? 1 : 0); PlayerPrefs.Save();
            for (int slot = 0; slot < 8; slot++) { acousticNext015[slot] = 0;
#if !UNITY_WEBGL || UNITY_EDITOR
                if (acousticSources015[slot] != null) acousticSources015[slot].spatialize = acousticSpatial015[slot] && requestSpatializer && acousticPluginAvailable015;
#endif
            }
            if (requestSpatializer) ExternalVoiceUnlockRequested015?.Invoke();
        }
        public bool StartAcousticTest015()
        {
            if (!CanPlayCue() || session == null || session.Hud == null || !session.Hud.IsAcousticSettingsVisible015 || IsNarrationDucking009 || !HasProductionCue(ProductionCue.GuardianAggro)) return false;
            CancelAcousticTest015(); acousticTestStep015 = 0; acousticTestNext015 = Time.unscaledTime; return true;
        }
        void TickAcousticTest015(float now)
        {
            if (session == null || session.Hud == null || !session.Hud.IsAcousticSettingsVisible015 || IsNarrationDucking009) { CancelAcousticTest015(); return; }
            if (acousticTestStep015 < 0) return;
            if (now < acousticTestNext015) return;
            Vector3 right = listenerTransform != null ? listenerTransform.right : Vector3.right;
            acousticTestCapture015 = true;
            bool played = PlayPrioritizedCue(productionClips[(int)ProductionCue.GuardianAggro], ListenerPosition() + right * (acousticTestStep015 == 0 ? -3f : 3f), true, .05f, 0, .2f);
            acousticTestCapture015 = false;
            if (played)
            { acousticTestStep015++; acousticTestNext015 = now + 1.2f; if (acousticTestStep015 >= 2) acousticTestStep015 = -1; }
            else { acousticTestStep015 = -1; }
        }
        void StopAcousticVoices015()
        {
            if (!acousticReady015) return; CancelAcousticTest015(); towerAdmission015.Clear();
            for (int slot = 0; slot < 8; slot++) { StopExternalVoice015(slot); acousticGeneration015[slot] = AudioAcousticMath015.Next(acousticGeneration015[slot]); }
        }
        void CancelAcousticTest015()
        {
            acousticTestStep015 = -1;
            for (int slot = 0; slot < 8; slot++) if ((acousticTestMask015 & (1 << slot)) != 0 && acousticTestGenerations015[slot] == acousticGeneration015[slot])
            { if (acousticSources015[slot] != null) acousticSources015[slot].Stop(); StopExternalVoice015(slot); acousticGeneration015[slot] = AudioAcousticMath015.Next(acousticGeneration015[slot]); }
            acousticTestMask015 = 0;
        }
        public void SuspendExternalAudio015()
        {
            if (!acousticReady015) return;
            for (int slot = 0; slot < 8; slot++) if (externalVoice015[slot])
            { StopExternalVoice015(slot); acousticGeneration015[slot] = AudioAcousticMath015.Next(acousticGeneration015[slot]); }
        }
        void DisposeAcoustics015()
        {
            if (!acousticReady015) return; StopAcousticVoices015(); CompleteAcousticReader015(); acousticOwner015 = AudioAcousticMath015.Next(acousticOwner015);
            acousticBlockers015.Dispose(); acousticTowers015.Dispose(); acousticQueries015.Dispose(); acousticHitValues015.Dispose(); acousticResults015.Dispose(); acousticReady015 = false;
            ExternalVoiceStart015 = null; ExternalVoiceUpdate015 = null; ExternalVoiceStop015 = null; ExternalVoiceUnlockRequested015 = null;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World listener/emitter poses; audio-local0.5m refresh Morton cells.
// Array Formats:8voice generations/gains;4096opaque collider IDs;8tower boxes;2queries/32hits; cold Persistent ownership.
// Dependencies/Pointers: Main-thread Physics/AudioSource adapter only; one native reader completed before mutation/disposal. Existing dialogue owners never enter these pools. Positive external start owns one whole lease; Unity fallback on rejection.
