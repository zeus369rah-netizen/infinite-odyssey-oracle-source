#if UNITY_EDITOR
using System;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    // Explicit root-run projection of actual native renderer bounds and actual uGUI corners.
    // No camera/scene/layout/prefs writes, Physics queries, screenshots or visibility inference.
    public static class ShrineMenuFramingChecks
    {
        [Serializable] public sealed class Projection
        {
            public Bounds WorldBounds;public Rect ScreenBounds;public bool AllCornersInFront,AllCornersOnScreen,ClearOfPanel;public int Renderers;
        }
        [Serializable] public sealed class Report
        {
            public string Status,Reason="",Meaning="Actual camera projection against actual StartGamePanel world corners; no occlusion/pixel/lighting proof. Campfire uses its conservative flame/smoke Renderer bounds; combined stone/log base is not independently isolated.";
            public int ScreenWidth,ScreenHeight;public Vector3 CameraPosition,CameraForward;public float FieldOfView,Aspect;
            public Rect PanelScreenBounds;public bool PanelActive,CandidatePoseApplied,PassedProjection;
            public Projection Shrine,Campfire;public Vector3 ShrineCenterViewport,CampfireCenterViewport;
        }
        public static string Run()
        {
            var report=new Report {Status="refused"};var session=GameSession.Instance;
            if(!Application.isPlaying||session==null||session.World==null||session.World.IsCoopArena||session.HasStarted||session.Opening==null||session.Hud==null)
                return Refuse(report,"Prepared native tutorial MAIN MENU required, not gameplay/arena.");
            Camera camera=session.Opening.ActiveCamera;
            if(camera==null||!camera.enabled||session.Opening.IsIntroPlaying||session.Opening.IsTransitioning)return Refuse(report,"Settled actual opening/menu camera required.");
            RectTransform panel=null;foreach(var candidate in session.Hud.GetComponentsInChildren<RectTransform>(true))if(candidate.name=="StartGamePanel"){panel=candidate;break;}
            if(panel==null||!panel.gameObject.activeInHierarchy||panel.rect.width<=0||panel.rect.height<=0)return Refuse(report,"Actual laid-out active StartGamePanel required; wait one ordinary frame.");
            Canvas canvas=panel.GetComponentInParent<Canvas>();if(canvas==null||canvas.renderMode!=RenderMode.ScreenSpaceOverlay)return Refuse(report,"Current ScreenSpaceOverlay contract required.");
            if(session.Combat==null||session.Combat.Shrine==null)return Refuse(report,"Actual built shrine renderer hierarchy required.");
            var corners=new Vector3[4];panel.GetWorldCorners(corners);Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach(var corner in corners){Vector2 point=RectTransformUtility.WorldToScreenPoint(null,corner);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
            report.PanelScreenBounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y);report.PanelActive=true;
            report.ScreenWidth=Screen.width;report.ScreenHeight=Screen.height;report.CameraPosition=camera.transform.position;report.CameraForward=camera.transform.forward;report.FieldOfView=camera.fieldOfView;report.Aspect=camera.aspect;
            Vector3 target=session.World.ShrinePosition+Vector3.right*4+Vector3.up*1.8f;
            report.CandidatePoseApplied=(camera.transform.position-new Vector3(1.8f,2.25f,-8.8f)).sqrMagnitude<1e-8f&&
                Quaternion.Angle(camera.transform.rotation,Quaternion.LookRotation(target-camera.transform.position,Vector3.up))<.001f&&Mathf.Abs(camera.fieldOfView-68)<.001f;
            if(!BoundsOf(session.Combat.Shrine.transform,out Bounds shrine,out int shrineCount))return Refuse(report,"No actual active shrine renderers.");
            VoxelFireEffect campfire=null;foreach(var fire in session.World.GetComponentsInChildren<VoxelFireEffect>(false))
                if(fire.name.IndexOf("campfire",StringComparison.OrdinalIgnoreCase)>=0&&(fire.transform.position-new Vector3(-4,.22f,5.25f)).sqrMagnitude<1){campfire=fire;break;}
            if(campfire==null||!BoundsOf(campfire.transform,out Bounds fireBounds,out int fireCount))return Refuse(report,"Actual campfire flame Renderer not found at authored tutorial campfire.");
            report.Shrine=Project(camera,shrine,shrineCount,report.PanelScreenBounds);report.Campfire=Project(camera,fireBounds,fireCount,report.PanelScreenBounds);
            report.ShrineCenterViewport=camera.WorldToViewportPoint(shrine.center);report.CampfireCenterViewport=camera.WorldToViewportPoint(fireBounds.center);
            report.PassedProjection=report.CandidatePoseApplied&&report.Shrine.AllCornersInFront&&report.Shrine.AllCornersOnScreen&&report.Shrine.ClearOfPanel&&
                report.Campfire.AllCornersInFront&&report.Campfire.AllCornersOnScreen&&report.Campfire.ClearOfPanel;
            report.Status=report.PassedProjection?"projection-check-passed-visual-review-pending":"projection-check-failed";
            return JsonUtility.ToJson(report,true);
        }
        static string Refuse(Report report,string reason){report.Reason=reason;return JsonUtility.ToJson(report,true);}
        static bool BoundsOf(Transform root,out Bounds bounds,out int count)
        {
            bounds=default;count=0;foreach(var renderer in root.GetComponentsInChildren<Renderer>(false))if(renderer.enabled&&!renderer.forceRenderingOff){
                if(count++==0)bounds=renderer.bounds;else bounds.Encapsulate(renderer.bounds);}
            return count>0;
        }
        static Projection Project(Camera camera,Bounds bounds,int count,Rect panel)
        {
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);bool front=true,onScreen=true;
            for(int mask=0;mask<8;mask++){
                Vector3 point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((mask&1)==0?-1:1,(mask&2)==0?-1:1,(mask&4)==0?-1:1));
                Vector3 screen=camera.WorldToScreenPoint(point);front&=screen.z>camera.nearClipPlane;onScreen&=screen.x>=0&&screen.y>=0&&screen.x<=Screen.width&&screen.y<=Screen.height;
                min=Vector2.Min(min,new Vector2(screen.x,screen.y));max=Vector2.Max(max,new Vector2(screen.x,screen.y));
            }
            Rect projected=Rect.MinMaxRect(min.x,min.y,max.x,max.y);Rect padding=Rect.MinMaxRect(panel.xMin-8,panel.yMin-8,panel.xMax+8,panel.yMax+8);
            return new Projection {WorldBounds=bounds,ScreenBounds=projected,AllCornersInFront=front,AllCornersOnScreen=onScreen,ClearOfPanel=!projected.Overlaps(padding),Renderers=count};
        }
    }
}
#endif
