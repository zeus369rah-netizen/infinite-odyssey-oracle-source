// Abstract recipe pseudocode, before typed syntax:
// 1. Accept a ground-centered world anchor and a value sink; stop if the anchor is non-finite.
// 2. Emit two nested basalt plinth tiers, four narrow edge inlays and four corner studs within a 4.5 m square.
// 3. Lay one dark rune plaque in the open front quadrant, then seven fixed cyan bars as an upward-pointing sigil.
// 4. Emit seven fixed stepped crystal records; each record has four narrowing tiers and one bright face facet.
// 5. Emit eight low root stones, keeping every command inside the 8 x 8 x 4 authority envelope.
// 6. Use only bounded scalar math and the existing Box sink; do not allocate, create materials, or change collision data.

using Unity.Mathematics;

namespace OdysseyOracle
{
    // Cold, value-only shrine geometry. The root anchor is the floor center of layout key 301.
    public static class CampaignShrineRecipe020
    {
        public const int CommandCount = 61;

        static readonly float4 Basalt = new float4(.075f, .095f, .135f, 1f);
        static readonly float4 BasaltEdge = new float4(.16f, .205f, .275f, 1f);
        static readonly float4 DeepCyan = new float4(.015f, .105f, .17f, 1f);
        static readonly float4 BlueCyan = new float4(.025f, .32f, .49f, 1f);
        static readonly float4 CrystalCyan = new float4(.04f, .66f, .91f, 1f);
        static readonly float4 CrystalIce = new float4(.46f, .91f, 1f, 1f);
        static readonly float4 RuneCyan = new float4(.055f, .78f, 1f, 1f);

        public static void Emit<T>(ref T sink, float3 origin)
            where T : struct, ICampaignBuildingSink020
        {
            if (!Finite(origin))
                return;

            Put(ref sink, origin, new float3(0f, .19f, 0f), new float3(4.5f, .38f, 4.5f), Basalt, 0f, 0f);
            Put(ref sink, origin, new float3(0f, .4825f, 0f), new float3(3.9f, .205f, 3.9f), BasaltEdge, 0f, 0f);

            Put(ref sink, origin, new float3(0f, .578f, -1.86f), new float3(3.56f, .046f, .075f), Basalt, 0f, 0f);
            Put(ref sink, origin, new float3(0f, .578f, 1.86f), new float3(3.56f, .046f, .075f), Basalt, 0f, 0f);
            Put(ref sink, origin, new float3(-1.86f, .578f, 0f), new float3(.075f, .046f, 3.56f), Basalt, 0f, 0f);
            Put(ref sink, origin, new float3(1.86f, .578f, 0f), new float3(.075f, .046f, 3.56f), Basalt, 0f, 0f);

            Put(ref sink, origin, new float3(-1.62f, .61f, -1.62f), new float3(.34f, .08f, .34f), BasaltEdge, 0f, .18f);
            Put(ref sink, origin, new float3(1.62f, .61f, -1.62f), new float3(.34f, .08f, .34f), BasaltEdge, 0f, -.18f);
            Put(ref sink, origin, new float3(-1.62f, .61f, 1.62f), new float3(.34f, .08f, .34f), BasaltEdge, 0f, -.18f);
            Put(ref sink, origin, new float3(1.62f, .61f, 1.62f), new float3(.34f, .08f, .34f), BasaltEdge, 0f, .18f);

            Put(ref sink, origin, new float3(0f, .584f, -1.27f), new float3(1.58f, .046f, .88f), Basalt, 0f, 0f);
            Put(ref sink, origin, new float3(-.22f, .616f, -1.10f), new float3(.43f, .034f, .065f), RuneCyan, 1.1f, .7853982f);
            Put(ref sink, origin, new float3(.22f, .616f, -1.10f), new float3(.43f, .034f, .065f), RuneCyan, 1.1f, -.7853982f);
            Put(ref sink, origin, new float3(-.22f, .616f, -1.44f), new float3(.43f, .034f, .065f), RuneCyan, 1.1f, -.7853982f);
            Put(ref sink, origin, new float3(.22f, .616f, -1.44f), new float3(.43f, .034f, .065f), RuneCyan, 1.1f, .7853982f);
            Put(ref sink, origin, new float3(0f, .616f, -1.27f), new float3(.075f, .034f, .54f), CrystalIce, 1.25f, 0f);
            Put(ref sink, origin, new float3(0f, .616f, -1.12f), new float3(.34f, .034f, .07f), CrystalIce, 1.25f, 0f);
            Put(ref sink, origin, new float3(0f, .616f, -1.42f), new float3(.34f, .034f, .07f), CrystalIce, 1.25f, 0f);

            Crystal(ref sink, origin, new float3(0f, .585f, .18f), .92f, 3.19f, .78f, .08f, 0);
            Crystal(ref sink, origin, new float3(-.83f, .585f, .20f), .72f, 2.22f, .70f, -.31f, 1);
            Crystal(ref sink, origin, new float3(.83f, .585f, .22f), .74f, 2.42f, .68f, .29f, 2);
            Crystal(ref sink, origin, new float3(-.48f, .585f, .91f), .66f, 1.98f, .63f, .49f, 3);
            Crystal(ref sink, origin, new float3(.49f, .585f, .92f), .69f, 2.18f, .66f, -.53f, 1);
            Crystal(ref sink, origin, new float3(-1.22f, .585f, -.48f), .56f, 1.55f, .54f, -.23f, 2);
            Crystal(ref sink, origin, new float3(1.21f, .585f, -.47f), .57f, 1.78f, .56f, .21f, 3);

            Put(ref sink, origin, new float3(-1.57f, .70f, -.08f), new float3(.36f, .28f, .40f), DeepCyan, .12f, .16f);
            Put(ref sink, origin, new float3(-1.50f, .68f, .48f), new float3(.34f, .24f, .38f), BasaltEdge, 0f, -.24f);
            Put(ref sink, origin, new float3(-.98f, .69f, 1.53f), new float3(.38f, .25f, .34f), DeepCyan, .12f, .34f);
            Put(ref sink, origin, new float3(-.34f, .68f, 1.61f), new float3(.40f, .22f, .31f), BasaltEdge, 0f, -.16f);
            Put(ref sink, origin, new float3(1.56f, .70f, -.08f), new float3(.36f, .28f, .40f), DeepCyan, .12f, -.16f);
            Put(ref sink, origin, new float3(1.49f, .68f, .48f), new float3(.34f, .24f, .38f), BasaltEdge, 0f, .24f);
            Put(ref sink, origin, new float3(.98f, .69f, 1.53f), new float3(.38f, .25f, .34f), DeepCyan, .12f, -.34f);
            Put(ref sink, origin, new float3(.34f, .68f, 1.61f), new float3(.40f, .22f, .31f), BasaltEdge, 0f, .16f);
        }

        static void Crystal<T>(ref T sink, float3 origin, float3 root, float width, float height, float depth, float yaw, int tint)
            where T : struct, ICampaignBuildingSink020
        {
            if (!Finite(root) || !math.isfinite(width) || !math.isfinite(height) || !math.isfinite(depth) ||
                width <= 0f || height <= 0f || depth <= 0f || !math.isfinite(yaw) || tint < 0 || tint > 3)
                return;

            float4 lower = DeepCyan;
            float4 body = BlueCyan;
            float4 upper = CrystalCyan;
            float4 tip = CrystalIce;
            if (tint == 1)
            {
                lower = BasaltEdge;
                body = BlueCyan;
                upper = CrystalIce;
                tip = CrystalCyan;
            }
            else if (tint == 2)
            {
                lower = DeepCyan;
                body = CrystalCyan;
                upper = BlueCyan;
                tip = CrystalIce;
            }
            else if (tint == 3)
            {
                lower = Basalt;
                body = BlueCyan;
                upper = CrystalIce;
                tip = RuneCyan;
            }

            Put(ref sink, origin, new float3(root.x, root.y + height * .18f, root.z),
                new float3(width * 1.08f, height * .36f, depth * 1.08f), lower, .18f, yaw);
            Put(ref sink, origin, new float3(root.x, root.y + height * .47f, root.z),
                new float3(width * .84f, height * .32f, depth * .84f), body, .34f, yaw + .18f);
            Put(ref sink, origin, new float3(root.x, root.y + height * .74f, root.z),
                new float3(width * .60f, height * .24f, depth * .60f), upper, .62f, yaw - .16f);
            Put(ref sink, origin, new float3(root.x, root.y + height * .93f, root.z),
                new float3(width * .31f, height * .14f, depth * .32f), tip, .82f, yaw + .22f);

            quaternion facing = quaternion.RotateY(yaw);
            float3 facetOffset = math.rotate(facing, new float3(width * .25f, 0f, -depth * .34f));
            float3 facetCenter = new float3(root.x + facetOffset.x, root.y + height * .49f, root.z + facetOffset.z);
            Put(ref sink, origin, facetCenter, new float3(width * .13f, height * .43f, .045f), CrystalIce, 1.05f, yaw);
        }

        static void Put<T>(ref T sink, float3 origin, float3 localCenter, float3 size, float4 color, float emission, float yaw)
            where T : struct, ICampaignBuildingSink020
        {
            if (!Finite(localCenter) || !Positive(size) || !Finite(color) || !math.isfinite(emission) || emission < 0f ||
                !math.isfinite(yaw))
                return;

            float3 worldCenter = origin + localCenter;
            if (!Finite(worldCenter))
                return;

            sink.Box(worldCenter, size, quaternion.RotateY(yaw), color, emission);
        }

        static bool Positive(float3 value)
        {
            return Finite(value) && value.x > 0f && value.y > 0f && value.z > 0f;
        }

        static bool Finite(float3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        static bool Finite(float4 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z) && math.isfinite(value.w);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Key 301 ground-center anchor, Unity metres/Y-up; all 61 visual commands remain inside the existing 8x8x4 shrine envelope.
// Array Formats: Generic struct sink streams fixed scalar float3/float4/quaternion box values; no arrays, collections, jobs, or managed allocations are created here.
// Dependencies/Pointers: Unity.Mathematics and ICampaignBuildingSink020 only; the existing portal recipe owns native capacity/overflow, mesh/material processing, physical collision, interaction and positive-emission rendering.