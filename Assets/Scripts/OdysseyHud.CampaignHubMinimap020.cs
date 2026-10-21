using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        private RectTransform campaignHubMapSurface020;
        private CampaignHubInkMapGraphic020 campaignHubMapGraphic020;
        private bool campaignHubMinimapPresentationActive020;
        private bool campaignHubMinimapPreparationAttempted020;
        private uint acceptedCampaignGeometryGeneration020;
        private Rect[] acceptedCampaignGeometryRooms020;
        private bool acceptedCampaignGeometryValidated020, acceptedCampaignGeometryValid020;

        // Hook this once per HUD update from UpdateCampaignHubHud020, before its !hub return.
        private void RefreshCampaignHubMinimap020()
        {
            if (!CampaignHubHudMode020)
            {
                HideCampaignHubMapOverlay020();
                if (mapSurface != null && !mapSurface.gameObject.activeSelf)
                    mapSurface.gameObject.SetActive(true);
                ResetAcceptedCampaignGeometryCache020();
                if (campaignHubMinimapPresentationActive020)
                {
                    campaignHubMinimapPresentationActive020 = false;
                    RefreshMinimapContext(true);
                }
                return;
            }

            var world = session != null ? session.World : null;
            if (session == null || !session.HasStarted || world == null || mapSurface == null || minimapRoot == null
                || session.Player == null || !session.Player.gameObject.activeInHierarchy
                || campaignHubMapGraphic020 == null || campaignHubMapSurface020 == null
                || campaignHubPlayerGlyphRect020 == null)
            {
                HideCampaignHubMap020();
                return;
            }

            uint generation = world.PortalLandingPrototypeGeneration020;
            if (generation == 0 || !world.PortalLandingPrototypeReady020
                || !world.TryGetPortalLandingPrototype020(generation, out var snapshot)
                || snapshot.Generation != generation || snapshot.Root == null
                || !snapshot.Root.gameObject.activeInHierarchy || !snapshot.Root.IsChildOf(world.transform)
                || !IsAcceptedCampaignBounds020(snapshot.MapBounds)
                || !HasAcceptedCampaignBandsForGeneration020(generation, snapshot.MapRooms, snapshot.MapBounds)
                || !IsMapPoint020(snapshot.Shrine, snapshot.MapBounds)
                || !IsMapPoint020(snapshot.HomePortal, snapshot.MapBounds))
            {
                HideCampaignHubMap020();
                return;
            }

            Vector3 player = session.Player.transform.position;
            float playerYaw = session.Player.transform.eulerAngles.y;
            if (!IsMapPoint020(player, snapshot.MapBounds) || !IsCampaignMapFinite020(playerYaw))
            {
                HideCampaignHubMap020();
                return;
            }

            if (mapSurface.gameObject.activeSelf)
                mapSurface.gameObject.SetActive(false);
            if (campaignHubMapSurface020 != null && !campaignHubMapSurface020.gameObject.activeSelf)
                campaignHubMapSurface020.gameObject.SetActive(true);
            campaignHubMapGraphic020.SetSnapshot020(generation, snapshot.MapRooms, snapshot.MapBounds,
                snapshot.Shrine, snapshot.HomePortal);
            if (!UpdateCampaignHubPlayerGlyph020(player, playerYaw, snapshot.MapBounds))
            {
                HideCampaignHubMap020();
                return;
            }
            if (!campaignHubMapGraphic020.gameObject.activeSelf)
                campaignHubMapGraphic020.gameObject.SetActive(true);
            if (!minimapRoot.gameObject.activeSelf)
                minimapRoot.gameObject.SetActive(true);
            if (minimapTitle != null && minimapTitle.text != "PORTAL LANDING")
                minimapTitle.text = "PORTAL LANDING";
            const string legend = "BLUE · YOU\nGOLD · SHRINE\nVIOLET · HOME PORTAL";
            if (mapLegend != null && mapLegend.text != legend)
                mapLegend.text = legend;
            campaignHubMinimapPresentationActive020 = true;
        }

        private void PrepareCampaignHubMinimap020()
        {
            if (campaignHubMinimapPreparationAttempted020 || mapSurface == null || mapSurface.parent == null) return;
            campaignHubMinimapPreparationAttempted020 = true;
            campaignHubMapGraphic020 = CreateCampaignHubMapGraphic020();
        }

        private CampaignHubInkMapGraphic020 CreateCampaignHubMapGraphic020()
        {
            if (mapSurface == null || mapSurface.parent == null) return null;
            if (campaignHubMapSurface020 == null)
            {
                var surfaceNode = new GameObject("CampaignHubAcceptedSurface020", typeof(RectTransform));
                campaignHubMapSurface020 = surfaceNode.GetComponent<RectTransform>();
                campaignHubMapSurface020.SetParent(mapSurface.parent, false);
                campaignHubMapSurface020.anchorMin = mapSurface.anchorMin;
                campaignHubMapSurface020.anchorMax = mapSurface.anchorMax;
                campaignHubMapSurface020.pivot = mapSurface.pivot;
                campaignHubMapSurface020.anchoredPosition = mapSurface.anchoredPosition;
                campaignHubMapSurface020.sizeDelta = mapSurface.sizeDelta;
                campaignHubMapSurface020.gameObject.AddComponent<RectMask2D>();
                campaignHubMapSurface020.SetAsLastSibling();
                campaignHubMapSurface020.gameObject.SetActive(false);
            }

            var node = new GameObject("CampaignHubAcceptedMap020", typeof(RectTransform));
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(campaignHubMapSurface020, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            var graphic = node.AddComponent<CampaignHubInkMapGraphic020>();
            graphic.raycastTarget = false;
            graphic.color = Color.white;
            campaignHubPlayerGlyphRect020 = CreateCampaignHubPlayerGlyph020(campaignHubMapSurface020);
            return graphic;
        }
        private void HideCampaignHubMap020()
        {
            HideCampaignHubMapOverlay020();
            if (mapSurface != null && mapSurface.gameObject.activeSelf)
                mapSurface.gameObject.SetActive(false);
            if (minimapRoot != null && minimapRoot.gameObject.activeSelf)
                minimapRoot.gameObject.SetActive(false);
        }

        private void HideCampaignHubMapOverlay020()
        {
            if (campaignHubMapGraphic020 != null)
            {
                campaignHubMapGraphic020.ClearSnapshot020();
                if (campaignHubMapGraphic020.gameObject.activeSelf)
                    campaignHubMapGraphic020.gameObject.SetActive(false);
            }
            if (campaignHubMapSurface020 != null && campaignHubMapSurface020.gameObject.activeSelf)
                campaignHubMapSurface020.gameObject.SetActive(false);
        }
        private static bool IsAcceptedCampaignBounds020(Rect bounds)
        {
            return IsCampaignMapFinite020(bounds.xMin) && IsCampaignMapFinite020(bounds.yMin)
                && IsCampaignMapFinite020(bounds.width) && IsCampaignMapFinite020(bounds.height)
                && Mathf.Abs(bounds.xMin + 98f) <= .001f && Mathf.Abs(bounds.yMin + 86f) <= .001f
                && Mathf.Abs(bounds.width - 196f) <= .001f && Mathf.Abs(bounds.height - 142f) <= .001f;
        }

        private static bool HasAcceptedCampaignBands020(Rect[] rooms, Rect bounds)
        {
            if (rooms == null || rooms.Length != PortalLandingLayout020.SurfaceCount)
                return false;
            for (int i = 0; i < rooms.Length; i++)
            {
                if (!PortalLandingLayout020.TrySurface(i, out LandingBox020 surface))
                    return false;
                Rect room = rooms[i];
                if (!IsCampaignMapFinite020(room.xMin) || !IsCampaignMapFinite020(room.yMin)
                    || !IsCampaignMapFinite020(room.width) || !IsCampaignMapFinite020(room.height)
                    || room.width <= 0f || room.height <= 0f
                    || room.xMin < bounds.xMin || room.yMin < bounds.yMin
                    || room.xMax > bounds.xMax || room.yMax > bounds.yMax
                    || !IsCampaignSurfaceFinite020(surface))
                    return false;

                float centerX = (surface.MinX + surface.MaxX) * .5f;
                float centerZ = (surface.MinZ + surface.MaxZ) * .5f;
                float halfX = (surface.MaxX - surface.MinX) * .5f;
                float halfZ = (surface.MaxZ - surface.MinZ) * .5f;
                float extentX = Mathf.Abs(surface.Cos) * halfX + Mathf.Abs(surface.Sin) * halfZ;
                float extentZ = Mathf.Abs(surface.Sin) * halfX + Mathf.Abs(surface.Cos) * halfZ;
                if (Mathf.Abs(room.xMin - (centerX - extentX)) > .005f
                    || Mathf.Abs(room.yMin - (centerZ - extentZ)) > .005f
                    || Mathf.Abs(room.width - extentX * 2f) > .01f
                    || Mathf.Abs(room.height - extentZ * 2f) > .01f)
                    return false;
            }
            return true;
        }

        private bool HasAcceptedCampaignBandsForGeneration020(uint generation, Rect[] rooms, Rect bounds)
        {
            if (acceptedCampaignGeometryValidated020 && acceptedCampaignGeometryGeneration020 == generation
                && object.ReferenceEquals(acceptedCampaignGeometryRooms020, rooms))
                return acceptedCampaignGeometryValid020;

            acceptedCampaignGeometryGeneration020 = generation;
            acceptedCampaignGeometryRooms020 = rooms;
            acceptedCampaignGeometryValid020 = HasAcceptedCampaignBands020(rooms, bounds);
            acceptedCampaignGeometryValidated020 = true;
            return acceptedCampaignGeometryValid020;
        }

        private void ResetAcceptedCampaignGeometryCache020()
        {
            if (!acceptedCampaignGeometryValidated020 && acceptedCampaignGeometryGeneration020 == 0
                && acceptedCampaignGeometryRooms020 == null)
                return;
            acceptedCampaignGeometryGeneration020 = 0;
            acceptedCampaignGeometryRooms020 = null;
            acceptedCampaignGeometryValidated020 = false;
            acceptedCampaignGeometryValid020 = false;
        }

        private static bool IsCampaignSurfaceFinite020(LandingBox020 surface)
        {
            if (!IsCampaignMapFinite020(surface.MinX) || !IsCampaignMapFinite020(surface.MinY)
                || !IsCampaignMapFinite020(surface.MinZ) || !IsCampaignMapFinite020(surface.MaxX)
                || !IsCampaignMapFinite020(surface.MaxY) || !IsCampaignMapFinite020(surface.MaxZ)
                || !IsCampaignMapFinite020(surface.Yaw) || !IsCampaignMapFinite020(surface.Cos) || !IsCampaignMapFinite020(surface.Sin)
                || surface.MaxX <= surface.MinX || surface.MaxY <= surface.MinY || surface.MaxZ <= surface.MinZ)
                return false;
            float radians = surface.Yaw * Mathf.Deg2Rad;
            return Mathf.Abs(surface.Cos * surface.Cos + surface.Sin * surface.Sin - 1f) <= .001f
                && Mathf.Abs(surface.Cos - Mathf.Cos(radians)) <= .001f
                && Mathf.Abs(surface.Sin - Mathf.Sin(radians)) <= .001f;
        }

        private static bool IsMapPoint020(Vector3 point, Rect bounds)
        {
            return IsCampaignMapFinite020(point.x) && IsCampaignMapFinite020(point.y) && IsCampaignMapFinite020(point.z)
                && bounds.Contains(new Vector2(point.x, point.z));
        }

        private static bool IsCampaignMapFinite020(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class CampaignHubMapGraphic020 : MaskableGraphic
    {
        private Rect[] floorBounds020;
        private Rect mapBounds020;
        private Vector3 player020, shrine020, portal020;
        private float playerYaw020;
        private uint generation020;
        private bool valid020;

        public void SetSnapshot020(uint generation, Rect[] floorBounds, Rect bounds, Vector3 player,
            float playerYaw, Vector3 shrine, Vector3 portal)
        {
            bool changed = !valid020 || generation020 != generation || !object.ReferenceEquals(floorBounds020, floorBounds)
                || !mapBounds020.Equals(bounds) || DistanceSquaredXZ020(player020, player) > .0004f
                || Mathf.Abs(Mathf.DeltaAngle(playerYaw020, playerYaw)) > .5f
                || DistanceSquaredXZ020(shrine020, shrine) > .0004f || DistanceSquaredXZ020(portal020, portal) > .0004f;
            if (!changed) return;
            valid020 = true;
            generation020 = generation;
            floorBounds020 = floorBounds;
            mapBounds020 = bounds;
            player020 = player;
            playerYaw020 = playerYaw;
            shrine020 = shrine;
            portal020 = portal;
            SetVerticesDirty();
        }

        public void ClearSnapshot020()
        {
            if (!valid020 && floorBounds020 == null) return;
            valid020 = false;
            generation020 = 0;
            floorBounds020 = null;
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
            if (!valid020 || floorBounds020 == null || floorBounds020.Length != PortalLandingLayout020.SurfaceCount
                || mapBounds020.width <= 0f || mapBounds020.height <= 0f)
                return;

            Rect view = rectTransform.rect;
            float scale = Mathf.Min(view.width / mapBounds020.width, view.height / mapBounds020.height);
            if (!IsCampaignMapFinite020(scale) || scale <= 0f) return;
            Vector2 centerOffset = new Vector2((view.width - mapBounds020.width * scale) * .5f,
                (view.height - mapBounds020.height * scale) * .5f);

            for (int i = 0; i < PortalLandingLayout020.SurfaceCount; i++)
            {
                if (!PortalLandingLayout020.TrySurface(i, out LandingBox020 surface))
                {
                    helper.Clear();
                    return;
                }
                AddSurface020(helper, surface, view, centerOffset, scale, FloorColor020(surface.MaxY));
            }

            Vector2 player = MapPoint020(new Vector2(player020.x, player020.z), view, centerOffset, scale);
            Vector2 shrine = MapPoint020(new Vector2(shrine020.x, shrine020.z), view, centerOffset, scale);
            Vector2 portal = MapPoint020(new Vector2(portal020.x, portal020.z), view, centerOffset, scale);
            AddPlayer020(helper, player, playerYaw020, new Color(.24f, .94f, .50f, 1f));
            AddQuad020(helper, shrine + new Vector2(-3.5f, -3.5f), shrine + new Vector2(-3.5f, 3.5f),
                shrine + new Vector2(3.5f, 3.5f), shrine + new Vector2(3.5f, -3.5f), new Color(1f, .76f, .20f, 1f));
            AddDiamond020(helper, portal, 5f, new Color(.74f, .40f, .96f, 1f));
        }

        private void AddSurface020(VertexHelper helper, LandingBox020 surface, Rect view, Vector2 centerOffset,
            float scale, Color color)
        {
            float centerX = (surface.MinX + surface.MaxX) * .5f;
            float centerZ = (surface.MinZ + surface.MaxZ) * .5f;
            float halfX = (surface.MaxX - surface.MinX) * .5f;
            float halfZ = (surface.MaxZ - surface.MinZ) * .5f;
            Vector2 a = RotateMapPoint020(centerX, centerZ, -halfX, -halfZ, surface.Cos, surface.Sin, view, centerOffset, scale);
            Vector2 b = RotateMapPoint020(centerX, centerZ, -halfX, halfZ, surface.Cos, surface.Sin, view, centerOffset, scale);
            Vector2 c = RotateMapPoint020(centerX, centerZ, halfX, halfZ, surface.Cos, surface.Sin, view, centerOffset, scale);
            Vector2 d = RotateMapPoint020(centerX, centerZ, halfX, -halfZ, surface.Cos, surface.Sin, view, centerOffset, scale);
            AddQuad020(helper, a, b, c, d, color);
        }

        private Vector2 RotateMapPoint020(float centerX, float centerZ, float localX, float localZ,
            float cosine, float sine, Rect view, Vector2 centerOffset, float scale)
        {
            float x = centerX + localX * cosine + localZ * sine;
            float z = centerZ - localX * sine + localZ * cosine;
            return MapPoint020(new Vector2(x, z), view, centerOffset, scale);
        }

        private Color FloorColor020(float topY)
        {
            float blend = Mathf.Clamp01((topY + 6f) / 6f);
            return Color.Lerp(new Color(.18f, .22f, .23f, .92f), new Color(.34f, .38f, .32f, .92f), blend);
        }

        private Vector2 MapPoint020(Vector2 worldPoint, Rect view, Vector2 centerOffset, float scale)
        {
            return new Vector2(view.xMin + centerOffset.x + (worldPoint.x - mapBounds020.xMin) * scale,
                view.yMin + centerOffset.y + (worldPoint.y - mapBounds020.yMin) * scale);
        }

        private static void AddPlayer020(VertexHelper helper, Vector2 center, float yaw, Color color)
        {
            float radians = yaw * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 tip = center + forward * 7f;
            Vector2 rear = center - forward * 4f;
            AddTriangle020(helper, tip, rear + right * 4f, rear - right * 4f, color);
        }

        private static void AddDiamond020(VertexHelper helper, Vector2 center, float radius, Color color)
        {
            AddQuad020(helper, center + new Vector2(0f, radius), center + new Vector2(radius, 0f),
                center + new Vector2(0f, -radius), center + new Vector2(-radius, 0f), color);
        }

        private static void AddTriangle020(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = helper.currentVertCount;
            AddVertex020(helper, a, color);
            AddVertex020(helper, b, color);
            AddVertex020(helper, c, color);
            helper.AddTriangle(start, start + 1, start + 2);
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

        private static void AddVertex020(VertexHelper helper, Vector2 point, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = point;
            vertex.color = color;
            helper.AddVert(vertex);
        }

        private static float DistanceSquaredXZ020(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }

        private static bool IsCampaignMapFinite020(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted PortalLanding world XZ projected into one masked Canvas Graphic; each of 69 layout surfaces is rotated from its exact local box, and floor elevation selects a restrained layer tint.
// Array Formats: Read-only generation-fenced Rect[69] AABB snapshot; validation caches (Generation, MapRooms reference), with one reused UI mesh and actual player/shrine/home-portal values.
// Dependencies/Pointers: BuildMinimap must run before BuildCampaignHubEntry020 calls PrepareCampaignHubMinimap020 on the cold setup path. UpdateCampaignHubHud020 only displays or hides the prepared Graphic before its non-hub return; exit clears geometry cache and forces RefreshMinimapContext(true) once after a shown CampaignHub map. Unchanged snapshots do not dirty the mesh; invalid snapshots hide/clear it and lifecycle hooks release the retained room array. No quest authority is introduced.
