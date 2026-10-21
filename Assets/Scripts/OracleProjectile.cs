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
        int combo, piercedCount, networkShotId, landingOrdinal;
        uint roomReactionFlight;
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
            combo = Mathf.Clamp(combination, 0, 6); piercedCount = 0; landingOrdinal = 0;
            networkShotId = Mathf.Max(0, authoritativeShotId);
            roomReactionFlight=owner.NextRoomReactionFlight();
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
            owner.ApplyPairFlight020((int)kind-(int)Kind.Fire,combo,IsCosmicNova,radius,visual,wingA,wingB,visualRenderer,wingRendererA,wingRendererB);
            Flying = true; gameObject.SetActive(true);
        }
        void Update()
        {
            if (!Flying || !owner.Playing) return;
            float distance = Mathf.Min(speed * Time.deltaTime, remaining);
            bool authorityBolt=kind==Kind.ShadeBolt&&owner.PeerArenaCombat;
            bool found=owner.SweepProjectileContact015(transform.position,radius,direction,distance,
                kind!=Kind.ShadeBolt,kind!=Kind.ShadeBolt||authorityBolt,kind!=Kind.ShadeBolt,
                piercedIds,piercedCount,out ProjectileContact015 nearest);
            float nearestDistance=found?nearest.Distance:float.PositiveInfinity;
            if(found&&nearest.Blocked){Retire();return;} // Invalid/saturated queries stop without damage.
            // Earliest world obstruction wins an equal-distance tie. Peer bodies
            // use actual validated capsules; clients never enter this damage path.
            float unobstructed=found?Mathf.Min(distance,nearestDistance):distance;
            // Only already-accepted elemental bolts enter this cosmetic offline owner.
            // The normal world obstruction wins ties; no casting permission is added.
            var lanternWorld=GameSession.Instance!=null?GameSession.Instance.World:null;
            if((int)kind>=(int)Kind.Fire&&lanternWorld!=null&&
                lanternWorld.TryHitLantern(transform.position,direction,unobstructed,radius,damageScale,out float lanternContact))
            {
                Vector3 contact=transform.position+direction*lanternContact;
                owner.ProjectileHit(kind,null,contact,damageScale,direction,combo,networkShotId,IsCosmicNova,statusMask,ingredientMask,roomReactionFlight,landingOrdinal++,true);
                Retire();return;
            }
            if(owner.TryRoomReactionProjectile(kind,combo,IsCosmicNova,ingredientMask,networkShotId,roomReactionFlight,transform.position,direction,unobstructed,radius,out float roomContact))
            {
                Vector3 contact=transform.position+direction*roomContact;
                owner.ProjectileHit(kind,null,contact,damageScale,direction,combo,networkShotId,IsCosmicNova,statusMask,ingredientMask,roomReactionFlight,landingOrdinal++,true);Retire();return;
            }
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
                OracleEnemy enemy=owner.ResolveMonsterContact015(nearest);
                if(nearest.HasMonster&&enemy==null){Retire();return;}
                OracleShrine shrine=nearest.WorldCollider!=null?nearest.WorldCollider.GetComponentInParent<OracleShrine>():null;
                bool player=nearest.WorldCollider!=null&&owner.IsPlayerCollider(nearest.WorldCollider);
                Vector3 impact=nearest.Point;
                if (kind == Kind.ShadeBolt)
                { RecordDangerContact(impact, nearest.Normal); owner.HostileProjectileHit(player, shrine, impact, damageScale); }
                else
                {
                    if (enemy == null)
                    {
                        BreakableVoxelWall wall = nearest.WorldCollider!=null?nearest.WorldCollider.GetComponentInParent<BreakableVoxelWall>():null;
                        if (wall != null) wall.DamageAt(impact, kind == Kind.Earth ? .65f : .4f);
                    }
                    owner.ProjectileHit(kind, enemy, impact, damageScale, direction, combo, networkShotId, IsCosmicNova, statusMask, ingredientMask,roomReactionFlight,landingOrdinal++,!(!IsCosmicNova&&combo==4&&enemy!=null&&piercedCount<2),nearest.HasMonster?nearest.Monster.Generation:0);
                }
                if (!IsCosmicNova && combo == 4 && enemy != null && piercedCount < 2)
                {
                    piercedIds[piercedCount++] = enemy.TargetId;
                    // Stay at the swept contact plane. Ignoring the pierced ID on
                    // the next sweep avoids jumping through a wall behind it.
                    float advance = Mathf.Max(0f, nearest.Distance);
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

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: host accepted World contacts and existing run/epoch/shot identity; no damage/HP/path permission changes.
// Array Formats: existing bounded launch receipts add three-bit landing dedupe; fixed256B shared wire unchanged; new64B arena frame through existing NGO pipeline.
// Dependencies/Pointers: sole host/network/audio owners retain authority, lifetime and gesture/focus policies; actual peer/audio acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world metres/Y-up, original admission-cached oriented monster box; no gameplay geometry/HP changes.
// Array Formats: Existing48 actor/projectile pools consume owned numeric48 snapshots and fixed World buffers.
// Dependencies/Pointers: Atomic numeric sweep consumer migration; old triggers disabled only with complete packet; root owns native acceptance and actual source integration.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world contacts, projectile-local three-part visuals and cached shot identity; physical collision and gameplay semantics unchanged.
// Array Formats: Existing48/12 bolt pools,20 three-part impact slots, fixed six numeric/cue recipes and one extra local combo integer per accepted-shot receipt;64B/256B wire formats unchanged.
// Dependencies/Pointers: Existing host acceptance/dedupe/expiry and audio voice policies remain canonical. Presentation failures grant no damage, status, recovery or authority; root owns actual renderer/listening/browser/peer/performance acceptance.
