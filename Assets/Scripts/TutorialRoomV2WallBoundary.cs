using System;
using System.Collections.Generic;
using P3=OdysseyOracle.TutorialRoomV2Layout.P3;
using R2=OdysseyOracle.TutorialRoomV2Layout.R2;

namespace OdysseyOracle
{
    // Cold authored wall union only. No Unity objects, frame loop, physics queries or native ownership.
    public sealed class TutorialRoomV2WallBoundary
    {
        public const float ConcealedJoinMargin=.001f;
        readonly ITutorialRoomV2Sink sink;
        readonly List<Solid> solids=new List<Solid>(80);
        readonly List<Face> faces=new List<Face>(1024);
        readonly struct Solid
        {
            public readonly P3 Min,Max;
            public readonly RoomV2Paint Paint;
            public readonly int FrontAxis,FrontSign;
            public readonly float FrontPlane;
            public Solid(P3 min,P3 max,RoomV2Paint paint,int frontAxis=-1,int frontSign=0,float frontPlane=0)
            {Min=min;Max=max;Paint=paint;FrontAxis=frontAxis;FrontSign=frontSign;FrontPlane=frontPlane;}
        }
        readonly struct Face
        {
            public readonly int Axis,Sign;
            public readonly float Plane;
            public readonly R2 Rect;
            public readonly RoomV2Paint Paint;
            public Face(int axis,int sign,float plane,R2 rect,RoomV2Paint paint)
            {Axis=axis;Sign=sign;Plane=plane;Rect=rect;Paint=paint;}
            public Face With(R2 r)=>new Face(Axis,Sign,Plane,r,Paint);
        }
        public TutorialRoomV2WallBoundary(ITutorialRoomV2Sink target)
        {sink=target??throw new ArgumentNullException(nameof(target));}
        public void Panel(int axis,int sign,float plane,float min,float max,float bottom,float top)
        {
            if(max-min<.0001f||top-bottom<.0001f)return;
            if((axis!=0&&axis!=2)||(sign!=-1&&sign!=1))throw new ArgumentOutOfRangeException();
            // The historical structural collider is retained exactly. Render volume fills its former
            // 32mm front return instead of adding a second, parallel front shell.
            P3 core=At(axis,plane+sign*.282f,(min+max)*.5f,(bottom+top)*.5f);
            P3 size=axis==0?new P3(.5f,top-bottom,max-min):new P3(max-min,top-bottom,.5f);
            sink.Collider("RoomV2 structural masonry",core,size);
            float back=plane+sign*.532f;
            P3 a=At(axis,Math.Min(plane,back),min,bottom),b=At(axis,Math.Max(plane,back),max,top);
            solids.Add(new Solid(a,b,RoomV2Paint.Stone,axis,-sign,plane));
        }
        public void Box(RoomV2Paint paint,P3 center,P3 size)
        {
            if(size.X<=0||size.Y<=0||size.Z<=0)throw new ArgumentOutOfRangeException(nameof(size));
            P3 half=size*.5f;solids.Add(new Solid(center-half,center+half,paint));
        }
        public void Finish()
        {
            if(solids.Count>128)throw new InvalidOperationException("Authored wall slab limit exceeded.");
            for(int i=0;i<solids.Count;i++)for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1})
            {
                Solid solid=solids[i];float plane=Coordinate(sign<0?solid.Min:solid.Max,axis);
                R2 rect=Projection(solid,axis);
                if(axis==solid.FrontAxis&&sign==solid.FrontSign&&plane==solid.FrontPlane)
                {
                    // Preserve the authored metre/0.8m original moss runs before union clipping.
                    for(float y=rect.MinV;y<rect.MaxV;){
                        float high=Math.Min(y+.8f,rect.MaxV),runStart=rect.MinU;
                        RoomV2Paint paint=RoomV2Paint.Stone;bool hasRun=false;
                        for(float u=rect.MinU;u<rect.MaxU;){
                            float end=Math.Min(u+1f,rect.MaxU);
                            RoomV2Paint next=Hash((int)(u*10),(int)(y*10),axis-solid.FrontSign+13)%13==0?RoomV2Paint.Moss:RoomV2Paint.Stone;
                            if(hasRun&&paint!=next){Expose(i,axis,sign,plane,new R2(runStart,y,u,high),paint);runStart=u;}
                            paint=next;hasRun=true;u=end;
                        }
                        if(hasRun)Expose(i,axis,sign,plane,new R2(runStart,y,rect.MaxU,high),paint);
                        y=high;
                    }
                }
                else Expose(i,axis,sign,plane,rect,solid.Paint);
            }
            Merge();
            foreach(Face face in faces)Emit(face);
        }
        void Expose(int owner,int axis,int sign,float plane,R2 rectangle,RoomV2Paint paint)
        {
            var remaining=new List<R2>{rectangle};
            float outside=plane+sign*ConcealedJoinMargin*.5f;
            for(int other=0;other<solids.Count&&remaining.Count>0;other++)
            {
                if(other==owner)continue;
                Solid neighbor=solids[other];float low=Coordinate(neighbor.Min,axis),high=Coordinate(neighbor.Max,axis);
                // A half-margin probe removes faces under genuine solid overlap/contact. If two
                // slabs share an exposed plane, the earlier record owns the overlap exactly once.
                bool covered=outside>low&&outside<high;
                float boundary=sign<0?low:high;
                covered|=other<owner&&Math.Abs(boundary-plane)<=.00001f;
                if(!covered)continue;
                R2 cut=Projection(neighbor,axis);var next=new List<R2>(remaining.Count+4);
                foreach(R2 r in remaining)Subtract(r,cut,next);
                remaining=next;
                if(remaining.Count>4096)throw new InvalidOperationException("Authored wall face fragmentation exceeded its bound.");
            }
            foreach(R2 r in remaining)faces.Add(new Face(axis,sign,plane,r,paint));
        }
        static void Subtract(R2 r,R2 cut,List<R2> output)
        {
            float u0=Math.Max(r.MinU,cut.MinU),u1=Math.Min(r.MaxU,cut.MaxU),v0=Math.Max(r.MinV,cut.MinV),v1=Math.Min(r.MaxV,cut.MaxV);
            if(u1<=u0||v1<=v0){output.Add(r);return;}
            if(u0>r.MinU)output.Add(new R2(r.MinU,r.MinV,u0,r.MaxV));
            if(u1<r.MaxU)output.Add(new R2(u1,r.MinV,r.MaxU,r.MaxV));
            if(v0>r.MinV)output.Add(new R2(u0,r.MinV,u1,v0));
            if(v1<r.MaxV)output.Add(new R2(u0,v1,u1,r.MaxV));
        }
        void Merge()
        {
            int previous;
            do {previous=faces.Count;MergeAxis(false);MergeAxis(true);}while(faces.Count<previous);
        }
        void MergeAxis(bool vertical)
        {
            faces.Sort((a,b)=>Compare(a,b,vertical));
            int written=0;
            for(int read=0;read<faces.Count;read++)
            {
                Face current=faces[read];
                if(written>0){Face last=faces[written-1];
                    bool same=last.Axis==current.Axis&&last.Sign==current.Sign&&last.Plane==current.Plane&&last.Paint==current.Paint;
                    bool adjacent=vertical?last.Rect.MinU==current.Rect.MinU&&last.Rect.MaxU==current.Rect.MaxU&&last.Rect.MaxV==current.Rect.MinV
                        :last.Rect.MinV==current.Rect.MinV&&last.Rect.MaxV==current.Rect.MaxV&&last.Rect.MaxU==current.Rect.MinU;
                    if(same&&adjacent){faces[written-1]=last.With(new R2(last.Rect.MinU,last.Rect.MinV,vertical?last.Rect.MaxU:current.Rect.MaxU,vertical?current.Rect.MaxV:last.Rect.MaxV));continue;}
                }
                faces[written++]=current;
            }
            if(written<faces.Count)faces.RemoveRange(written,faces.Count-written);
        }
        static int Compare(Face a,Face b,bool vertical)
        {
            int c=a.Axis.CompareTo(b.Axis);if(c!=0)return c;c=a.Sign.CompareTo(b.Sign);if(c!=0)return c;c=a.Plane.CompareTo(b.Plane);if(c!=0)return c;c=a.Paint.CompareTo(b.Paint);if(c!=0)return c;
            if(vertical){c=a.Rect.MinU.CompareTo(b.Rect.MinU);if(c!=0)return c;c=a.Rect.MaxU.CompareTo(b.Rect.MaxU);if(c!=0)return c;return a.Rect.MinV.CompareTo(b.Rect.MinV);}
            c=a.Rect.MinV.CompareTo(b.Rect.MinV);if(c!=0)return c;c=a.Rect.MaxV.CompareTo(b.Rect.MaxV);if(c!=0)return c;return a.Rect.MinU.CompareTo(b.Rect.MinU);
        }
        void Emit(Face face)
        {
            R2 r=face.Rect;P3 a=At(face.Axis,face.Plane,r.MinU,r.MinV),b=At(face.Axis,face.Plane,r.MaxU,r.MinV),c=At(face.Axis,face.Plane,r.MaxU,r.MaxV),d=At(face.Axis,face.Plane,r.MinU,r.MaxV);
            bool reverse=face.Axis==2?face.Sign<0:face.Sign>0;
            if(reverse){sink.Triangle(face.Paint,a,c,b);sink.Triangle(face.Paint,a,d,c);}
            else{sink.Triangle(face.Paint,a,b,c);sink.Triangle(face.Paint,a,c,d);}
        }
        static float Coordinate(P3 p,int axis)=>axis==0?p.X:axis==1?p.Y:p.Z;
        static R2 Projection(Solid s,int axis)=>axis==0?new R2(s.Min.Z,s.Min.Y,s.Max.Z,s.Max.Y):axis==1?new R2(s.Min.X,s.Min.Z,s.Max.X,s.Max.Z):new R2(s.Min.X,s.Min.Y,s.Max.X,s.Max.Y);
        static P3 At(int axis,float n,float u,float v)=>axis==0?new P3(n,v,u):axis==1?new P3(u,n,v):new P3(u,v,n);
        static uint Hash(int x,int z,int seed){unchecked{uint n=(uint)(x*374761393+z*668265263+seed*982451653);return(n^(n>>13))*1274126177u;}}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original Y-up RoomV2 metres; exposed planes unchanged, 1mm probe strictly inside concealed joins.
// Array Formats: bounded cold scalar solid/face records; neighboring occupied face rectangles are removed and merged before emission.
// Dependencies/Pointers: ITutorialRoomV2Sink borrows output; exact structural colliders/floors retained; no UnityEngine object API, frame allocation, persistent native buffer or global geometry guarantee.
