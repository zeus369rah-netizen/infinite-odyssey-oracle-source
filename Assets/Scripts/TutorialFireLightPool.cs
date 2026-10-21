using System;
using Unity.Collections;
#if UNITY_EDITOR
using ReloadCallback = UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using ReloadCallback = System.Action;
#endif
using UnityEngine;

namespace OdysseyOracle
{
    [DisallowMultipleComponent]
    public sealed class TutorialFireLightPool : MonoBehaviour
    {
        public enum Fixture { Torch, CandleCluster, SmallFlame }
        [Serializable]
        struct Emitter
        {
            public Vector3 LocalPosition;
            public float Range, Intensity, CrackleRange, CrackleGain;
            public Color Color;
            public int FlickerGroup;
        }
        struct NativeReader : IFireLightSelectionData
        {
            public NativeArray<FireLightCandidate> Candidates;
            public NativeArray<int> Slots, Ranked;
            public int Length;
            public int Count => Length;
            public FireLightCandidate Candidate(int index) => Candidates[index];
            public int Previous(int slot) => Slots[slot];
            public int Selected(int slot) => Ranked[slot];
            public void SetSelected(int slot, int index) { Ranked[slot] = index; }
        }
        const float SelectionSeconds = .2f;
        // One active tutorial geometry root owns this reserved ambient ID range.
        // A caller creating another simultaneous pool must provide a distinct base.
        const int DefaultAmbientKeyBase = 1700090000;
        // Legacy four slots remain serialized for safe hot-reload migration, but never illuminate.
        // Camera-nearest selection now controls only the existing four crackle audio records.
        [SerializeField] Light[] lights = new Light[FireLightSelector.MaximumLights];
        [SerializeField] Light[] fixtureLights = new Light[FireLightSelector.MaximumEmitters];
        [SerializeField] float[] roomReactionFuel;
        void EnsureRoomReactionFuel(){if(roomReactionFuel!=null&&roomReactionFuel.Length==FireLightSelector.MaximumEmitters)return;roomReactionFuel=new float[FireLightSelector.MaximumEmitters];for(int i=0;i<roomReactionFuel.Length;i++)roomReactionFuel[i]=1;}
        public void SetRoomReactionFuel(int index,float gain){EnsureRoomReactionFuel();if(index<0||index>=count)return;gain=float.IsNaN(gain)||float.IsInfinity(gain)?0:Mathf.Clamp01(gain);roomReactionFuel[index]=gain;if(fixtureLights[index]!=null)fixtureLights[index].intensity=registry[index].Intensity*gain;}
        const string FixturePrefix = "Permanent tutorial fire spill ";
        [SerializeField] Emitter[] registry = new Emitter[FireLightSelector.MaximumEmitters];
        [SerializeField] Transform geometry;
        [SerializeField] GameSession session;
        [SerializeField] int count, budget = 4, ambientKeyBase = DefaultAmbientKeyBase;
        [NonSerialized] NativeArray<Emitter> emitters;
        [NonSerialized] NativeArray<FireLightCandidate> candidates;
        [NonSerialized] NativeArray<int> slots, ranked;
        [NonSerialized] GameAudioDirector audio;
        [NonSerialized] float nextSelection;
        [NonSerialized] SpatialLightPulseClock fallbackPulseClock;
        [NonSerialized] bool audioRegistered, audioSilent = true, initialized, reloadSubscribed;
        [NonSerialized] ReloadCallback reloadAction;
        public bool NativeStorageCreated => BuffersReady;
        bool BuffersReady => emitters.IsCreated && candidates.IsCreated && slots.IsCreated && ranked.IsCreated;

        public int RegisteredEmitterCount => count;
        public int PermanentLightCount {
            get { int total=0;for(int i=0;fixtureLights!=null && i<count;i++)if(fixtureLights[i]!=null)total++;return total; }
        }
        public int ActivePermanentLightCount {
            get { int total=0;for(int i=0;fixtureLights!=null && i<count;i++)if(fixtureLights[i]!=null && fixtureLights[i].isActiveAndEnabled)total++;return total; }
        }
        public bool TryGetPermanentLight(int index, out Light light) {
            light=fixtureLights!=null && index>=0 && index<count?fixtureLights[index]:null;return light!=null;
        }
        // Compatibility: LightBudget/ActiveLightCount refer to retired detail slots only.
        // Permanent fixture illumination is independent of every quality/audio budget.
        public int LightBudget => budget;
        public int ActiveLightCount
        {
            get { int active = 0; for (int i = 0; lights != null && i < lights.Length; i++) if (lights[i] != null && lights[i].enabled) active++; return active; }
        }

        public void Initialize(Transform geometryRoot, GameSession owner, int positiveAmbientKeyBase = DefaultAmbientKeyBase)
        {
            if (geometryRoot == null) throw new ArgumentNullException(nameof(geometryRoot));
            if (positiveAmbientKeyBase < 0 || positiveAmbientKeyBase > int.MaxValue - 4)
                throw new ArgumentOutOfRangeException(nameof(positiveAmbientKeyBase));
            if (geometry != null && (geometry != geometryRoot || ambientKeyBase != positiveAmbientKeyBase))
            { PrepareForReload(); ClearRegistry(); }
            geometry = geometryRoot; session = owner; ambientKeyBase = positiveAmbientKeyBase;
            RestoreAfterReload();
        }

        public void RestoreAfterReload()
        {
            if (reloadAction == null) reloadAction = PrepareForReload;
            if (!reloadSubscribed) { LightPoolLifecycle.SubscribeBeforeReload(reloadAction); reloadSubscribed = true; }
            if (geometry == null) return;
            EnsureRegistry();
            bool cold = !BuffersReady;
            bool missingLight = lights == null || lights.Length != FireLightSelector.MaximumLights;
            if (!missingLight) for (int i = 0; i < lights.Length; i++) if (lights[i] == null) missingLight = true;
            if (cold || missingLight)
            {
                LightPoolLifecycle.Ensure(geometry, ref lights, FireLightSelector.MaximumLights, "Reused tutorial fire spill ");
                for (int i = 0; i < lights.Length; i++)
                { lights[i].renderMode = LightRenderMode.Auto; lights[i].bounceIntensity = 0f; }
            }
            if (cold) AllocateNativeState();
            DisableLights();
            RestorePermanentLights();
            initialized = BuffersReady;
            nextSelection = 0f;
        }

        void EnsureRegistry()
        {
            if (registry == null || registry.Length != FireLightSelector.MaximumEmitters)
            {
                Emitter[] previous = registry;
                registry = new Emitter[FireLightSelector.MaximumEmitters];
                if (previous != null) Array.Copy(previous, registry, Math.Min(previous.Length, registry.Length));
                else count = 0;
            }
            count = Mathf.Clamp(count, 0, registry.Length);
            int valid = 0;
            for (int i = 0; i < count; i++)
                if (Finite(registry[i].LocalPosition) && registry[i].Range > 0f && registry[i].Intensity >= 0f)
                    registry[valid++] = registry[i];
            count = valid;
        }

        void AllocateNativeState()
        {
            DisposeNativeState();
            try
            {
                emitters = new NativeArray<Emitter>(FireLightSelector.MaximumEmitters, Allocator.Persistent);
                candidates = new NativeArray<FireLightCandidate>(FireLightSelector.MaximumEmitters, Allocator.Persistent);
                slots = new NativeArray<int>(FireLightSelector.MaximumLights, Allocator.Persistent);
                ranked = new NativeArray<int>(FireLightSelector.MaximumLights, Allocator.Persistent);
                for (int i = 0; i < count; i++) emitters[i] = registry[i];
                for (int i = 0; i < FireLightSelector.MaximumLights; i++) slots[i] = ranked[i] = -1;
                initialized = true;
            }
            catch { DisposeNativeState(); throw; }
        }

        public bool TryGetEmitterPosition(int index, out Vector3 localPosition)
        {
            if (registry != null && index >= 0 && index < count)
            { localPosition = BuffersReady ? emitters[index].LocalPosition : registry[index].LocalPosition; return true; }
            localPosition = Vector3.zero; return false;
        }

        public bool RegisterEmitter(Vector3 localPosition, Fixture fixture, int flickerGroup)
        {
            if (geometry == null || count >= FireLightSelector.MaximumEmitters || !Finite(localPosition)) return false;
            EnsureRegistry();
            bool torch = fixture == Fixture.Torch;
            Emitter emitter = new Emitter {
                LocalPosition = localPosition, Range = torch ? 6.5f : 3f,
                Intensity = (torch ? 6f : 2.2f) * .85f,
                Color = torch ? new Color(1f, .44f, .12f) : new Color(1f, .55f, .23f),
                CrackleRange = torch ? 10f : 3f, CrackleGain = torch ? .025f : .004f,
                FlickerGroup = flickerGroup
            };
            registry[count] = emitter;
            if (BuffersReady) emitters[count] = emitter;
            count++;
            EnsurePermanentLight(count-1);
            nextSelection = 0f; return true;
        }

        /// <summary>Retains native capacity and owned Light objects; an empty registry illuminates nothing.</summary>
        public void ClearRegistry()
        {
            count = 0;
            if (registry != null) Array.Clear(registry, 0, registry.Length);
            if (roomReactionFuel != null) for (int i = 0; i < roomReactionFuel.Length; i++) roomReactionFuel[i] = 1f;
            if (slots.IsCreated && ranked.IsCreated) for (int i = 0; i < 4; i++) slots[i] = ranked[i] = -1;
            DisableLights(); DisablePermanentLights(); SilenceAudio(); nextSelection = 0f;
        }
        // This compatibility budget now controls nearest-four audio admission only.
        // It must never disable or relocate permanent fixture illumination, including while paused.
        public void SetLightBudget(int maximum)
        {
            budget = Mathf.Clamp(maximum, 0, 4);
            nextSelection = 0f;
            // A menu or background window freezes selection. Enforce a reduced
            // budget immediately so paused frames cannot retain extra lights.
            for (int slot = budget; slot < 4; slot++)
            {
                if (BuffersReady) slots[slot] = -1;
                if (lights != null && slot < lights.Length && lights[slot] != null)
                    lights[slot].enabled = false;
                WriteAudio(slot, Vector3.zero, 2f, 0f);
            }
            if (budget == 0) { DisableLights(); SilenceAudio(); }
        }

        void Update()
        {
            if (!initialized || !BuffersReady || geometry == null) { DisableLights(); return; }
            var camera = session != null && session.Player != null ? session.Player.ViewCamera : null;
            if (camera == null) { DisableLights(); SilenceAudio(); nextSelection = 0f; return; }
            // A paused book/menu freezes flame variation, rather than extinguishing
            // the walls. Geometry-root disable still retires the four lights.
            if (!Application.isFocused || session != null && session.HasStarted && session.Paused)
            { SilenceAudio(); nextSelection = 0f; return; }
            TickPermanentLightPulse();
            if (Time.unscaledTime < nextSelection) return;
            nextSelection = Time.unscaledTime + SelectionSeconds;
            Matrix4x4 worldFromLocal = geometry.localToWorldMatrix;
            for (int i = 0; i < count; i++)
            {
                Vector3 world = worldFromLocal.MultiplyPoint3x4(emitters[i].LocalPosition);
                candidates[i] = new FireLightCandidate { X = world.x, Y = world.y, Z = world.z, StableId = i, Enabled = 1 };
            }
            Vector3 viewer = camera.transform.position;
            var reader = new NativeReader { Candidates = candidates, Slots = slots, Ranked = ranked, Length = count };
            int selectedCount = FireLightSelector.Select(ref reader, viewer.x, viewer.y, viewer.z, budget);
            // Preserve surviving fixture-to-Light assignments even if distance rank changes.
            for (int slot = 0; slot < 4; slot++)
            {
                bool kept = false;
                for (int r = 0; r < selectedCount; r++) if (slots[slot] == ranked[r]) kept = true;
                if (slot >= budget || !kept) slots[slot] = -1;
            }
            for (int r = 0; r < selectedCount; r++)
            {
                bool assigned = false;
                for (int slot = 0; slot < budget; slot++) if (slots[slot] == ranked[r]) assigned = true;
                if (assigned) continue;
                for (int slot = 0; slot < budget; slot++) if (slots[slot] < 0) { slots[slot] = ranked[r]; break; }
            }
            AttachAudio();
            for (int slot = 0; slot < 4; slot++)
            {
                Light light = lights[slot]; if (light == null) continue;
                int index = slots[slot];
                if (slot >= budget || index < 0 || index >= count) { slots[slot] = -1; light.enabled = false; WriteAudio(slot, Vector3.zero, 2f, 0f); continue; }
                Emitter emitter = emitters[index];
                light.transform.localPosition = emitter.LocalPosition;
                light.range = emitter.Range; light.color = emitter.Color;
                // Retire the old moving-light path. Fixed fixture Lights own illumination.
                light.intensity = 0f;
                light.enabled = false;
                FireLightCandidate world = candidates[index];
                WriteAudio(slot, new Vector3(world.X, world.Y, world.Z), emitter.CrackleRange, emitter.CrackleGain*roomReactionFuel[index]);
            }
        }

        void TickPermanentLightPulse()
        {
            double seconds = session != null && session.World != null ? session.World.SpatialLightPulseTime
                : fallbackPulseClock.Sample(Time.frameCount, Time.unscaledDeltaTime, false);
            for (int i = 0; i < count; i++)
            {
                Light light = fixtureLights[i];
                if (light == null || !light.isActiveAndEnabled) continue;
                Emitter emitter = registry[i];
                light.intensity = emitter.Intensity * SpatialLightPulseRules.Evaluate(SpatialLightPulseKind.Warm,
                    seconds, SpatialLightPulseRules.WarmPhase(emitter.FlickerGroup))*roomReactionFuel[i];
            }
        }

        void AttachAudio()
        {
            GameAudioDirector next = session != null ? session.Audio : null;
            if (!ReferenceEquals(next, audio)) { ReleaseAudio(); audio = next; }
            if (audio == null || audioRegistered) return;
            // Four records are allocated once per active-world lifetime. Zero-gain
            // records must be skipped by the accompanying one-line Audio hook.
            audioRegistered = true;
            audioSilent = true;
            for (int i = 0; i < 4; i++) if (!audio.SetAmbientEmitter(ambientKeyBase + i, "torch_crackle", Vector3.zero, true, 2f, 0f))
                audioRegistered = false;
        }
        void WriteAudio(int slot, Vector3 position, float range, float gain)
        {
            if (audio == null || !audioRegistered) return;
            audio.SetAmbientEmitter(ambientKeyBase + slot, "torch_crackle", position, true, range, gain);
            if (gain > 0f) audioSilent = false;
        }
        void SilenceAudio()
        {
            if (!audioRegistered || audioSilent) return;
            for (int i = 0; i < 4; i++) WriteAudio(i, Vector3.zero, 2f, 0f);
            audioSilent = true;
        }
        void ReleaseAudio()
        {
            if (audio != null) for (int i = 0; i < 4; i++) audio.RemoveAmbientEmitter(ambientKeyBase + i);
            audioRegistered = false; audioSilent = true; audio = null;
        }
        void DisableLights() => LightPoolLifecycle.Disable(lights);
        void OnEnable() => RestoreAfterReload();
        void OnDisable() => PrepareForReload();
        void OnDestroy() { PrepareForReload(); LightPoolLifecycle.Destroy(lights); LightPoolLifecycle.Destroy(fixtureLights); }

        public void PrepareForReload()
        {
            DisableLights(); DisablePermanentLights(); ReleaseAudio(); DisposeNativeState(); nextSelection = 0f;
            if (reloadAction != null) LightPoolLifecycle.UnsubscribeBeforeReload(reloadAction);
            reloadSubscribed = false;
        }

        void RestorePermanentLights()
        {
            EnsurePermanentArray();
            for(int i=0;i<count;i++)EnsurePermanentLight(i);
            for(int i=count;i<fixtureLights.Length;i++)if(fixtureLights[i]!=null)fixtureLights[i].enabled=false;
        }
        void EnsurePermanentArray()
        {
            if(fixtureLights!=null && fixtureLights.Length==FireLightSelector.MaximumEmitters)return;
            Light[] previous=fixtureLights;fixtureLights=new Light[FireLightSelector.MaximumEmitters];
            if(previous!=null)Array.Copy(previous,fixtureLights,Math.Min(previous.Length,fixtureLights.Length));
        }
        void EnsurePermanentLight(int index)
        {
            if(geometry==null || index<0 || index>=count)return;
            EnsurePermanentArray();EnsureRoomReactionFuel();
            Light light=fixtureLights[index];
            for(int earlier=0;earlier<index;earlier++)if(light==fixtureLights[earlier])light=null;
            if(light==null) {
                Transform child=geometry.Find(FixturePrefix+index);
                if(child!=null)light=child.GetComponent<Light>();
                for(int earlier=0;earlier<index;earlier++)if(light==fixtureLights[earlier])light=null;
                if(light==null) {
                    var source=new GameObject(FixturePrefix+index);source.transform.SetParent(geometry,false);
                    light=source.AddComponent<Light>();
                }
            }
            fixtureLights[index]=light;
            Emitter emitter=registry[index];
            light.name=FixturePrefix+index;light.transform.SetParent(geometry,false);
            light.transform.localPosition=emitter.LocalPosition;
            light.type=LightType.Point;light.shadows=LightShadows.None;
            light.renderMode=LightRenderMode.Auto;light.bounceIntensity=0f;
            light.range=emitter.Range;light.color=emitter.Color;light.intensity=emitter.Intensity*roomReactionFuel[index];
            light.enabled=isActiveAndEnabled;
            session?.World?.RegisterRoomReactionFixture(index,emitter.LocalPosition);
        }
        void DisablePermanentLights() => LightPoolLifecycle.Disable(fixtureLights);

        void DisposeNativeState()
        {
            if (emitters.IsCreated) emitters.Dispose(); emitters = default;
            if (candidates.IsCreated) candidates.Dispose(); candidates = default;
            if (slots.IsCreated) slots.Dispose(); slots = default;
            if (ranked.IsCreated) ranked.Dispose(); ranked = default;
            initialized = false;
        }
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x)
            && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    }
}

// # ARCHITECTURE SNAPSHOT
// Registry positions are geometry-root local metres. Exactly one shadowless Point Light is created
// per registered emitter, at build/restore time only (23 in the authored tutorial; hard cap128).
// Permanent lights are never selected, moved or disabled by camera distance, pause, focus,
// graphics preset or audio budget. Existing intensity gently varies80-100% on a shared clock;
// pause/blur freezes variation. Parent geometry lifecycle still owns visibility and disposal.
// Serialized Light references survive reload; Prepare/OnDisable disables, Restore re-enables at
// original positions. Four old detail slots remain disabled for reload migration compatibility.
// Native nearest-four selection now serves bounded crackle audio only; no new runtime allocation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.
