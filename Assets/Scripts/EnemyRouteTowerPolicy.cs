using Unity.Mathematics;

namespace OdysseyOracle
{
    // One value snapshot per registered tower; no Unity object or native owner is retained.
    internal struct EnemyRouteTowerCandidate
    {
        internal int Id;
        internal float3 Position, Aim;
        internal byte Eligible;
    }

    // The existing four-entry registry is sampled on the main thread before this kernel.
    internal struct EnemyRouteTowerSet
    {
        internal EnemyRouteTowerCandidate A, B, C, D;
    }

    internal static class EnemyRouteTowerPolicy
    {
        internal const float MaximumRange = 8f, RouteHalfWidth = 7f, PassedTolerance = .15f;

        internal static bool OnRoute(float3 feet, float3 aim, float3 origin, float3 core,
            float range, EnemyRouteTowerCandidate tower)
        {
            if (tower.Eligible == 0 || tower.Id < 1 || !math.all(math.isfinite(feet)) ||
                !math.all(math.isfinite(aim)) || !math.all(math.isfinite(origin)) ||
                !math.all(math.isfinite(core)) || !math.all(math.isfinite(tower.Position)) ||
                !math.all(math.isfinite(tower.Aim)) || !math.isfinite(range) || range <= 0f) return false;
            float3 path = core - origin; path.y = 0f;
            float lengthSq = math.lengthsq(path);
            if (lengthSq < .01f) return false;
            float length = math.sqrt(lengthSq);
            float3 forward = path / length;
            float3 delta = tower.Position - feet; delta.y = 0f;
            float3 fromOrigin = tower.Position - origin; fromOrigin.y = 0f;
            float along = math.dot(fromOrigin, forward);
            float limit = math.min(range, MaximumRange);
            // World aim distance also bounds flyers: XZ proximity cannot authorize a dive hit.
            return math.lengthsq(tower.Aim - aim) <= limit * limit &&
                math.dot(delta, forward) >= -PassedTolerance && along >= 0f && along <= length &&
                math.lengthsq(fromOrigin - forward * along) <= RouteHalfWidth * RouteHalfWidth;
        }

        internal static int NearestVisible(float3 aim, EnemyRouteTowerSet towers)
        {
            int selected = -1, id = int.MaxValue;
            float best = float.PositiveInfinity;
            Consider(aim, towers.A, 0, ref selected, ref id, ref best);
            Consider(aim, towers.B, 1, ref selected, ref id, ref best);
            Consider(aim, towers.C, 2, ref selected, ref id, ref best);
            Consider(aim, towers.D, 3, ref selected, ref id, ref best);
            return selected;
        }

        static void Consider(float3 aim, EnemyRouteTowerCandidate tower, int slot,
            ref int selected, ref int id, ref float best)
        {
            if (tower.Eligible == 0 || tower.Id < 1 || !math.all(math.isfinite(aim)) ||
                !math.all(math.isfinite(tower.Aim))) return;
            float distance = math.lengthsq(tower.Aim - aim);
            if (distance > best + .00001f || math.abs(distance - best) <= .00001f && tower.Id >= id) return;
            selected = slot; id = tower.Id; best = distance;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// World metres, Y-up/XZ route. Four fixed value snapshots carry positive tower IDs,
// feet/aim float3s and one eligibility byte; no arrays, pointers, Unity objects or allocations.
// Registry/LOS/authority stay at the main-thread boundary; this numeric kernel owns no memory.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing metre/Y-up/XZ routes, saved art local+Z with existing1.20 scale only.
// Array Formats: Four fixed numeric tower value slots; stable-ID four-theme palette; no new spatial/native storage.
// Dependencies/Pointers: Main-thread registry/LOS adapter is separate from pure math; V1 owns pivots/gait and composite V2 lease owns disposal; host HP/damage/projectile authority remains unchanged.
