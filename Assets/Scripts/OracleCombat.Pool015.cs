using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        readonly OracleEnemy[] pooledEnemies015 = new OracleEnemy[MaxActiveEnemies];
        MonsterPoolTemplates015 poolTemplates015;
        bool enemyPoolReady015, enemyPoolDisposed015, enemyPoolDisposing015;
        public int PrewarmedEnemyCapacity015 => enemyPoolReady015 ? pooledEnemies015.Length : 0;

        // Called once after encounter resources exist, before accepted player actions/admissions.
        void PrepareEnemyPool015()
        {
            if (enemyPoolReady015) return;
            if (enemyPoolDisposed015 || enemyRoot == null) throw new System.InvalidOperationException("Enemy pool lifetime is unavailable.");
            try
            {
                poolTemplates015 = new MonsterPoolTemplates015(enemyRoot, stone, darkStone, turquoise, violet);
                for (int i = 0; i < pooledEnemies015.Length; i++)
                {
                    var root = new GameObject("Prewarmed original monster slot " + i);
                    root.SetActive(false); root.transform.SetParent(enemyRoot, false);
                    var actor = root.AddComponent<OracleEnemy>(); pooledEnemies015[i] = actor;
                    actor.PreparePooled015(this, poolTemplates015, stone, darkStone, turquoise, violet, red, barBack);actor.BindFeedbackSlot022(i);
                }
                enemyPoolReady015 = true;PrepareEnemyFeedback022();
            }
            catch { DisposeEnemyPool015(); throw; }
        }
        OracleEnemy RentPooledEnemy015(Vector3 point, int stableId, MonsterVoxelCosmetics.Style style, bool flying, bool seated)
        {
            if (!enemyPoolReady015 || enemyPoolDisposed015 || stableId < 1) return null;
            SweepRetiredEnemies015();
            for (int i = 0; i < pooledEnemies015.Length; i++)
            {
                OracleEnemy actor = pooledEnemies015[i];
                if (actor == null) return null; // Unexpected lost capacity is not rebuilt during combat.
                if (actor.PoolLeased015) continue;
                if (actor.ActivatePooled015(point, stableId, style, flying, seated)) return actor;
            }
            return null; // Admission owner defers; no destroy/new graph or silent over-capacity.
        }
        void SweepRetiredEnemies015()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                OracleEnemy actor = enemies[i];
                if (actor != null && (actor.Alive || actor.gameObject.activeSelf)) continue;
                if (actor != null) actor.RetirePooled015();
                enemies.RemoveAt(i);
            }
        }
        internal void RetireEnemy015(OracleEnemy actor)
        {
            if (actor == null) return;
            if (!actor.PreparedPool015) throw new System.InvalidOperationException("Unowned actor cannot enter this pool.");
            bool owned = false;
            for (int i = 0; i < pooledEnemies015.Length; i++) if (object.ReferenceEquals(pooledEnemies015[i], actor)) { owned = true; break; }
            if (!owned) throw new System.InvalidOperationException("A different Combat owner owns this actor.");
            actor.RetirePooled015(); enemies.Remove(actor);
        }
        void RetireAllEnemies015()
        {
            AdvanceEnemyFeedbackScope022();
            for (int i = 0; i < pooledEnemies015.Length; i++)
                if (pooledEnemies015[i] != null) pooledEnemies015[i].RetirePooled015();
            enemies.Clear();
        }
        // Ledger owner rollback only, before accepted admission counters advance; never a death/reward.
        internal bool AbortUncommittedEnemy015(OracleEnemy actor, int expectedStableId, uint expectedPoolGeneration)
        {
            if (actor == null || !actor.PoolLeased015 || actor.TargetId != expectedStableId || expectedPoolGeneration == 0u ||
                actor.PoolGeneration015 != expectedPoolGeneration) return false;
            bool owned = false;
            for (int i = 0; i < pooledEnemies015.Length; i++) if (ReferenceEquals(pooledEnemies015[i], actor)) { owned = true; break; }
            if (!owned) return false;
            RetireEnemy015(actor); targetGrid.Clear(); crowdSpacing.Clear(); towerAttackSlots.Clear(); return true;
        }
        void ResetRetainedCombatPresentation015()
        {
            for (int i = 0; i < projectiles.Count; i++) { projectiles[i].Retire(); projectiles[i].ClearDangerState(); }
            ClearCosmeticShots(); remoteHostileBolts?.Clear(); hostileGeneration = 0;
            for (int i = 0; i < impacts.Count; i++) { impacts[i].Remaining = 0f; impacts[i].Root.gameObject.SetActive(false); }
        }
        void DisposeEnemyPool015()
        {
            if (enemyPoolDisposed015 || enemyPoolDisposing015) return;
            enemyPoolDisposing015 = true; enemyPoolReady015 = false;DisposeEnemyFeedback022();
            try
            {
                for (int i = 0; i < pooledEnemies015.Length; i++)
                    if (pooledEnemies015[i] != null) pooledEnemies015[i].DisposePooledResources015();
                // A view failure retains its owner and all template leases for explicit retry.
                poolTemplates015?.Dispose(); poolTemplates015 = null; enemyPoolDisposed015 = true;
            }
            finally { enemyPoolDisposing015 = false; }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world foot anchors; original actor-local+Z/Y-up views, original navigation/HP authority.
// Array Formats: Exactly48 persistent actor references, four template leases and existing48 projectile/12 cosmetic/20 impact pools retained over resets.
// Dependencies/Pointers: Loading-only graph allocation; corpse-tail holds slot; host admission defers or rolls back generation-matched uncommitted rental; view fences/detachment precede shared-template disposal.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authoritative legacy Combat/World/revision and actor pool lifetime, world metres/Y-up.
// Array Formats: Existing48 actors with bounded feedback values/Morton jobs and one synchronous accepted-damage observation; no extra damage, mesh or material owner.
// Dependencies/Pointers: Positive actual HP loss owns immediate cached MPB red flash and observer dispatch. Root composes this exact work projection and verifies native collision/flash/audio/network/GC; replicas never move authoritatively.
