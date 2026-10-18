using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>Fixed-pool automatic element bolts, with non-allocating swept wall/target collision.</summary>
    public sealed class OracleProjectile : MonoBehaviour
    {
        internal enum Kind { MagicMissile, Resonance, ArcaneOrb, ShadeBolt, ShrineBolt, Fire, Water, Earth, Air }
        internal bool Flying { get; private set; }
        internal bool IsCosmicNova { get; private set; }
        OracleCombat owner;
        Kind kind;
        Vector3 direction;
        float speed, remaining, radius, damageScale;
        int statusMask, ingredientMask;
        int combo, piercedCount, networkShotId;
        // # ARCHITECTURE SNAPSHOT
        // Host pooled-flight terminal values only; remote copies never run this
        // collision/damage component. Slot generation survives terminal snapshots.
        int dangerGeneration, dangerSourceId, dangerAttackSerial, dangerLane;
        Vector3 dangerOrigin, dangerPosition, dangerNormal;
        bool dangerImpact;
        double dangerRetiredAt;
        readonly int[] piercedIds = new int[3];
        Transform visual, wingA, wingB;
        Renderer visualRenderer, wingRendererA, wingRendererB;
        internal void Initialize(OracleCombat combat, Material material)
        {
            owner = combat;
            visual = OracleCombat.CreateBlock(transform, "Element focus", Vector3.zero, Vector3.one, material);
            wingA = OracleCombat.CreateBlock(transform, "Element orbit A", Vector3.zero, Vector3.one, material);
            wingB = OracleCombat.CreateBlock(transform, "Element orbit B", Vector3.zero, Vector3.one, material);
            visualRenderer = visual.GetComponent<Renderer>();
            wingRendererA = wingA.GetComponent<Renderer>();
            wingRendererB = wingB.GetComponent<Renderer>();
            gameObject.SetActive(false);
        }
        internal void Launch(Kind projectileKind, Vector3 origin, Vector3 flightDirection,
            float flightSpeed, float range, float collisionRadius, Material material, float multiplier = 1f, int combination = 0, int authoritativeShotId = 0, bool cosmicNova = false,
            int hostileSourceId = 0, int hostileSerial = 0, int hostileLane = -1, int hostileGeneration = 0, int statusMask = 0, int ingredientMask = 0)
        {
            kind = projectileKind; IsCosmicNova = cosmicNova;
            dangerGeneration = projectileKind == Kind.ShadeBolt ? hostileGeneration : 0;
            dangerSourceId = hostileSourceId; dangerAttackSerial = hostileSerial; dangerLane = hostileLane;
            dangerOrigin = dangerPosition = origin; dangerNormal = default; dangerImpact = false; dangerRetiredAt = double.NegativeInfinity;
            direction = flightDirection.sqrMagnitude > .001f ? flightDirection.normalized : Vector3.forward;
            speed = flightSpeed; remaining = range; radius = collisionRadius; damageScale = multiplier;
            combo = Mathf.Clamp(combination, 0, 6); piercedCount = 0;
            networkShotId = Mathf.Max(0, authoritativeShotId);
            // Snapshot every pooled launch, including zero-mask ward/hostile shots.
            this.statusMask = statusMask & 0xFFF;
            this.ingredientMask = (int)kind >= (int)Kind.Fire ? SpellIngredientRules.ResolveMask((int)kind - (int)Kind.Fire, combo, ingredientMask) : 0;
            transform.position = origin; transform.rotation = Quaternion.LookRotation(direction);
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(radius * 1.5f, radius * 1.5f, radius * (kind == Kind.Earth ? 1.8f : 3.5f));
            visualRenderer.sharedMaterial = wingRendererA.sharedMaterial = wingRendererB.sharedMaterial = material;
            bool elemental = (int)kind >= (int)Kind.Fire;
            wingA.gameObject.SetActive(elemental); wingB.gameObject.SetActive(elemental);
            wingA.localPosition = new Vector3(radius * 1.4f, 0f, -.06f);
            wingB.localPosition = new Vector3(-radius * 1.4f, 0f, -.06f);
            Vector3 wingScale = kind == Kind.Air ? new Vector3(.22f, .025f, .08f) :
                kind == Kind.Fire ? new Vector3(.06f, .06f, .18f) : new Vector3(.07f, .07f, .07f);
            wingA.localScale = wingB.localScale = wingScale;
            Flying = true; gameObject.SetActive(true);
        }
        void Update()
        {
            if (!Flying || !owner.Playing) return;
            float distance = Mathf.Min(speed * Time.deltaTime, remaining);
            RaycastHit nearest = default;
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            bool authorityBolt=kind==Kind.ShadeBolt&&owner.PeerArenaCombat;
            int count = owner.QueryProjectileSweep(transform.position, radius, direction, distance, out RaycastHit[] hits);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider.GetComponentInParent<OracleProjectile>() != null) continue;
                bool player = owner.IsPlayerCollider(hit.collider);
                OracleEnemy enemy = hit.collider.GetComponentInParent<OracleEnemy>();
                bool alreadyPierced = false;
                if (enemy != null) for (int j = 0; j < piercedCount; j++) if (piercedIds[j] == enemy.TargetId) alreadyPierced = true;
                if (alreadyPierced) continue;
                OracleShrine shrine = hit.collider.GetComponentInParent<OracleShrine>();
                if(kind==Kind.ShadeBolt&&(enemy!=null||authorityBolt&&player))continue;
                if (kind != Kind.ShadeBolt && (player || shrine != null)) continue;
                if (enemy != null && !enemy.Targetable) continue;
                if (hit.distance < nearestDistance) { nearestDistance = hit.distance; nearest = hit; found = true; }
            }
            // Earliest world obstruction wins an equal-distance tie. Peer bodies
            // use actual validated capsules; clients never enter this damage path.
            float unobstructed=found?Mathf.Min(distance,nearestDistance):distance;
            if(authorityBolt&&dangerSourceId>0&&dangerAttackSerial>0&&
                owner.FindPeerProjectileContact(transform.position,direction,unobstructed,radius,dangerLane,
                    out ulong peerId,out float peerDistance)&&(!found||peerDistance<nearestDistance-.00001f))
            {
                Vector3 contact=transform.position+direction*peerDistance;
                RecordDangerContact(contact,-direction);
                owner.PeerProjectileHit(peerId,dangerSourceId,dangerAttackSerial,contact,damageScale);
                Retire();return;
            }
            if (found)
            {
                OracleEnemy enemy = nearest.collider.GetComponentInParent<OracleEnemy>();
                OracleShrine shrine = nearest.collider.GetComponentInParent<OracleShrine>();
                bool player = owner.IsPlayerCollider(nearest.collider);
                Vector3 impact = nearest.distance <= .0001f ? nearest.collider.ClosestPoint(transform.position) : nearest.point;
                if (kind == Kind.ShadeBolt)
                { RecordDangerContact(impact, nearest.normal); owner.HostileProjectileHit(player, shrine, impact, damageScale); }
                else
                {
                    if (enemy == null)
                    {
                        BreakableVoxelWall wall = nearest.collider.GetComponentInParent<BreakableVoxelWall>();
                        if (wall != null) wall.DamageAt(impact, kind == Kind.Earth ? .65f : .4f);
                    }
                    owner.ProjectileHit(kind, enemy, impact, damageScale, direction, combo, networkShotId, IsCosmicNova, statusMask, ingredientMask);
                }
                if (!IsCosmicNova && combo == 4 && enemy != null && piercedCount < 2)
                {
                    piercedIds[piercedCount++] = enemy.TargetId;
                    // Stay at the swept contact plane. Ignoring the pierced ID on
                    // the next sweep avoids jumping through a wall behind it.
                    float advance = Mathf.Max(0f, nearest.distance);
                    transform.position += direction * advance;
                    remaining -= advance;
                    if (remaining <= 0f) Retire();
                    return;
                }
                Retire(); return;
            }
            transform.position += direction * distance;
            visual.Rotate(0f, 0f, Time.deltaTime * (kind == Kind.Water ? 110f : 210f), Space.Self);
            wingA.Rotate(0f, 0f, Time.deltaTime * 180f, Space.Self);
            wingB.Rotate(0f, 0f, -Time.deltaTime * 180f, Space.Self);
            remaining -= distance;
            if (remaining <= 0f) Retire();
        }
        void RecordDangerContact(Vector3 point, Vector3 normal)
        {
            if (dangerGeneration < 1) return;
            dangerImpact = true; dangerPosition = point;
            dangerNormal = RemoteDangerPolicy.Direction(normal) ? normal.normalized : -direction;
        }
        internal bool CopyDangerState(int slot, out HostileBoltState state)
        {
            state = default;
            if (dangerGeneration < 1) return false;
            float age = Flying ? 0f : (float)(Time.timeAsDouble - dangerRetiredAt);
            if (!Flying && (age < 0f || age > RemoteDangerPolicy.TerminalRetention)) return false;
            state = new HostileBoltState { slot = slot, generation = dangerGeneration,
                sourceEnemyId = dangerSourceId, attackSerial = dangerAttackSerial, lane = dangerLane,
                active = Flying, impact = !Flying && dangerImpact, origin = dangerOrigin,
                position = Flying ? transform.position : dangerPosition, direction = direction,
                normal = dangerNormal, speed = speed, remaining = Mathf.Max(0f, remaining), age = age };
            return RemoteDangerPolicy.Bolt(state);
        }
        internal void ClearDangerState() { dangerGeneration = 0; dangerRetiredAt = double.NegativeInfinity; }
        internal void Retire()
        {
            if (Flying && dangerGeneration > 0)
            { if (!dangerImpact) dangerPosition = transform.position; dangerRetiredAt = Time.timeAsDouble; }
            Flying = false; IsCosmicNova = false; gameObject.SetActive(false);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Unity metres, Y-up/XZ; four ingredient bits and caller-owned four-double recovery timestamps.
// Existing host/solo owners retain simulation clocks, pooled48 projectiles and private native memory lifetimes.
