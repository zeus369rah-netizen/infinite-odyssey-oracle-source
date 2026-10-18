using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>Bounded main-thread XZ spacing. Actor triggers never hard-block a lane.</summary>
    public sealed class CrowdSpacingSolver : IDisposable
    {
        readonly SpatialTargetGrid grid;
        readonly Dictionary<int, OracleEnemy> actors;
        readonly int[] neighbors = new int[12];
        float nextSnapshot;
        // # ARCHITECTURE SNAPSHOT
        // Existing persistent native XZ grid,10Hz snapshots,100 actors and12 IDs.
        // The hot loop reads cached scalar envelopes; no mesh access or allocation.
        // Physical actors remain triggers; separation is speed-limited movement.
        const float SpacingPadding = .04f, QueryRadius = 3.8f;

        public CrowdSpacingSolver(int capacity = 100)
        { grid = new SpatialTargetGrid(capacity, 2f); actors = new Dictionary<int, OracleEnemy>(capacity); }

        public void Clear() { grid.Clear(); actors.Clear(); nextSnapshot = 0f; }

        public void Refresh(IReadOnlyList<OracleEnemy> enemies, float now)
        {
            if (now < nextSnapshot) return;
            nextSnapshot = now + .1f;
            grid.Clear(); actors.Clear();
            for (int i = 0; i < enemies.Count; i++) if (enemies[i] != null && enemies[i].Alive) Track(enemies[i]);
        }

        public void Track(OracleEnemy enemy)
        { actors[enemy.TargetId] = enemy; grid.Add(enemy.TargetId, enemy.transform.position); }

        public bool CanAdmit(Vector3 position) => CanAdmit(position, OracleEnemy.SpawnFootprintRadius(false));

        public bool CanAdmit(Vector3 position, float radius)
        {
            int count = grid.QueryRadius(position, QueryRadius, int.MinValue, neighbors);
            // A full caller buffer cannot prove that every close envelope was
            // considered. Fail closed for admission; replicas bypass admission.
            if (count == neighbors.Length) return false;
            for (int i = 0; i < count; i++)
                if (actors.TryGetValue(neighbors[i], out OracleEnemy actor) && actor != null && actor.Alive)
                { Vector3 delta = actor.transform.position - position; delta.y = 0f; float gap = radius + actor.CrowdRadius + SpacingPadding; if (delta.sqrMagnitude < gap * gap) return false; }
            return true;
        }

        public Vector3 Resolve(OracleEnemy enemy, Vector3 desired, Vector3 laneOrigin, Vector3 laneForward,
            bool constrainLane, float maximumSpeed, float dt, OraclePlayer localPlayer,float laneHalfWidth=.8f,PeerCombatTarget[] peers=null,int peerCount=0)
        {
            if (dt <= 0f) return Vector3.zero;
            Vector3 position = enemy.transform.position;
            desired.y = 0f;
            Vector3 result = desired, correction = Vector3.zero;
            int count = grid.QueryRadius(position, QueryRadius, enemy.TargetId, neighbors);
            for (int i = 0; i < count; i++)
            {
                if (!actors.TryGetValue(neighbors[i], out OracleEnemy other) || other == null || !other.Alive) continue;
                Avoid(position, other.transform.position, enemy.TargetId, other.TargetId, laneForward, enemy.CrowdRadius + other.CrowdRadius + SpacingPadding, dt, ref result, ref correction);
            }
            if (localPlayer != null && localPlayer.gameObject.activeInHierarchy)
                Avoid(position, localPlayer.transform.position, enemy.TargetId, int.MinValue, laneForward, enemy.NavigationRadius + .29f + SpacingPadding, dt, ref result, ref correction);
            int boundedPeers=peers==null?0:Mathf.Clamp(peerCount,0,Mathf.Min(8,peers.Length));
            for(int i=0;i<boundedPeers;i++)if(peers[i].Targetable)
                Avoid(position,peers[i].Feet,enemy.TargetId,int.MinValue+i,laneForward,
                    enemy.NavigationRadius+.29f+SpacingPadding,dt,ref result,ref correction);
            result += Vector3.ClampMagnitude(correction, 1.3f * dt);
            result = Vector3.ClampMagnitude(result, Mathf.Max(.8f, maximumSpeed) * dt);
            if (constrainLane)
            {
                Vector3 side = Vector3.Cross(Vector3.up, laneForward);
                float current = Vector3.Dot(position - laneOrigin, side);
                float next = current + Vector3.Dot(result, side);
                float allowed = Mathf.Clamp(next, -laneHalfWidth, laneHalfWidth);
                result += side * Mathf.Clamp(allowed - next, -maximumSpeed * dt, maximumSpeed * dt);
                result = Vector3.ClampMagnitude(result, Mathf.Max(.8f, maximumSpeed) * dt);
            }
            // Recheck the final lane-projected step against live neighbor poses.
            // This is still at most12, never a second all-actor search.
            for (int i = 0; i < count; i++)
                if (actors.TryGetValue(neighbors[i], out OracleEnemy other) && other != null && other.Alive)
                    result = PresentationFootprint.LimitSweptXZ(position, result, other.transform.position,
                        enemy.CrowdRadius + other.CrowdRadius + SpacingPadding);
            if (localPlayer != null && localPlayer.gameObject.activeInHierarchy)
                result = PresentationFootprint.LimitSweptXZ(position, result, localPlayer.transform.position,
                    enemy.NavigationRadius + .29f + SpacingPadding);
            for(int i=0;i<boundedPeers;i++)if(peers[i].Targetable)
                result=PresentationFootprint.LimitSweptXZ(position,result,peers[i].Feet,enemy.NavigationRadius+.29f+SpacingPadding);
            result = PlacedElementShrine.LimitMovementAroundTowers(position, result, enemy.TowerBodyRadius, enemy.MinimumVisualY, enemy.MaximumVisualY);
            result.y = 0f;
            return result;
        }

        static void Avoid(Vector3 self, Vector3 other, int selfId, int otherId, Vector3 forward,
            float spacing, float dt, ref Vector3 movement, ref Vector3 correction)
        {
            Vector3 toward = other - self; toward.y = 0f;
            float distance = toward.magnitude;
            Vector3 direction = distance > .0001f ? toward / distance : selfId < otherId ? -forward : forward;
            float permittedApproach = Mathf.Max(0f, distance - spacing);
            float approach = Vector3.Dot(movement, direction);
            if (approach > permittedApproach) movement -= direction * (approach - permittedApproach);
            if (distance < spacing)
                correction -= direction * Mathf.Min((spacing - distance) * .5f, 1.3f * dt);
        }

        public void Dispose() { grid.Dispose(); actors.Clear(); }
    }
}
