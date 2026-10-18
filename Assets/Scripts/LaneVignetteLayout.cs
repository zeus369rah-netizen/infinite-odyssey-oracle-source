using System;
using System.Collections.Generic;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure campaign-neutral geometry contract. World supplies current dimensions and
    // authoritative pad/spawn/old-prop keepouts; recipes never infer active player count.
    // Only build-time arrays/reservations allocate. No Unity, frame loop or gameplay.
    public readonly struct VignettePoint
    {
        public readonly double X, Y, Z;
        public VignettePoint(double x, double y, double z) { X = x; Y = y; Z = z; }
    }
    public readonly struct VignetteKeepout
    {
        public readonly double X, Z, Radius;
        public VignetteKeepout(double x, double z, double radius) { X = x; Z = z; Radius = radius; }
    }
    public enum VignetteSurface
    {
        Stone, PaleStone, DarkStone, Moss, Wood, WoodEnd, Iron, Brass,
        Soil, Green, RuneGreen, Sand, Snow, Ember, Water, Azure,
        PortalCrystal, Blossom, BlossomPale, Pine, Autumn, Flame, FlameCore, Cloud
    }
    public enum VignettePrimitive { Box, Crystal, CanopyBox, LavaTop, SteamQuad }
    public readonly struct LaneVignettePiece
    {
        public readonly int Lane;
        public readonly VignettePrimitive Primitive;
        public readonly VignetteSurface Surface;
        public readonly VignettePoint Center, Size, TreeRoot;
        public readonly double YawDegrees, TreeHeight, Red, Green, Blue, Glow;
        public LaneVignettePiece(int lane, VignettePrimitive primitive, VignetteSurface surface,
            VignettePoint center, VignettePoint size, double yaw, VignettePoint root = default,
            double treeHeight = 0, double red = 1, double green = 1, double blue = 1, double glow = 0)
        { Lane = lane; Primitive = primitive; Surface = surface; Center = center; Size = size; YawDegrees = yaw;
          TreeRoot = root; TreeHeight = treeHeight; Red = red; Green = green; Blue = blue; Glow = glow; }
        public int Triangles => Primitive == VignettePrimitive.Crystal ? 24
            : Primitive == VignettePrimitive.SteamQuad ? 4 : Primitive == VignettePrimitive.LavaTop ? 2 : 12;
    }
    public enum VignetteProxyKind { Trunk, NonwalkableScenery }
    public readonly struct VignetteTrunkProxy
    {
        public readonly int Lane;
        public readonly VignettePoint Center, Size;
        public readonly VignetteProxyKind Kind;
        public readonly double YawDegrees;
        public VignetteTrunkProxy(int lane, VignettePoint center, VignettePoint size,VignetteProxyKind kind=VignetteProxyKind.Trunk,double yaw=0)
        { Lane=lane;Center=center;Size=size;Kind=kind;YawDegrees=yaw; }
    }
    public delegate void LaneVignetteWriter(LaneVignettePiece piece);
    public delegate void LaneVignetteColliderWriter(VignetteTrunkProxy proxy);

    public sealed class LaneVignetteLayout
    {
        public readonly double ArenaRadius, PortalRadius, RingRadius, RoadHalfWidth;
        public readonly VignettePoint[] Pads, EnemySpawns;
        public readonly VignetteKeepout[] ExistingKeepouts;
        private readonly List<VignetteKeepout> accepted = new List<VignetteKeepout>(128);
        public LaneVignetteLayout(double arenaRadius, double portalRadius, double ringRadius, double roadHalfWidth,
            VignettePoint[] pads, VignettePoint[] enemySpawns, VignetteKeepout[] existingKeepouts)
        {
            if (!Finite(arenaRadius) || !Finite(portalRadius) || !Finite(ringRadius) || !Finite(roadHalfWidth)
                || arenaRadius < 30 || portalRadius < 20 || ringRadius <= portalRadius || ringRadius >= arenaRadius
                || roadHalfWidth <= 0 || pads == null || enemySpawns == null || existingKeepouts == null
                || pads.Length > 32 || enemySpawns.Length > 100 || existingKeepouts.Length > 4096)
                throw new ArgumentException("Supply the current bounded World coordinate contract.");
            ArenaRadius = arenaRadius; PortalRadius = portalRadius; RingRadius = ringRadius; RoadHalfWidth = roadHalfWidth;
            Pads = (VignettePoint[])pads.Clone(); EnemySpawns = (VignettePoint[])enemySpawns.Clone();
            ExistingKeepouts = (VignetteKeepout[])existingKeepouts.Clone();
            foreach (var p in Pads) if (!Finite(p.X) || !Finite(p.Z)) throw new ArgumentException("Invalid pad.");
            foreach (var p in EnemySpawns) if (!Finite(p.X) || !Finite(p.Z)) throw new ArgumentException("Invalid spawn.");
            foreach (var p in ExistingKeepouts) if (!Finite(p.X) || !Finite(p.Z) || !Finite(p.Radius) || p.Radius < 0) throw new ArgumentException("Invalid keepout.");
        }
        public void ResetReservations() { accepted.Clear(); }
        public bool IsClear(double x, double z, double radius)
        {
            if (!Finite(x) || !Finite(z) || !Finite(radius) || radius <= 0) return false;
            double distance = Math.Sqrt(x * x + z * z);
            if (distance - radius < 10 || distance + radius > RingRadius - 2.1) return false;
            foreach (var p in Pads) if (Distance(x, z, p.X, p.Z) < 3 + radius) return false;
            foreach (var p in EnemySpawns) if (Distance(x, z, p.X, p.Z) < 1.25 + radius) return false;
            for (int lane = 0; lane < 8; lane++)
            {
                double a = lane * Math.PI / 4, dx = Math.Cos(a), dz = Math.Sin(a);
                if (x * dx + z * dz >= 0 && Math.Abs(-x * dz + z * dx) < RoadHalfWidth + 1.1 + radius) return false;
                if (Distance(x, z, dx * PortalRadius, dz * PortalRadius) < 5 + radius) return false;
            }
            if (Math.Abs(Math.Abs(x) + Math.Abs(z) - PortalRadius) / Math.Sqrt(2) < 2.4 + radius) return false;
            foreach (var k in ExistingKeepouts) if (Distance(x, z, k.X, k.Z) < k.Radius + radius + .10) return false;
            foreach (var k in accepted) if (Distance(x, z, k.X, k.Z) < k.Radius + radius + .15) return false;
            return true;
        }
        public bool TryReserve(double x, double z, double radius)
        {
            if (accepted.Count >= 128 || !IsClear(x, z, radius)) return false;
            accepted.Add(new VignetteKeepout(x, z, radius)); return true;
        }
        public bool TryAnchor(int lane, bool hero, double radius, out VignettePoint point)
        {
            // Derive all anchors from the supplied current portal scale, never radius76 constants.
            double[] offsets = hero ? HeroAngles : SecondaryAngles;
            for (int step = 0; step < 5; step++)
            for (int index = 0; index < offsets.Length; index++)
            {
                double r = PortalRadius * (hero ? .925 : .625) + RadialOffsets[step];
                double a = (lane * 45 + offsets[index]) * Math.PI / 180;
                double x = Math.Cos(a) * r, z = Math.Sin(a) * r;
                if (!TryReserve(x, z, radius)) continue;
                point = new VignettePoint(x, 0, z); return true;
            }
            point = default; return false;
        }
        public bool TryBuildingAnchor(int lane, double radius, out VignettePoint point)
        {
            for (int step = 0; step < 5; step++)
            for (int side = 0; side < 2; side++)
            {
                double r = PortalRadius * 1.025 - step * 1.5;
                double a = (lane * 45 + (side == 0 ? 21.5 : -21.5)) * Math.PI / 180;
                double x = Math.Cos(a) * r, z = Math.Sin(a) * r;
                if (!TryReserve(x, z, radius)) continue;
                point = new VignettePoint(x, 0, z); return true;
            }
            point = default; return false;
        }
        private static readonly double[] HeroAngles = { 17, 21, 13, -17, -21, -13 };
        private static readonly double[] SecondaryAngles = { -21, -17, -13, 21, 17, 13 };
        private static readonly double[] RadialOffsets = { 0, -2, 2, -4, 4 };
        private static double Distance(double x, double z, double px, double pz) => Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
        private static bool Finite(double value) => !Double.IsNaN(value) && !Double.IsInfinity(value);
    }
}
