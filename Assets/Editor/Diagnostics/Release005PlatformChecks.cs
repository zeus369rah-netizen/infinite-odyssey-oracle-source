#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle.Diagnostics
{
    // Root-run only after constructing the actual005 arena. Does not build,
    // move, enable/disable, or destroy scene objects or change player input.
    public static class Release005PlatformChecks
    {
        [Serializable] sealed class Course
        {public int pad,course;public Vector3 center,size;public bool registered;}
        [Serializable] sealed class Receipt
        {
            public string status,unityVersion,method;
            public int assertions,padCount,uniqueCourseColliders,canonicalTopHits;
            public List<string> failures=new List<string>();public List<Course> courses=new List<Course>();
            public string limitations="Read-only native collider/query check; does not prove renderer appearance, mouse interaction, host RPCs, route acceptance, browser performance or multiplayer.";
        }
        public static string Run(GameSession session)
        {
            var r=new Receipt{unityVersion=Application.unityVersion,method="Actual World registry, public Collider bounds and Physics queries; no scene mutations."};
            if(session==null||session.World==null||!session.World.IsCoopArena||session.World.TowerPlatforms==null)
            {r.status="NOT_RUN";r.failures.Add("Construct the composed005 co-op arena first.");return JsonUtility.ToJson(r,true);}
            DungeonWorld world=session.World;TowerPlatformGeometry geometry=world.TowerPlatforms;
            r.padCount=world.TowerBuildPads.Length;Check(r,r.padCount==4,"Exactly four canonical pads");
            Check(r,ReferenceEquals(world.TowerBuildPads,geometry.TopCenters),"World and physics share canonical centres");
            var identities=new HashSet<Collider>();
            for(int pad=0;pad<Math.Min(4,r.padCount);pad++)
            {
                Vector3 p=world.TowerBuildPads[pad];Check(r,Mathf.Abs(p.y-.36f)<.00001f,"Raised canonical top for pad "+pad);
                Check(r,geometry.TryGetCanonical(p,out Vector3 canonical,out int id)&&id==pad&&canonical==p,"Canonical stable ID "+pad);
                Check(r,!geometry.TryGetCanonical(new Vector3(p.x,0,p.z),out _,out _),"Floor-height request rejected "+pad);
                bool top=geometry.TryGetTopSurface(pad,out RaycastHit hit);if(top)r.canonicalTopHits++;
                Check(r,top&&hit.collider==geometry.GetCourseCollider(pad,2)&&hit.normal.y>.8f&&Mathf.Abs(hit.point.y-p.y)<.002f,"Actual canonical-top ray "+pad);
                for(int course=0;course<3;course++)
                {
                    Collider c=geometry.GetCourseCollider(pad,course);TowerPlatformGeometry.GetCourse(p,course,out Vector3 center,out Vector3 size);
                    bool registered=c!=null&&geometry.IsPadCollider(pad,c);Check(r,registered,"Registered course "+pad+"/"+course);
                    if(c==null)continue;Check(r,identities.Add(c),"Unique course identity "+pad+"/"+course);
                    Check(r,c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.gameObject.layer!=2,"Active solid course "+pad+"/"+course);
                    Bounds bounds=c.bounds;Check(r,(bounds.center-center).sqrMagnitude<.0000001f&&(bounds.size-size).sqrMagnitude<.0000001f,"Actual course bounds "+pad+"/"+course);
                    r.courses.Add(new Course{pad=pad,course=course,center=bounds.center,size=bounds.size,registered=registered});
                }
            }
            r.uniqueCourseColliders=identities.Count;Check(r,identities.Count==12,"Twelve unique physical courses");
            Check(r,world.ActiveLaneCount>=1&&world.ActiveLaneCount<=2,"Current one/two-lane contract");
            r.status=r.failures.Count==0?"PASS":"FAIL";return JsonUtility.ToJson(r,true);
        }
        static void Check(Receipt r,bool value,string label){r.assertions++;if(!value)r.failures.Add(label);}
    }
}
#endif
