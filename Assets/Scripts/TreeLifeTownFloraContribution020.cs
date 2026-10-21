using Unity.Collections;
using Unity.Mathematics;

namespace OdysseyOracle
{
    // Work-only mesh contribution. The caller owns bounded NativeArray scratch and the completed LOD upload.
    public static class TreeLifeTownFloraContribution020
    {
        const float TwoPi = math.PI * 2f;
        const float FlowerWindMask = .10f;
        const float BrushWindMask = .05f;

        public static int AddedQuads(int lod) => lod == 0 ? 168 : lod == 1 ? 84 : lod == 2 ? 40 : 0;

        public static float4 CanopyColor(float angle, int tier)
        {
            int sector = Sector(angle);
            int shade = tier % 3;
            if (shade < 0) shade += 3;
            switch (sector)
            {
                case 0: return shade == 0 ? new float4(.06f,.30f,.35f,1) : shade == 1 ? new float4(.09f,.47f,.52f,1) : new float4(.25f,.66f,.68f,1);
                case 1: return shade == 0 ? new float4(.12f,.29f,.08f,1) : shade == 1 ? new float4(.22f,.44f,.13f,1) : new float4(.35f,.55f,.19f,1);
                case 2: return shade == 0 ? new float4(.58f,.50f,.68f,1) : shade == 1 ? new float4(.78f,.72f,.88f,1) : new float4(.92f,.88f,.96f,1);
                default:return shade == 0 ? new float4(.36f,.045f,.09f,1) : shade == 1 ? new float4(.62f,.10f,.18f,1) : new float4(.84f,.27f,.34f,1);
            }
        }

        static int Sector(float angle)
        {
            int value = (int)math.floor((angle + math.PI * .25f) / (math.PI * .5f));
            return value & 3;
        }

        static float4 FlowerColor(int sector, int shade)
        {
            shade = shade % 3;
            if (shade < 0) shade += 3;
            switch (sector & 3)
            {
                case 0: return shade == 0 ? new float4(.24f,.67f,.68f,1) : shade == 1 ? new float4(.36f,.80f,.78f,1) : new float4(.58f,.91f,.87f,1);
                case 1: return shade == 0 ? new float4(.48f,.68f,.19f,1) : shade == 1 ? new float4(.70f,.84f,.31f,1) : new float4(.86f,.93f,.52f,1);
                case 2: return shade == 0 ? new float4(.62f,.53f,.77f,1) : shade == 1 ? new float4(.82f,.75f,.94f,1) : new float4(.94f,.90f,.99f,1);
                default:return shade == 0 ? new float4(.58f,.07f,.13f,1) : shade == 1 ? new float4(.82f,.19f,.25f,1) : new float4(.97f,.39f,.42f,1);
            }
        }

        static float4 StoneColor(int index, uint seed)
        {
            uint value = Mix(unchecked(seed + (uint)index * 747796405u));
            switch (value % 3u)
            {
                case 0: return new float4(.23f,.24f,.22f,1);
                case 1: return new float4(.31f,.29f,.25f,1);
                default:return new float4(.19f,.22f,.20f,1);
            }
        }

        static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }

        static float Random01(uint seed, int slot)
        {
            uint value = Mix(unchecked(seed + (uint)slot * 747796405u));
            return ((value >> 8) & 65535u) / 65535f;
        }

        static float LocalMeters(float meters) => meters / TreeLifeRecipe020.Scale;

        // Endpoint keys wrap before angle and height calculation, making adjacent segments share bit-identical seams.
        static float EndpointAngle(int endpoint, int segments) => endpoint == 0 ? 0f : TwoPi * (endpoint / (float)segments);
        static float InnerEndpointY(uint seed, int endpoint) => LocalMeters(.17f) + (Random01(seed, 1000 + endpoint) - .5f) * LocalMeters(.01f);
        static float OuterEndpointY(uint seed, int endpoint) => LocalMeters(.05f) + (Random01(seed, 2000 + endpoint) - .5f) * LocalMeters(.005f);

        static void Vertex(NativeArray<TreeLifeVertex020> vertices, ref int cursor, float3 position, float3 normal,
            float4 color, float2 uv, float3 anchor, float mask)
        {
            float scale = TreeLifeRecipe020.Scale;
            vertices[cursor++] = new TreeLifeVertex020
            {
                Position = position * scale,
                Normal = normal,
                Color = color,
                UV = uv,
                Anchor = new float4(anchor * scale, mask)
            };
        }

        static void Quad(NativeArray<TreeLifeVertex020> vertices, NativeArray<int> indices, ref int vertex, ref int index,
            float3 p0, float3 p1, float3 p2, float3 p3, float3 normal, float4 color,
            float3 anchor, float mask0, float mask1, float2 uv0, float2 uv1, float2 uv2, float2 uv3)
        {
            int start = vertex;
            Vertex(vertices, ref vertex, p0, normal, color, uv0, anchor, mask0);
            Vertex(vertices, ref vertex, p1, normal, color, uv1, anchor, mask0);
            Vertex(vertices, ref vertex, p2, normal, color, uv2, anchor, mask1);
            Vertex(vertices, ref vertex, p3, normal, color, uv3, anchor, mask1);
            indices[index++] = start;
            indices[index++] = start + 1;
            indices[index++] = start + 2;
            indices[index++] = start;
            indices[index++] = start + 2;
            indices[index++] = start + 3;
        }

        static void StaticStoneQuad(NativeArray<TreeLifeVertex020> vertices, NativeArray<int> indices, ref int vertex, ref int index,
            float3 p0, float3 p1, float3 p2, float3 p3, float3 normal, float4 color)
        {
            float2 opaque = new float2(-1, 0);
            Quad(vertices, indices, ref vertex, ref index, p0, p1, p2, p3, normal, color, float3.zero, 0, 0,
                opaque, opaque, opaque, opaque);
        }

        static void LeafBlade(NativeArray<TreeLifeVertex020> vertices, NativeArray<int> indices, ref int vertex, ref int index,
            float3 basePoint, float yaw, float width, float height, float4 color, float windMask)
        {
            float3 right = new float3(math.cos(yaw), 0, -math.sin(yaw));
            float3 up = new float3(0, 1, 0);
            float3 normal = math.cross(right, up);
            float3 anchor = basePoint;
            float3 p0 = basePoint - right * (width * .5f);
            float3 p1 = basePoint + right * (width * .5f);
            float3 p2 = basePoint + right * (width * .12f) + up * height;
            float3 p3 = basePoint - right * (width * .12f) + up * height;
            Quad(vertices, indices, ref vertex, ref index, p0, p1, p2, p3, normal, color, anchor, 0, windMask,
                new float2(0,0), new float2(1,0), new float2(1,1), new float2(0,1));
        }

        public static bool Append(int lod, uint seed, NativeArray<TreeLifeVertex020> vertices, NativeArray<int> indices,
            ref int vertex, ref int index)
        {
            if (lod < 0 || lod > 2 || !vertices.IsCreated || !indices.IsCreated) return false;

            int segments = lod == 0 ? 32 : lod == 1 ? 16 : 8;
            int flowers = lod == 0 ? 24 : lod == 1 ? 12 : 8;
            int petalsPerFlower = lod == 2 ? 1 : 2;
            int cardsPerBrush = lod == 0 ? 6 : lod == 1 ? 3 : 2;
            int addedQuads = AddedQuads(lod);
            if (addedQuads == 0 || vertex < 0 || index < 0 ||
                vertices.Length - vertex < addedQuads * 4 || indices.Length - index < addedQuads * 6)
                return false;

            int vertexStart = vertex;
            int indexStart = index;
            float inner = LocalMeters(2.15f);
            float outer = LocalMeters(2.35f);
            float bottom = 0f; // Local grade: both skirts meet the supporting plane.
            float innerHeight = LocalMeters(.17f);
            float outerHeight = LocalMeters(.05f);

            for (int segment = 0; segment < segments; segment++)
            {
                int endpoint0 = segment;
                int endpoint1 = (segment + 1) % segments;
                float a0 = EndpointAngle(endpoint0, segments);
                float a1 = EndpointAngle(endpoint1, segments);
                float middle = TwoPi * ((segment + .5f) / segments);
                float3 in0 = new float3(math.cos(a0) * inner, InnerEndpointY(seed, endpoint0), math.sin(a0) * inner);
                float3 in1 = new float3(math.cos(a1) * inner, InnerEndpointY(seed, endpoint1), math.sin(a1) * inner);
                float3 out0 = new float3(math.cos(a0) * outer, OuterEndpointY(seed, endpoint0), math.sin(a0) * outer);
                float3 out1 = new float3(math.cos(a1) * outer, OuterEndpointY(seed, endpoint1), math.sin(a1) * outer);
                float slope = (outerHeight - innerHeight) / (outer - inner);
                float3 radial = new float3(math.cos(middle), 0, math.sin(middle));
                float3 topNormal = math.normalize(new float3(-slope * radial.x, 1, -slope * radial.z));
                float4 stone = StoneColor(segment, seed);

                // The sloped top joins two endpoint-keyed heights, so adjacent segments share exact ring vertices.
                StaticStoneQuad(vertices, indices, ref vertex, ref index, in0, in1, out1, out0, topNormal, stone);
                float3 outerBottom0 = new float3(out0.x, bottom, out0.z);
                float3 outerBottom1 = new float3(out1.x, bottom, out1.z);
                StaticStoneQuad(vertices, indices, ref vertex, ref index, out0, out1, outerBottom1, outerBottom0, radial, stone);
                float3 innerBottom0 = new float3(in0.x, bottom, in0.z);
                float3 innerBottom1 = new float3(in1.x, bottom, in1.z);
                // Reversed winding gives the inner skirt an inward-facing normal and closes the former hover gap.
                StaticStoneQuad(vertices, indices, ref vertex, ref index, in0, innerBottom0, innerBottom1, in1, -radial, stone);
            }

            int flowerStride = 24 / flowers;
            for (int flower = 0; flower < flowers; flower++)
            {
                int stableSlot = flower * flowerStride;
                float angle = (stableSlot + .5f) * (TwoPi / 24f);
                float radius = LocalMeters(2.52f + (Random01(seed, 3000 + stableSlot) - .5f) * .08f);
                float3 basePoint = new float3(math.cos(angle) * radius, LocalMeters(.02f), math.sin(angle) * radius);
                float4 flowerColor = FlowerColor(Sector(angle), stableSlot);
                for (int petal = 0; petal < petalsPerFlower; petal++)
                    LeafBlade(vertices, indices, ref vertex, ref index, basePoint, angle + petal * (math.PI / 3f),
                        LocalMeters(.14f), LocalMeters(.08f), flowerColor, FlowerWindMask);
            }

            for (int sector = 0; sector < 4; sector++)
            {
                float angle = sector * (math.PI * .5f);
                float3 center = new float3(math.cos(angle) * LocalMeters(2.82f), LocalMeters(.01f), math.sin(angle) * LocalMeters(2.82f));
                int cardStride = 6 / cardsPerBrush;
                for (int card = 0; card < cardsPerBrush; card++)
                {
                    int stableCard = card * cardStride;
                    float yaw = angle + stableCard * (math.PI / 6f);
                    LeafBlade(vertices, indices, ref vertex, ref index, center, yaw,
                        LocalMeters(.12f), LocalMeters(.07f), CanopyColor(angle, sector + stableCard), BrushWindMask);
                }
            }

            return vertex - vertexStart == addedQuads * 4 && index - indexStart == addedQuads * 6;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Tree-root local meters encoded through the existing single Scale=3 conversion; trunk/root/canopy source and accepted world anchor remain unchanged.
// Array Formats: 64B TreeLifeVertex020 records and fixed native vertex/index arrays; one deterministic static contribution adds 168/84/40 quads for LOD0/1/2 with preflight capacity checks and no managed allocation.
// Dependencies/Pointers: Called once inside the cold TreeLifeMeshJob before Written is published; existing shader handles negative-UV opaque stone and low-mask leaf wind. No new GameObjects, renderer, material, collider, draw call, or runtime clock owner.
