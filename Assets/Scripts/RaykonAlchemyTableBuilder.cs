using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle.VisualDressing
{
    // Original procedural creation for ZeusRa/The Oracle's Return,2026. No external art input.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct AlchemyBox32
    {
        public int3 MinimumMillimetres, MaximumMillimetres;
        public uint ColorId;
        public float Glow;
    }

    public static class RaykonAlchemyTableRecipe
    {
        public const int BottleCount = 6, CrystalCount = 3, BoxCount = 25;
        public const int VertexCount = BoxCount * 24 + CrystalCount * 34;
        public const int IndexCount = BoxCount * 36 + CrystalCount * 42;
        public static readonly float3 Maximum = new float3(750, 1290, 350)*.001f;
        public static readonly float3 Minimum = new float3(-750, 0, -350)*.001f;
        public static AlchemyBox32 Box(int index)
        {
            if (index == 0) return Part(new int3(-750, 860, -350), new int3(750, 940, 350), 6);
            if (index < 5)
            {
                int leg = index - 1, x = (leg & 1) == 0 ? -620 : 620, z = (leg & 2) == 0 ? -230 : 230;
                return Part(new int3(x - 55, 0, z - 55), new int3(x + 55, 860, z + 55), 7);
            }
            if (index < 7)
            {
                int z = index == 5 ? -300 : 300;
                return Part(new int3(-690, 760, z - 30), new int3(690, 860, z + 30), 7);
            }
            int bottle = (index - 7) / 3, part = (index - 7) % 3;
            int cx = -500 + (bottle % 3) * 320, cz = bottle < 3 ? -175 : 175, top = 1100 + (bottle % 3) * 20;
            if (part == 0) return Part(new int3(cx - 70, 940, cz - 70), new int3(cx + 70, top, cz + 70), (uint)bottle, .14f);
            if (part == 1) return Part(new int3(cx - 30, top, cz - 30), new int3(cx + 30, top + 80, cz + 30), (uint)bottle, .06f);
            return Part(new int3(cx - 40, top + 80, cz - 40), new int3(cx + 40, top + 110, cz + 40), 8);
        }
        public static int3 CrystalPoint(int crystal, int vertex)
        {
            int z = -210 + crystal * 210, shoulder = 1070 + crystal * 35;
            if (vertex == 8) return new int3(500, shoulder + 150, z);
            int ring = vertex / 4, side = vertex % 4;
            int dx = side == 0 ? -65 : side == 2 ? 65 : 0;
            int dz = side == 1 ? 65 : side == 3 ? -65 : 0;
            return new int3(500 + dx, ring == 0 ? 940 : shoulder, z + dz);
        }
        private static AlchemyBox32 Part(int3 a, int3 b, uint color, float glow = 0) =>
            new AlchemyBox32 { MinimumMillimetres = a, MaximumMillimetres = b, ColorId = color, Glow = glow };
    }

    public static class RaykonAlchemyTableBuilder
    {
        public static Mesh Build()
        {
            var vertices = new List<Vector3>(RaykonAlchemyTableRecipe.VertexCount);
            var normals = new List<Vector3>(RaykonAlchemyTableRecipe.VertexCount);
            var colors = new List<Color>(RaykonAlchemyTableRecipe.VertexCount);
            var uv = new List<Vector2>(RaykonAlchemyTableRecipe.VertexCount);
            var indices = new List<int>(RaykonAlchemyTableRecipe.IndexCount);
            for (int i = 0; i < RaykonAlchemyTableRecipe.BoxCount; ++i)
            {
                AlchemyBox32 box = RaykonAlchemyTableRecipe.Box(i);
                Vector3 a = V(box.MinimumMillimetres), b = V(box.MaximumMillimetres); Color color = Palette(box.ColorId);
                Quad(new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,a.y,a.z),color,box.Glow);
                Quad(new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,a.y,b.z),color,box.Glow);
                Quad(new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z),new Vector3(a.x,a.y,a.z),color,box.Glow);
                Quad(new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,a.y,b.z),color,box.Glow);
                Quad(new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z),color,box.Glow);
                Quad(new Vector3(a.x,a.y,b.z),new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),color,box.Glow);
            }
            for (int crystal = 0; crystal < RaykonAlchemyTableRecipe.CrystalCount; ++crystal)
            {
                Color color = Palette((uint)(crystal * 2));
                for (int side = 0; side < 4; ++side)
                {
                    int next = (side + 1) % 4;
                    Vector3 a = V(RaykonAlchemyTableRecipe.CrystalPoint(crystal,side));
                    Vector3 b = V(RaykonAlchemyTableRecipe.CrystalPoint(crystal,next));
                    Vector3 upperA = V(RaykonAlchemyTableRecipe.CrystalPoint(crystal,side + 4));
                    Vector3 upperB = V(RaykonAlchemyTableRecipe.CrystalPoint(crystal,next + 4));
                    Quad(a,b,upperB,upperA,color,.4f);
                    Tri(upperA,upperB,V(RaykonAlchemyTableRecipe.CrystalPoint(crystal,8)),color,.65f);
                }
                Vector3 p0=V(RaykonAlchemyTableRecipe.CrystalPoint(crystal,0)),p1=V(RaykonAlchemyTableRecipe.CrystalPoint(crystal,1));
                Vector3 p2=V(RaykonAlchemyTableRecipe.CrystalPoint(crystal,2)),p3=V(RaykonAlchemyTableRecipe.CrystalPoint(crystal,3));
                Tri(p0,p2,p1,color,.2f); Tri(p0,p3,p2,color,.2f);
            }
            if (vertices.Count != RaykonAlchemyTableRecipe.VertexCount || indices.Count != RaykonAlchemyTableRecipe.IndexCount)
                throw new InvalidOperationException("Alchemy mesh recipe count changed.");
            var mesh = new Mesh { name = "Original Raykon alchemy table six potions three crystals" };
            try { mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0,uv); mesh.SetTriangles(indices,0); mesh.RecalculateBounds(); return mesh; }
            catch { if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh); throw; }

            void Tri(Vector3 a,Vector3 b,Vector3 c,Color color,float glow)
            {
                int at=vertices.Count;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                for(int i=0;i<3;++i){normals.Add(normal);colors.Add(color);uv.Add(new Vector2(glow,0));}
                indices.Add(at);indices.Add(at+1);indices.Add(at+2);
            }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float glow)
            {
                int at=vertices.Count;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                for(int i=0;i<4;++i){normals.Add(normal);colors.Add(color);uv.Add(new Vector2(glow,0));}
                indices.Add(at);indices.Add(at+1);indices.Add(at+2);indices.Add(at);indices.Add(at+2);indices.Add(at+3);
            }
        }
        private static Vector3 V(int3 p) => new Vector3(p.x*.001f,p.y*.001f,p.z*.001f);
        private static Color Palette(uint id)
        {
            switch(id)
            {
                case 0:return new Color(.88f,.16f,.30f);case 1:return new Color(.12f,.75f,.56f);
                case 2:return new Color(.85f,.61f,.12f);case 3:return new Color(.60f,.24f,.88f);
                case 4:return new Color(.18f,.49f,.95f);case 5:return new Color(.42f,.80f,.17f);
                case 6:return new Color(.30f,.16f,.09f);case 7:return new Color(.19f,.095f,.05f);
                default:return new Color(.54f,.37f,.18f);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original table-local Y-up metres from shared integer millimetre vertices;
// floor leg bottomsY0/tabletop and bottle/crystal basesY940mm exactly share the same formula.
// Array Formats: AlchemyBox32 data,25boxes+3crystals,702vertex/1026index records; fixed6bottles,
//3crystals,one combined mesh/COLOR+UV0 glow/effect0. No skins or individual prop objects.
// Dependencies/Pointers: Pure numeric recipe precedes cold Mesh binding; caller owns returned
// mesh/resources. No repeated runtime allocations, external assets, lights or NPC/quest authority.
