using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure value math in world metres: moving projectile sphere against the
    // actual upright1.85m player capsule (axis centers feet+.29..1.56, radius.29).
    // Caller owns fixed8 validated peer data and compares this distance with the
    // existing physical wall/core sweep. No collider, allocation or native owner.
    internal static class PeerCapsuleSweep
    {
        internal static bool TryHit(Vector3 origin, Vector3 direction, float distance,
            float projectileRadius, Vector3 feet, out float contact)
        {
            contact = 0f;
            if (!Finite(origin) || !Finite(direction) || !Finite(feet) ||
                float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f ||
                float.IsNaN(projectileRadius) || float.IsInfinity(projectileRadius) || projectileRadius < 0f || projectileRadius > 2f ||
                direction.sqrMagnitude < .000001f) return false;
            direction /= Mathf.Sqrt(direction.sqrMagnitude);
            const float height = 1.27f;
            float radius = .29f + projectileRadius;
            Vector3 p = origin - feet - Vector3.up * .29f;
            float closestY = Mathf.Clamp(p.y, 0f, height);
            float vertical = p.y - closestY;
            if (p.x * p.x + p.z * p.z + vertical * vertical <= radius * radius)
                return true;
            bool found = false;
            float best = distance;
            float a = direction.x * direction.x + direction.z * direction.z;
            if (a > .0000001f)
            {
                float b = p.x * direction.x + p.z * direction.z;
                float c = p.x * p.x + p.z * p.z - radius * radius;
                float discriminant = b * b - a * c;
                if (discriminant >= 0f)
                {
                    float t = (-b - Mathf.Sqrt(discriminant)) / a;
                    float y = p.y + direction.y * t;
                    if (t >= 0f && t <= best && y >= 0f && y <= height)
                    { best = t; found = true; }
                }
            }
            Sphere(p, direction, radius, ref best, ref found);
            Sphere(p - Vector3.up * height, direction, radius, ref best, ref found);
            if (found) contact = best;
            return found;
        }

        static void Sphere(Vector3 p, Vector3 direction, float radius, ref float best, ref bool found)
        {
            float b = Vector3.Dot(p, direction), c = p.sqrMagnitude - radius * radius;
            float discriminant = b * b - c;
            if (discriminant < 0f) return;
            float t = -b - Mathf.Sqrt(discriminant);
            if (t < 0f || t > best) return;
            best = t; found = true;
        }
        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
            !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
