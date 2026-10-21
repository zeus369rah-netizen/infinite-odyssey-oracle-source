using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace OdysseyOracle
{
    public struct LandingDraw020 {public float3 Center,Size;public quaternion Rotation;public float4 Color;public float Emission,Floor;}
    public struct LandingVertex020 {public float3 Position,Normal;public float4 Color;public float2 Detail;}
    public static class PortalLandingPrototypeGeometry020
    {
        public const int MaximumDraws=3072,MaximumVertices=MaximumDraws*24,MaximumIndices=MaximumDraws*36,TorchCount=8;
        public static bool TryTorch(int index,out float3 point)
        {
            point=default;if(!PortalLandingFixtures020.TryTorch(index,out var value))return false;
            point=new float3(value.X,value.Y,value.Z);return true;
        }
    }
    [BurstCompile]
    public struct PortalLandingRecipeJob020:IJob,ICampaignBuildingSink020
    {
        public NativeArray<LandingDraw020> Draws;
        [ReadOnly]public NativeArray<CampaignPlantPatchAdmission020> PlantAdmissions;
        public ulong PlantWorldToken;
        public uint PlantGeneration,PlantWorldRevision,PlantGeometryRevision;
        public NativeArray<int> Count;
        int written;bool overflow;
        static readonly float4 Stone=new float4(.29f,.31f,.34f,1),Pale=new float4(.42f,.43f,.44f,1),Wood=new float4(.19f,.10f,.055f,1),Warm=new float4(1f,.48f,.12f,1),Cyan=new float4(.08f,.65f,1f,1),Pink=new float4(1f,.04f,.72f,1);
        static readonly float4 Iron=new float4(.19f,.22f,.25f,1),Bronze=new float4(.34f,.17f,.055f,1);
        static readonly float3 FlameOuter=new float3(.95f,.20f,.035f),FlameCore=new float3(1f,.56f,.14f),EmberColor=new float3(1f,.29f,.045f),SmokeColor=new float3(.20f,.18f,.16f);
        void Add(float3 center,float3 size,float4 color,float emission=0,float floor=0,quaternion rotation=default)
        {
            if(written>=Draws.Length||!math.all(math.isfinite(center))||!math.all(math.isfinite(size))||math.any(size<=0)||!math.all(math.isfinite(color))||!math.isfinite(emission)||!math.isfinite(floor)){overflow=true;return;}
            if(math.lengthsq(rotation.value)<.5f)rotation=quaternion.identity;
            Draws[written++]=new LandingDraw020{Center=center,Size=size,Rotation=rotation,Color=color,Emission=emission,Floor=floor};
        }
        void Box(in LandingBox020 box,float4 color,float floor=0)
        {Add(new float3((box.MinX+box.MaxX)*.5f,(box.MinY+box.MaxY)*.5f,(box.MinZ+box.MaxZ)*.5f),new float3(box.MaxX-box.MinX,box.MaxY-box.MinY,box.MaxZ-box.MinZ),color,0,floor,quaternion.RotateY(math.radians(box.Yaw)));}
        public void Box(float3 center,float3 size,quaternion rotation,float4 color,float emission)
        {Add(center,size,color,emission,0,rotation);}
        static float Seed(int fixture,int member)
        {
            uint value;
            unchecked{value=(uint)(fixture+1)*747796405u+(uint)(member+3)*2891336453u;value^=value>>16;value*=2246822519u;value^=value>>13;}
            return (value&0x00ffffffu)*(1f/16777215f);
        }
        static float MotionTag(int kind,float seed)
        {return kind==1?.05f+.14f*seed:kind==2?.30f+.14f*seed:.56f+.14f*seed;}
        void MotionBox(float3 center,float3 size,float3 rgb,float emission,int kind,float seed,float sourceY=0f)
        {Add(center,size,new float4(rgb,MotionTag(kind,seed)),emission,sourceY);}
        void RaiseFixture(int fixture,int first)
        {
            if(written-first!=(fixture<6?23:7)||!PortalLandingFixtures020.TryPose(fixture,out var pose)){overflow=true;return;}
            // Leave the two grounded base/cap commands fixed. Only cold presentation grows.
            for(int item=first+2;item<written;item++)
            {
                var draw=Draws[item];
                draw.Center.y=pose.FloorY+(draw.Center.y-pose.FloorY)*1.5f;
                draw.Size.y*=1.5f;
                if(draw.Color.w<.24f)draw.Floor=pose.FloorY+(draw.Floor-pose.FloorY)*1.5f;
                Draws[item]=draw;
            }
        }
        void EmitStairTorch(int index,float3 point)
        {
            // An open iron cage grows from the existing wooden brazier. It stays inside the rail gap.
            for(int arm=0;arm<4;arm++)
            {
                float sx=(arm&1)==0?-.385f:.385f,sz=(arm&2)==0?-.385f:.385f;
                Add(point+new float3(sx,-.075f,sz),new float3(.07f,.52f,.07f),Iron);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Add(point+new float3(0,-.3125f,side*.385f),new float3(.77f,.055f,.055f),Bronze);
                Add(point+new float3(side*.385f,-.3125f,0),new float3(.055f,.055f,.77f),Bronze);
            }

            float sourceY=point.y-.32f;
            for(int tongue=0;tongue<3;tongue++)
            {
                float x=tongue==1?-.16f:tongue==2?.16f:0f;
                float z=tongue==1?.10f:tongue==2?-.10f:0f;
                bool core=tongue==0;float seed=Seed(index,10+tongue);
                float3 flameOrigin=new float3(point.x+x,sourceY,point.z+z);
                if(core)
                {
                    MotionBox(flameOrigin+new float3(0,.27f,0),new float3(.28f,.54f,.28f),FlameOuter,1.7f,1,seed,sourceY);
                    MotionBox(flameOrigin+new float3(0,.78f,0),new float3(.15f,.48f,.15f),FlameCore,2.2f,1,seed,sourceY);
                }
                else
                {
                    MotionBox(flameOrigin+new float3(0,.18f,0),new float3(.18f,.36f,.18f),FlameOuter,1.35f,1,seed,sourceY);
                    MotionBox(flameOrigin+new float3(0,.53f,0),new float3(.105f,.34f,.105f),FlameCore,1.7f,1,seed,sourceY);
                }
            }
            for(int particle=0;particle<3;particle++)
            {
                float seed=Seed(index,30+particle);
                float x=particle==0?-.20f:particle==1?.18f:.03f;
                float z=particle==0?.04f:particle==1?-.06f:.18f;
                float y=sourceY+.54f+.12f*seed;
                MotionBox(new float3(point.x+x,y,point.z+z),new float3(.065f,.065f,.065f),EmberColor,1.4f,2,seed);
            }
            for(int particle=0;particle<2;particle++)
            {
                float seed=Seed(index,40+particle),side=particle==0?-.09f:.09f;
                float y=sourceY+.88f+.12f*seed;
                MotionBox(new float3(point.x+side,y,point.z-side*.35f),new float3(.10f,.10f,.10f),SmokeColor,0f,3,seed);
            }
        }
        public void Execute()
        {
            written=0;overflow=false;
            if(!PortalLandingFixtures020.ValidateAllSupport(out _)){Count[0]=-1;return;}
            for(int i=0;i<PortalLandingLayout020.SurfaceCount;i++){PortalLandingLayout020.TrySurface(i,out var box);Box(box,box.Key>=100?Pale:Stone,1);}
            for(int i=0;i<PortalLandingLayout020.ObstacleCount;i++)
            {
                PortalLandingLayout020.TryObstacle(i,out var box);
                if(box.Kind==PortalLandingContour020.GuardKind)
                {
                    quaternion turn=quaternion.RotateY(math.radians(box.Yaw));
                    float3 center=new float3((box.MinX+box.MaxX)*.5f,(box.MinY+box.MaxY)*.5f,(box.MinZ+box.MaxZ)*.5f);
                    int parts=PortalLandingContour020.GuardPartCount(box);
                    for(int p=0;p<parts;p++)
                    {
                        if(!PortalLandingContour020.TryGuardPart(box,p,out var part)){overflow=true;break;}
                        Add(center+math.rotate(turn,new float3(part.Center.X,part.Center.Y,part.Center.Z)),new float3(part.Size.X,part.Size.Y,part.Size.Z),part.Material==0?Pale:Wood,0,0,turn);
                    }
                }
                else if(box.Kind!=PortalLandingFixtures020.ColliderKind&&!CampaignPortalRecipe020.IsColumnObstacle(box)&&!CampaignLandingObstacleSet020.IsCollisionOnly(box)&&(box.Kind!=4||box.Key>=306))Box(box,box.Kind==3?Wood:Stone);
            }
            var sink=this;
            CampaignHubBuildingRecipe020.Emit(ref sink,0,new float3(-25,0,30),0);
            CampaignHubBuildingRecipe020.Emit(ref sink,1,new float3(25,0,22),0);
            CampaignHubBuildingRecipe020.Emit(ref sink,2,new float3(-45,0,-18),-90);
            this=sink;
            float3 shrine=new float3(-30*.9510565f,0,-30*.309017f);
            sink=this;CampaignShrineRecipe020.Emit(ref sink,shrine);this=sink;
            Add(new float3(0,.65f,-10),new float3(8,1.3f,4),Wood);
            Add(new float3(0,1.31f,-10),new float3(5,.02f,3.8f),new float4(.24f,.055f,.09f,1));
            sink=this;CampaignPortalRecipe020.Emit(ref sink);this=sink;
            for(int side=-1;side<=1;side+=2)for(int rune=0;rune<4;rune++)Add(new float3(side*6.8f,7.5f+rune*1.4f,49.03f),new float3(.7f,.7f,.10f),Cyan,1);
            for(int i=0;i<PortalLandingPrototypeGeometry020.TorchCount;i++)
            {
                PortalLandingPrototypeGeometry020.TryTorch(i,out var p);int first=written;
                for(int part=0;part<PortalLandingFixtures020.RigidPartCount;part++)
                {
                    if(!PortalLandingFixtures020.TryRigidPart(i,part,out var fixture)){overflow=true;break;}
                    Add(new float3(fixture.Center.X,fixture.Center.Y,fixture.Center.Z),new float3(fixture.Size.X,fixture.Size.Y,fixture.Size.Z),fixture.Material==0?Pale:Wood,0,0,quaternion.RotateY(math.radians(fixture.Yaw)));
                }
                if(i<6)EmitStairTorch(i,p);
                else
                {
                    Add(p,new float3(.30f,.85f,.30f),Warm,3);
                    Add(p+new float3(.2f,-.12f,.09f),new float3(.20f,.50f,.20f),Warm,2);
                    Add(p+new float3(-.19f,-.13f,-.07f),new float3(.20f,.56f,.20f),new float4(1,.78f,.17f,1),3);
                }
                RaiseFixture(i,first);
            }
            sink=this;CampaignDressingRecipe020.EmitPrototype(ref sink);this=sink;
            sink=this;
            if(!CampaignRearFarmRecipe020.Emit(ref sink,Draws.Length-written,out int farmCommands)||
                farmCommands!=CampaignRearFarmRecipe020.CommandCount)sink.overflow=true;
            this=sink;
            sink=this;if(!CampaignCliffRecipe020.Build(ref sink,173u,Draws.Length-written,out _))sink.overflow=true;this=sink;
            sink=this;
            if(!CampaignPlantedForecourtRecipe020.Emit(ref sink,PlantAdmissions,PlantWorldToken,PlantGeneration,
                PlantWorldRevision,PlantGeometryRevision,Draws.Length-written,out int plantedCommands)||
                plantedCommands!=CampaignPlantedForecourtRecipe020.RequiredCommands){Count[0]=-1;return;}
            this=sink;
            Count[0]=overflow?-1:written;
        }
    }
    [BurstCompile]
    public struct PortalLandingMeshJob020:IJob
    {
        [ReadOnly]public NativeArray<LandingDraw020> Draws;
        public int Count,AssetFloorIndex;
        public NativeArray<int> AssetFloorSpan;
        public NativeArray<LandingVertex020> Vertices;
        public NativeArray<int> Indices,Written;
        public NativeArray<float3> Extents;
        static float3 Corner(float3 h,int face,int corner)
        {
            switch(face)
            {
                case 0:return new float3(corner==0||corner==3?-h.x:h.x,corner<2?-h.y:h.y,h.z);
                case 1:return new float3(corner==0||corner==3?h.x:-h.x,corner<2?-h.y:h.y,-h.z);
                case 2:return new float3(h.x,corner<2?-h.y:h.y,corner==0||corner==3?h.z:-h.z);
                case 3:return new float3(-h.x,corner<2?-h.y:h.y,corner==0||corner==3?-h.z:h.z);
                case 4:return new float3(corner==0||corner==3?-h.x:h.x,h.y,corner<2?h.z:-h.z);
                default:return new float3(corner==0||corner==3?-h.x:h.x,-h.y,corner<2?-h.z:h.z);
            }
        }
        bool Hidden(int item,int face,float3 a,float3 b,float3 c,float3 d,float3 normal)
        {
            for(int other=0;other<Count;other++)
            {
                if(other==item)continue;var box=Draws[other];var inverse=math.conjugate(box.Rotation);float3 h=box.Size*.5f+new float3(.0001f);
                float3 probe=(a+b+c+d)*.25f+normal*.001f;
                if(math.all(math.abs(math.rotate(inverse,probe-box.Center))<h)&&math.all(math.abs(math.rotate(inverse,a-box.Center))<=h)&&
                   math.all(math.abs(math.rotate(inverse,b-box.Center))<=h)&&math.all(math.abs(math.rotate(inverse,c-box.Center))<=h)&&math.all(math.abs(math.rotate(inverse,d-box.Center))<=h))return true;
            }
            return false;
        }
        public void Execute()
        {
            int vertex=0,index=0,culled=0;Written[0]=-1;if(AssetFloorSpan.IsCreated)AssetFloorSpan[0]=-1;
            if(Count<1||Count>PortalLandingPrototypeGeometry020.MaximumDraws)return;
            float3 lower=new float3(float.MaxValue),upper=new float3(float.MinValue);
            for(int item=0;item<Count;item++)
            {
                var draw=Draws[item];float3 h=draw.Size*.5f;
                if(draw.Color.w<.75f)
                {
                    float lateral=draw.Color.w<.24f?.045f:draw.Color.w<.49f?.14f:.20f;
                    float rise=draw.Color.w<.24f?.025f:draw.Color.w<.49f?.82f:1.20f;
                    float3 swept=new float3(lateral,0,lateral);
                    float drop=draw.Color.w<.24f?.025f:0f;
                    lower=math.min(lower,draw.Center-h-swept-new float3(0,drop,0));upper=math.max(upper,draw.Center+h+swept+new float3(0,rise,0));
                }
                for(int face=0;face<6;face++)
                {
                    float3 a=draw.Center+math.rotate(draw.Rotation,Corner(h,face,0)),b=draw.Center+math.rotate(draw.Rotation,Corner(h,face,1)),c=draw.Center+math.rotate(draw.Rotation,Corner(h,face,2)),d=draw.Center+math.rotate(draw.Rotation,Corner(h,face,3));
                    lower=math.min(lower,math.min(math.min(a,b),math.min(c,d)));upper=math.max(upper,math.max(math.max(a,b),math.max(c,d)));
                    float3 normal=math.normalizesafe(math.cross(b-a,c-a));
                    if(Hidden(item,face,a,b,c,d,normal)){culled++;continue;}
                    if(vertex+4>Vertices.Length||index+6>Indices.Length)return;
                    if(AssetFloorSpan.IsCreated&&item==AssetFloorIndex&&face==4)AssetFloorSpan[0]=index;
                    float3 storedNormal=draw.Color.w<.75f?draw.Center:normal;
                    for(int corner=0;corner<4;corner++)Vertices[vertex+corner]=new LandingVertex020{Position=corner==0?a:corner==1?b:corner==2?c:d,Normal=storedNormal,Color=draw.Color,Detail=new float2(draw.Emission,draw.Floor)};
                    Indices[index++]=vertex;Indices[index++]=vertex+1;Indices[index++]=vertex+2;Indices[index++]=vertex;Indices[index++]=vertex+2;Indices[index++]=vertex+3;vertex+=4;
                }
            }
            if(Extents.IsCreated&&Extents.Length>=2){Extents[0]=lower;Extents[1]=upper;}
            Written[0]=vertex;Written[1]=index;Written[2]=culled;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: First-person-scale, original provisional hub-local metres/Y-up with oriented bridge frames, north6m portal floor, lower-6m ruins and13.3/14.2m building envelopes.
// Array Formats:3072max flat draw values,73728max48B vertex records,110592indices,8fixed real flame anchors; six stair fixtures add96 bounded draws, no per-frame allocation or object-per-voxel path.
// Dependencies/Pointers: Existing floor/support/collider streams and fixture anchors remain authoritative. Six stair flames, ember and smoke voxels animate from the shared held shader clock; tagged Normal stores the mesh-local motion pivot and tagged alpha/detail store role/seed/source-height only for those vertices. Existing eight anchors and one surface material are reused. Main-thread World owns one immutable mesh and the original bounded scratch; actual shader/native images and performance remain root-owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing eight hub fixtures; only upper presentation Y grows1.5 about each original support plane. Grounded bases/caps and allXZ/yaw remain exact.
// Array Formats: Same six23-command stair fixtures and two7-command bridge fixtures,152total; no extra commands, runtime buffers, lights or frame allocations.
// Dependencies/Pointers: Fixture providers/anchors/colliders remain authoritative. Cold geometry raises post/bowl/cage/fire and tagged source heights; the mesh writer retains correct center-as-Normal pivots and swept bounds. Shared shader projects the same visual light centers without raising brightness; clock/capture/cleanup/farm/footstep ownership remains unchanged. Native visual/HLSL/Burst acceptance is root-owned.
