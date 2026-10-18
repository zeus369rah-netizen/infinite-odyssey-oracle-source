using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle.Diagnostics
{
    // Public native receipt entry. Read-only: no movement, spawns or scene edits.
    // Root runs AFTER integration during an actual walk/stop/attack/seated pose.
    public static class Release003GroundingCapture
    {
        [Serializable] sealed class Actor
        {
            public int id, legCount;
            public bool awake, flying, alive, supportFound;
            public string phase;
            public Vector3 root;
            public float soleY, floorY, soleGap, crowdRadius, navigationRadius;
        }
        [Serializable] sealed class Receipt
        {
            public string method="Native transformed leg mesh AABB support sample; no authored-math substitution";
            public string limits="Single observed frame only; no travel/crowd/network/FPS acceptance. Flying soles intentionally not compared with ground.";
            public int frame;
            public Actor[] actors;
        }

        public static string Capture()
        {
            var found=UnityEngine.Object.FindObjectsByType<OracleEnemy>(FindObjectsInactive.Exclude);
            var rows=new List<Actor>(found.Length);
            foreach(var enemy in found)
            {
                var row=new Actor {id=enemy.TargetId,awake=enemy.IsAwake,flying=enemy.IsFlying,alive=enemy.Alive,
                    phase=enemy.AttackPhase.ToString(),root=enemy.transform.position,
                    soleY=float.MaxValue,crowdRadius=enemy.CrowdRadius,navigationRadius=enemy.NavigationRadius};
                var meshes=enemy.GetComponentsInChildren<MeshFilter>();
                foreach(var filter in meshes)
                {
                    string name=filter.gameObject.name;
                    if(name!="Left leg"&&name!="Right leg"&&!name.StartsWith("Spider leg ",StringComparison.Ordinal))continue;
                    if(filter.sharedMesh==null)continue;
                    Bounds b=filter.sharedMesh.bounds;Matrix4x4 m=filter.transform.localToWorldMatrix;Vector3 e=b.extents;
                    float sole=m.MultiplyPoint3x4(b.center).y-(Mathf.Abs(m.m10)*e.x+Mathf.Abs(m.m11)*e.y+Mathf.Abs(m.m12)*e.z);
                    row.soleY=Mathf.Min(row.soleY,sole);row.legCount++;
                }
                if(!row.flying&&Physics.Raycast(row.root+Vector3.up*1.5f,Vector3.down,out RaycastHit hit,3f,~0,QueryTriggerInteraction.Ignore)&&hit.normal.y>=.75f)
                {row.supportFound=true;row.floorY=hit.point.y;row.soleGap=row.soleY-row.floorY;}
                rows.Add(row);
            }
            return JsonUtility.ToJson(new Receipt {frame=Time.frameCount,actors=rows.ToArray()},true);
        }
    }
}
