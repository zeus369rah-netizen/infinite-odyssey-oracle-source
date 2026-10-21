using System;

namespace OracleNatureBoundary020
{
    public struct BoundaryPoint020
    {
        public double X,Z;
        public BoundaryPoint020(double x,double z){X=x;Z=z;}
    }
    public struct BoundaryQuad020
    {
        public BoundaryPoint020 A,B,C,D;
        public uint Key;
        public float Top;
    }
    public struct BoundaryEdge020
    {
        public BoundaryPoint020 A,B;
        public float TopMin,TopMax;
        public uint Id,SourceKey,Morton;
        public int Next,Mark;
    }
    public struct BoundaryNode020
    {
        public BoundaryPoint020 Min,Max;
        public int Start,Count,Left,Right,Depth;
    }
    public struct BoundaryScope020
    {
        public ulong Session,World,Root;
        public uint Epoch,Generation,WorldRevision,GeometryRevision;
    }
    public enum BoundaryStatus020:uint { None,Ready,Held,Input,Capacity,Topology,Scope,Tree }
    public struct BoundaryHeader020
    {
        public BoundaryScope020 Scope;
        public int EdgeCount,NodeCount,OuterLoops,Phase,SourceDiagnostic;
        public BoundaryStatus020 Status;
    }
    public struct BoundaryQuery020
    {
        public BoundaryScope020 Scope;
        public ulong Player;
        public BoundaryPoint020 Position;
        public uint Step;
        public byte Accepted,Held;
    }
    public struct BoundaryResult020
    {
        public BoundaryScope020 Scope;
        public ulong Player;
        public BoundaryPoint020 Closest;
        public uint Step,EdgeId,Visits;
        public float Distance;
        public BoundaryStatus020 Status;
    }

    // Pure blittable pointer kernel shared by the cold .NET oracle and NativeArray jobs.
    public static unsafe class WindBoundaryMath020
    {
        public const int Quads=69,Edges=512,Nodes=1023,Cuts=140,Points=1024,Stack=32;
        public const double Tolerance=.0000001; //0.1micrometre endpoint/interior ambiguity, not a world raster.
        static bool Finite(double a)=>!double.IsNaN(a)&&!double.IsInfinity(a);
        static bool Valid(BoundaryPoint020 p)=>Finite(p.X)&&Finite(p.Z)&&p.X>=-128&&p.X<128&&p.Z>=-128&&p.Z<128;
        static BoundaryPoint020 Add(BoundaryPoint020 a,BoundaryPoint020 b)=>new BoundaryPoint020(a.X+b.X,a.Z+b.Z);
        static BoundaryPoint020 Sub(BoundaryPoint020 a,BoundaryPoint020 b)=>new BoundaryPoint020(a.X-b.X,a.Z-b.Z);
        static BoundaryPoint020 Mul(BoundaryPoint020 a,double t)=>new BoundaryPoint020(a.X*t,a.Z*t);
        static double Dot(BoundaryPoint020 a,BoundaryPoint020 b)=>a.X*b.X+a.Z*b.Z;
        static double Cross(BoundaryPoint020 a,BoundaryPoint020 b)=>a.X*b.Z-a.Z*b.X;
        static double Length(BoundaryPoint020 a)=>Math.Sqrt(Dot(a,a));
        static double Clamp(double t)=>Math.Max(0,Math.Min(1,t));
        static bool Equal(BoundaryPoint020 a,BoundaryPoint020 b)=>a.X==b.X&&a.Z==b.Z;
        static bool Close(BoundaryPoint020 a,BoundaryPoint020 b){var d=Sub(a,b);return Dot(d,d)<=Tolerance*Tolerance;}
        static bool Less(BoundaryPoint020 a,BoundaryPoint020 b)=>a.X<b.X||a.X==b.X&&a.Z<b.Z;
        static BoundaryPoint020 Corner(in BoundaryQuad020 q,int n)=>n==0?q.A:n==1?q.B:n==2?q.C:q.D;
        public static bool ScopeValid(in BoundaryScope020 s)=>s.Session!=0&&s.World!=0&&s.Root!=0&&s.Epoch!=0&&s.Generation!=0&&s.WorldRevision!=0&&s.GeometryRevision!=0;
        public static bool Same(in BoundaryScope020 a,in BoundaryScope020 b)=>a.Session==b.Session&&a.World==b.World&&a.Root==b.Root&&a.Epoch==b.Epoch&&a.Generation==b.Generation&&a.WorldRevision==b.WorldRevision&&a.GeometryRevision==b.GeometryRevision;

        public static bool FromBox(uint key,float minX,float minZ,float maxX,float maxZ,float cosine,float sine,float top,out BoundaryQuad020 q)
        {
            q=default;
            if(key==0||!Finite(minX)||!Finite(minZ)||!Finite(maxX)||!Finite(maxZ)||!Finite(cosine)||!Finite(sine)||!Finite(top)||
                maxX-minX<.001||maxZ-minZ<.001||Math.Abs((double)cosine*cosine+(double)sine*sine-1)>.00001)return false;
            double cx=((double)minX+maxX)*.5,cz=((double)minZ+maxZ)*.5,hx=((double)maxX-minX)*.5,hz=((double)maxZ-minZ)*.5;
            q.Key=key;q.Top=top;
            // Inverse of the actual LandingBox.Local world-to-local transform.
            q.A=new BoundaryPoint020(cx-hx*cosine-hz*sine,cz+hx*sine-hz*cosine);
            q.B=new BoundaryPoint020(cx+hx*cosine-hz*sine,cz-hx*sine-hz*cosine);
            q.C=new BoundaryPoint020(cx+hx*cosine+hz*sine,cz-hx*sine+hz*cosine);
            q.D=new BoundaryPoint020(cx-hx*cosine+hz*sine,cz+hx*sine+hz*cosine);
            return QuadValid(in q);
        }
        static bool QuadValid(in BoundaryQuad020 q)
        {
            if(q.Key==0||!Finite(q.Top))return false;
            for(int i=0;i<4;i++)
            {
                var a=Corner(in q,i);var b=Corner(in q,(i+1)&3);var c=Corner(in q,(i+2)&3);
                if(!Valid(a)||Length(Sub(b,a))<.001||Cross(Sub(b,a),Sub(c,b))<=1e-10)return false;
            }
            return true;
        }
        static bool Clip(BoundaryPoint020 a,BoundaryPoint020 d,in BoundaryQuad020 q,out double lo,out double hi)
        {
            lo=0;hi=1;
            for(int i=0;i<4;i++)
            {
                var p=Corner(in q,i);var e=Sub(Corner(in q,(i+1)&3),p);
                double u=Cross(e,Sub(a,p)),v=Cross(e,d);
                if(Math.Abs(v)<1e-13){if(u < -Tolerance*Length(e))return false;continue;}
                double t=-u/v;
                if(v>0)lo=Math.Max(lo,t);else hi=Math.Min(hi,t);
                if(lo>hi+1e-13)return false;
            }
            if(hi<0||lo>1)return false;lo=Clamp(lo);hi=Clamp(hi);return lo<=hi;
        }
        static BoundaryStatus020 Exposed(BoundaryPoint020 p,BoundaryPoint020 direction,ulong own,BoundaryQuad020* q,int count,int source,out bool exposed)
        {
            exposed=true;double dl=Length(direction);
            for(int i=0;i<count;i++)
            {
                if(i==source)continue;
                bool closed=true,strict=true,aligned=false;
                for(int k=0;k<4;k++)
                {
                    var a=Corner(in q[i],k);var e=Sub(Corner(in q[i],(k+1)&3),a);
                    double distance=Cross(e,Sub(p,a))/Length(e);
                    if(distance < -Tolerance){closed=false;break;}
                    if(distance<=Tolerance)strict=false;
                }
                if(!closed)continue;
                if(strict){exposed=false;return BoundaryStatus020.Ready;}
                for(int k=0;k<4;k++)
                {
                    var a=Corner(in q[i],k);var e=Sub(Corner(in q[i],(k+1)&3),a);double el=Length(e);
                    if(Math.Abs(Cross(e,Sub(p,a))/el)>Tolerance||Math.Abs(Cross(e,direction))>1e-10*el*dl)continue;
                    aligned=true;
                    if(Dot(e,direction)<0||(((ulong)q[i].Key<<2)|(uint)k)<own){exposed=false;return BoundaryStatus020.Ready;}
                }
                if(!aligned)return BoundaryStatus020.Topology;
            }
            return BoundaryStatus020.Ready;
        }
        static bool Link(BoundaryEdge020* edges,int n)
        {
            for(int i=0;i<n;i++)
            {
                int next=-1,incoming=0;
                for(int j=0;j<n;j++)
                {
                    if(Equal(edges[i].B,edges[j].A)){if(next>=0)return false;next=j;}
                    if(Equal(edges[j].B,edges[i].A))incoming++;
                }
                if(next<0||incoming!=1)return false;edges[i].Next=next;
            }
            return true;
        }
        static void Remove(BoundaryEdge020* edges,int at,ref int n){for(int i=at;i<n-1;i++)edges[i]=edges[i+1];n--;}
        static bool EdgeLess(in BoundaryEdge020 a,in BoundaryEdge020 b)
        {
            if(!Equal(a.A,b.A))return Less(a.A,b.A);
            return Less(a.B,b.B);
        }
        static uint Morton(BoundaryPoint020 p)
        {
            uint x=(uint)((p.X+128)*4),z=(uint)((p.Z+128)*4),key=0;
            for(int i=0;i<10;i++)key|=((x>>i)&1u)<<(2*i)|((z>>i)&1u)<<(2*i+1);
            return key;
        }
        static bool EdgeValid(in BoundaryEdge020 e)=>e.Id!=0&&Valid(e.A)&&Valid(e.B)&&Length(Sub(e.B,e.A))>Tolerance&&Finite(e.TopMin)&&Finite(e.TopMax)&&e.TopMin<=e.TopMax;
        static BoundaryPoint020 Min(BoundaryPoint020 a,BoundaryPoint020 b)=>new BoundaryPoint020(Math.Min(a.X,b.X),Math.Min(a.Z,b.Z));
        static BoundaryPoint020 Max(BoundaryPoint020 a,BoundaryPoint020 b)=>new BoundaryPoint020(Math.Max(a.X,b.X),Math.Max(a.Z,b.Z));
        static bool BoxValid(in BoundaryNode020 n)=>Valid(n.Min)&&Valid(n.Max)&&n.Min.X<=n.Max.X&&n.Min.Z<=n.Max.Z;
        static bool Overlap(void* a,ulong aBytes,void* b,ulong bBytes)
        {
            ulong x=(ulong)a,y=(ulong)b;
            if(x==0||y==0||x>ulong.MaxValue-aBytes||y>ulong.MaxValue-bBytes)return true;
            return x<y+bBytes&&y<x+aBytes;
        }

        public static BoundaryStatus020 Build(BoundaryQuad020* quads,int quadCount,BoundaryEdge020* edges,int edgeCapacity,
            BoundaryNode020* nodes,int nodeCapacity,double* cuts,BoundaryPoint020* points,in BoundaryScope020 scope,out BoundaryHeader020 header)
        {
            header=new BoundaryHeader020{Scope=scope,Status=BoundaryStatus020.Input};
            if(quads==null||edges==null||nodes==null||cuts==null||points==null||!ScopeValid(in scope)||quadCount<1||quadCount>Quads||
                edgeCapacity<1||edgeCapacity>Edges||nodeCapacity<1||nodeCapacity>Nodes)return header.Status;
            ulong qb=(ulong)(quadCount*sizeof(BoundaryQuad020)),eb=(ulong)(edgeCapacity*sizeof(BoundaryEdge020)),nb=(ulong)(nodeCapacity*sizeof(BoundaryNode020)),cutBytes=(ulong)(Cuts*sizeof(double)),pb=(ulong)(Points*sizeof(BoundaryPoint020));
            if(Overlap(quads,qb,edges,eb)||Overlap(quads,qb,nodes,nb)||Overlap(quads,qb,cuts,cutBytes)||Overlap(quads,qb,points,pb)||
                Overlap(edges,eb,nodes,nb)||Overlap(edges,eb,cuts,cutBytes)||Overlap(edges,eb,points,pb)||Overlap(nodes,nb,cuts,cutBytes)||Overlap(nodes,nb,points,pb)||Overlap(cuts,cutBytes,points,pb))return header.Status;
            for(int i=0;i<quadCount;i++){if(!QuadValid(in quads[i]))return header.Status;for(int j=0;j<i;j++)if(quads[j].Key==quads[i].Key)return header.Status;}
            header.Phase=2;int n=0;
            for(int i=0;i<quadCount;i++)for(int k=0;k<4;k++)
            {
                header.SourceDiagnostic=i*4+k;var a=Corner(in quads[i],k);var d=Sub(Corner(in quads[i],(k+1)&3),a);double length=Length(d);
                int nc=2;cuts[0]=0;cuts[1]=1;
                for(int j=0;j<quadCount;j++)if(j!=i&&Clip(a,d,in quads[j],out double lo,out double hi))
                {if(nc+2>Cuts)return Fail(BoundaryStatus020.Capacity,ref header);cuts[nc++]=lo;cuts[nc++]=hi;}
                for(int j=1;j<nc;j++){double v=cuts[j];int at=j-1;while(at>=0&&cuts[at]>v){cuts[at+1]=cuts[at];at--;}cuts[at+1]=v;}
                int unique=1;for(int j=1;j<nc;j++)if((cuts[j]-cuts[unique-1])*length>Tolerance)cuts[unique++]=cuts[j];
                for(int j=0;j<unique-1;j++)
                {
                    if((cuts[j+1]-cuts[j])*length<=Tolerance)continue;
                    var midpoint=Add(a,Mul(d,(cuts[j]+cuts[j+1])*.5));
                    var status=Exposed(midpoint,d,((ulong)quads[i].Key<<2)|(uint)k,quads,quadCount,i,out bool exposed);
                    if(status!=BoundaryStatus020.Ready)return Fail(status,ref header);if(!exposed)continue;
                    if(n>=edgeCapacity)return Fail(BoundaryStatus020.Capacity,ref header);
                    edges[n++]=new BoundaryEdge020{A=Add(a,Mul(d,cuts[j])),B=Add(a,Mul(d,cuts[j+1])),SourceKey=quads[i].Key,TopMin=quads[i].Top,TopMax=quads[i].Top};
                }
            }
            header.Phase=3;if(n<3)return Fail(BoundaryStatus020.Topology,ref header);
            // Immutable original endpoints make representative choice independent of source iteration order.
            for(int i=0;i<n;i++){points[2*i]=edges[i].A;points[2*i+1]=edges[i].B;}
            for(int i=0;i<n;i++)
            {
                var a=points[2*i];var b=points[2*i+1];var ca=a;var cb=b;
                for(int j=0;j<2*n;j++){if(Close(a,points[j])&&Less(points[j],ca))ca=points[j];if(Close(b,points[j])&&Less(points[j],cb))cb=points[j];}
                edges[i].A=ca;edges[i].B=cb;
                if(Close(ca,cb))return Fail(BoundaryStatus020.Topology,ref header);
            }
            for(int i=0;i<n;i++)for(int j=i+1;j<n;j++)
            {
                if(Equal(edges[i].A,edges[j].B)&&Equal(edges[i].B,edges[j].A))return Fail(BoundaryStatus020.Topology,ref header);
                if(!Equal(edges[i].A,edges[j].A)||!Equal(edges[i].B,edges[j].B))continue;
                edges[i].SourceKey=Math.Min(edges[i].SourceKey,edges[j].SourceKey);edges[i].TopMin=Math.Min(edges[i].TopMin,edges[j].TopMin);edges[i].TopMax=Math.Max(edges[i].TopMax,edges[j].TopMax);
                Remove(edges,j,ref n);j--;
            }
            if(!Link(edges,n))return Fail(BoundaryStatus020.Topology,ref header);
            header.Phase=4;int loops=0,cycle=0;
            for(int i=0;i<n;i++)if(edges[i].Mark==0)
            {
                cycle++;int at=i,visited=0;double area=0;
                do{if(at<0||at>=n||edges[at].Mark!=0||++visited>n)return Fail(BoundaryStatus020.Topology,ref header);edges[at].Mark=cycle;area+=Cross(edges[at].A,edges[at].B);at=edges[at].Next;}while(at!=i);
                if(!Finite(area)||Math.Abs(area)<1e-10)return Fail(BoundaryStatus020.Topology,ref header);
                if(area>0)loops++;else{at=i;do{edges[at].Mark=-cycle;at=edges[at].Next;}while(at!=i);}
            }
            for(int i=n-1;i>=0;i--)if(edges[i].Mark<0)Remove(edges,i,ref n);
            if(n<3||!Link(edges,n))return Fail(BoundaryStatus020.Topology,ref header);
            header.Phase=5;int merges=0;bool changed;
            do
            {
                changed=false;
                for(int i=0;i<n;i++)
                {
                    int j=edges[i].Next;var a=Sub(edges[i].B,edges[i].A);var b=Sub(edges[j].B,edges[j].A);
                    if(j==i||Dot(a,b)<=0||Math.Abs(Cross(a,b))>1e-10*Length(a)*Length(b))continue;
                    edges[i].B=edges[j].B;edges[i].TopMin=Math.Min(edges[i].TopMin,edges[j].TopMin);edges[i].TopMax=Math.Max(edges[i].TopMax,edges[j].TopMax);edges[i].SourceKey=Math.Min(edges[i].SourceKey,edges[j].SourceKey);
                    Remove(edges,j,ref n);if(++merges>Edges||!Link(edges,n))return Fail(BoundaryStatus020.Topology,ref header);changed=true;break;
                }
            }while(changed);
            for(int i=1;i<n;i++){var v=edges[i];int j=i-1;while(j>=0&&EdgeLess(in v,in edges[j])){edges[j+1]=edges[j];j--;}edges[j+1]=v;}
            for(int i=0;i<n;i++){edges[i].Id=(uint)(i+1);edges[i].Morton=Morton(Mul(Add(edges[i].A,edges[i].B),.5));edges[i].Next=-1;edges[i].Mark=0;}
            for(int i=1;i<n;i++){var v=edges[i];int j=i-1;while(j>=0&&(edges[j].Morton>v.Morton||edges[j].Morton==v.Morton&&edges[j].Id>v.Id)){edges[j+1]=edges[j];j--;}edges[j+1]=v;}
            header.Phase=6;int required=2*n-1;if(required>nodeCapacity)return Fail(BoundaryStatus020.Capacity,ref header);
            int used=1;nodes[0]=new BoundaryNode020{Start=0,Count=n,Left=-1,Right=-1};
            for(int i=0;i<used;i++)if(nodes[i].Count>1)
            {
                int leftCount=nodes[i].Count/2,l=used++,r=used++;if(r>=required)return Fail(BoundaryStatus020.Tree,ref header);
                nodes[i].Left=l;nodes[i].Right=r;
                nodes[l]=new BoundaryNode020{Start=nodes[i].Start,Count=leftCount,Depth=nodes[i].Depth+1,Left=-1,Right=-1};
                nodes[r]=new BoundaryNode020{Start=nodes[i].Start+leftCount,Count=nodes[i].Count-leftCount,Depth=nodes[i].Depth+1,Left=-1,Right=-1};
            }
            for(int i=used-1;i>=0;i--)
            {
                if(nodes[i].Count==1){var e=edges[nodes[i].Start];nodes[i].Min=Min(e.A,e.B);nodes[i].Max=Max(e.A,e.B);}
                else{nodes[i].Min=Min(nodes[nodes[i].Left].Min,nodes[nodes[i].Right].Min);nodes[i].Max=Max(nodes[nodes[i].Left].Max,nodes[nodes[i].Right].Max);}
            }
            header.EdgeCount=n;header.NodeCount=used;header.OuterLoops=loops;header.Status=BoundaryStatus020.Ready;
            if(!Validate(edges,nodes,in header))return Fail(BoundaryStatus020.Tree,ref header);header.Phase=7;header.SourceDiagnostic=0;
            return header.Status;
        }
        static BoundaryStatus020 Fail(BoundaryStatus020 status,ref BoundaryHeader020 h){h.Status=status;h.EdgeCount=h.NodeCount=h.OuterLoops=0;return status;}

        public static bool Validate(BoundaryEdge020* edges,BoundaryNode020* nodes,in BoundaryHeader020 h)
        {
            if(edges==null||nodes==null||h.Status!=BoundaryStatus020.Ready||!ScopeValid(in h.Scope)||h.EdgeCount<3||h.EdgeCount>Edges||h.NodeCount!=2*h.EdgeCount-1||h.NodeCount>Nodes||h.OuterLoops<1)return false;
            byte* seen=stackalloc byte[Edges];for(int i=0;i<Edges;i++)seen[i]=0;
            for(int i=0;i<h.EdgeCount;i++)
            {
                if(!EdgeValid(in edges[i]))return false;
                if(i>0&&(edges[i].Morton<edges[i-1].Morton||edges[i].Morton==edges[i-1].Morton&&edges[i].Id<=edges[i-1].Id))return false;
                for(int j=0;j<i;j++)if(edges[j].Id==edges[i].Id)return false;
            }
            for(int i=0;i<h.NodeCount;i++)
            {
                ref BoundaryNode020 node=ref nodes[i];
                if(!BoxValid(in node)||node.Start<0||node.Count<1||node.Start+node.Count>h.EdgeCount||node.Depth<0||node.Depth>9)return false;
                if(i==0&&(node.Start!=0||node.Count!=h.EdgeCount||node.Depth!=0))return false;
                if(node.Count==1)
                {
                    if(node.Left!=-1||node.Right!=-1||seen[node.Start]++!=0)return false;
                    if(!Equal(node.Min,Min(edges[node.Start].A,edges[node.Start].B))||!Equal(node.Max,Max(edges[node.Start].A,edges[node.Start].B)))return false;
                }
                else
                {
                    if(!Children(nodes,in node,i,h.NodeCount))return false;
                    if(!Equal(node.Min,Min(nodes[node.Left].Min,nodes[node.Right].Min))||!Equal(node.Max,Max(nodes[node.Left].Max,nodes[node.Right].Max)))return false;
                }
            }
            for(int i=0;i<h.EdgeCount;i++)if(seen[i]!=1)return false;return true;
        }
        static bool Children(BoundaryNode020* nodes,in BoundaryNode020 n,int index,int count)
        {
            if(n.Left<=index||n.Right<=index||n.Left>=count||n.Right>=count||n.Left==n.Right)return false;
            ref BoundaryNode020 a=ref nodes[n.Left];ref BoundaryNode020 b=ref nodes[n.Right];
            return a.Start==n.Start&&a.Count==n.Count/2&&b.Start==a.Start+a.Count&&b.Count==n.Count-a.Count&&a.Depth==n.Depth+1&&b.Depth==n.Depth+1&&BoxValid(in a)&&BoxValid(in b);
        }
        static double BoxDistance(BoundaryPoint020 p,in BoundaryNode020 n)
        {
            double x=Math.Max(n.Min.X-p.X,Math.Max(0,p.X-n.Max.X)),z=Math.Max(n.Min.Z-p.Z,Math.Max(0,p.Z-n.Max.Z));return x*x+z*z;
        }
        public static double SegmentDistanceSquared(BoundaryPoint020 p,in BoundaryEdge020 e,out BoundaryPoint020 closest)
        {
            var d=Sub(e.B,e.A);double t=Clamp(Dot(Sub(p,e.A),d)/Dot(d,d));closest=Add(e.A,Mul(d,t));var delta=Sub(p,closest);return Dot(delta,delta);
        }
        public static BoundaryResult020 Nearest(BoundaryEdge020* edges,BoundaryNode020* nodes,in BoundaryHeader020 h,in BoundaryQuery020 q)
        {
            var result=new BoundaryResult020{Scope=q.Scope,Player=q.Player,Step=q.Step,Status=BoundaryStatus020.Input};
            if(!ScopeValid(in q.Scope)||!Same(in h.Scope,in q.Scope)){result.Status=BoundaryStatus020.Scope;return result;}
            if(q.Player==0||q.Step==0||q.Step==uint.MaxValue||q.Accepted!=1||q.Held>1||!Valid(q.Position))return result;
            if(edges==null||nodes==null||h.Status!=BoundaryStatus020.Ready||h.EdgeCount<3||h.EdgeCount>Edges||h.NodeCount!=2*h.EdgeCount-1||h.NodeCount>Nodes){result.Status=BoundaryStatus020.Tree;return result;}
            if(q.Held==1){result.Status=BoundaryStatus020.Held;return result;}
            int* stack=stackalloc int[Stack];int sp=1;stack[0]=0;double best=double.MaxValue;uint id=0;BoundaryPoint020 closest=default;
            while(sp>0)
            {
                int index=stack[--sp];if(index<0||index>=h.NodeCount||++result.Visits>(uint)h.NodeCount){result.Status=BoundaryStatus020.Tree;return result;}
                ref BoundaryNode020 node=ref nodes[index];
                if(!BoxValid(in node)||node.Start<0||node.Count<1||node.Start+node.Count>h.EdgeCount||node.Depth<0||node.Depth>9){result.Status=BoundaryStatus020.Tree;return result;}
                if(BoxDistance(q.Position,in node)>best)continue;
                if(node.Count==1)
                {
                    if(node.Left!=-1||node.Right!=-1||!EdgeValid(in edges[node.Start])){result.Status=BoundaryStatus020.Tree;return result;}
                    double d=SegmentDistanceSquared(q.Position,in edges[node.Start],out var at);
                    if(d<best||d==best&&edges[node.Start].Id<id){best=d;id=edges[node.Start].Id;closest=at;}
                }
                else
                {
                    if(!Children(nodes,in node,index,h.NodeCount)||sp+2>Stack){result.Status=BoundaryStatus020.Tree;return result;}
                    double l=BoxDistance(q.Position,in nodes[node.Left]),r=BoxDistance(q.Position,in nodes[node.Right]);
                    if(l<=r){stack[sp++]=node.Right;stack[sp++]=node.Left;}else{stack[sp++]=node.Left;stack[sp++]=node.Right;}
                }
            }
            double distance=Math.Sqrt(best);
            if(id==0||!Finite(distance)||!Valid(closest)){result.Status=BoundaryStatus020.Tree;return result;}
            result.Closest=closest;result.EdgeId=id;result.Distance=(float)distance;result.Status=BoundaryStatus020.Ready;return result;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact analytic World-XZ union of accepted yaw-correct convex floor quads; no obstacle or island-AABB exposure approximation.
// Array Formats:69quads,140cuts,1024endpoint scratch,512edges,1023Morton-rangeBVH nodes and32query stack entries in blittable values.
// Dependencies/Pointers: Unsafe pointer kernel requires caller-validated distinct capacities and cold/live staging ownership; NativeArray jobs wrap it without Object APIs. Root owns exact reference identity/fences/publication and audio consumption.
