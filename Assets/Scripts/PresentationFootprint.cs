using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Data: actor-local metre Bounds + scalar XZ clearances, cached at admission.
    // Measure is a main-thread mesh/Transform adapter used only at initialization
    // and after flying fins are created. Its temporary MeshFilter array is not a
    // per-frame allocation. Hot spacing reads only the cached value-type data.
    // Existing SpatialTargetGrid/native storage owns neighbor data; no new grid,
    // native container, projectile type, material, collider wall or job is added.
    internal struct PresentationFootprint
    {
        internal Bounds Hitbox;
        internal float CrowdRadius, NavigationRadius, CapsuleBottom, CapsuleTop;
        internal float LowerBodyRadius, MinimumVisualY;

        internal static PresentationFootprint Measure(Transform actor, Transform visual,
            Transform leftArm, Transform rightArm, float preservedNavigationRadius = 0f)
        {
            var filters = visual.GetComponentsInChildren<MeshFilter>();
            Bounds bounds = default;
            bool seeded = false;
            float idleRadiusSquared = 0f, sweptRadiusSquared = 0f, lowerRadiusSquared = 0f;
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            Matrix4x4 worldToActor = actor.worldToLocalMatrix;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter.sharedMesh == null) continue;
                Bounds local = SpiderLegPresentation009.AuthorityBounds(filter.sharedMesh);
                Matrix4x4 toActor = worldToActor * filter.transform.localToWorldMatrix;
                Transform arm = filter.transform.IsChildOf(leftArm) ? leftArm :
                    filter.transform.IsChildOf(rightArm) ? rightArm : null;
                Matrix4x4 toArm = arm != null ? arm.worldToLocalMatrix * filter.transform.localToWorldMatrix : Matrix4x4.identity;
                Matrix4x4 armParentToActor = arm != null ? worldToActor * arm.parent.localToWorldMatrix : Matrix4x4.identity;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    Vector3 actorPoint = toActor.MultiplyPoint3x4(p);
                    Include(actorPoint, ref bounds, ref seeded, ref sweptRadiusSquared);
                    idleRadiusSquared = Mathf.Max(idleRadiusSquared, XZSquared(actorPoint));
                    if (arm == null && actorPoint.y < .78f) lowerRadiusSquared = Mathf.Max(lowerRadiusSquared, XZSquared(actorPoint));
                    minY = Mathf.Min(minY, actorPoint.y); maxY = Mathf.Max(maxY, actorPoint.y);
                    if (arm != null)
                        IncludeArmSweep(toArm.MultiplyPoint3x4(p), arm.localPosition, armParentToActor,
                            ref bounds, ref seeded, ref sweptRadiusSquared);
                }
            }
            if (!seeded) return new PresentationFootprint { Hitbox = new Bounds(Vector3.up * 1.2f, new Vector3(1.2f, 2.4f, 1.2f)),
                CrowdRadius = 1.30f, NavigationRadius = .87f, CapsuleBottom = .90f, CapsuleTop = 1.57f,
                LowerBodyRadius = .62f, MinimumVisualY = .02f };
            // Trigger covers all procedural arm angles and the existing .035m bob.
            // Intended attack limbs may enter their victim; they do not widen reach.
            bounds.Expand(.08f);
            float navigation = preservedNavigationRadius > 0f ? preservedNavigationRadius : Mathf.Sqrt(idleRadiusSquared) + .008f;
            float bottom = Mathf.Max(.02f, minY) + navigation;
            return new PresentationFootprint { Hitbox = bounds,
                CrowdRadius = Mathf.Sqrt(sweptRadiusSquared) + .02f,
                NavigationRadius = navigation, CapsuleBottom = bottom,
                LowerBodyRadius = Mathf.Sqrt(lowerRadiusSquared) + .008f, MinimumVisualY = minY,
                CapsuleTop = Mathf.Max(bottom, maxY + .035f - navigation) };
        }

        static void IncludeArmSweep(Vector3 point, Vector3 pivot, Matrix4x4 parentToActor,
            ref Bounds bounds, ref bool seeded, ref float radiusSquared)
        {
            const float low = -105f * Mathf.Deg2Rad, high = 35f * Mathf.Deg2Rad;
            IncludeAt(point, pivot, parentToActor, low, ref bounds, ref seeded, ref radiusSquared);
            IncludeAt(point, pivot, parentToActor, high, ref bounds, ref seeded, ref radiusSquared);
            // Endpoints + analytic sin/cos extrema cover every angle in the
            // current procedural arm range, without sampling every animation frame.
            for (int turn = -2; turn <= 2; turn++)
            {
                float y = Mathf.Atan2(-point.z, point.y) + turn * Mathf.PI;
                float z = Mathf.Atan2(point.y, point.z) + turn * Mathf.PI;
                if (y > low && y < high) IncludeAt(point, pivot, parentToActor, y, ref bounds, ref seeded, ref radiusSquared);
                if (z > low && z < high) IncludeAt(point, pivot, parentToActor, z, ref bounds, ref seeded, ref radiusSquared);
            }
        }

        static void IncludeAt(Vector3 p, Vector3 pivot, Matrix4x4 parent, float angle,
            ref Bounds bounds, ref bool seeded, ref float radiusSquared)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            Include(parent.MultiplyPoint3x4(pivot + new Vector3(p.x, p.y * c - p.z * s, p.y * s + p.z * c)),
                ref bounds, ref seeded, ref radiusSquared);
        }

        static void Include(Vector3 point, ref Bounds bounds, ref bool seeded, ref float radiusSquared)
        {
            if (!seeded) { bounds = new Bounds(point, Vector3.zero); seeded = true; }
            else bounds.Encapsulate(point);
            radiusSquared = Mathf.Max(radiusSquared, XZSquared(point));
        }
        static float XZSquared(Vector3 p) => p.x * p.x + p.z * p.z;

        // Pure value-type movement constraint. Never adds a solid PhysX blocker.
        internal static Vector3 LimitSweptXZ(Vector3 origin, Vector3 movement, Vector3 center, float clearance)
        {
            Vector3 relative = origin - center; relative.y = 0f; movement.y = 0f;
            float a = movement.sqrMagnitude, c = relative.sqrMagnitude - clearance * clearance;
            if (a < .0000001f) return movement;
            if (c < 0f)
            {
                // Existing overlap can move outward; remove only inward motion.
                float inward = Vector3.Dot(movement, relative);
                if (inward < 0f && relative.sqrMagnitude > .000001f)
                    movement -= relative * (inward / relative.sqrMagnitude);
                return movement;
            }
            float b = 2f * Vector3.Dot(relative, movement), discriminant = b * b - 4f * a * c;
            if (b >= 0f || discriminant < 0f) return movement;
            float contact = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            return contact >= 0f && contact <= 1f ? movement * Mathf.Max(0f, contact - .001f) : movement;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original actor-local metres, +Y up/+Z forward; existing1.20 body scale only.
// Array Formats: Eight rigid spider legs and bounded numeric job outputs; baseline admission geometry remains authoritative.
// Dependencies/Pointers: New SpiderLegPresentation009 lease owns only visuals/native pose scratch; original V1/V2 lifetime and host damage remain.
