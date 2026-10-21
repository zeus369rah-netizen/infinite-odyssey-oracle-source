using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        const string WindowDarkCue015 = "window_breeze_dark015", WindowBrightCue015 = "window_breeze_bright015";
        const int WindowDarkEmitter015 = 2147483111, WindowBrightEmitter015 = 2147483112;
        struct BreezeResult015 { public int Nearest, Inside; public uint Lease; }
        [BurstCompile]
        struct BreezeQuery015 : IJob
        {
            [ReadOnly] public NativeArray<float3> Anchors;
            [ReadOnly] public NativeArray<float4> Rooms;
            public NativeArray<BreezeResult015> Result;
            public float3 Listener;
            public float4x4 WorldToLocal;
            public float Ceiling;
            public uint Lease;
            public void Execute()
            {
                var result = new BreezeResult015 { Nearest = -1, Lease = Lease };
                if (!math.all(math.isfinite(Listener))) { Result[0] = result; return; }
                float nearest = 24f * 24f;
                for (int i = 0; i < 3; i++) {
                    float distance = math.lengthsq(Anchors[i] - Listener);
                    if (distance <= nearest && (result.Nearest < 0 || distance < nearest)) { nearest = distance; result.Nearest = i; }
                }
                float3 local = math.transform(WorldToLocal, Listener);
                if (math.all(math.isfinite(local)) && local.y >= 0 && local.y <= Ceiling)
                    for (int i = 0; i < 7; i++) {
                        float4 room = Rooms[i];
                        if (local.x >= room.x && local.z >= room.y && local.x <= room.z && local.z <= room.w) { result.Inside = 1; break; }
                    }
                Result[0] = result;
            }
        }
        NativeArray<float3> windowAnchors015;
        NativeArray<float4> windowRooms015;
        NativeArray<BreezeResult015> windowResult015;
        readonly RaycastHit[] windowRayHits015 = new RaycastHit[16];
        JobHandle windowReader015;
        AmbientEmitter windowDarkRecord015, windowBrightRecord015;
        AudioClip windowDarkClip015, windowBrightClip015;
        DungeonWorld windowWorld015;
        Matrix4x4 windowToLocal015;
        float windowCeiling015, windowQueryAt015, windowDarkGain015, windowBrightGain015, windowTargetDark015, windowTargetBright015;
        uint windowRevision015, windowLease015;
        int windowCurrent015 = -1, windowRequested015 = -1;
        bool windowReady015, windowPending015, windowInside015, windowOccluded015;
        public bool WindowBreezeReady015 => windowReady015 && windowDarkClip015 != null && windowBrightClip015 != null &&
            windowDarkClip015.loadState == AudioDataLoadState.Loaded && windowBrightClip015.loadState == AudioDataLoadState.Loaded &&
            windowDarkClip015.samples > 0 && windowDarkClip015.samples == windowBrightClip015.samples &&
            windowDarkClip015.frequency > 0 && windowDarkClip015.frequency == windowBrightClip015.frequency;
        public int WindowBreezeWindow015 => windowCurrent015;
        public bool WindowBreezeInside015 => windowInside015;
        public bool WindowBreezeOccluded015 => windowOccluded015;
        public float WindowBreezeDarkGain015 => windowDarkGain015;
        public float WindowBreezeBrightGain015 => windowBrightGain015;

        void PrepareWindowBreezeAudio015()
        {
            if (windowReady015) return;
            AudioClip dark = Resources.Load<AudioClip>("Audio/RefugeSound015/" + WindowDarkCue015);
            AudioClip bright = Resources.Load<AudioClip>("Audio/RefugeSound015/" + WindowBrightCue015);
            windowDarkClip015 = dark; windowBrightClip015 = bright;
            Prime(dark); Prime(bright);
            namedClips[WindowDarkCue015] = dark; namedNext[WindowDarkCue015] = 0;
            namedClips[WindowBrightCue015] = bright; namedNext[WindowBrightCue015] = 0;
            try {
                windowAnchors015 = new NativeArray<float3>(3, Allocator.Persistent);
                windowRooms015 = new NativeArray<float4>(7, Allocator.Persistent);
                windowResult015 = new NativeArray<BreezeResult015>(1, Allocator.Persistent);
                windowDarkRecord015 = new AmbientEmitter { Cue = WindowDarkCue015, Range = 24f };
                windowBrightRecord015 = new AmbientEmitter { Cue = WindowBrightCue015, Range = 24f };
                windowReady015 = true; KeepWindowBreezeRecords015();
            } catch { DisposeWindowBreeze015(); throw; }
        }
        // Existing dictionary preallocates32 records; reinsert the same two objects, never construct during Update/clear.
        void KeepWindowBreezeRecords015()
        {
            if (!windowReady015) return;
            KeepWindowBreezeRecord015(WindowDarkEmitter015, windowDarkRecord015);
            KeepWindowBreezeRecord015(WindowBrightEmitter015, windowBrightRecord015);
        }
        void KeepWindowBreezeRecord015(int id, AmbientEmitter record)
        {
            if (record != null && !ambientEmitters.ContainsKey(id) && ambientEmitters.Count < MaximumAmbientEmitters) ambientEmitters.Add(id, record);
        }
        void CompleteWindowBreezeReader015()
        {
            if (!windowPending015) return;
            windowReader015.Complete(); windowPending015 = false;
        }
        bool CacheWindowBreezeWorld015(DungeonWorld world)
        {
            StopWindowBreeze015(); windowWorld015 = null;
            if (world == null || world.RefugeWindowWindCount015 != 3 || world.RefugeWindRoomCount015 != 7 ||
                !world.TryReadRefugeWindSpace015(out windowToLocal015, out windowCeiling015)) return false;
            for (int i = 0; i < 3; i++) {
                if (!world.TryGetRefugeWindowWindAnchor015(i, out Vector3 p) || !Finite(p)) return false;
                windowAnchors015[i] = new float3(p.x, p.y, p.z);
            }
            for (int i = 0; i < 7; i++) {
                if (!world.TryGetRefugeWindRoom015(i, out Rect r)) return false;
                windowRooms015[i] = new float4(r.xMin, r.yMin, r.xMax, r.yMax);
            }
            windowWorld015 = world; windowRevision015 = world.PassiveWindRevision015; windowQueryAt015 = 0;
            return true;
        }
        static float4x4 BreezeMatrix015(Matrix4x4 m) => new float4x4(
            new float4(m.m00, m.m10, m.m20, m.m30), new float4(m.m01, m.m11, m.m21, m.m31),
            new float4(m.m02, m.m12, m.m22, m.m32), new float4(m.m03, m.m13, m.m23, m.m33));
        static float BreezeApproach015(float current, float target, float dt) =>
            current + (target - current) * (1f - Mathf.Exp(-Mathf.Clamp(dt, 0f, .1f) / .55f));

        void TickWindowBreeze015(float dt)
        {
            DungeonWorld world = session != null ? session.World : null;
            bool allowed = WindowBreezeReady015 && GuideGestureObserved015 && isActiveAndEnabled && CanPlayCue() && session != null &&
                session.HasStarted && !session.Paused && !session.IsDead && !session.IsDowned && !session.Victory && world != null &&
                world.IsTutorialRefuge && !world.IsCoopArena && world.RefugeWindowWindCount015 == 3;
            if (!allowed) { StopWindowBreeze015(); return; }
            if (!object.ReferenceEquals(world, windowWorld015) || windowRevision015 != world.PassiveWindRevision015)
                if (!CacheWindowBreezeWorld015(world)) return;
            Vector3 listener = ListenerPosition();
            if (!Finite(listener)) { StopWindowBreeze015(); return; }
            if (windowPending015 && windowReader015.IsCompleted) {
                CompleteWindowBreezeReader015(); BreezeResult015 frame = windowResult015[0];
                if (frame.Lease == windowLease015 && frame.Nearest >= 0 && frame.Nearest < 3) {
                    windowRequested015 = frame.Nearest; windowInside015 = frame.Inside != 0;
                    float3 a = windowAnchors015[frame.Nearest]; Vector3 anchor = new Vector3(a.x, a.y, a.z);
                    Vector3 delta = anchor - listener; float distance = delta.magnitude; bool blocked = false;
                    if (distance > .02f) {
                        // One bounded main-thread PhysX adapter call; job math never touches Objects/Physics.
                        int count = Physics.RaycastNonAlloc(listener, delta / distance, windowRayHits015, distance - .015f, ~0, QueryTriggerInteraction.Ignore);
                        blocked = count >= windowRayHits015.Length;
                        for (int i = 0; i < count && i < windowRayHits015.Length; i++)
                            if (windowRayHits015[i].distance > .05f && windowRayHits015[i].distance < distance - .02f) { blocked = true; break; }
                    }
                    windowOccluded015 = blocked;
                    float gain = windowInside015 ? .028f : .055f;
                    float brightness = windowInside015 ? .12f : .75f;
                    if (blocked) { gain *= .55f; brightness *= .35f; }
                    float gust = .85f + .15f * Mathf.Clamp01(world.PassiveWindField015.w);
                    windowTargetDark015 = gain * (1f - brightness) * gust;
                    windowTargetBright015 = gain * brightness * gust;
                } else { windowRequested015 = -1; windowInside015 = windowOccluded015 = false; windowTargetDark015 = windowTargetBright015 = 0; }
            }
            if (!windowPending015 && Time.unscaledTime >= windowQueryAt015) {
                windowQueryAt015 = Time.unscaledTime + .25f;
                windowReader015 = new BreezeQuery015 { Anchors = windowAnchors015, Rooms = windowRooms015, Result = windowResult015,
                    Listener = new float3(listener.x, listener.y, listener.z), WorldToLocal = BreezeMatrix015(windowToLocal015), Ceiling = windowCeiling015,
                    Lease = windowLease015 }.Schedule(); windowPending015 = true;
            }
            bool switching = windowCurrent015 != windowRequested015;
            if (switching && windowDarkGain015 < .00001f && windowBrightGain015 < .00001f) {
                windowCurrent015 = windowRequested015; switching = false;
                if (windowCurrent015 >= 0) {
                    float3 a = windowAnchors015[windowCurrent015]; Vector3 anchor = new Vector3(a.x, a.y, a.z);
                    windowDarkRecord015.Position = windowBrightRecord015.Position = anchor;
                    nextAmbientCheck = 0;
                }
            }
            float prior = windowDarkGain015 + windowBrightGain015;
            windowDarkGain015 = BreezeApproach015(windowDarkGain015, switching ? 0 : windowTargetDark015, dt);
            windowBrightGain015 = BreezeApproach015(windowBrightGain015, switching ? 0 : windowTargetBright015, dt);
            windowDarkRecord015.Gain = windowCurrent015 >= 0 ? windowDarkGain015 : 0;
            windowBrightRecord015.Gain = windowCurrent015 >= 0 ? windowBrightGain015 : 0;
            if (prior == 0 && windowDarkRecord015.Gain + windowBrightRecord015.Gain > 0) nextAmbientCheck = 0;
        }
        // Called only for an admitted stopped wind voice. Mute until its public sample cursor is aligned.
        bool StartWindowBreezeVoiceAligned015(int slot, int id, AudioSource source, AudioClip desired)
        {
            if (id != WindowDarkEmitter015 && id != WindowBrightEmitter015) return false;
            source.volume = 0;
            if (!WindowBreezeReady015 || desired != (id == WindowDarkEmitter015 ? windowDarkClip015 : windowBrightClip015)) {
                source.Stop(); ambientGains[slot] = 0; return true;
            }
            source.Play();
            // Same-band donor matters when the greedy pool moves a surviving band into an earlier slot.
            int cursor;
            if (!TryReadWindowBreezeCursor015(slot, 0, out cursor) && !TryReadWindowBreezeCursor015(slot, 1, out cursor) &&
                !TryReadWindowBreezeCursor015(slot, 2, out cursor) && !TryReadWindowBreezeCursor015(slot, 3, out cursor)) cursor = 0;
            source.timeSamples = cursor;
            source.volume = sfxVolume * WindowBreezeVoiceGain015(id, ambientGains[slot]) * duck;
            return true;
        }
        bool TryReadWindowBreezeCursor015(int excluded, int slot, out int cursor)
        {
            cursor = 0;
            if (slot == excluded || (ambientIds[slot] != WindowDarkEmitter015 && ambientIds[slot] != WindowBrightEmitter015)) return false;
            AudioSource peer = ambientPool[slot];
            if (peer == null || !peer.isPlaying || (peer.clip != windowDarkClip015 && peer.clip != windowBrightClip015)) return false;
            AudioClip clip = peer.clip;
            if (clip.samples != windowDarkClip015.samples || clip.frequency != windowDarkClip015.frequency) return false;
            int sample = peer.timeSamples;
            if (sample < 0 || sample >= clip.samples) return false;
            cursor = sample; return true;
        }
        float WindowBreezeVoiceGain015(int id, float fallback) => id == WindowDarkEmitter015 ? windowDarkGain015 : id == WindowBrightEmitter015 ? windowBrightGain015 : fallback;
        void StopWindowBreezeVoice015(int slot)
        {
            if (ambientIds[slot] != WindowDarkEmitter015 && ambientIds[slot] != WindowBrightEmitter015) return;
            if (ambientPool[slot] != null) ambientPool[slot].Stop(); ambientIds[slot] = -1; ambientGains[slot] = 0;
        }
        void StopWindowBreeze015()
        {
            CompleteWindowBreezeReader015(); unchecked { windowLease015++; } windowQueryAt015 = 0;
            windowCurrent015 = windowRequested015 = -1; windowDarkGain015 = windowBrightGain015 = windowTargetDark015 = windowTargetBright015 = 0;
            windowInside015 = windowOccluded015 = false;
            if (windowDarkRecord015 != null) windowDarkRecord015.Gain = 0;
            if (windowBrightRecord015 != null) windowBrightRecord015.Gain = 0;
            StopWindowBreezeVoice015(0); StopWindowBreezeVoice015(1); StopWindowBreezeVoice015(2); StopWindowBreezeVoice015(3);
        }
        void RemoveOwnedWindowBreezeRecord015(int id, AmbientEmitter record)
        {
            if (record != null && ambientEmitters.TryGetValue(id, out var existing) && object.ReferenceEquals(existing, record)) ambientEmitters.Remove(id);
        }
        void DisposeWindowBreeze015()
        {
            StopWindowBreeze015(); windowReady015 = false;
            if (windowResult015.IsCreated) windowResult015.Dispose();
            if (windowRooms015.IsCreated) windowRooms015.Dispose();
            if (windowAnchors015.IsCreated) windowAnchors015.Dispose();
            RemoveOwnedWindowBreezeRecord015(WindowDarkEmitter015, windowDarkRecord015); RemoveOwnedWindowBreezeRecord015(WindowBrightEmitter015, windowBrightRecord015);
            windowDarkRecord015 = windowBrightRecord015 = null; windowWorld015 = null;
            windowDarkClip015 = windowBrightClip015 = null;
            // Shared Resources clips are borrowed; no Destroy/Unload of either band.
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Three exact supported World window anchors and seven actual local floor rectangles/Y ceiling, never invented apertures; Unity world+Z/Yup.
// Array Formats: Persistent3float3/7float4/1result +16 RaycastHit buffer; two cold reused registry records in existing32-cap dictionary and at most2 of4 ambient voices.
// Dependencies/Pointers: Pure Burst nearest/enclosure math at4Hz, one bounded main-thread ray, reader complete before cache/dispose, source/lease fences on stop. Existing Master*SFX*duck/real gesture/focus/pause owner; seven derived provider assets, no new AudioSource/live synthesis or fixed38 bank/JS changes. Runtime band alignment/audibility remains root acceptance.
