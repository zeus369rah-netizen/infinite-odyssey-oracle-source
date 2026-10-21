using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread presentation adapter: world-metre floor probes and root delta,
    // actor-local rigid block poses. Cached mesh Bounds and eight ray slots are
    // allocated once per actor. The verified default-OFF native pilot is optional.
    // This class never selects a victim, changes HP, fires a shot or advances an
    // attack clock. Only the existing host simulation may correct foot-root Y.
    internal sealed class GroundedGolemMotion
    {
        readonly Transform root, body, leftArm, rightArm;
        readonly RigidPoseAnimation015 clip015;
        Transform leftLeg, rightLeg;
        readonly Transform spine, head;
        Bounds leftBounds, rightBounds;
        readonly MonsterPooledView015 pooledView015;
        readonly MonsterVoxelCosmetics.Lease cosmeticParts;
        readonly RaycastHit[] groundHits = new RaycastHit[8];
        Vector3 previous;
        float gait, weight, nextProbe, deathTime;
        GroundedFloorSample floor;

        internal GroundedGolemMotion(Transform root, Transform body, Transform leftLeg,
            Transform rightLeg, Transform leftArm, Transform rightArm, MonsterVoxelCosmetics.Lease cosmeticParts = null, MonsterPooledView015 pooledView015 = null)
        {
            this.root = root; this.body = body; this.leftLeg = leftLeg; this.rightLeg = rightLeg;
            this.cosmeticParts = cosmeticParts; this.pooledView015 = pooledView015;
            this.leftArm = leftArm; this.rightArm = rightArm;
            if (pooledView015 != null)
            {
                spine = pooledView015.Spine; head = pooledView015.Head; RebindPooled015(); clip015=RigidPoseAnimation015.TryCreate(root.GetComponent<OracleEnemy>(),body); return;
            }
            leftBounds = leftLeg.GetComponent<MeshFilter>().sharedMesh.bounds;
            rightBounds = rightLeg.GetComponent<MeshFilter>().sharedMesh.bounds;
            if (IsSpider015)
            { previous = root.position; clip015=RigidPoseAnimation015.TryCreate(root.GetComponent<OracleEnemy>(),body); return; } // Eight-leg model has no biped spine/head owner.
            spine = Pivot(body, "Procedural torso pivot", new Vector3(0f, .65f, 0f));
            head = Pivot(spine, "Procedural head pivot", body.TransformPoint(new Vector3(0f, 1.48f, 0f)), true);
            // Original rigid pieces/colors stay intact; only their parents change.
            Reparent(body, spine, "Torso"); Reparent(body, spine, "Chest rune");
            Reparent(body, head, "Stone brow"); Reparent(body, head, "Face shadow");
            Reparent(body, head, "Left rune eye"); Reparent(body, head, "Right rune eye");
            Reparent(body, head, "Shade crown");
            previous = root.position;
            clip015=RigidPoseAnimation015.TryCreate(root.GetComponent<OracleEnemy>(),body);
        }

        internal bool NativePoseActive015=>clip015!=null&&clip015.Active;
        internal bool NativeShadePose015=>clip015!=null&&clip015.IsShadeProfile;
        internal bool NativeSpiderPose015=>clip015!=null&&clip015.IsSpiderProfile;
        internal ulong NativePoseEvaluations015=>clip015!=null?clip015.EvaluationCount:0ul;
        internal uint NativePoseGeneration015=>clip015!=null?clip015.Generation:0u;
        internal void SynchronizeNativeOwner015(){clip015?.SynchronizeOwner();}
        internal void ResetNativePose015(uint generation){clip015?.ResetForLife(generation);if(IsSpider015)SetNativeSpiderSole015();previous=root.position;gait=weight=0f;}
        internal void DisposeNativePose015(){clip015?.Dispose();}

        bool IsSpider015 => pooledView015 != null ? pooledView015.IsSpider : cosmeticParts != null && cosmeticParts.SpiderLegs != null;
        void PoseSpider015(float value, float blend, bool flying)
        { if (pooledView015 != null) pooledView015.PoseSpider(value, blend, flying); else cosmeticParts.PoseSpider(value, blend, flying); }
        void SetNativeSpiderSole015()
        {if(pooledView015!=null)pooledView015.SetNativeSpiderSole015();else cosmeticParts?.SetNativeSpiderSole015();}
        float LowestSpiderSole015() => pooledView015 != null ? pooledView015.LowestSpiderSole : cosmeticParts.LowestSpiderSole();
        internal void RebindPooled015()
        {
            if (pooledView015 == null) return;
            leftLeg = pooledView015.LeftLeg; rightLeg = pooledView015.RightLeg;
            leftBounds = pooledView015.LeftBounds; rightBounds = pooledView015.RightBounds;
            previous = root.position; gait = weight = nextProbe = deathTime = 0f; floor = default;
            spine.localRotation = head.localRotation = Quaternion.identity;
        }

        internal void Tick(float dt, bool alive, bool flying, bool dormant, bool authoritative,
            OracleEnemy.GuardianAttackPhase phase, float progress, float hurt)
        {
            Vector3 delta = root.position - previous; previous = root.position; delta.y = 0f;
            if (dt <= 0f) return;
            if (!alive)
            {
                deathTime += dt;
                body.localRotation = Quaternion.Euler(Mathf.Min(26f, deathTime * 34f), 0f, deathTime * 11f);
                // Host already owns despawn timing; replicas receive no damage here.
                if (!authoritative)
                {
                    body.localPosition = Vector3.down * deathTime * 1.7f;
                    body.localScale = Vector3.one * (OracleEnemy.PresentationScale * Mathf.Max(.05f, 1f - deathTime * 1.3f));
                }
                return;
            }
            deathTime = 0f;
            if (dormant)
            {
                // Keep the authored seated articulation, but plant its actual
                // soles as well. No gait or attack clocks advance in this path.
                if (!flying) SupportFeet(dt, authoritative, IsSpider015);
                return;
            }
            float distance = delta.magnitude;
            bool moving = distance > .0001f && distance < 1f;
            weight = Mathf.MoveTowards(weight, moving ? 1f : 0f, dt * 6f);
            if (moving) gait += distance * (Mathf.PI * 2f / .9f);
            float stride = Mathf.Sin(gait) * weight;
            float lean = phase == OracleEnemy.GuardianAttackPhase.Windup ? Mathf.Lerp(0f, 7f, progress) :
                phase == OracleEnemy.GuardianAttackPhase.Strike ? Mathf.Lerp(-11f, 2f, progress) : -3f * weight;
            float recoil = hurt > 0f ? Mathf.Clamp01(hurt / .15f) : 0f;
            body.localRotation = Quaternion.identity;
            bool spider = IsSpider015;
            bool nativePose=NativePoseActive015;
            if (!spider && !nativePose)
            {
                spine.localRotation = Quaternion.Euler(lean + recoil * 5f, 0f, -stride * 1.2f);
                head.localRotation = Quaternion.Euler(-lean * .35f, -stride * 2f, 0f);
            }
            body.localPosition = new Vector3(0f, 0f, -recoil * .022f);
            if (!nativePose && !authoritative && (phase == OracleEnemy.GuardianAttackPhase.Windup || phase == OracleEnemy.GuardianAttackPhase.Strike))
            {
                float angle = phase == OracleEnemy.GuardianAttackPhase.Windup ? -105f * progress : Mathf.Lerp(-105f, 35f, progress);
                leftArm.localRotation = rightArm.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
            if (spider)
            {
                if(nativePose&&NativeSpiderPose015&&!flying)
                {clip015.Evaluate(dt,weight,gait/(Mathf.PI*2f),phase,progress);SetNativeSpiderSole015();}
                else PoseSpider015(gait, weight, flying);
                if (!flying) SupportFeet(dt, authoritative, true);
                return;
            }
            if (flying)
            {
                if(nativePose)clip015.Evaluate(dt,weight,gait/(Mathf.PI*2f),phase,progress);
                else
                {
                    spine.localRotation = head.localRotation = Quaternion.identity;
                    leftLeg.localRotation = rightLeg.localRotation = Quaternion.Euler(-48f, 0f, 0f);
                }
                return; // Existing host/peer root hover owns altitude; no grounded sole correction in flight.
            }
            if(nativePose)clip015.Evaluate(dt,weight,gait/(Mathf.PI*2f),phase,progress);
            else
            {
                PoseLeg(leftLeg, -.23f, stride, lean);
                PoseLeg(rightLeg, .23f, -stride, lean);
                if (phase == OracleEnemy.GuardianAttackPhase.Ready || phase == OracleEnemy.GuardianAttackPhase.Recovery)
                {
                    leftArm.localRotation = Quaternion.Euler(-stride * 16f, 0f, 0f);
                    rightArm.localRotation = Quaternion.Euler(stride * 16f, 0f, 0f);
                }
            }
            SupportFeet(dt, authoritative, false);
        }

        void SupportFeet(float dt, bool authoritative, bool spider)
        {
            nextProbe -= dt;
            if (nextProbe <= 0f)
            {
                nextProbe = .1f;
                ProbeFloor();
            }
            Vector3 rootPosition = root.position;
            if (!floor.IsUsable(rootPosition.x, rootPosition.y, rootPosition.z)) return;
            if (authoritative)
            {
                Vector3 p = rootPosition; p.y = floor.Height; root.position = p; previous = p;
            }
            float sole = spider ? LowestSpiderSole015() : Mathf.Min(Sole(leftLeg, leftBounds), Sole(rightLeg, rightBounds));
            float up = root.TransformVector(Vector3.up).y;
            if (Mathf.Abs(up) > .001f)
                body.localPosition += Vector3.up * ((floor.Height + .004f - sole) / up);
        }

        static void PoseLeg(Transform leg, float x, float stride, float lean)
        {
            leg.localPosition = new Vector3(x, .29f + Mathf.Max(0f, stride) * .07f, -stride * .075f);
            leg.localRotation = Quaternion.Euler(stride * 18f - lean, 0f, 0f);
        }

        static Transform Pivot(Transform parent, string name, Vector3 position, bool world = false)
        {
            Transform pivot = new GameObject(name).transform; pivot.SetParent(parent, false);
            if (world) pivot.position = position; else pivot.localPosition = position;
            return pivot;
        }

        static void Reparent(Transform parent, Transform pivot, string name)
        { Transform part = parent.Find(name); if (part != null) part.SetParent(pivot, true); }

        static float Sole(Transform leg, Bounds bounds)
        {
            Matrix4x4 m = leg.localToWorldMatrix;
            Vector3 center = m.MultiplyPoint3x4(bounds.center), e = bounds.extents;
            return center.y - (Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z);
        }

        void ProbeFloor()
        {
            Vector3 origin = root.position;
            floor.Begin(origin.x, origin.z);
            int count = Physics.RaycastNonAlloc(root.position + Vector3.up * 1.5f, Vector3.down,
                groundHits, 3f, ~0, QueryTriggerInteraction.Ignore);
            if (count == groundHits.Length) return; // Unknown complete result: do not reuse stale support.
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(root) || hit.collider.GetComponentInParent<OraclePlayer>() != null ||
                    hit.collider.GetComponentInParent<OracleShrine>() != null || hit.collider.GetComponentInParent<OracleProjectile>() != null) continue;
                floor.Consider(hit.distance, hit.point.y, hit.normal.y, origin.y);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world metres and original actor-local+Z/Y-up; pooled lifetime only.
// Array Formats: Exactly48 persistent actor slots/four retained original templates/17 reusable part slots per actor.
// Dependencies/Pointers: Existing HP/projectile/attack owners remain; root integrates guarded hunks; native/Burst/memory acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current world-root authority and six original body-local pivots; native pilot remains OFF by default.
// Array Formats: Existing support buffers and pool caches; at most two owned graphs, no new per-frame allocation.
// Dependencies/Pointers: Current pool/XP/health/death/reward owners unchanged; graph evaluation precedes original SupportFeet and pool generation resets.

