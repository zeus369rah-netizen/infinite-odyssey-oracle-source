using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    /// <summary>Editor-only deterministic checks. Run explicitly; never run when importing.</summary>
    public static class SpatialTargetGridChecks
    {
        [Serializable]
        private sealed class Report
        {
            public string status = "passed";
            public int targetCount = 240;
            public int bruteForceComparisons;
            public string[] cases = { "negative-floor-cells", "inclusive-radius", "xz-distance",
                "signed-stable-ids", "deterministic-ties", "duplicate-update", "capacity-growth",
                "240-target-brute-force", "reverse-registration", "empty-grid", "huge-radius-fallback",
                "int-cell-boundaries", "invalid-input", "idempotent-disposal", "disposed-access" };
            public string execution = "Synchronous Editor checks; no background-thread or runtime-performance claim.";
        }

        [MenuItem("Odyssey/Check spatial target grid")]
        private static void RunFromMenu() { Debug.Log("ODYSSEY_SPATIAL_GRID_CHECKS " + Run()); }

        public static string Run()
        {
            var report = new Report();
            using (var grid = new SpatialTargetGrid(2, 2f))
            {
                Require(grid.GetCell(new Vector3(-.001f, 900f, -2.001f)).Equals(new int2(-1, -2)), "Negative cells use floor.");
                Require(grid.GetCell(new Vector3(-2f, 0f, 2f)).Equals(new int2(-1, 1)), "Exact cell boundaries.");
                Require(grid.GetCell(new Vector3(-float.Epsilon, 0f, float.Epsilon)).Equals(new int2(-1, 0)), "Negative subnormal cell.");
                Require(!grid.TryFindNearest(Vector3.zero, 100f, out int emptyId, out Vector3 emptyPosition)
                    && emptyId == -1 && emptyPosition == Vector3.zero, "Empty grid outputs.");

                grid.Add(7919, new Vector3(3f, 500f, 4f));
                Require(grid.TryFindNearest(Vector3.zero, 5f, out int id, out Vector3 position)
                    && id == 7919 && position.y == 500f, "Radius boundary is inclusive and Y is ignored.");
                Require(!grid.TryFindNearest(Vector3.zero, 4.99999f, out _, out _), "Outside radius rejected.");
                Require(grid.TryFindNearest(new Vector3(3f, -900f, 4f), 0f, out id, out _) && id == 7919, "Zero radius XZ match.");
                grid.Clear();
                grid.Add(88, new Vector3(-1f, 0f, 0f));
                grid.Add(-22, new Vector3(1f, 500f, 0f));
                Require(grid.TryFindNearest(Vector3.zero, 1f, out id, out _) && id == -22, "Tie uses lower signed stable ID.");
                grid.Add(-22, new Vector3(21f, 20f, 0f));
                Require(grid.Count == 2 && grid.TryFindNearest(Vector3.zero, 1f, out id, out _) && id == 88, "Update removes old cell entry.");
                Require(grid.TryFindNearest(new Vector3(21f, -50f, 0f), 0f, out id, out _) && id == -22, "Update preserves ID in new cell.");
                grid.Add(-22, new Vector3(21.25f, 40f, .25f));
                Require(grid.Count == 2 && grid.TryFindNearest(new Vector3(21.25f, 0f, .25f), 0f, out id, out position)
                    && id == -22 && position.y == 40f, "Same-cell update retains one entry and latest position.");

                var positions = new Vector3[240];
                var ids = new int[240];
                var random = new System.Random(671904);
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = new Vector3((float)(random.NextDouble() * 180d - 90d),
                        (float)(random.NextDouble() * 1000d - 500d), (float)(random.NextDouble() * 180d - 90d));
                    ids[i] = i % 2 == 0 ? 10000 + i * 37 : -10000 - i * 37;
                }
                grid.Clear();
                for (int i = 0; i < positions.Length; i++) grid.Add(ids[i], positions[i]);
                Require(grid.Count == 240 && grid.Capacity >= 240, "Persistent capacity grows to fit targets.");
                for (int pass = 0; pass < 2; pass++)
                {
                    random = new System.Random(3917);
                    for (int q = 0; q < 192; q++)
                    {
                        Vector3 query = q < positions.Length / 4 ? positions[q] : new Vector3(
                            (float)(random.NextDouble() * 240d - 120d), 3000f,
                            (float)(random.NextDouble() * 240d - 120d));
                        float radius = q % 11 == 0 ? 0f : q % 17 == 0 ? 10000f : .1f + (float)random.NextDouble() * 35f;
                        CompareWithBruteForce(grid, positions, ids, query, radius);
                        report.bruteForceComparisons++;
                    }
                    if (pass == 0)
                    {
                        int capacity = grid.Capacity;
                        grid.Clear();
                        Require(grid.Capacity == capacity, "Clear retains allocated capacity.");
                        for (int i = positions.Length - 1; i >= 0; i--) grid.Add(ids[i], positions[i]);
                    }
                }
                CompareWithBruteForce(grid, positions, ids, Vector3.zero, float.MaxValue);
                report.bruteForceComparisons++;

                Throws<ArgumentException>(() => grid.Add(3, new Vector3(float.NaN, 0f, 0f)), "NaN target rejected.");
                Throws<ArgumentOutOfRangeException>(() => grid.GetCell(new Vector3(float.MaxValue, 0f, 0f)), "Unrepresentable cell rejected.");
                Throws<ArgumentOutOfRangeException>(() => grid.TryFindNearest(Vector3.zero, -1f, out _, out _), "Negative radius rejected.");
                Throws<ArgumentOutOfRangeException>(() => grid.TryFindNearest(Vector3.zero, float.NaN, out _, out _), "NaN radius rejected.");
                grid.Clear();
                Require(grid.Count == 0 && !grid.TryFindNearest(Vector3.zero, 5f, out _, out _), "Cleared grid is empty.");
            }

            using (var edge = new SpatialTargetGrid(1, 1f))
            {
                edge.Add(int.MinValue, new Vector3(int.MinValue, 0f, 0f));
                Require(edge.TryFindNearest(new Vector3(int.MinValue, 300f, 0f), 0f, out int id, out _) && id == int.MinValue,
                    "Minimum signed cell and stable ID.");
                edge.Add(int.MaxValue, new Vector3(2147483520f, 0f, 0f));
                Require(edge.TryFindNearest(new Vector3(2147483520f, 0f, 0f), 256f, out id, out _) && id == int.MaxValue,
                    "Maximum representable float cell bounds do not overflow query loops.");
            }
            var disposed = new SpatialTargetGrid(1);
            disposed.Dispose();
            disposed.Dispose();
            Require(!disposed.IsCreated, "Dispose is idempotent and releases containers.");
            Throws<ObjectDisposedException>(() => disposed.Clear(), "Disposed clear rejected.");
            Throws<ObjectDisposedException>(() => disposed.Add(1, Vector3.zero), "Disposed add rejected.");
            Throws<ObjectDisposedException>(() => disposed.TryFindNearest(Vector3.zero, 1f, out _, out _), "Disposed query rejected.");
            Throws<ObjectDisposedException>(() => disposed.GetCell(Vector3.zero), "Disposed cell access rejected.");
            Throws<ArgumentOutOfRangeException>(() => { using (new SpatialTargetGrid(0)) { } }, "Zero capacity rejected.");
            Throws<ArgumentOutOfRangeException>(() => { using (new SpatialTargetGrid(1048577)) { } }, "Excess capacity rejected.");
            Throws<ArgumentOutOfRangeException>(() => { using (new SpatialTargetGrid(1, 0f)) { } }, "Zero cell size rejected.");
            Throws<ArgumentOutOfRangeException>(() => { using (new SpatialTargetGrid(1, float.PositiveInfinity)) { } }, "Infinite cell size rejected.");
            return JsonUtility.ToJson(report);
        }

        private static void CompareWithBruteForce(SpatialTargetGrid grid, Vector3[] positions, int[] ids, Vector3 origin, float radius)
        {
            int best = -1;
            double nearest = (double)radius * radius;
            for (int i = 0; i < positions.Length; i++)
            {
                double dx = (double)positions[i].x - origin.x;
                double dz = (double)positions[i].z - origin.z;
                double squared = dx * dx + dz * dz;
                if (squared > nearest || (best >= 0 && squared == nearest && ids[i] >= ids[best])) continue;
                nearest = squared;
                best = i;
            }
            bool found = grid.TryFindNearest(origin, radius, out int id, out Vector3 position);
            Require(found == (best >= 0), "Grid and brute-force found flags agree.");
            if (best >= 0)
                Require(id == ids[best] && position.x == positions[best].x && position.y == positions[best].y
                    && position.z == positions[best].z, "Grid and brute-force stable target/position agree.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("SpatialTargetGrid check failed: " + message);
        }

        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("SpatialTargetGrid check failed: " + message);
        }
    }
}
