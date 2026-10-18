using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class PlacedElementShrine
    {
        public static bool TryFindLaneThreatTower(Vector3 enemyPosition, Vector3 enemyAim, Vector3 laneOrigin,
            Vector3 corePosition, float range, out PlacedElementShrine target)
        {
            var candidates = new EnemyRouteTowerSet
            {
                A = SampleEnemyVariantRouteTower(0, enemyPosition, enemyAim, laneOrigin, corePosition, range),
                B = SampleEnemyVariantRouteTower(1, enemyPosition, enemyAim, laneOrigin, corePosition, range),
                C = SampleEnemyVariantRouteTower(2, enemyPosition, enemyAim, laneOrigin, corePosition, range),
                D = SampleEnemyVariantRouteTower(3, enemyPosition, enemyAim, laneOrigin, corePosition, range)
            };
            int selected = EnemyRouteTowerPolicy.NearestVisible(EnemyVariantRoutePoint(enemyAim), candidates);
            target = selected >= 0 ? registered[selected] : null;
            return target != null;
        }

        static EnemyRouteTowerCandidate SampleEnemyVariantRouteTower(int slot, Vector3 feet, Vector3 aim,
            Vector3 origin, Vector3 core, float range)
        {
            PlacedElementShrine tower = registered[slot];
            if (tower == null || tower.IsDestroyed || !tower.gameObject.activeInHierarchy || tower.session == null ||
                tower.session.Combat == null || tower.session.Defense == null ||
                !tower.session.Defense.IsAuthority || !tower.session.Defense.Running) return default;
            var candidate = new EnemyRouteTowerCandidate
            { Id = tower.Id, Position = EnemyVariantRoutePoint(tower.Position), Aim = EnemyVariantRoutePoint(tower.AimPoint), Eligible = 1 };
            if (!EnemyRouteTowerPolicy.OnRoute(EnemyVariantRoutePoint(feet), EnemyVariantRoutePoint(aim), EnemyVariantRoutePoint(origin),
                EnemyVariantRoutePoint(core), range, candidate) || !tower.session.Combat.HasLineOfSight(aim, tower.AimPoint))
                candidate.Eligible = 0;
            return candidate;
        }

        static Unity.Mathematics.float3 EnemyVariantRoutePoint(Vector3 point)
        { return new Unity.Mathematics.float3(point.x, point.y, point.z); }

    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing metre/Y-up/XZ routes, saved art local+Z with existing1.20 scale only.
// Array Formats: Four fixed numeric tower value slots; stable-ID four-theme palette; no new spatial/native storage.
// Dependencies/Pointers: Main-thread registry/LOS adapter is separate from pure math; V1 owns pivots/gait and composite V2 lease owns disposal; host HP/damage/projectile authority remains unchanged.
