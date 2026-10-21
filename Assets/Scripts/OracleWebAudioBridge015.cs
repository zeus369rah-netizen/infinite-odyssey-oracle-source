using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>Main-thread scalar bridge. Root cold-binds after the acoustic adapter.</summary>
    [DisallowMultipleComponent]
    public sealed class OracleWebAudioBridge015 : MonoBehaviour
    {
        readonly ulong[] clipIdentities = new ulong[OracleWebAudioCatalog015.Count];
        GameAudioDirector owner;
#if UNITY_WEBGL && !UNITY_EDITOR
        Transform listener;
#endif
        uint lifetime;
        bool initialized, wasReady, unlockControlArmed, ownerSuspended015;
        Func<AudioVoiceFrame015, bool> start;
        Action<AudioVoiceFrame015> update;
        Action<int, uint, uint> stop;
        Action unlock;
        bool HasCurrentOwner015
        {
            get {
#if UNITY_WEBGL && !UNITY_EDITOR
                return initialized && lifetime != 0 && owner != null && owner.isActiveAndEnabled &&
                    owner.AcousticOwnerRevision015 == lifetime && listener != null && isActiveAndEnabled;
#else
                return false;
#endif
            }
        }
        public bool IsReady
        {
            get {
#if UNITY_WEBGL && !UNITY_EDITOR
                return HasCurrentOwner015 && owner.AcousticSpatializerRequested015 && OracleAudio015_Ready(lifetime) == 1;
#else
                return false;
#endif
            }
        }
        public bool Initialize(GameAudioDirector director, Transform listenerTransform, uint ownerRevision)
        {
            if (initialized || director == null || listenerTransform == null || ownerRevision == 0 ||
                director.AcousticOwnerRevision015 != ownerRevision || director.ExternalVoiceStart015 != null || director.ExternalVoiceUpdate015 != null ||
                director.ExternalVoiceStop015 != null || director.ExternalVoiceUnlockRequested015 != null) return false;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (OracleAudio015_Boot(Application.streamingAssetsPath + "/OracleAudio015", OracleWebAudioCatalog015.ManifestSha256, ownerRevision) != 1) return false;
            owner = director; listener = listenerTransform; lifetime = ownerRevision;
            // Cold immutable clip identities only. Never load Resources inside a frame/cue callback.
            for (int i = 0; i < OracleWebAudioCatalog015.Count; i++) {
                AudioClip clip = Resources.Load<AudioClip>(OracleWebAudioCatalog015.Resources[i]);
                clipIdentities[i] = clip != null ? EntityId.ToULong(clip.GetEntityId()) : 0;
            }
            start = TryStart; update = UpdateVoice; stop = StopVoice; unlock = ShowUnlockControl;
            director.ExternalVoiceStart015 = start; director.ExternalVoiceUpdate015 = update; director.ExternalVoiceStop015 = stop;
            director.ExternalVoiceUnlockRequested015 = unlock;
            initialized = true; return true;
#else
            return false;
#endif
        }
        public void ShowUnlockControl()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (HasCurrentOwner015 && owner.AcousticSpatializerRequested015) { OracleAudio015_Arm(lifetime); unlockControlArmed = true; }
#endif
        }
        bool TryStart(AudioVoiceFrame015 frame)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!frame.Spatial || frame.OwnerRevision != lifetime || frame.ClipIdentity == 0 || !IsReady || listener == null) return false;
            int selected = -1;
            // Fixed value-only comparisons, no Unity Object invocation or managed allocation.
            for (int i = 0; i < clipIdentities.Length; i++) if (clipIdentities[i] == frame.ClipIdentity) { selected = i; break; }
            if (selected < 0) return false;
            if (!SendListener()) return false;
            return OracleAudio015_Start(frame.Slot, selected, frame.Generation, lifetime, frame.GeometryRevision, frame.ProfileRevision,
                frame.Position.x, frame.Position.y, frame.Position.z, frame.Gain, frame.Cutoff, frame.Pitch) == 1;
#else
            return false;
#endif
        }
        internal void ObserveDecision015(AudioVoiceFrame015 frame, bool browserAccepted)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Deliberately no readiness requirement: genuine Unity fallback must remain observable.
            if (!HasCurrentOwner015 || frame.OwnerRevision != lifetime || frame.ClipIdentity == 0 ||
                frame.ClipIdentity != clipIdentities[0]) return;
            try {
                OracleAudio015_Decision(frame.Slot, 0, frame.Generation, lifetime, frame.GeometryRevision, frame.ProfileRevision,
                    frame.Position.x, frame.Position.y, frame.Position.z, frame.Gain, frame.Cutoff, frame.Pitch,
                    frame.Duration, browserAccepted ? 1 : 2);
            } catch (Exception) { /* Diagnostic failure cannot change the completed playback decision. */ }
#endif
        }
        void UpdateVoice(AudioVoiceFrame015 frame)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (HasCurrentOwner015 && frame.OwnerRevision == lifetime)
                OracleAudio015_Update(frame.Slot, frame.Generation, lifetime, frame.GeometryRevision, frame.ProfileRevision,
                    frame.Position.x, frame.Position.y, frame.Position.z, frame.Gain, frame.Cutoff);
#endif
        }
        void StopVoice(int slot, uint generation, uint ownerRevision)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (initialized && ownerRevision == lifetime) OracleAudio015_Stop(slot, generation, lifetime);
#endif
        }
        bool SendListener()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (listener == null) return false;
            Vector3 p = listener.position, f = listener.forward, u = listener.up;
            return OracleAudio015_Listener(lifetime, p.x, p.y, p.z, f.x, f.y, f.z, u.x, u.y, u.z) == 1;
#else
            return false;
#endif
        }
        void Update()
        {
            if (!initialized) return;
            if (!HasCurrentOwner015) {
                // Even an unarmed/unready owner can have an unlock/decode still pending.
                if (!ownerSuspended015) { Suspend(); ownerSuspended015 = true; }
                return;
            }
            ownerSuspended015 = false;
            bool ready = IsReady;
            if (ready && !SendListener()) ready = false;
            bool requested = owner != null && owner.AcousticSpatializerRequested015;
            if (!requested && (wasReady || unlockControlArmed)) { Suspend(); return; }
            if (wasReady && !ready && owner != null) owner.SuspendExternalAudio015();
            if (ready != wasReady && owner != null) owner.SetExternalAcousticCapability015(ready, ready, ready);
            wasReady = ready;
            if (ready) unlockControlArmed = false;
            else if (requested && !unlockControlArmed && Application.isFocused) ShowUnlockControl();
        }
        public void Suspend()
        {
            if (!initialized) return;
            if (owner != null && owner.AcousticOwnerRevision015 == lifetime) { owner.SuspendExternalAudio015(); owner.SetExternalAcousticCapability015(false, false, false); }
#if UNITY_WEBGL && !UNITY_EDITOR
            OracleAudio015_Pause(lifetime);
#endif
            wasReady = false; unlockControlArmed = false;
        }
        void OnApplicationFocus(bool focused) { if (!focused) Suspend(); }
        void OnApplicationPause(bool paused) { if (paused) Suspend(); }
        void OnDisable() { Suspend(); }
        void OnDestroy()
        {
            if (!initialized) return;
            Suspend();
            if (owner != null) {
                if (owner.ExternalVoiceStart015 == start) owner.ExternalVoiceStart015 = null;
                if (owner.ExternalVoiceUpdate015 == update) owner.ExternalVoiceUpdate015 = null;
                if (owner.ExternalVoiceStop015 == stop) owner.ExternalVoiceStop015 = null;
                if (owner.ExternalVoiceUnlockRequested015 == unlock) owner.ExternalVoiceUnlockRequested015 = null;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            OracleAudio015_Dispose(lifetime);
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            listener = null;
#endif
            initialized = false; owner = null;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int OracleAudio015_Boot(string baseUrl, string catalogSha256, uint owner);
        [DllImport("__Internal")] static extern int OracleAudio015_Arm(uint owner);
        [DllImport("__Internal")] static extern int OracleAudio015_Ready(uint owner);
        [DllImport("__Internal")] static extern int OracleAudio015_Start(int slot, int clip, uint generation, uint owner, uint geometry, uint profile, float x, float y, float z, float gain, float cutoff, float pitch);
        [DllImport("__Internal")] static extern void OracleAudio015_Decision(int slot, int clip, uint generation, uint owner, uint geometry, uint profile, float x, float y, float z, float gain, float cutoff, float pitch, float duration, int decision);
        [DllImport("__Internal")] static extern int OracleAudio015_Update(int slot, uint generation, uint owner, uint geometry, uint profile, float x, float y, float z, float gain, float cutoff);
        [DllImport("__Internal")] static extern void OracleAudio015_Stop(int slot, uint generation, uint owner);
        [DllImport("__Internal")] static extern int OracleAudio015_Listener(uint owner, float x, float y, float z, float fx, float fy, float fz, float ux, float uy, float uz);
        [DllImport("__Internal")] static extern void OracleAudio015_Pause(uint owner);
        [DllImport("__Internal")] static extern void OracleAudio015_Dispose(uint owner);
#endif
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: cached listener and AudioVoiceFrame015 use Unity world metres
// Y-up/+Z; JavaScript alone reflects Z for WebAudio. Main-thread adapter only.
// Array Formats: cold catalog-sized ulong clip identity map; hot imports use fixed
// scalar slots/generations/revisions, no JSON/string/PCM or managed-frame allocation.
// Dependencies/Pointers: GameAudioDirector owns LRU/protection/final gain and lease
// state; accepted browser start alone suppresses Unity; unmapped/unready falls back.
