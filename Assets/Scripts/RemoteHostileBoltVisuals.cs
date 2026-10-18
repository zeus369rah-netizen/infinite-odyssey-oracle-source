using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread visual adapter:48 fixed borrowed-mesh/material markers.
    // Host snapshot positions are world metres. Extrapolation is capped150ms,
    // then freezes until corrected. Never collides, targets, damages or sends.
    internal sealed class RemoteHostileBoltVisuals
    {
        readonly OracleCombat owner;
        readonly Transform root;
        readonly Transform[] visuals = new Transform[RemoteDangerPolicy.Capacity];
        readonly HostileBoltState[] states = new HostileBoltState[RemoteDangerPolicy.Capacity];
        readonly bool[] seen = new bool[RemoteDangerPolicy.Capacity];
        readonly float[] elapsed = new float[RemoteDangerPolicy.Capacity];
        readonly RemoteBoltLedger ledger = new RemoteBoltLedger();
        bool disposed, primed;

        internal RemoteHostileBoltVisuals(OracleCombat owner, Transform parent, Material material)
        {
            this.owner = owner;
            root = new GameObject("Pooled remote hostile danger visuals").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i] = OracleCombat.CreateBlock(root, "Remote hostile bolt " + i, Vector3.zero,
                    new Vector3(.24f, .24f, .56f), material);
                Renderer renderer = visuals[i].GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                visuals[i].gameObject.SetActive(false);
            }
        }
        internal void Apply(HostileBoltState[] incoming, int count)
        {
            if (disposed || incoming == null || count < 0 || count > RemoteDangerPolicy.Capacity || count > incoming.Length) return;
            for (int i = 0; i < seen.Length; i++) seen[i] = false;
            for (int i = 0; i < count; i++)
            {
                HostileBoltState state = incoming[i];
                if (!ledger.Apply(state, out bool impact))
                {
                    if (state.slot >= 0 && state.slot < seen.Length)
                        seen[state.slot] = states[state.slot].active && visuals[state.slot].gameObject.activeSelf;
                    continue;
                }
                int slot = state.slot;
                bool newLaunch = primed && state.active && state.generation > states[slot].generation;
                seen[slot] = state.active; states[slot] = state; elapsed[slot] = 0f;
                visuals[slot].position = state.position;
                visuals[slot].rotation = Quaternion.LookRotation(state.direction);
                visuals[slot].gameObject.SetActive(state.active);
                if (newLaunch) owner.PlayRemoteHostileLaunch(state.origin);
                if (impact) owner.PlayRemoteHostileImpact(state.position);
            }
            for (int i = 0; i < seen.Length; i++) if (!seen[i]) visuals[i].gameObject.SetActive(false);
            primed = true;
        }
        internal void Tick(float dt)
        {
            if (disposed || dt <= 0f || !RemoteDangerPolicy.Finite(dt)) return;
            for (int i = 0; i < visuals.Length; i++)
            {
                if (!seen[i] || !visuals[i].gameObject.activeSelf) continue;
                elapsed[i] = Mathf.Min(RemoteDangerPolicy.MaximumExtrapolation, elapsed[i] + dt);
                HostileBoltState state = states[i];
                visuals[i].position = state.position + state.direction * Mathf.Min(state.remaining, state.speed * elapsed[i]);
            }
        }
        internal void Clear()
        {
            ledger.Clear(); primed = false;
            for (int i = 0; i < visuals.Length; i++)
            { seen[i] = false; elapsed[i] = 0f; states[i] = default; if (!disposed && visuals[i] != null) visuals[i].gameObject.SetActive(false); }
        }
        internal void Dispose()
        { if (disposed) return; Clear(); disposed = true; if (root != null) Object.Destroy(root.gameObject); }
    }
}
