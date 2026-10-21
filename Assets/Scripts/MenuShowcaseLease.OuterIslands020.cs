using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class MenuShowcaseLease
    {
        Mesh outerIslandMesh020;
        public int OuterIslandMeshCount020=>outerIslandMesh020!=null?1:0;
        public int OuterIslandVertexCount020=>outerIslandMesh020!=null?OuterIslandMath020.VertexCount:0;
        static Bounds OuterIslandPresentationBounds020(MenuShowcaseCatalog catalog)
        {
            Bounds complete=MenuIslandGeometry015.FullIslandBounds(catalog.ViewBounds,catalog.HubAnchor.y);
            var shader=Resources.Load<Shader>("Shaders/OracleMenuIsland015");
            if(shader==null||!shader.isSupported||!OuterIslandGeometry020.TryLayout(catalog.ViewBounds,catalog.HubAnchor.y,out _,out var extra))return complete;
            var expanded=complete;expanded.Encapsulate(extra);
            return float.IsFinite(expanded.extents.magnitude)&&expanded.extents.magnitude<=255f?expanded:complete;
        }
        void InitializeOuterIslands020(MenuShowcaseCatalog catalog,Material rock)
        {
            if(!OuterIslandGeometry020.TryLayout(catalog.ViewBounds,catalog.HubAnchor.y,out var layout,out var bounds))return;
            var complete=MenuIslandGeometry015.FullIslandBounds(catalog.ViewBounds,catalog.HubAnchor.y);complete.Encapsulate(bounds);
            if(!float.IsFinite(complete.extents.magnitude)||complete.extents.magnitude>255f)return;
            outerIslandMesh020=OuterIslandGeometry020.Build(layout,bounds);
            Draw("Original six separate floating perimeter islands020",root.transform,outerIslandMesh020,rock);
        }
        void DisposeOuterIslands020()
        {
            if(outerIslandMesh020!=null){UnityEngine.Object.Destroy(outerIslandMesh020);outerIslandMesh020=null;}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing catalog-local preview stage/layer30/rendering-layer7; complete wide bounds include each detached outer solid.
// Array Formats: one immutable1620vertex mesh/draw borrowing the existing private rock material; no persistent native store, clock, frame upload or new material.
// Dependencies/Pointers: MenuShowcaseLease destroys exactly its own mesh after completed jobs. Original catalog, four underside/weather meshes, held clock, actor routes, room/settings/saves remain canonical; unsupported bounds omit optional art.
