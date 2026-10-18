using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // World-owned cold geometry/physics adapter: four canonical top centres in
    // world metres, twelve cached course colliders, one reusable32-hit query.
    // Existing TowerPadPolicy owns the shared3x12cm dimensions. No input/Update,
    // jobs, native allocation, damage, token/readiness or network authority here.
    public sealed class TowerPlatformGeometry
    {
        public const int PadCount=4,PadsPerLane=2;
        public const float MaximumReach=25f,TopTolerance=.025f,HorizontalTolerance=.2f;
        public static Vector3 MatchForgePosition=>new Vector3(5.6f,0f,-5.6f);
        public const float MatchForgeYaw=315f;
        readonly Transform parent;
        readonly Collider[] courses=new Collider[PadCount*TowerPadPolicy.CourseCount];
        readonly RaycastHit[] hits=new RaycastHit[32];
        public Vector3[] TopCenters {get;}

        public TowerPlatformGeometry(Transform parent,Vector3[] canonicalTopCenters)
        {
            if(parent==null||canonicalTopCenters==null||canonicalTopCenters.Length!=PadCount)
                throw new ArgumentException("Exactly four canonical platform centres and their World root are required.");
            for(int i=0;i<PadCount;i++)if(!Finite(canonicalTopCenters[i])||
                Mathf.Abs(canonicalTopCenters[i].y-TowerPadPolicy.TopHeight)>TopTolerance)
                throw new ArgumentException("Platform centres must use the shared canonical raised top.");
            this.parent=parent;TopCenters=canonicalTopCenters;
        }

        public static Vector3[] CreateTopCenters()=>new[]{
            new Vector3(-14,TowerPadPolicy.TopHeight,4.8f),new Vector3(-22,TowerPadPolicy.TopHeight,-4.8f),
            new Vector3(14,TowerPadPolicy.TopHeight,-4.8f),new Vector3(22,TowerPadPolicy.TopHeight,4.8f)};

        public static void GetCourse(Vector3 canonicalTop,int course,out Vector3 center,out Vector3 size)
        {
            float width=TowerPadPolicy.CourseWidth(course);
            center=canonicalTop+Vector3.up*TowerPadPolicy.CourseCenterBelowTop(course);
            size=new Vector3(width,TowerPadPolicy.CourseHeight,width);
        }

        // Called once per World course; World owns each resulting child object.
        public BoxCollider CreateCourseCollider(int pad,int course)
        {
            CheckIndex(pad,course);int index=pad*TowerPadPolicy.CourseCount+course;
            if(courses[index]!=null)throw new InvalidOperationException("A platform course already owns a collider.");
            GetCourse(TopCenters[pad],course,out Vector3 center,out Vector3 size);
            var go=new GameObject("Tower platform "+pad+" course "+course);
            go.transform.SetParent(parent,false);go.transform.localPosition=center;
            var collider=go.AddComponent<BoxCollider>();collider.size=size;courses[index]=collider;return collider;
        }

        public Collider GetCourseCollider(int pad,int course)
        {CheckIndex(pad,course);return courses[pad*TowerPadPolicy.CourseCount+course];}

        // All three physical courses belong to the pad. A caller must separately
        // require the hit's upward normal/actual surface via MatchesCourseSurface.
        public bool IsPadCollider(int pad,Collider collider)
        {
            if(pad<0||pad>=PadCount||collider==null)return false;
            for(int course=0;course<TowerPadPolicy.CourseCount;course++)if(courses[pad*TowerPadPolicy.CourseCount+course]==collider)return true;
            return false;
        }

        public bool TryGetCanonical(Vector3 requested,out Vector3 canonical,out int pad)
        {
            canonical=default;pad=-1;if(!Finite(requested))return false;float best=HorizontalTolerance*HorizontalTolerance;
            for(int i=0;i<TopCenters.Length;i++)
            {
                Vector3 p=TopCenters[i];if(!Finite(p)||!TowerPadPolicy.MatchesTop(requested.y,p.y))continue;
                float dx=requested.x-p.x,dz=requested.z-p.z,square=dx*dx+dz*dz;
                if(square>best||pad>=0&&square==best)continue;best=square;pad=i;canonical=p;
            }
            return pad>=0;
        }

        // Locked first-person mouse aim is the actual camera viewport centre.
        // A nearby floor point, hidden rear course or wall cannot select a pad.
        public bool TryPick(OraclePlayer player,float reach,out int pad,out RaycastHit hit)
        {
            pad=-1;hit=default;
            if(parent==null||!parent.gameObject.activeInHierarchy||player==null||player.ViewCamera==null||
                !Finite(reach)||reach<=0f)return false;
            reach=Mathf.Min(reach,MaximumReach);Camera camera=player.ViewCamera;
            Ray ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            // Both camera ray and known feet-to-canonical distance use the same
            // bounded metre reach. Input/lifecycle permission remains in Builder.
            // ViewportPointToRay starts at the near plane. Start the physics ray
            // at the actual eye instead so it cannot extend the25m range by nearClip.
            if(!TryNearestSolid(camera.transform.position,ray.direction,reach,player.transform,out hit))return false;
            int course;
            if(!TryIdentify(hit.collider,out pad,out course)||!MatchesCourseSurface(pad,course,hit.point,hit.normal))
            {pad=-1;return false;}
            Vector3 feet=player.transform.position;if(!Finite(feet)||(feet-TopCenters[pad]).sqrMagnitude>reach*reach)
            {pad=-1;return false;}
            return true;
        }

        // Host/current Builder can validate the real unobstructed canonical top.
        // Actor colliders are ignored here like the existing placement ground
        // query; the separate live-body and LOS validation remains authoritative.
        public bool TryGetTopSurface(int pad,out RaycastHit hit)
        {
            hit=default;if(pad<0||pad>=PadCount||parent==null||!parent.gameObject.activeInHierarchy)return false;
            Vector3 p=TopCenters[pad];int count=Physics.RaycastNonAlloc(p+Vector3.up*2f,Vector3.down,hits,4f,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;float nearest=float.PositiveInfinity;bool found=false;
            for(int i=0;i<count;i++)
            {
                Collider c=hits[i].collider;
                if(c==null||hits[i].distance>=nearest||c.GetComponentInParent<OraclePlayer>()!=null||
                    c.GetComponentInParent<OracleEnemy>()!=null||c.GetComponentInParent<OracleProjectile>()!=null)continue;
                nearest=hits[i].distance;hit=hits[i];found=true;
            }
            return found&&hit.collider==courses[pad*TowerPadPolicy.CourseCount+TowerPadPolicy.CourseCount-1]&&
                MatchesCourseSurface(pad,TowerPadPolicy.CourseCount-1,hit.point,hit.normal);
        }

        public bool MatchesCourseSurface(int pad,int course,Vector3 point,Vector3 normal)
        {
            if(pad<0||pad>=PadCount||course<0||course>=TowerPadPolicy.CourseCount||!Finite(point)||!Finite(normal)||normal.y<=.8f)return false;
            GetCourse(TopCenters[pad],course,out Vector3 center,out Vector3 size);
            return Mathf.Abs(point.y-(center.y+size.y*.5f))<=.002f&&
                Mathf.Abs(point.x-center.x)<=size.x*.5f+.002f&&Mathf.Abs(point.z-center.z)<=size.z*.5f+.002f;
        }

        bool TryNearestSolid(Vector3 origin,Vector3 direction,float reach,Transform ignored,out RaycastHit hit)
        {
            hit=default;if(!Finite(origin)||!Finite(direction)||direction.sqrMagnitude<.00001f)return false;
            int count=Physics.RaycastNonAlloc(origin,direction,hits,reach,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;float nearest=float.PositiveInfinity;bool found=false;
            for(int i=0;i<count;i++)
            {
                Collider c=hits[i].collider;if(c==null||hits[i].distance>=nearest)continue;
                if(ignored!=null&&(c.transform==ignored||c.transform.IsChildOf(ignored)))continue;
                nearest=hits[i].distance;hit=hits[i];found=true;
            }
            return found;
        }
        bool TryIdentify(Collider collider,out int pad,out int course)
        {
            pad=course=-1;if(collider==null)return false;
            for(int i=0;i<courses.Length;i++)if(courses[i]==collider){pad=i/TowerPadPolicy.CourseCount;course=i%TowerPadPolicy.CourseCount;return true;}
            return false;
        }
        static void CheckIndex(int pad,int course)
        {if(pad<0||pad>=PadCount||course<0||course>=TowerPadPolicy.CourseCount)throw new ArgumentOutOfRangeException();}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
    }
}
