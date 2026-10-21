using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class PlacedElementShrine
    {
        // Cold accepted model/lifetime boundaries only; the existing tower remains collider-free.
        void RegisterAcousticTower015()
        {
            RegisterXpBody015();
            if (session == null || session.Audio == null || healthVisuals == null || IsDestroyed || !gameObject.activeInHierarchy) return;
            bool found = false; Bounds bounds = default;
            for (int index = 0; index < healthVisuals.Length; index++)
            {
                Renderer renderer = healthVisuals[index]; if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (found)
            {
                Vector3 origin = transform.position, lower = bounds.min, upper = bounds.max;
                float x = Mathf.Max(Mathf.Abs(lower.x - origin.x), Mathf.Abs(upper.x - origin.x));
                float z = Mathf.Max(Mathf.Abs(lower.z - origin.z), Mathf.Abs(upper.z - origin.z));
                float radius = Mathf.Sqrt(x * x + z * z) + .15f; // Conservative yaw/recoil envelope, not exact acoustic geometry.
                bounds.SetMinMax(new Vector3(origin.x - radius, lower.y - .15f, origin.z - radius), new Vector3(origin.x + radius, upper.y + .15f, origin.z + radius));
                session.Audio.SetAcousticTower015(EntityId.ToULong(GetEntityId()), bounds, true);
            }
        }
        void RemoveAcousticTower015()
        { RemoveXpBody015(); if (session != null && session.Audio != null) session.Audio.SetAcousticTower015(EntityId.ToULong(GetEntityId()), default, false); }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Conservative accepted tower render-bounds in Unity World metres.
// Array Formats:Existing borrowed renderer array -> one of8native proxy boxes; no physics collider added.
// Dependencies/Pointers:Cold initialize/tier/enable/disable/destroy hooks only. Audio completes reader before proxy revision/write; tower gameplay ownership unchanged.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current world solid AABB supersets and original XP anchors; floor and complete rotating/bobbing octahedron envelope included.
// Array Formats: Fixed4104 native solids/Morton grid and4095 source-pair certificates; independent world/source generations.
// Dependencies/Pointers: World and source owners complete readers before mutation/dispose. Existing claims/recipient masks/range/LOS and XP arithmetic remain canonical; native real merges/performance pending root.
