using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>
    /// First-person adaptation: a fixed pool of Oracle voxel wands orbit, release
    /// along the crosshair direction, then return. This future upgrade stays locked
    /// until explicitly enabled and is not a second starter attack stream. Uses the 10-damage basic
    /// baseline without granting Arcane charge or claiming original progression.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrbitalWeaponSystem : MonoBehaviour
    {
        public const int MinimumWeapons = 10;
        public const int MaximumWeapons = 14;
        public const int MaximumTrackedEnemies = 100;
        public int ActiveCount => weaponCount;
        public bool Unlocked { get; private set; }
        public int ReleasedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < weaponCount; i++)
                    if (weapons[i].State == WeaponState.Released) count++;
                return count;
            }
        }

        public enum WeaponState { Orbit, Released, Returning }

        const float OrbitRadius = 1.05f;
        const float OrbitHeight = 1.14f;
        const float OrbitSpeed = 1.05f;
        const float ReleaseInterval = 1f;
        const float Range = 396f / 32f;
        const float FlightSpeed = 740f / 32f;
        const float CollisionRadius = .065f;
        const float ReturnSpeed = 12f;

        sealed class Weapon
        {
            public Transform Root;
            public WeaponState State;
            public OracleEnemy Target;
            public Vector3 Position;
            public Vector3 Direction;
            public float Cooldown;
            public float RemainingRange;
            public float ReturnElapsed;
            public float DissolveElapsed;
            public bool Dissolving;
            public int Element;
            public float DamageScale;
            public MeshRenderer Renderer;
        }

        readonly Weapon[] weapons = new Weapon[MaximumWeapons];
        readonly RaycastHit[] sweepBuffer = new RaycastHit[48];
        OraclePlayer player;
        OracleCombat combat;
        SpatialTargetGrid targetGrid;
        Transform poolRoot;
        Material wandMaterial;
        Mesh wandMesh;
        Texture2D paletteTexture, glowTexture;
        int weaponCount;
        int nextWeapon;
        float phase;
        float releaseRemaining;
        bool initialized;
        bool hadStarted;

        public void Initialize(OraclePlayer oraclePlayer, OracleCombat oracleCombat, int count = 12)
        {
            if (initialized) return;
            if (oraclePlayer == null || oracleCombat == null) return;
            player = oraclePlayer;
            combat = oracleCombat;
            weaponCount = Mathf.Clamp(count, MinimumWeapons, MaximumWeapons);
            targetGrid = new SpatialTargetGrid(MaximumTrackedEnemies, 2f);
            // Unity native objects are constructed here, never in a field initializer.
            CreateSharedWandVisual();
            // This root is deliberately independent of player movement. Released
            // meshes retain world positions while the player walks and turns.
            poolRoot = new GameObject("Oracle orbital voxel weapons — fixed pool").transform;
            for (int i = 0; i < weaponCount; i++)
            {
                var weapon = new Weapon { Root = new GameObject("Orbital Oracle wand " + (i + 1)).transform };
                weapon.Root.SetParent(poolRoot, false);
                weapon.Root.gameObject.AddComponent<MeshFilter>().sharedMesh = wandMesh;
                weapon.Renderer = weapon.Root.gameObject.AddComponent<MeshRenderer>();
                weapon.Renderer.sharedMaterial = wandMaterial;
                weapon.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                weapon.Renderer.receiveShadows = false;
                weapons[i] = weapon;
            }
            initialized = true;
            ResetOrbit();
            poolRoot.gameObject.SetActive(Unlocked && GameSession.Instance != null && GameSession.Instance.CombatUnlocked);
        }

        public void SetUnlocked(bool unlocked)
        {
            Unlocked = unlocked;
            if (!initialized) return;
            ResetOrbit();
            poolRoot.gameObject.SetActive(Unlocked && GameSession.Instance != null && GameSession.Instance.CombatUnlocked);
        }

        /// <summary>Call after the player's restart teleport. Reuses every pooled mesh and native allocation.</summary>
        public void ResetOrbit()
        {
            if (!initialized || player == null) return;
            phase = 0f;
            releaseRemaining = .4f;
            nextWeapon = 0;
            targetGrid.Clear();
            hadStarted = GameSession.Instance != null && GameSession.Instance.HasStarted;
            for (int i = 0; i < weaponCount; i++)
            {
                Weapon weapon = weapons[i];
                weapon.State = WeaponState.Orbit;
                weapon.Target = null;
                weapon.Cooldown = 0f;
                weapon.RemainingRange = 0f;
                weapon.ReturnElapsed = weapon.DissolveElapsed = 0f;
                weapon.Dissolving = false;
                weapon.Root.localScale = Vector3.one;
                PlaceInOrbit(weapon, i);
            }
        }

        void Update()
        {
            if (!initialized || player == null || combat == null) return;
            GameSession session = GameSession.Instance;
            poolRoot.gameObject.SetActive(Unlocked && session != null && session.CombatUnlocked);
            if (!Unlocked) return;
            // Defensive reset also covers a restart caller that omitted ResetOrbit.
            if (session != null && hadStarted && !session.HasStarted) ResetOrbit();
            hadStarted = session != null && session.HasStarted;
            bool simulate = combat.Playing && !AbilityBindings.IsRebinding;
            bool animateOrbit = simulate || session != null && session.IsPlaying && session.CombatUnlocked;
            if (!animateOrbit) return;
            float dt = Time.deltaTime;
            phase = Mathf.Repeat(phase + dt * OrbitSpeed, Mathf.PI * 2f);
            for (int i = 0; i < weaponCount; i++)
            {
                Weapon weapon = weapons[i];
                if (simulate) weapon.Cooldown = Mathf.Max(0f, weapon.Cooldown - dt);
                if (weapon.State == WeaponState.Orbit) PlaceInOrbit(weapon, i);
                else if (simulate && weapon.State == WeaponState.Released) UpdateReleased(weapon, i, dt);
                else if (simulate) UpdateReturning(weapon, i, dt);
            }
            if (!simulate) return;
            releaseRemaining = Mathf.Max(0f, releaseRemaining - dt);
            if (releaseRemaining <= 0f) TryRelease();
        }

        Vector3 OrbitPosition(int index)
        {
            float angle = phase + index * Mathf.PI * 2f / weaponCount;
            return player.transform.position + new Vector3(Mathf.Cos(angle) * OrbitRadius,
                OrbitHeight + Mathf.Sin(angle * 2f) * .06f, Mathf.Sin(angle) * OrbitRadius);
        }

        void PlaceInOrbit(Weapon weapon, int index)
        {
            float angle = phase + index * Mathf.PI * 2f / weaponCount;
            weapon.Position = OrbitPosition(index);
            weapon.Root.position = weapon.Position;
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), .1f, Mathf.Cos(angle));
            weapon.Root.rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, 28f);
            // Keep the stationary ring outside the central aiming area even when
            // the player looks down. Released wands remain visible as they fly.
            Vector3 screen = player.ViewCamera.WorldToViewportPoint(weapon.Position);
            bool coversAim = screen.z > player.ViewCamera.nearClipPlane &&
                Mathf.Abs(screen.x - .5f) < .15f && Mathf.Abs(screen.y - .5f) < .16f;
            weapon.Renderer.enabled = !coversAim;
        }

        void TryRelease()
        {
            releaseRemaining = .2f;
            Weapon selected = null;
            int index = -1;
            Vector3 center = player.transform.position + Vector3.up * OrbitHeight;
            for (int n = 0; n < weaponCount; n++)
            {
                int candidate = (nextWeapon + n) % weaponCount;
                Weapon weapon = weapons[candidate];
                if (weapon.State != WeaponState.Orbit || weapon.Cooldown > 0f) continue;
                // A ring can be occluded by architecture at a corner. Such a wand
                // cannot launch from the far side of that wall.
                if (!combat.HasLineOfSight(center, weapon.Position)) continue;
                selected = weapon;
                index = candidate;
                break;
            }
            if (selected == null) return;
            nextWeapon = (index + 1) % weaponCount;
            selected.State = WeaponState.Released;
            selected.Renderer.enabled = true;
            selected.Target = null;
            selected.Element = GameSession.Instance.Progression != null ? GameSession.Instance.Progression.SelectedElement : 0;
            selected.DamageScale = combat.DamageMultiplier(selected.Element);
            selected.Direction = player.ViewCamera.transform.forward;
            selected.RemainingRange = Range;
            selected.Root.rotation = Quaternion.LookRotation(selected.Direction);
            releaseRemaining = ReleaseInterval;
        }

        OracleEnemy FindNearestVisible(Vector3 origin)
        {
            targetGrid.Clear();
            var enemies = combat.Enemies;
            int count = Mathf.Min(enemies.Count, MaximumTrackedEnemies);
            for (int i = 0; i < count; i++)
            {
                OracleEnemy enemy = enemies[i];
                if (enemy == null || !enemy.Targetable) continue;
                Vector3 target = enemy.AimPoint;
                float dx = target.x - origin.x, dz = target.z - origin.z;
                if (dx * dx + dz * dz > Range * Range || !combat.HasLineOfSight(origin, target)) continue;
                targetGrid.Add(enemy.TargetId, target);
            }
            if (!targetGrid.TryFindNearest(origin, Range, out int stableId, out Vector3 position)) return null;
            for (int i = 0; i < count; i++)
                if (enemies[i] != null && enemies[i].Targetable && enemies[i].TargetId == stableId) return enemies[i];
            return null;
        }

        void UpdateReleased(Weapon weapon, int index, float dt)
        {
            float distance = Mathf.Min(FlightSpeed * dt, weapon.RemainingRange);
            if (Sweep(weapon.Position, weapon.Direction, distance, false, out ProjectileContact015 hit))
            {
                if(hit.Blocked){BeginReturn(weapon,index);return;}
                Vector3 impact=hit.Point;
                OracleEnemy enemy=combat.ResolveMonsterContact015(hit);
                if(hit.HasMonster&&enemy==null){BeginReturn(weapon,index);return;}
                if (enemy != null && enemy.Targetable)
                {
                    enemy.TakeHit(10f * weapon.DamageScale,weapon.Direction,weapon.Element>=0&&weapon.Element<4?1<<weapon.Element:0,impact,true);
                    enemy.ApplyElementEffect(weapon.Element, weapon.Direction);
                }
                else
                {
                    BreakableVoxelWall wall = hit.WorldCollider!=null?hit.WorldCollider.GetComponentInParent<BreakableVoxelWall>():null;
                    if (wall != null) wall.DamageAt(impact, .3f);
                }
                combat.SpawnImpact(impact, new Color(.5f, 1f, .88f), .36f);
                weapon.Position = impact - weapon.Direction * .09f;
                weapon.Root.position = weapon.Position;
                BeginReturn(weapon, index);
                return;
            }
            weapon.Position += weapon.Direction * distance;
            weapon.Root.position = weapon.Position;
            weapon.Root.rotation = Quaternion.LookRotation(weapon.Direction);
            weapon.RemainingRange -= distance;
            if (weapon.RemainingRange <= 0f) BeginReturn(weapon, index);
        }

        void BeginReturn(Weapon weapon, int index)
        {
            weapon.State = WeaponState.Returning;
            weapon.Target = null;
            weapon.Cooldown = 1.6f + index * .025f;
            weapon.ReturnElapsed = weapon.DissolveElapsed = 0f;
            weapon.Dissolving = false;
        }

        void UpdateReturning(Weapon weapon, int index, float dt)
        {
            weapon.ReturnElapsed += dt;
            if (weapon.ReturnElapsed < .12f) return;
            Vector3 destination = OrbitPosition(index);
            Vector3 offset = destination - weapon.Position;
            if (!weapon.Dissolving && offset.sqrMagnitude < .035f)
            { Rejoin(weapon, index); return; }
            if (!weapon.Dissolving)
            {
                float distance = Mathf.Min(offset.magnitude, ReturnSpeed * dt);
                if (weapon.ReturnElapsed > 3f ||
                    Sweep(weapon.Position, offset.normalized, distance, true, out ProjectileContact015 blocked))
                    weapon.Dissolving = true;
                else
                {
                    weapon.Position += offset.normalized * distance;
                    weapon.Root.position = weapon.Position;
                    if (offset.sqrMagnitude > .001f) weapon.Root.rotation = Quaternion.LookRotation(offset.normalized);
                }
            }
            if (weapon.Dissolving)
            {
                // A blocked recall dissolves locally and reforms at the ring,
                // rather than visibly travelling through the wall.
                weapon.DissolveElapsed += dt;
                weapon.Root.localScale = Vector3.one * Mathf.Clamp01(1f - weapon.DissolveElapsed / .22f);
                if (weapon.DissolveElapsed >= .22f) Rejoin(weapon, index);
            }
        }

        void Rejoin(Weapon weapon, int index)
        {
            weapon.State = WeaponState.Orbit;
            weapon.Root.localScale = Vector3.one;
            weapon.Dissolving = false;
            PlaceInOrbit(weapon, index);
        }

        bool Sweep(Vector3 origin,Vector3 direction,float distance,bool ignoreEnemies,out ProjectileContact015 nearest)
        {
            return combat.SweepProjectileContact015(origin,CollisionRadius,direction,distance,
                !ignoreEnemies,true,false,null,0,out nearest);
        }

        void CreateSharedWandVisual()
        {
            paletteTexture = new Texture2D(3, 1, TextureFormat.RGBA32, false)
            {
                name = "Oracle wand three-color palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            paletteTexture.SetPixels(new[]
            {
                new Color(.025f, .065f, .05f), new Color(.58f, .42f, .17f), new Color(.282f, .871f, .835f)
            });
            paletteTexture.Apply(false, true);
            glowTexture = new Texture2D(3, 1, TextureFormat.RGBA32, false)
            {
                name = "Oracle wand focus glow mask", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            glowTexture.SetPixels(new[] { Color.black, new Color(.035f, .02f, .005f), new Color(.282f, .871f, .835f) });
            glowTexture.Apply(false, true);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Texture");
            wandMaterial = new Material(shader) { name = "Shared orbital Oracle palette", enableInstancing = true };
            if (wandMaterial.HasProperty("_BaseMap")) wandMaterial.SetTexture("_BaseMap", paletteTexture);
            if (wandMaterial.HasProperty("_MainTex")) wandMaterial.SetTexture("_MainTex", paletteTexture);
            if (wandMaterial.HasProperty("_BaseColor")) wandMaterial.SetColor("_BaseColor", Color.white);
            if (wandMaterial.HasProperty("_Color")) wandMaterial.SetColor("_Color", Color.white);
            if (wandMaterial.HasProperty("_Smoothness")) wandMaterial.SetFloat("_Smoothness", .24f);
            if (wandMaterial.HasProperty("_EmissionMap"))
            {
                wandMaterial.EnableKeyword("_EMISSION");
                wandMaterial.SetTexture("_EmissionMap", glowTexture);
                wandMaterial.SetColor("_EmissionColor", Color.white * 1.2f);
            }
            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var uvs = new List<Vector2>(96);
            var indices = new List<int>(144);
            AddWandBox(vertices, normals, uvs, indices, new Vector3(0f, 0f, -.045f),
                new Vector3(.043f, .043f, .27f), Quaternion.identity, 0);
            AddWandBox(vertices, normals, uvs, indices, new Vector3(0f, 0f, .075f),
                new Vector3(.07f, .07f, .045f), Quaternion.identity, 1);
            AddWandBox(vertices, normals, uvs, indices, new Vector3(0f, 0f, .145f),
                new Vector3(.062f, .062f, .11f), Quaternion.Euler(0f, 0f, 45f), 2);
            AddWandBox(vertices, normals, uvs, indices, new Vector3(0f, 0f, -.19f),
                new Vector3(.055f, .055f, .035f), Quaternion.identity, 1);
            wandMesh = new Mesh { name = "Shared four-block Oracle wand (one submesh)" };
            wandMesh.SetVertices(vertices);
            wandMesh.SetNormals(normals);
            wandMesh.SetUVs(0, uvs);
            wandMesh.SetTriangles(indices, 0);
            wandMesh.RecalculateBounds();
            wandMesh.UploadMeshData(true);
        }

        static void AddWandBox(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> indices, Vector3 center, Vector3 size, Quaternion rotation, int palette)
        {
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector2 uv = new Vector2((palette + .5f) / 3f, .5f);
            foreach (Vector3 normal in directions)
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.Cross(Vector3.up, normal);
                Vector3 v = Vector3.Cross(normal, u);
                Vector3 faceCenter = Vector3.Scale(normal, size) * .5f;
                Vector3 halfU = Vector3.Scale(u, size) * .5f;
                Vector3 halfV = Vector3.Scale(v, size) * .5f;
                int start = vertices.Count;
                vertices.Add(center + rotation * (faceCenter - halfU - halfV));
                vertices.Add(center + rotation * (faceCenter + halfU - halfV));
                vertices.Add(center + rotation * (faceCenter + halfU + halfV));
                vertices.Add(center + rotation * (faceCenter - halfU + halfV));
                for (int i = 0; i < 4; i++) { normals.Add(rotation * normal); uvs.Add(uv); }
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            }
        }

        void OnDestroy()
        {
            if (targetGrid != null) targetGrid.Dispose();
            if (poolRoot != null) Destroy(poolRoot.gameObject);
            if (wandMesh != null) Destroy(wandMesh);
            if (wandMaterial != null) Destroy(wandMaterial);
            if (paletteTexture != null) Destroy(paletteTexture);
            if (glowTexture != null) Destroy(glowTexture);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world metres/Y-up, original admission-cached oriented monster box; no gameplay geometry/HP changes.
// Array Formats: Existing48 actor/projectile pools consume owned numeric48 snapshots and fixed World buffers.
// Dependencies/Pointers: Atomic numeric sweep consumer migration; old triggers disabled only with complete packet; root owns native acceptance and actual source integration.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authoritative legacy Combat/World/revision and actor pool lifetime, world metres/Y-up.
// Array Formats: Existing48 actors with bounded feedback values/Morton jobs and one synchronous accepted-damage observation; no extra damage, mesh or material owner.
// Dependencies/Pointers: Positive actual HP loss owns immediate cached MPB red flash and observer dispatch. Root composes this exact work projection and verifies native collision/flash/audio/network/GC; replicas never move authoritatively.
