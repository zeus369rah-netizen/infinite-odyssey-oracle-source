using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using OdysseyOracle.Foundation.World;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        // Cold only. Book is behind the second existing desk; the twig is beside
        // the first desk, and its destination is outside the0.92m hearth envelope.
        public bool TryGetRefugeDiscoveryFloors(out Vector3 hiddenBook, out Vector3 twigPickup, out Vector3 twigHearth)
        {
            hiddenBook = twigPickup = twigHearth = default;
            if (tutorialRefuge == null) return false;
            NativeArray<float4> rectangles = default;
            NativeArray<RefugeFloorQuery32> queries = default;
            NativeArray<ulong> occupancy = default;
            NativeArray<RefugeFloorResult32> results = default;
            JobHandle fence = default;
            try
            {
                rectangles = new NativeArray<float4>(tutorialRefuge.Rooms.Length, Allocator.TempJob);
                for (int i = 0; i < rectangles.Length; ++i)
                {
                    var r = tutorialRefuge.Rooms[i]; rectangles[i] = new float4(r.MinU, r.MinV, r.MaxU, r.MaxV);
                }
                queries = new NativeArray<RefugeFloorQuery32>(3, Allocator.TempJob);
                queries[0] = FloorQuery(-3.2f, 8.35f, .42f, 0);
                queries[1] = FloorQuery(-3.2f, 3.15f, .23f, 1);
                queries[2] = FloorQuery(-5.2f, 5.25f, .23f, 2);
                fence = RefugeFloorSupportCold.Schedule(rectangles, queries, Allocator.TempJob, out _, out occupancy, out results);
                fence.Complete();
                if (results[0].Status != 1 || results[0].QueryId != 0 || results[1].Status != 1 ||
                    results[1].QueryId != 1 || results[2].Status != 1 || results[2].QueryId != 2) return false;
                hiddenBook = transform.TransformPoint((Vector3)results[0].Position);
                twigPickup = transform.TransformPoint((Vector3)results[1].Position);
                twigHearth = transform.TransformPoint((Vector3)results[2].Position);
                return true;
            }
            finally
            {
                fence.Complete();
                if (results.IsCreated) results.Dispose(); if (occupancy.IsCreated) occupancy.Dispose();
                if (queries.IsCreated) queries.Dispose(); if (rectangles.IsCreated) rectangles.Dispose();
            }
        }
        // Replays the EXISTING pure cold recipe to retain its full accepted scenery
        // reservations. A fresh bare layout alone would permit overlap with new vistas.
        public bool TryGetArenaDiscoveryFloors(Vector3[] floors)
        {
            if (!IsCoopArena || floors == null || floors.Length != 8) return false;
            LaneVignetteLayout layout;
            try { layout = CreateCurrentVignetteLayout(); LaneVignetteRecipes.Build(layout, IgnoreDiscoveryRecipePiece, null, IgnoreDiscoveryTreePlan); }
            catch (InvalidOperationException) { return false; }
            for (int lane = 0; lane < 8; ++lane)
            {
                if (!layout.TryAnchor(lane, false, .6, out VignettePoint p)) return false;
                // The actual continuous arena foundation has topY0 across[-77,77].
                // Layout already admits only inside RingRadius-2.1 with pads/roads/props clear.
                floors[lane] = transform.TransformPoint(new Vector3((float)p.X, 0f, (float)p.Z));
            }
            return true;
        }
        static void IgnoreDiscoveryRecipePiece(LaneVignettePiece piece) { }
        static void IgnoreDiscoveryTreePlan(BlossomTreePlan48 plan) { }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual exact refuge rectangle union and existing arena foundation top; output world metres/Y-up.
// Array Formats: Three cold32B support queries with fenced TempJob arrays; eight cold anchors reuse current pure scenery reservations.
// Dependencies/Pointers: World alone owns geometry; no collider/light/path changes. Actual prop clearance and all12 placement visibility remain root native acceptance gates.
