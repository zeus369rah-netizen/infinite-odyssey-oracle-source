using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class CampaignHubInkMapGraphic020 : MaskableGraphic
    {
        private const float MapInset020 = 8f;
        private Rect[] floorBounds020;
        private Rect mapBounds020;
        private Vector3 shrine020, portal020;
        private uint generation020;
        private bool valid020;

        private static readonly Color Navy020 = new Color32(24, 47, 66, 255);
        private static readonly Color Ink020 = new Color32(25, 51, 70, 245);
        private static readonly Color InkWash020 = new Color32(53, 79, 94, 112);
        private static readonly Color Paper020 = new Color32(220, 205, 164, 255);
        private static readonly Color PaperLight020 = new Color32(235, 222, 186, 255);
        private static readonly Color LowerFloor020 = new Color32(175, 190, 190, 255);
        private static readonly Color MidFloor020 = new Color32(218, 205, 165, 255);
        private static readonly Color UpperFloor020 = new Color32(239, 221, 177, 255);
        private static readonly Color Shrine020 = new Color32(202, 143, 54, 255);
        private static readonly Color Portal020 = new Color32(132, 94, 157, 255);

        public void SetSnapshot020(uint generation, Rect[] floorBounds, Rect bounds, Vector3 shrine, Vector3 portal)
        {
            bool changed = !valid020 || generation020 != generation
                || !object.ReferenceEquals(floorBounds020, floorBounds) || !mapBounds020.Equals(bounds)
                || DistanceSquaredXZ020(shrine020, shrine) > .0004f
                || DistanceSquaredXZ020(portal020, portal) > .0004f;
            if (!changed) return;

            generation020 = generation;
            floorBounds020 = floorBounds;
            mapBounds020 = bounds;
            shrine020 = shrine;
            portal020 = portal;
            valid020 = true;
            SetVerticesDirty();
        }

        public void ClearSnapshot020()
        {
            if (!valid020 && floorBounds020 == null) return;
            valid020 = false;
            generation020 = 0;
            floorBounds020 = null;
            shrine020 = default;
            portal020 = default;
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            valid020 = false;
            generation020 = 0;
            floorBounds020 = null;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            valid020 = false;
            generation020 = 0;
            floorBounds020 = null;
            base.OnDestroy();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!valid020 || generation020 == 0 || floorBounds020 == null
                || floorBounds020.Length != PortalLandingLayout020.SurfaceCount
                || mapBounds020.width <= 0f || mapBounds020.height <= 0f)
                return;

            Rect view = rectTransform.rect;
            if (!TryProjection020(view, mapBounds020, out Rect content, out float scale, out Vector2 offset))
                return;

            AddQuad020(helper, new Vector2(view.xMin, view.yMin), new Vector2(view.xMin, view.yMax),
                new Vector2(view.xMax, view.yMax), new Vector2(view.xMax, view.yMin), Navy020);
            AddQuad020(helper, new Vector2(content.xMin, content.yMin), new Vector2(content.xMin, content.yMax),
                new Vector2(content.xMax, content.yMax), new Vector2(content.xMax, content.yMin), Paper020);
            DrawFrame020(helper, content);

            for (int i = 0; i < PortalLandingLayout020.SurfaceCount; i++)
            {
                if (!PortalLandingLayout020.TrySurface(i, out LandingBox020 surface))
                {
                    helper.Clear();
                    return;
                }
                DrawFloorFill020(helper, surface, view, mapBounds020, content, offset, scale, FloorColor020(surface.MaxY));
            }

            for (int i = 0; i < PortalLandingLayout020.SurfaceCount; i++)
            {
                if (!PortalLandingLayout020.TrySurface(i, out LandingBox020 surface))
                {
                    helper.Clear();
                    return;
                }
                DrawFloorInk020(helper, surface, view, mapBounds020, content, offset, scale);
            }

            if (!TryProjectPoint020(view, mapBounds020, new Vector2(shrine020.x, shrine020.z), out Vector2 shrine)
                || !TryProjectPoint020(view, mapBounds020, new Vector2(portal020.x, portal020.z), out Vector2 portal))
            {
                helper.Clear();
                return;
            }
            DrawShrine020(helper, shrine);
            DrawPortal020(helper, portal);
        }

        internal static bool TryProjectPoint020(Rect view, Rect bounds, Vector2 worldXZ, out Vector2 localPoint)
        {
            localPoint = default;
            if (!IsFinite020(worldXZ.x) || !IsFinite020(worldXZ.y)
                || !TryProjection020(view, bounds, out Rect content, out float scale, out Vector2 offset))
                return false;

            localPoint = new Vector2(
                content.xMin + offset.x + (worldXZ.x - bounds.xMin) * scale,
                content.yMin + offset.y + (worldXZ.y - bounds.yMin) * scale);
            return IsFinite020(localPoint.x) && IsFinite020(localPoint.y);
        }

        private static bool TryProjection020(Rect view, Rect bounds, out Rect content, out float scale, out Vector2 offset)
        {
            content = Rect.MinMaxRect(view.xMin + MapInset020, view.yMin + MapInset020,
                view.xMax - MapInset020, view.yMax - MapInset020);
            scale = 0f;
            offset = default;
            if (!IsFinite020(view.width) || !IsFinite020(view.height)
                || !IsFinite020(bounds.xMin) || !IsFinite020(bounds.yMin)
                || !IsFinite020(bounds.width) || !IsFinite020(bounds.height)
                || content.width <= 0f || content.height <= 0f || bounds.width <= 0f || bounds.height <= 0f)
                return false;

            scale = Mathf.Min(content.width / bounds.width, content.height / bounds.height);
            if (!IsFinite020(scale) || scale <= 0f) return false;
            offset = new Vector2((content.width - bounds.width * scale) * .5f,
                (content.height - bounds.height * scale) * .5f);
            return IsFinite020(offset.x) && IsFinite020(offset.y);
        }

        private static void DrawFloorFill020(VertexHelper helper, LandingBox020 surface, Rect view, Rect bounds,
            Rect content, Vector2 offset, float scale, Color color)
        {
            GetSurfacePoints020(surface, view, bounds, content, offset, scale,
                out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d);
            AddQuad020(helper, a, b, c, d, color);
        }

        private static void DrawFloorInk020(VertexHelper helper, LandingBox020 surface, Rect view, Rect bounds,
            Rect content, Vector2 offset, float scale)
        {
            GetSurfacePoints020(surface, view, bounds, content, offset, scale,
                out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d);
            AddLine020(helper, a, b, Ink020, .8f);
            AddLine020(helper, b, c, Ink020, .8f);
            AddLine020(helper, c, d, Ink020, .8f);
            AddLine020(helper, d, a, Ink020, .8f);

            float halfX = (surface.MaxX - surface.MinX) * .5f;
            float halfZ = (surface.MaxZ - surface.MinZ) * .5f;
            if (halfX < 2.5f || halfZ < 1.5f) return;
            int hatchCount = (int)(surface.Key % 3u);
            float halfHatch = Mathf.Min(.68f, halfX * .32f);
            for (int i = 0; i < hatchCount; i++)
            {
                float fraction = (i + 1f) / (hatchCount + 1f);
                float localZ = -halfZ * .28f + halfZ * .56f * fraction;
                Vector2 h0 = SurfacePoint020(surface, -halfHatch, localZ, view, bounds, content, offset, scale);
                Vector2 h1 = SurfacePoint020(surface, halfHatch, localZ, view, bounds, content, offset, scale);
                AddLine020(helper, h0, h1, InkWash020, .42f);
            }
        }

        private static void GetSurfacePoints020(LandingBox020 surface, Rect view, Rect bounds, Rect content,
            Vector2 offset, float scale, out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d)
        {
            float centerX = (surface.MinX + surface.MaxX) * .5f;
            float centerZ = (surface.MinZ + surface.MaxZ) * .5f;
            float halfX = (surface.MaxX - surface.MinX) * .5f;
            float halfZ = (surface.MaxZ - surface.MinZ) * .5f;
            a = SurfacePoint020(surface, -halfX, -halfZ, view, bounds, content, offset, scale, centerX, centerZ);
            b = SurfacePoint020(surface, -halfX, halfZ, view, bounds, content, offset, scale, centerX, centerZ);
            c = SurfacePoint020(surface, halfX, halfZ, view, bounds, content, offset, scale, centerX, centerZ);
            d = SurfacePoint020(surface, halfX, -halfZ, view, bounds, content, offset, scale, centerX, centerZ);
        }

        private static Vector2 SurfacePoint020(LandingBox020 surface, float localX, float localZ,
            Rect view, Rect bounds, Rect content, Vector2 offset, float scale)
        {
            float centerX = (surface.MinX + surface.MaxX) * .5f;
            float centerZ = (surface.MinZ + surface.MaxZ) * .5f;
            return SurfacePoint020(surface, localX, localZ, view, bounds, content, offset, scale, centerX, centerZ);
        }

        private static Vector2 SurfacePoint020(LandingBox020 surface, float localX, float localZ,
            Rect view, Rect bounds, Rect content, Vector2 offset, float scale, float centerX, float centerZ)
        {
            float worldX = centerX + localX * surface.Cos + localZ * surface.Sin;
            float worldZ = centerZ - localX * surface.Sin + localZ * surface.Cos;
            return new Vector2(content.xMin + offset.x + (worldX - bounds.xMin) * scale,
                content.yMin + offset.y + (worldZ - bounds.yMin) * scale);
        }

        
        

        private static void DrawFrame020(VertexHelper helper, Rect content)
        {
            AddLine020(helper, new Vector2(content.xMin, content.yMin), new Vector2(content.xMin, content.yMax), Ink020, 1.2f);
            AddLine020(helper, new Vector2(content.xMin, content.yMax), new Vector2(content.xMax, content.yMax), Ink020, 1.2f);
            AddLine020(helper, new Vector2(content.xMax, content.yMax), new Vector2(content.xMax, content.yMin), Ink020, 1.2f);
            AddLine020(helper, new Vector2(content.xMax, content.yMin), new Vector2(content.xMin, content.yMin), Ink020, 1.2f);
        }

        private static Color FloorColor020(float topY)
        {
            if (topY < -2f) return LowerFloor020;
            return topY > 3f ? UpperFloor020 : MidFloor020;
        }

        private static void DrawShrine020(VertexHelper helper, Vector2 center)
        {
            AddCircle020(helper, center, 5.1f, Ink020, 12);
            AddCircle020(helper, center, 3.2f, Shrine020, 12);
            AddLine020(helper, center + new Vector2(-1.5f, 0f), center + new Vector2(1.5f, 0f), PaperLight020, .8f);
            AddLine020(helper, center + new Vector2(0f, -1.5f), center + new Vector2(0f, 1.5f), PaperLight020, .8f);
        }

        private static void DrawPortal020(VertexHelper helper, Vector2 center)
        {
            AddDiamond020(helper, center, 5.2f, Ink020);
            AddDiamond020(helper, center, 3.35f, Portal020);
            AddCircle020(helper, center, 1.0f, PaperLight020, 8);
        }

        private static void AddDiamond020(VertexHelper helper, Vector2 center, float radius, Color color)
        {
            AddQuad020(helper, center + new Vector2(0f, radius), center + new Vector2(radius, 0f),
                center + new Vector2(0f, -radius), center + new Vector2(-radius, 0f), color);
        }

        internal static void AddCircle020(VertexHelper helper, Vector2 center, float radius, Color color, int segments)
        {
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * step;
                float a1 = (i + 1) * step;
                Vector2 p0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                AddTriangle020(helper, center, p0, p1, color);
            }
        }

        private static void AddLine020(VertexHelper helper, Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 delta = b - a;
            float lengthSquared = delta.sqrMagnitude;
            if (lengthSquared <= .000001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x) * (width * .5f / Mathf.Sqrt(lengthSquared));
            AddQuad020(helper, a + normal, b + normal, b - normal, a - normal, color);
        }

        private static void AddQuad020(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = helper.currentVertCount;
            AddVertex020(helper, a, color);
            AddVertex020(helper, b, color);
            AddVertex020(helper, c, color);
            AddVertex020(helper, d, color);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        internal static void AddTriangle020(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = helper.currentVertCount;
            AddVertex020(helper, a, color);
            AddVertex020(helper, b, color);
            AddVertex020(helper, c, color);
            helper.AddTriangle(start, start + 1, start + 2);
        }

        private static void AddVertex020(VertexHelper helper, Vector2 point, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(point.x, point.y, 0f);
            vertex.color = color;
            helper.AddVert(vertex);
        }

        private static float DistanceSquaredXZ020(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }

        private static bool IsFinite020(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class CampaignHubPlayerGlyph020 : MaskableGraphic
    {
        private static readonly Color Ink020 = new Color32(18, 43, 62, 255);
        private static readonly Color Blue020 = new Color32(43, 145, 220, 255);
        private static readonly Color Arrow020 = new Color32(242, 238, 218, 255);

        internal static Vector2 ToAnchoredPosition020(Rect parentRect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 childPivot, Vector2 localPoint)
        {
            float anchorFractionX = anchorMin.x + (anchorMax.x - anchorMin.x) * childPivot.x;
            float anchorFractionY = anchorMin.y + (anchorMax.y - anchorMin.y) * childPivot.y;
            Vector2 anchorReference = new Vector2(
                parentRect.xMin + parentRect.width * anchorFractionX,
                parentRect.yMin + parentRect.height * anchorFractionY);
            return localPoint - anchorReference;
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            CampaignHubInkMapGraphic020.AddCircle020(helper, Vector2.zero, 4.7f, Ink020, 16);
            CampaignHubInkMapGraphic020.AddCircle020(helper, Vector2.zero, 3.25f, Blue020, 16);
            CampaignHubInkMapGraphic020.AddTriangle020(helper, new Vector2(0f, 7.1f),
                new Vector2(-2.7f, 2.5f), new Vector2(2.7f, 2.5f), Ink020);
            CampaignHubInkMapGraphic020.AddTriangle020(helper, new Vector2(0f, 6.0f),
                new Vector2(-1.35f, 3.0f), new Vector2(1.35f, 3.0f), Arrow020);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Unity world XZ projected into the existing campaign minimap's local UI rectangle; player yaw maps world +Z to UI +Y.
// Array Formats: One cold-batched UI mesh for the exact 69 accepted floors and two current landmarks; one static glyph mesh updated by cached RectTransform position/rotation.
// Dependencies/Pointers: Existing Ready generation/root/bounds/surface and live-player gates remain the admission authority; no camera, RenderTexture, future city, or per-frame geometry rebuild.



