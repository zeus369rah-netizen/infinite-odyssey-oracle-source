using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Fixed six tower records plus one core record, with two ground leases or
    // one exclusive flying melee lease each. World
    // XZ metres; stable leases survive ordinary frame/order changes and are freed
    // as soon as an actor dies, changes target or its tower is destroyed. No
    // PhysX blocker, per-frame collection, N-by-N neighbor scan or native owner.
    internal sealed class TowerAttackSlots
    {
        struct Record { internal PlacedElementShrine Tower; internal OracleEnemy A, B, Flying; internal Vector3 Forward; }
        struct CoreRecord { internal OracleShrine Core; internal OracleEnemy A, B, Flying; internal Vector3 Forward; }
        readonly Record[] records = new Record[PlacedElementShrine.MaximumRegisteredTowers];
        CoreRecord coreRecord;
        internal void Clear() { for (int i = 0; i < records.Length; i++) records[i] = default; coreRecord = default; }

        static bool Valid(OracleEnemy enemy, PlacedElementShrine target) =>
            enemy != null && enemy.Targetable && enemy.AttackTower == target && !enemy.IsRanged && !enemy.IsFlying;

        internal bool Approach(OracleEnemy enemy, PlacedElementShrine tower, Vector3 forward,
            out Vector3 point, out bool canStrike)
        {
            point = enemy.transform.position; canStrike = false;
            forward.y = 0f;
            if (forward.sqrMagnitude < .001f) return false;
            forward.Normalize();
            int record = -1, free = -1;
            for (int i = 0; i < records.Length; i++)
            {
                Record r = records[i];
                if (r.Tower == null || r.Tower.IsDestroyed || !r.Tower.gameObject.activeInHierarchy)
                { records[i] = default; if (free < 0) free = i; continue; }
                if (!Valid(r.A, r.Tower)) r.A = null;
                if (!Valid(r.B, r.Tower)) r.B = null;
                if (r.Flying == null || !r.Flying.Targetable || !r.Flying.IsFlying || r.Flying.IsRanged || r.Flying.AttackTower != r.Tower) r.Flying = null;
                records[i] = r;
                if (r.Tower == tower) record = i;
            }
            if (tower == null || tower.IsDestroyed) return false;
            if (record < 0)
            {
                if (free < 0) return false;
                record = free; records[record] = new Record { Tower = tower, Forward = Cardinal(forward) };
            }
            Vector3 incoming=forward;
            forward = records[record].Forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Record target = records[record];
            // Ranged actors retain their existing warning/bolt behavior. A flying
            // melee actor can lease the front exclusively, with unchanged reach.
            if (enemy.IsRanged) return false;
            if (enemy.IsFlying && target.A == null && target.B == null && target.Flying == null)
            { target.Flying = enemy; records[record] = target; }
            if (!enemy.IsFlying && target.Flying == null && target.A != enemy && target.B != enemy)
            {
                if (target.A == null) target.A = enemy;
                else if (target.B == null) target.B = enemy;
                records[record] = target;
            }
            bool leased=target.A==enemy||target.B==enemy||target.Flying==enemy;
            Vector3 goal;
            if(target.A==enemy||target.B==enemy)goal=tower.Position+side*(target.A==enemy?-1.82f:1.82f);
            else if(target.Flying==enemy){
                float radius=enemy.TowerBodyRadius+tower.MovementClearanceRadius(enemy.MinimumVisualY,enemy.MaximumVisualY)+.03f;
                goal=tower.Position-forward*Mathf.Max(1.10f,radius);
            }
            else goal=tower.Position-incoming*4.9f;
            if(leased){
                float padHalf=PadHalfClearance(enemy,tower);
                if(!SquareWaypoint(enemy.transform.position,tower.Position,goal,padHalf,Mathf.Max(1.72f,padHalf+.025f),out point))return false;
                Vector3 delta=goal-enemy.transform.position;delta.y=0;
                canStrike=delta.sqrMagnitude<.22f*.22f;
            }else point=goal;
            point.y = enemy.transform.position.y;
            return true;
        }

        static bool ValidCore(OracleEnemy enemy, OracleShrine core) => enemy != null && enemy.Targetable &&
            enemy.AttackCore == core && !enemy.IsRanged;

        internal bool ApproachCore(OracleEnemy enemy, OracleShrine core, Vector3 forward,
            out Vector3 point, out bool canStrike)
        {
            point = enemy.transform.position; canStrike = false;
            if (core == null || core.IsDestroyed || !core.gameObject.activeInHierarchy) { coreRecord = default; return false; }
            forward.y = 0f;
            if (forward.sqrMagnitude < .001f || enemy.IsRanged) return false;
            forward.Normalize();
            if (coreRecord.Core != core) coreRecord = new CoreRecord { Core = core, Forward = Cardinal(forward) };
            if (!ValidCore(coreRecord.A, core) || coreRecord.A.IsFlying) coreRecord.A = null;
            if (!ValidCore(coreRecord.B, core) || coreRecord.B.IsFlying) coreRecord.B = null;
            if (!ValidCore(coreRecord.Flying, core) || !coreRecord.Flying.IsFlying) coreRecord.Flying = null;
            Vector3 incoming=forward;
            forward = coreRecord.Forward; Vector3 side = Vector3.Cross(Vector3.up, forward);
            if (enemy.IsFlying && coreRecord.A == null && coreRecord.B == null && coreRecord.Flying == null) coreRecord.Flying = enemy;
            if (!enemy.IsFlying && coreRecord.Flying == null && coreRecord.A != enemy && coreRecord.B != enemy)
            {
                if (coreRecord.A == null) coreRecord.A = enemy;
                else if (coreRecord.B == null) coreRecord.B = enemy;
            }
            Vector3 center = core.transform.position;
            bool leased = coreRecord.A == enemy || coreRecord.B == enemy || coreRecord.Flying == enemy;
            Vector3 goal=coreRecord.Flying==enemy?center-forward*1.82f:
                coreRecord.A==enemy||coreRecord.B==enemy?center+side*(coreRecord.A==enemy?-1.82f:1.82f):center-incoming*4.9f;
            if(leased){
                float half=.9f+enemy.NavigationRadius+.04f;
                if(!SquareWaypoint(enemy.transform.position,center,goal,half,1.84f,out point))return false;
                Vector3 delta=goal-enemy.transform.position;delta.y=0;
                canStrike=delta.sqrMagnitude<.22f*.22f;
            }else point=goal;
            point.y = enemy.transform.position.y;
            return true;
        }
        // # ARCHITECTURE SNAPSHOT
        // Bounded local route around ONE authored axis-aligned objective/pad, not a NavMesh.
        // Four value corners/16 candidate pairs; no arrays/collections, physics or damage ownership.
        // Physics CanMove and live crowd/tower sweep remain the final authority on every step.
        static Vector3 Cardinal(Vector3 f)=>Mathf.Abs(f.x)>Mathf.Abs(f.z)?
            new Vector3(f.x<0?-1:1,0,0):new Vector3(0,0,f.z<0?-1:1);
        static float PadHalfClearance(OracleEnemy enemy,PlacedElementShrine tower)
        {
            // Include the existing logical tower sweep even on current flat pads.
            float half=enemy.TowerBodyRadius+tower.MovementClearanceRadius(enemy.MinimumVisualY,enemy.MaximumVisualY)+.02f;
            // Current old pads atY0 have no raised geometry; later three-course pad top isY.36.
            if(tower.Position.y<TowerPadPolicy.TopHeight-.025f)return half;
            float r=enemy.NavigationRadius,bottom=enemy.transform.position.y+enemy.NavigationBottom;
            for(int course=0;course<TowerPadPolicy.CourseCount;course++){
                float top=tower.Position.y+TowerPadPolicy.CourseCenterBelowTop(course)+TowerPadPolicy.CourseHeight*.5f;
                float vertical=Mathf.Max(0,bottom-top);
                if(vertical>=r)continue;
                float projected=Mathf.Sqrt(Mathf.Max(0,r*r-vertical*vertical));
                half=Mathf.Max(half,TowerPadPolicy.CourseWidth(course)*.5f+projected+.04f);
            }return half;
        }
        internal static bool SquareWaypoint(Vector3 origin,Vector3 center,Vector3 goal,float half,float corner,out Vector3 point)
        {
            point=origin;Vector3 a=origin-center,b=goal-center;a.y=b.y=0;
            if(half<=0||ClearSegment(a,b,half)){point=goal;return true;}
            // Unsupported actor/goal or starts inside this expanded footprint need native recovery.
            if(Mathf.Abs(b.x)<half&&Mathf.Abs(b.z)<half||Mathf.Abs(a.x)<half&&Mathf.Abs(a.z)<half||corner<=half)return false;
            float best=float.PositiveInfinity;Vector3 chosen=Vector3.zero;bool found=false;
            for(int i=0;i<4;i++){
                Vector3 first=Corner(i,corner);if(!ClearSegment(a,first,half))continue;
                if(ClearSegment(first,b,half)){float cost=(a-first).magnitude+(first-b).magnitude;if(cost<best){best=cost;chosen=first;found=true;}}
                for(int j=0;j<4;j++){
                    if(i==j)continue;Vector3 second=Corner(j,corner);
                    if(!ClearSegment(first,second,half)||!ClearSegment(second,b,half))continue;
                    float cost=(a-first).magnitude+(first-second).magnitude+(second-b).magnitude;
                    if(cost<best){
                        Vector3 next=first;
                        if((a-first).sqrMagnitude<.012f*.012f && ClearSegment(a,second,half))next=second;
                        best=cost;chosen=next;found=true;
                    }
                }
            }
            if(found){point=center+chosen;point.y=origin.y;}return found;
        }
        static Vector3 Corner(int i,float r)=>new Vector3((i&1)==0?-r:r,0,(i&2)==0?-r:r);
        internal static bool ClearSegment(Vector3 a,Vector3 b,float half)
        {
            float enter=0,exit=1;
            return !Slab(a.x,b.x-a.x,half,ref enter,ref exit)||!Slab(a.z,b.z-a.z,half,ref enter,ref exit)||exit<=enter;
        }
        static bool Slab(float position,float delta,float half,ref float enter,ref float exit)
        {
            if(Mathf.Abs(delta)<.0000001f)return Mathf.Abs(position)<half;
            float a=(-half-position)/delta,b=(half-position)/delta;if(a>b){float swap=a;a=b;b=swap;}
            enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);return exit>enter;
        }
    }
}
