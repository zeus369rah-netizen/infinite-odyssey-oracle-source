using System;
using System.Collections.Generic;
using P3=OdysseyOracle.TutorialRoomV2Layout.P3;
using R2=OdysseyOracle.TutorialRoomV2Layout.R2;

namespace OdysseyOracle
{
    public enum RoomV2Paint { Stone,PaleStone,DeepStone,Moss,Floor,Wood,Iron,Brass,Green,Azure,Cloth,Beam,Wax,FlameCore }
    public interface ITutorialRoomV2Sink
    {
        void Box(RoomV2Paint paint,P3 center,P3 size,float yaw=0,float pitch=0,float roll=0);
        void Triangle(RoomV2Paint paint,P3 a,P3 b,P3 c);
        void Collider(string name,P3 center,P3 size);
    }
    // # ARCHITECTURE SNAPSHOT
    // Cold original geometry recipe. Pure scalar loops feed borrowed World batches or a reusable prefab baker.
    // Structural stone cores and independent simple collision are authored together. Metadata apertures,
    // grille members and anchors originate in Layout; no frame loop, object-per-block or light exists here.
    public static class TutorialRoomV2Geometry
    {
        const int CircleSegments=64;
        readonly struct D2 {public readonly double X,Z;public D2(double x,double z){X=x;Z=z;}}
        public static void Build(TutorialRoomV2Layout layout,ITutorialRoomV2Sink sink,bool hasWorkbench=false,bool hasLanterns=false)
        {
            if(layout==null||sink==null)throw new ArgumentNullException("Room layout/sink required.");
            BuildFloors(layout,sink);BuildWalls(layout,sink);BuildVault(layout,sink);BuildWindows(layout,sink);BuildDressing(layout,sink,hasWorkbench,hasLanterns);
        }
        static void BuildFloors(TutorialRoomV2Layout l,ITutorialRoomV2Sink s)
        {
            P3 circle=l.Anchor(TutorialRoomV2Layout.AnchorId.Shrine);
            foreach(R2 room in l.Rooms){
                float width=room.MaxU-room.MinU,depth=room.MaxV-room.MinV;
                s.Box(RoomV2Paint.Floor,new P3((room.MinU+room.MaxU)*.5f,-.18f,(room.MinV+room.MaxV)*.5f),new P3(width,.3f,depth));
                s.Collider("RoomV2 continuous floor",new P3((room.MinU+room.MaxU)*.5f,-.15f,(room.MinV+room.MaxV)*.5f),new P3(width,.3f,depth));
                for(float z=room.MinV;z<room.MaxV-.001f;z+=1f)for(float x=room.MinU;x<room.MaxU-.001f;x+=1f){
                    float right=Math.Min(x+1f,room.MaxU),back=Math.Min(z+1f,room.MaxV);
                    var polygon=new List<D2>{new D2(x+.001,z+.001),new D2(x+.001,back-.001),new D2(right-.001,back-.001),new D2(right-.001,z+.001)};
                    RoomV2Paint paint=Hash((int)(x*10),(int)(z*10),17)%19==0?RoomV2Paint.Moss:RoomV2Paint.Floor;
                    EmitOutsideCircle(polygon,circle,TutorialRoomV2Layout.CircleRadius,paint,s);
                }
            }
            // SharedY0, disjoint fan/annulus sectors: a broad flat dais with no overlapping top planes.
            for(int i=0;i<CircleSegments;i++){
                double a=i*Math.PI*2/CircleSegments,b=(i+1)*Math.PI*2/CircleSegments;
                P3 innerA=Radial(circle,3.10f,a),innerB=Radial(circle,3.10f,b),outerA=Radial(circle,TutorialRoomV2Layout.CircleRadius,a),outerB=Radial(circle,TutorialRoomV2Layout.CircleRadius,b);
                s.Triangle(RoomV2Paint.Stone,circle,innerB,innerA);
                RoomV2Paint rim=i%8==0?RoomV2Paint.Brass:RoomV2Paint.PaleStone;
                s.Triangle(rim,innerA,innerB,outerB);s.Triangle(rim,innerA,outerB,outerA);
            }
        }
        static P3 Radial(P3 center,float radius,double angle)=>new P3(center.X+(float)Math.Cos(angle)*radius,0,center.Z+(float)Math.Sin(angle)*radius);
        static void EmitOutsideCircle(List<D2> polygon,P3 center,double radius,RoomV2Paint paint,ITutorialRoomV2Sink sink)
        {
            if(polygon[2].X<center.X-radius||polygon[0].X>center.X+radius||polygon[2].Z<center.Z-radius||polygon[0].Z>center.Z+radius){EmitFloor(polygon,paint,sink);return;}
            double apothem=radius*Math.Cos(Math.PI/CircleSegments);
            for(int edge=0;edge<CircleSegments&&polygon.Count>0;edge++){
                double angle=(edge+.5)*Math.PI*2/CircleSegments,nx=Math.Cos(angle),nz=Math.Sin(angle),distance=apothem+nx*center.X+nz*center.Z;
                List<D2> outside=Clip(polygon,nx,nz,distance,false);EmitFloor(outside,paint,sink);
                polygon=Clip(polygon,nx,nz,distance,true);
            }
        }
        static List<D2> Clip(List<D2> input,double nx,double nz,double distance,bool inside)
        {
            var output=new List<D2>(8);if(input.Count==0)return output;D2 previous=input[input.Count-1];double before=previous.X*nx+previous.Z*nz-distance;bool was=inside?before<=0:before>=0;
            foreach(D2 current in input){double after=current.X*nx+current.Z*nz-distance;bool keep=inside?after<=0:after>=0;
                if(was!=keep){double t=before/(before-after);output.Add(new D2(previous.X+(current.X-previous.X)*t,previous.Z+(current.Z-previous.Z)*t));}
                if(keep)output.Add(current);previous=current;before=after;was=keep;
            }return output;
        }
        static void EmitFloor(List<D2> polygon,RoomV2Paint paint,ITutorialRoomV2Sink sink)
        {
            if(polygon.Count<3)return;P3 a=new P3((float)polygon[0].X,0,(float)polygon[0].Z);
            for(int i=1;i+1<polygon.Count;i++){
                P3 b=new P3((float)polygon[i].X,0,(float)polygon[i].Z),c=new P3((float)polygon[i+1].X,0,(float)polygon[i+1].Z);
                float up=(b.Z-a.Z)*(c.X-a.X)-(b.X-a.X)*(c.Z-a.Z);
                if(up<-.00001f)throw new InvalidOperationException("Floor clipping reversed a nondegenerate face.");
                if(up>.00000001f)sink.Triangle(paint,a,b,c);
            }
        }
        static void BuildWalls(TutorialRoomV2Layout l,ITutorialRoomV2Sink s)
        {
            var walls=new TutorialRoomV2WallBoundary(s);
            foreach(int side in new[]{-1,1}){
                float cursor=TutorialRoomV2Layout.StartZ;
                foreach(float c in l.AlcoveCenters){walls.Panel(0,side,side*l.HalfWidth,cursor,c-TutorialRoomV2Layout.DoorHalfWidth,0,l.CeilingY);cursor=c+TutorialRoomV2Layout.DoorHalfWidth;}
                walls.Panel(0,side,side*l.HalfWidth,cursor,TutorialRoomV2Layout.EndZ,0,l.CeilingY);
                foreach(float c in l.AlcoveCenters){
                    walls.Panel(0,side,side*l.HalfWidth,c-TutorialRoomV2Layout.DoorHalfWidth,c+TutorialRoomV2Layout.DoorHalfWidth,TutorialRoomV2Layout.DoorHeight,l.CeilingY);
                    float min=side<0?-l.OuterX:l.HalfWidth,max=side<0?-l.HalfWidth:l.OuterX;
                    foreach(int end in new[]{-1,1})walls.Panel(2,end,c+end*TutorialRoomV2Layout.AlcoveHalfLength,min,max,0,l.CeilingY);
                    if(side<0)walls.Panel(0,side,side*l.OuterX,c-TutorialRoomV2Layout.AlcoveHalfLength,c+TutorialRoomV2Layout.AlcoveHalfLength,0,l.CeilingY);
                    else{
                        walls.Panel(0,1,l.OuterX,c-TutorialRoomV2Layout.AlcoveHalfLength,c-TutorialRoomV2Layout.WindowHalfWidth,0,l.CeilingY);
                        walls.Panel(0,1,l.OuterX,c+TutorialRoomV2Layout.WindowHalfWidth,c+TutorialRoomV2Layout.AlcoveHalfLength,0,l.CeilingY);
                        walls.Panel(0,1,l.OuterX,c-TutorialRoomV2Layout.WindowHalfWidth,c+TutorialRoomV2Layout.WindowHalfWidth,0,TutorialRoomV2Layout.WindowBottom);
                        walls.Panel(0,1,l.OuterX,c-TutorialRoomV2Layout.WindowHalfWidth,c+TutorialRoomV2Layout.WindowHalfWidth,TutorialRoomV2Layout.WindowTop,l.CeilingY);
                    }
                    // Framing stays outside the explicit rectangular clear aperture.
                    foreach(int end in new[]{-1,1})walls.Box(RoomV2Paint.PaleStone,new P3(side*l.HalfWidth,2.6f,c+end*2.49f),new P3(.72f,5.2f,.38f));
                    walls.Box(RoomV2Paint.PaleStone,new P3(side*l.HalfWidth,5.4f,c),new P3(.76f,.4f,4.6f));
                    for(int step=0;step<3;step++)foreach(int end in new[]{-1,1})walls.Box(RoomV2Paint.DeepStone,new P3(side*l.HalfWidth,5.8f+step*.26f,c+end*(1.7f-step*.6f)),new P3(.8f,.3f,.9f));
                }
            }
            walls.Panel(2,-1,-13.95f,-l.HalfWidth,l.HalfWidth,0,l.CeilingY);walls.Panel(2,1,27.85f,-l.HalfWidth,l.HalfWidth,0,l.CeilingY);
            walls.Finish();
        }
        static void BuildVault(TutorialRoomV2Layout l,ITutorialRoomV2Sink s)
        {
            var vault=new TutorialRoomV2WallBoundary(s);
            foreach(R2 room in l.Rooms)vault.Box(RoomV2Paint.Stone,new P3((room.MinU+room.MaxU)*.5f,l.CeilingY+.282f,(room.MinV+room.MaxV)*.5f),new P3(room.MaxU-room.MinU,.5f,room.MaxV-room.MinV));
            foreach(float z in l.PierRows)foreach(int side in new[]{-1,1}){
                float x=side*l.PierX;
                s.Box(RoomV2Paint.Stone,new P3(x,3.95f,z),new P3(.90f,7.9f,.94f));
                s.Collider("RoomV2 stone pier",new P3(x,3.95f,z),new P3(.955f,7.9f,1f));
                s.Collider("RoomV2 supported pier foot",new P3(x,.15f,z),new P3(1.4f,.3f,1.45f));
                s.Collider("RoomV2 supported pier plinth",new P3(x,.45f,z),new P3(1.18f,.3f,1.26f));
                s.Box(RoomV2Paint.DeepStone,new P3(x,.15f,z),new P3(1.4f,.3f,1.45f));
                s.Box(RoomV2Paint.PaleStone,new P3(x,.45f,z),new P3(1.18f,.3f,1.26f));
                for(int row=0;row<13;row++)s.Box(row%4==0?RoomV2Paint.PaleStone:RoomV2Paint.Stone,new P3(x,.88f+row*.54f,z),new P3(.955f,.538f,1f));
                s.Box(RoomV2Paint.DeepStone,new P3(x,7.87f,z),new P3(1.2f,.28f,1.28f));
                s.Box(RoomV2Paint.PaleStone,new P3(x,8.13f,z),new P3(1.48f,.24f,1.52f));
                s.Box(RoomV2Paint.DeepStone,new P3(x-side*.48f,2.7f,z),new P3(.055f,2.8f,.4f));
                s.Box(RoomV2Paint.Brass,new P3(x-side*.52f,3.82f,z),new P3(.055f,.07f,.5f));
                for(int block=0;block<7;block++){float along=(l.PierX-.35f)*(1f-block/7f);float y=8.18f+block*(l.CeilingY-.35f-8.18f)/6f;vault.Box(block%2==0?RoomV2Paint.Stone:RoomV2Paint.PaleStone,new P3(side*along,y,z),new P3((l.PierX-.35f)/7f+.09f,.6f,1.18f));}
                // Restrained moss runs cling to existing stone, avoiding the mounted fire panel.
                for(int leaf=0;leaf<7;leaf++)s.Box(leaf%2==0?RoomV2Paint.Moss:RoomV2Paint.Green,new P3(x+side*.50f,7.0f-leaf*.42f,z+.36f),new P3(.065f,.22f,.16f),0,0,side*12f);
            }
            // Width derives from the last arch step; each supported hall width keeps a25mm overlap.
            float keyWidth=Math.Max(.85f,(l.PierX-.35f)/7f-.04f);
            foreach(float z in l.PierRows)vault.Box(RoomV2Paint.PaleStone,new P3(0,l.CeilingY-.08f,z),new P3(keyWidth,.55f,1.2f));
            vault.Finish();
            foreach(float z in new[]{-1.3f,10.8f,21f}){
                float y=z>10f&&z<11f?5.7f:5.9f;
                int links=(int)Math.Ceiling((l.CeilingY-y-.08f)/.17f)+1;
                for(int link=0;link<links;link++){
                    float cy=y+.10f+link*.17f,yaw=(link%2)*90;
                    s.Box(RoomV2Paint.Iron,new P3(0,cy-.085f,z),new P3(.14f,.025f,.025f),yaw);
                    s.Box(RoomV2Paint.Iron,new P3(0,cy+.085f,z),new P3(.14f,.025f,.025f),yaw);
                    foreach(int side in new[]{-1,1})s.Box(RoomV2Paint.Iron,new P3(link%2==0?side*.0575f:0,cy,z+(link%2==0?0:side*.0575f)),new P3(.025f,.17f,.025f));
                }
                s.Box(RoomV2Paint.Iron,new P3(0,y,z),new P3(1.52f,.09f,.14f));s.Box(RoomV2Paint.Iron,new P3(0,y,z),new P3(.14f,.09f,1.52f));
                for(int arm=0;arm<4;arm++){
                    float a=arm*(float)Math.PI*.5f;P3 p=new P3((float)Math.Cos(a)*.7f,y,(float)Math.Sin(a)*.7f+z);
                    s.Box(RoomV2Paint.Brass,p+new P3(0,-.02f,0),new P3(.23f,.09f,.23f));
                    s.Box(RoomV2Paint.Wax,p+new P3(0,.14f,0),new P3(.085f,.25f,.085f));s.Box(RoomV2Paint.FlameCore,p+new P3(0,.32f,0),new P3(.065f,.11f,.065f));
                }
            }
        }
        static void BuildWindows(TutorialRoomV2Layout l,ITutorialRoomV2Sink s)
        {
            foreach(float c in l.AlcoveCenters){
                float x=l.OuterX,mid=(TutorialRoomV2Layout.WindowBottom+TutorialRoomV2Layout.WindowTop)*.5f;
                foreach(int side in new[]{-1,1}){P3 frame=new P3(x-.15f,mid,c+side*1.82f),size=new P3(1.2f,6.2f,.16f);s.Box(RoomV2Paint.PaleStone,frame,size);s.Collider("RoomV2 window reveal side",frame,size);}
                foreach(float height in new[]{1.68f,7.72f}){P3 frame=new P3(x-.15f,height,c),size=new P3(1.2f,.16f,3.8f);s.Box(RoomV2Paint.PaleStone,frame,size);s.Collider("RoomV2 window reveal sill/lintel",frame,size);}
                for(int bar=0;bar<6;bar++){
                    float z=c-1.5f+bar*.6f;P3 center=new P3(x-.22f,4.7f,z),size=new P3(.16f,5.88f,.10f);
                    s.Box(RoomV2Paint.Iron,center,size);s.Collider("RoomV2 solid window grille",center,size);
                    for(int strap=0;strap<5;strap++)s.Box(RoomV2Paint.Brass,new P3(x-.32f,2.35f+strap*1.15f,z),new P3(.035f,.075f,.075f));
                }
                for(int strap=0;strap<5;strap++){P3 rail=new P3(x-.27f,2.35f+strap*1.15f,c),size=new P3(.18f,.10f,3.48f);s.Box(RoomV2Paint.Iron,rail,size);s.Collider("RoomV2 solid window grille rail",rail,size);}
                foreach(int side in new[]{-1,1})for(int vine=0;vine<8;vine++)s.Box(vine%2==0?RoomV2Paint.Moss:RoomV2Paint.Green,new P3(x-.79f,7.5f-vine*.42f,c+side*1.85f),new P3(.10f,.24f,.12f),0,side*12f,0);
                // Cheap original dust flecks and translucent shafts; no volumetric fog/point lights.
                for(int dust=0;dust<12;dust++)s.Box(RoomV2Paint.Azure,new P3(x-.95f-(dust%4)*.55f,1.4f+(dust%5)*.7f,c-1.1f+(dust%3)*.7f),new P3(.012f,.012f,.012f));
                P3 a=new P3(x-.74f,7.1f,c-1.35f),b=new P3(x-.74f,7.1f,c+1.35f),d=new P3(l.HalfWidth-2.2f,.035f,c-1.7f),e=new P3(l.HalfWidth-2.2f,.035f,c+1.7f);
                s.Triangle(RoomV2Paint.Beam,a,b,e);s.Triangle(RoomV2Paint.Beam,a,e,d);
            }
        }
        static void BuildDressing(TutorialRoomV2Layout l,ITutorialRoomV2Sink s,bool hasWorkbench,bool hasLanterns)
        {
            // Three small flasks rest on the actual kit iron workplate's flat topY1.0695m.
            // Anchor_WorkSurfaceY1.08 is an interaction anchor, not a physical supporting plane.
            float tableX=-l.HalfWidth+1.55f;
            for(int flask=0;hasWorkbench&&flask<3;flask++){
                P3 p=new P3(tableX-.48f+flask*.14f,1.0695f,-7.92f);
                s.Box(flask==1?RoomV2Paint.Azure:RoomV2Paint.Wood,p+new P3(0,.12f,0),new P3(.12f,.24f,.12f));
                s.Box(RoomV2Paint.Brass,p+new P3(0,.245f,0),new P3(.11f,.035f,.11f));s.Box(RoomV2Paint.Wood,p+new P3(0,.31f,0),new P3(.065f,.10f,.065f));
                s.Box(RoomV2Paint.Brass,p+new P3(0,.37f,0),new P3(.085f,.035f,.085f));
            }
            // Original crossed weapons occupy the back of the eastern supply alcove, outside travel/guide pads.
            // Its plane runs along the outer wall, with all depth clear of the deep window sill.
            P3 rack=new P3(l.OuterX-1.0f,0,7.3f);
            foreach(int side in new[]{-1,1})s.Box(RoomV2Paint.Wood,RackPoint(rack,new P3(side*.65f,1.1f,0)),new P3(.12f,2.2f,.16f),90);
            foreach(float y in new[]{.32f,1.6f})s.Box(RoomV2Paint.Iron,RackPoint(rack,new P3(0,y,0)),new P3(1.5f,.08f,.10f),90);
            for(int weapon=0;weapon<3;weapon++){
                float x=-.45f+weapon*.45f;s.Box(RoomV2Paint.Wood,RackPoint(rack,new P3(x,1.05f,-.12f)),new P3(.055f,1.65f,.055f),90,0,weapon==1?8:-5);
                s.Box(RoomV2Paint.Iron,RackPoint(rack,new P3(x,1.94f,-.12f)),new P3(weapon==1?.28f:.09f,.30f,.055f),90);
                s.Box(RoomV2Paint.Brass,RackPoint(rack,new P3(x,1.75f,-.12f)),new P3(.12f,.055f,.075f),90);
            }
            // Chain links rise from each exact kit hanging-loop offset to the solid ceiling.
            foreach(TutorialRoomV2Layout.Placement prop in l.PropPlacements)if(hasLanterns&&prop.Id==TutorialRoomV2Layout.PropId.WallLantern){
                float sign=prop.Position.X<0?1:-1,x=prop.Position.X+sign*.49f,start=prop.Position.Y+.22f;
                int links=(int)Math.Ceiling((l.CeilingY-start)/.16f)+1;
                for(int link=0;link<links;link++){
                    float y=start+link*.16f,yaw=(link%2)*90;
                    s.Box(RoomV2Paint.Iron,new P3(x,y-.075f,prop.Position.Z),new P3(.14f,.025f,.025f),yaw);
                    s.Box(RoomV2Paint.Iron,new P3(x,y+.075f,prop.Position.Z),new P3(.14f,.025f,.025f),yaw);
                    // Side rails are axis-swapped on alternating links, preserving closed links.
                    foreach(int side in new[]{-1,1})s.Box(RoomV2Paint.Iron,new P3(x+(link%2==0?side*.0575f:0),y,prop.Position.Z+(link%2==0?0:side*.0575f)),new P3(.025f,.15f,.025f));
                }
            }
        }
        static P3 RackPoint(P3 origin,P3 local)=>origin+new P3(local.Z,local.Y,-local.X);
        static uint Hash(int x,int z,int seed){unchecked{uint n=(uint)(x*374761393+z*668265263+seed*982451653);return(n^(n>>13))*1274126177u;}}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact existing Y-up room lattice and supported furniture bases.
// Array Formats: Cold room mesh recipes; borrowed saved geometry and fixed colliders.
// Dependencies/Pointers: DungeonWorld owns render resources; Wall render slabs emit one exposed union boundary; exact original core colliders/floors retained. No global overlap/CSG guarantee.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact current RoomV2 roof/arch/key solids; only exposed union faces replace overlapping coplanar surfaces.
// Array Formats: Existing bounded97cold solid records in TutorialRoomV2WallBoundary; original geometry/material definitions and every collider call retained.
// Dependencies/Pointers: Existing sink/boundary/resource owners; no legacy arch, layout, wall/window/floor/dressing/light/profile/save or gameplay-authority changes. Root verifies actual mesh/render captures.
