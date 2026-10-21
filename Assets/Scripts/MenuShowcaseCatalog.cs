using System;
using UnityEngine;

namespace OdysseyOracle
{
    // Root-native snapshot of existing original arena geometry, never a World owner.
    public sealed class MenuShowcaseCatalog:ScriptableObject
    {
        public const string ResourceKey="Showcase/MenuCoop009/Catalog";
        public const int MaximumMeshes=384,MaximumMaterials=96,RouteCount=12,MaximumSegmentsPerRoute=128;
        [Serializable] public struct Draw {public Mesh Mesh;public Material Material;}
        [Serializable] public struct HubLight {public Vector3 Position;public Color Color;public float Intensity,Range;}
        public const int MaximumHubLights=5;
        // Optional visual-only fields: existing V5 catalogs remain valid.
        public Draw[] HubDraws;
        public HubLight[] HubLights;
        public Vector3 HubAnchor;
        public Draw[] Draws;
        public Vector3[] Pads;
        public MenuShowcaseSegment32[] Segments;
        public Vector2Int[] RouteRanges;
        public Bounds ViewBounds;
        public string SourceFingerprint,Provenance;
        public bool TryValidate(out string reason)
        {
            reason="Showcase catalog unavailable.";
            if(Draws==null||Draws.Length<1||Draws.Length>MaximumMeshes||Pads==null||Pads.Length!=4||
                Segments==null||Segments.Length<1||Segments.Length>RouteCount*MaximumSegmentsPerRoute||
                RouteRanges==null||RouteRanges.Length!=RouteCount||string.IsNullOrEmpty(SourceFingerprint)||string.IsNullOrEmpty(Provenance))return false;
            for(int i=0;i<Draws.Length;i++)if(Draws[i].Mesh==null||Draws[i].Material==null)return false;
            int hubCount=HubDraws==null?0:HubDraws.Length,lightCount=HubLights==null?0:HubLights.Length;
            if(hubCount+Draws.Length>MaximumMeshes||lightCount>MaximumHubLights||!Finite(HubAnchor))return false;
            if((hubCount==0)!=(lightCount==0))return false;
            if(hubCount>0&&lightCount!=MaximumHubLights)return false;
            for(int i=0;i<hubCount;i++)if(HubDraws[i].Mesh==null||HubDraws[i].Material==null)return false;
            for(int i=0;i<lightCount;i++)
            {var light=HubLights[i];if(!Finite(light.Position)||!Finite(new Vector3(light.Color.r,light.Color.g,light.Color.b))||light.Color.r<0||light.Color.g<0||light.Color.b<0||light.Color.maxColorComponent>1||!float.IsFinite(light.Intensity)||!float.IsFinite(light.Range)||light.Intensity<=0||light.Intensity>7.5f||light.Range<=0||light.Range>12)return false;}
            for(int i=0;i<Pads.Length;i++)if(!Finite(Pads[i]))return false;
            for(int i=0;i<Segments.Length;i++)
            {var s=Segments[i];if(!Finite(new Vector3(s.A.x,s.A.y,s.A.z))||!Finite(new Vector3(s.B.x,s.B.y,s.B.z))||float.IsNaN(s.Begin)||float.IsInfinity(s.Begin)||float.IsNaN(s.End)||float.IsInfinity(s.End)||s.Begin<0||s.End>1||s.End<=s.Begin)return false;}
            for(int i=0;i<RouteRanges.Length;i++)if(RouteRanges[i].x<0||RouteRanges[i].y<1||RouteRanges[i].y>MaximumSegmentsPerRoute||
                RouteRanges[i].x+RouteRanges[i].y>Segments.Length)return false;
            for(int i=0;i<RouteRanges.Length;i++)
            {var r=RouteRanges[i];if(Mathf.Abs(Segments[r.x].Begin)>1e-5f||Mathf.Abs(Segments[r.x+r.y-1].End-1)>1e-5f)return false;for(int j=1;j<r.y;j++)if(Mathf.Abs(Segments[r.x+j].Begin-Segments[r.x+j-1].End)>1e-5f)return false;}
            if(!Finite(ViewBounds.center)||!Finite(ViewBounds.size)||ViewBounds.size.x<=0||ViewBounds.size.z<=0||ViewBounds.size.magnitude>512)return false;
            reason="";return true;
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual arena-local metres/Y-up/+Z forward. Saved material-combined geometry and12 ground-supported numeric routes.
// Array Formats: Caps384 combined environment+optional hub draws/96 materials/4 pads/1536 route segments; optional hub has exactly five bounded light records. Runtime borrows every saved asset; no World, rooms, HP or saves.
// Dependencies/Pointers: Explicit capture/catalog borrows existing World and saved asset references; no live actor, gameplay or save ownership is transferred.
