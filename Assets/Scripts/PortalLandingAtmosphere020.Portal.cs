using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public sealed partial class PortalLandingAtmosphere020
    {
        static readonly Vector3 PortalCenter020 = new Vector3(0f,11f,50.15f);
        static readonly Vector3 PortalSize020 = new Vector3(7.82f,8.86f,.04f);
        Mesh portalEffectMesh020;
        Material portalEffectMaterial020;
        Matrix4x4 portalEffectMatrix020;
        Bounds portalEffectBounds020;
        bool portalEffectAttempted020,portalEffectPrepared020;
        int portalEffectBatches020;
        string portalEffectStatus020="Unprepared";

        public bool PortalEffectReady020=>Bound020&&portalEffectPrepared020&&portalEffectMesh020!=null&&portalEffectMaterial020!=null;
        public int LastSubmittedPortalBatches020=>Bound020?portalEffectBatches020:0;
        public string PortalEffectStatus020=>portalEffectStatus020;

        void PreparePortalEffect020()
        {
            if(portalEffectAttempted020)return;
            portalEffectAttempted020=true;
            Shader shader=Resources.Load<Shader>("Shaders/PortalLandingGravity020");
            if(shader==null||!shader.isSupported)
            {portalEffectStatus020="Optional shader unavailable; original portal retained";return;}
            try
            {
                // Track each native acquisition before any subsequent property setter can throw.
                portalEffectMaterial020=new Material(shader);
                portalEffectMaterial020.name="Campaign private analytic gravity portal020";
                portalEffectMaterial020.hideFlags=HideFlags.DontSave;
                if(!portalEffectMaterial020.HasProperty(ClockId)||!portalEffectMaterial020.HasProperty(QualityId))
                    throw new InvalidOperationException("Portal shader scalar contract changed.");
                portalEffectMesh020=new Mesh();
                portalEffectMesh020.name="Campaign fixed two-triangle portal sheet020";
                portalEffectMesh020.hideFlags=HideFlags.DontSave;
                portalEffectMesh020.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                portalEffectMesh020.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
                portalEffectMesh020.triangles=new[]{0,1,2,0,2,3};
                portalEffectMesh020.bounds=new Bounds(Vector3.zero,new Vector3(1,1,.01f));
                portalEffectMesh020.UploadMeshData(true);
                portalEffectMatrix020=Matrix4x4.TRS(PortalCenter020,Quaternion.identity,new Vector3(PortalSize020.x,PortalSize020.y,1));
                portalEffectBounds020=new Bounds(PortalCenter020,PortalSize020);
                portalEffectPrepared020=true;
                portalEffectStatus020="Prepared optional analytic portal";
            }
            catch
            {
                portalEffectStatus020="Optional preparation failed; original portal retained";
                ReleasePortalEffect020();
            }
        }

        void SetPortalEffectUniforms020()
        {
            portalEffectBatches020=0;
            if(!portalEffectPrepared020||portalEffectMaterial020==null)return;
            float clock=presentationShaderClock020;
            if(float.IsNaN(clock)||float.IsInfinity(clock)||clock<0||clock>4096)clock=0;
            portalEffectMaterial020.SetFloat(ClockId,clock);
            portalEffectMaterial020.SetFloat(QualityId,Mathf.Clamp(quality,0,2));
        }

        void DrawPortalEffect020(Camera camera)
        {
            portalEffectBatches020=0;
            if(!portalEffectPrepared020||portalEffectMesh020==null||portalEffectMaterial020==null||camera==null||!bound||!OwnsAdmission())return;
            if(admittedRoot020==null||!admittedRoot020.localToWorldMatrix.Equals(Matrix4x4.identity)||
                !world.transform.localToWorldMatrix.Equals(Matrix4x4.identity))return;
            try
            {
                var parameters=new RenderParams(portalEffectMaterial020)
                {
                    camera=camera,worldBounds=portalEffectBounds020,layer=admittedRoot020.gameObject.layer,
                    shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false,
                    lightProbeUsage=LightProbeUsage.Off,reflectionProbeUsage=ReflectionProbeUsage.Off
                };
                Graphics.RenderMesh(parameters,portalEffectMesh020,0,portalEffectMatrix020);
                portalEffectBatches020=1;
            }
            catch
            {
                // Fail once to the untouched opaque portal field; retain private resources for final release.
                portalEffectPrepared020=false;portalEffectBatches020=0;
                portalEffectStatus020="Optional submission failed; original portal retained";
            }
        }

        void ReleasePortalEffect020()
        {
            portalEffectPrepared020=false;portalEffectBatches020=0;
            Exception failure=null;
            try{if(portalEffectMesh020!=null){DestroyOwned(portalEffectMesh020);portalEffectMesh020=null;}}
            catch(Exception error){failure=error;}
            try{if(portalEffectMaterial020!=null){DestroyOwned(portalEffectMaterial020);portalEffectMaterial020=null;}}
            catch(Exception error){if(failure==null)failure=error;}
            if(failure!=null)throw failure;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Fixed world XY sheet centered(0,11,50.15),7.82x8.86m behind existing magenta rings/emblem/facets; exact identity World/root guard.
// Array Formats: One immutable4vertex/6index mesh, one private material, cached matrix/bounds/scalars; no GameObject/native-array/RT/light or frame allocation.
// Dependencies/Pointers: Existing atmosphere accepted-camera/current root-generation-revision lease and held shader clock/quality. Optional failures retain original169-command portal; final native resources release independently. Unity owns queued draw lifetime; native GPU/ZeroGC/visual acceptance is root-only.
