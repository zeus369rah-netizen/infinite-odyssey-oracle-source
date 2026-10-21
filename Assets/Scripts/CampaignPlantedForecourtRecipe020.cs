using Unity.Collections;
using Unity.Mathematics;

namespace OdysseyOracle
{
    // PSEUDOCODE:
    // Derive eight fixed2m patches from accepted service/tree anchors; sample nine inset points.
    // Require current world/generation/revisions, all native support samples, and every cold clearance.
    // Emit bounded timber planter/rim/moss/bush/grass/ivy/flower/vine boxes to the existing opaque one-mesh sink.
    // Any missing, stale, unsupported, overlapping or over-capacity patch emits no foliage.
    public struct CampaignPlantPatchAdmission020
    {
        public ulong WorldToken;
        public uint Generation,WorldRevision,GeometryRevision;
        public float SurfaceY,MinimumSupportY,MaximumSupportY,MinimumNormalY;
        public ushort SupportMask;
        public byte PatchIndex;
    }

    public struct CampaignPlantCandidate020
    {
        public float3 Center,LocalCenter,BuildingOrigin;
        public float SurfaceY,Yaw,BuildingYaw;
        public int BuildingRole;
        public uint Seed;
    }

    public static class CampaignPlantedForecourtRecipe020
    {
        public const int PatchCount=8,CommandsPerPatchMaximum=32,MaximumCommands=PatchCount*CommandsPerPatchMaximum;
        public const int MaximumVertices=MaximumCommands*24,MaximumIndices=MaximumCommands*36;
        public const int RequiredCommands=MaximumCommands;
        public const float PatchSize=2f,SupportInset=.9f,MaximumVegetationHeight=1.05f;
        public const byte PlanterColliderKind=8;
        const uint FirstPlanterKey=6000u;
        const float WallOffset=.88f,WallThickness=.10f,CornerY=.265f,CornerHeight=.05f;
        public const float SolidHalfExtent=WallOffset+WallThickness*.5f,SolidHeight=CornerY+CornerHeight*.5f;
        public const float SoilSide020=1.66f,SoilCenterY020=.03f,SoilHeight020=.06f;
        const ushort AllNineSamples=0x1ff;
        const float RouteBodyRadius=.43f,ServiceCapsuleRadius=.4f,TreeReservedRadius=4.5f;
        const float StairMargin=.25f,ServiceAnchorMargin=.5f,PropMargin=.1f;
        static readonly float4 Soil=new float4(.16f,.125f,.075f,1f);
        static readonly float4 BoxWood=new float4(.24f,.115f,.052f,1f);
        static readonly float4 BoxTrim=new float4(.48f,.285f,.105f,1f);
        static readonly float4 Moss=new float4(.12f,.27f,.075f,1f);
        static readonly float4 Green0=new float4(.12f,.29f,.08f,1f);
        static readonly float4 Green1=new float4(.22f,.44f,.13f,1f);
        static readonly float4 Green2=new float4(.35f,.55f,.19f,1f);
        static readonly float4 Green3=new float4(.075f,.22f,.12f,1f);
        static readonly float4 Flower=new float4(.58f,.055f,.075f,1f);

        public static bool TryCandidate(int index,out CampaignPlantCandidate020 value)
        {
            value=default;
            switch(index)
            {
                case 0:return BuildingCandidate(0,new float3(-25f,0f,30f),0f,new float3(5.1f,0f,-7.5f),0f,0x31A0u,out value);
                case 1:return BuildingCandidate(0,new float3(-25f,0f,30f),0f,new float3(-5.1f,0f,-7.5f),0f,0x31A1u,out value);
                case 2:return BuildingCandidate(2,new float3(-45f,0f,-18f),-90f,new float3(4.7f,0f,-8.2f),0f,0x31A2u,out value);
                case 3:return BuildingCandidate(2,new float3(-45f,0f,-18f),-90f,new float3(-4.7f,0f,-8.2f),0f,0x31A3u,out value);
                case 4:return TreeCandidate(45f,0x31A4u,out value);
                case 5:return TreeCandidate(135f,0x31A5u,out value);
                case 6:return TreeCandidate(225f,0x31A6u,out value);
                case 7:return TreeCandidate(315f,0x31A7u,out value);
                default:return false;
            }
        }

        // Collision-only body union; flowers, moss and vines never enlarge the physical body.
        // Root publishes all eight only after TryCurrentAdmissionMask returns 0xff for this build.
        // Same numeric pose/constants used by EmitGardenBox, separate from native floor proof.
        public static bool TrySoilBox020(int index,out float3 center,out float3 size,out float yaw)
        {
            center=size=default;yaw=0f;if(!TryCandidate(index,out var candidate))return false;
            center=candidate.Center+new float3(0f,SoilCenterY020,0f);size=new float3(SoilSide020,SoilHeight020,SoilSide020);yaw=candidate.Yaw;return true;
        }
        public static bool TryPlanterBody(int index,out LandingBox020 value)
        {
            value=default;if(!TryCandidate(index,out var candidate))return false;
            float3 center=candidate.Center;
            float angle=math.radians(candidate.Yaw);
            if(!PortalLandingLayout020.TryMorton(center.x,center.z,out uint morton))return false;
            value=new LandingBox020(center.x-SolidHalfExtent,center.y,center.z-SolidHalfExtent,
                center.x+SolidHalfExtent,center.y+SolidHeight,center.z+SolidHalfExtent,
                FirstPlanterKey+(uint)index,PlanterColliderKind,0,morton,candidate.Yaw,math.cos(angle),math.sin(angle));return true;
        }
        public static bool IsPlanterBody(in LandingBox020 value)=>value.Kind==PlanterColliderKind&&value.Key>=FirstPlanterKey&&value.Key<FirstPlanterKey+(uint)PatchCount;
        public static bool TryCurrentAdmissionMask(NativeArray<CampaignPlantPatchAdmission020> admissions,
            ulong worldToken,uint generation,uint worldRevision,uint geometryRevision,out int acceptedMask)
        {
            acceptedMask=0;
            if(!admissions.IsCreated||admissions.Length!=PatchCount||worldToken==0||generation==0||worldRevision==0||geometryRevision==0)return false;
            for(int patch=0;patch<PatchCount;patch++)
            {
                if(!TryCandidate(patch,out var candidate))continue;
                if(Admitted(admissions[patch],patch,candidate,worldToken,generation,worldRevision,geometryRevision)&&ColdClearances(candidate))acceptedMask|=1<<patch;
            }
            return true;
        }

        static bool BuildingCandidate(int role,float3 origin,float buildingYaw,float3 local,float supportY,uint seed,out CampaignPlantCandidate020 value)
        {
            value=default;
            if(role<0||role>=CampaignHubBuildingRecipe020.RoleCount||
                !CampaignHubBuildingRecipe020.TryAnchor(role,0,origin,buildingYaw,out _))return false;
            quaternion turn=quaternion.RotateY(math.radians(buildingYaw));
            float3 center=origin+math.rotate(turn,local);center.y=supportY;
            value=new CampaignPlantCandidate020{Center=center,LocalCenter=local,BuildingOrigin=origin,SurfaceY=supportY,
                Yaw=buildingYaw,BuildingYaw=buildingYaw,BuildingRole=role,Seed=seed};
            return math.all(math.isfinite(center));
        }

        static bool TreeCandidate(float angleDegrees,uint seed,out CampaignPlantCandidate020 value)
        {
            float angle=math.radians(angleDegrees);
            float3 center=new float3(math.cos(angle)*6.5f,-6f,-62f+math.sin(angle)*6.5f);
            value=new CampaignPlantCandidate020{Center=center,LocalCenter=center,BuildingOrigin=new float3(0f,-6f,-62f),
                SurfaceY=-6f,Yaw=0f,BuildingYaw=0f,BuildingRole=-1,Seed=seed};
            return math.all(math.isfinite(center));
        }

        // The root owner casts from point+0.25m downward; these are the exact nine expected hit XZ positions.
        public static bool TrySupportPoint(int patchIndex,int sampleIndex,out float3 point)
        {
            point=default;
            if(sampleIndex<0||sampleIndex>=9||!TryCandidate(patchIndex,out var candidate))return false;
            int column=sampleIndex%3,row=sampleIndex/3;
            point=candidate.Center+new float3((column-1)*SupportInset,0f,(row-1)*SupportInset);
            return math.all(math.isfinite(point));
        }

        // Missing admission is a safe no-op so the baseline builder stays usable until root wires native support.
        public static bool Emit<TSink>(ref TSink sink,NativeArray<CampaignPlantPatchAdmission020> admissions,
            ulong worldToken,uint generation,uint worldRevision,uint geometryRevision,int remainingCommands,out int emittedCommands)
            where TSink:struct,ICampaignBuildingSink020
        {
            emittedCommands=0;
            if(remainingCommands<RequiredCommands||!TryCurrentAdmissionMask(admissions,worldToken,generation,
                worldRevision,geometryRevision,out int acceptedMask)||acceptedMask!=255)return false;
            for(int patch=0;patch<PatchCount;patch++)
            {
                if((acceptedMask&(1<<patch))==0)continue;
                TryCandidate(patch,out var candidate);
                emittedCommands+=EmitPatch(ref sink,candidate,patch);
            }
            return emittedCommands==RequiredCommands;
        }

        static bool Admitted(in CampaignPlantPatchAdmission020 value,int index,in CampaignPlantCandidate020 candidate,
            ulong worldToken,uint generation,uint worldRevision,uint geometryRevision)
        {
            if(value.WorldToken!=worldToken||value.Generation!=generation||value.WorldRevision!=worldRevision||
                value.GeometryRevision!=geometryRevision||value.PatchIndex!=index||value.SupportMask!=AllNineSamples)return false;
            if(!math.isfinite(value.SurfaceY)||!math.isfinite(value.MinimumSupportY)||!math.isfinite(value.MaximumSupportY)||
                !math.isfinite(value.MinimumNormalY)||value.MinimumNormalY<.95f||value.MinimumSupportY>value.MaximumSupportY||
                value.MaximumSupportY-value.MinimumSupportY>.02f||math.abs(value.SurfaceY-candidate.SurfaceY)>.02f||
                math.abs(value.MinimumSupportY-candidate.SurfaceY)>.02f||math.abs(value.MaximumSupportY-candidate.SurfaceY)>.02f)return false;
            return CanonicalFloorCoverage(index,candidate);
        }

        static bool CanonicalFloorCoverage(int patchIndex,in CampaignPlantCandidate020 candidate)
        {
            for(int sample=0;sample<9;sample++)
            {
                if(!TrySupportPoint(patchIndex,sample,out var point))return false;
                bool found=false;
                for(int floor=0;floor<PortalLandingLayout020.SurfaceCount;floor++)
                {
                    if(!PortalLandingLayout020.TrySurface(floor,out var box)||math.abs(box.MaxY-candidate.SurfaceY)>.001f)continue;
                    LandingPoint020 local=box.Local(new LandingPoint020(point.x,point.y,point.z));
                    if(local.X>=box.MinX&&local.X<=box.MaxX&&local.Z>=box.MinZ&&local.Z<=box.MaxZ){found=true;break;}
                }
                if(!found)return false;
            }
            return true;
        }

        static bool ColdClearances(in CampaignPlantCandidate020 candidate)
        {
            float2 center=new float2(candidate.Center.x,candidate.Center.z);const float half=PatchSize*.5f;
            if(!math.all(math.isfinite(center))||center.x-half< -98f||center.x+half>98f||center.y-half< -86f||center.y+half>56f)return false;
            if(OverlapsRect(center,half,-10f-StairMargin,10f+StairMargin,20f-StairMargin,54f+StairMargin)||
                OverlapsRect(center,half,-12f-StairMargin,12f+StairMargin,-44f-StairMargin,-20f+StairMargin)||
                OverlapsRect(center,half,-1f,1f,-62f,-44f)||
                OverlapsRect(center,half,-26.325f,-23.675f,20f,27f))return false;
            if(!TreeRootClear(center,half)||!RouteClear(center,half)||!StationCapsulesClear(center,half)||
                !ExistingPlantersClear(center,half)||!BuildingClearance(candidate))return false;
            return true;
        }

        static bool BuildingClearance(in CampaignPlantCandidate020 candidate)
        {
            if(candidate.BuildingRole<0)return true;
            if(!CampaignHubBuildingRecipe020.TryAnchor(candidate.BuildingRole,0,candidate.BuildingOrigin,candidate.BuildingYaw,out var anchor))return false;
            float3 localAnchor=math.rotate(quaternion.RotateY(math.radians(-candidate.BuildingYaw)),anchor.Position-candidate.BuildingOrigin);
            float localX=candidate.LocalCenter.x,localZ=candidate.LocalCenter.z;
            float halfX=CampaignHubBuildingRecipe020.Dimensions(candidate.BuildingRole).x*.5f;
            float halfZ=CampaignHubBuildingRecipe020.Dimensions(candidate.BuildingRole).z*.5f;
            bool bodyOverlap=math.abs(localX)<=halfX+PatchSize*.5f&&math.abs(localZ)<=halfZ+PatchSize*.5f;
            if(bodyOverlap)return false;
            float serviceX=PatchSize*.5f+anchor.MaximumSize.x*.5f+ServiceAnchorMargin;
            float serviceZ=PatchSize*.5f+anchor.MaximumSize.z*.5f+ServiceAnchorMargin;
            return math.abs(localX-localAnchor.x)>serviceX||math.abs(localZ-localAnchor.z)>serviceZ;
        }

        static bool TreeRootClear(float2 center,float half)
        {
            float dx=math.max(math.abs(center.x)-half,0f),dz=math.max(math.abs(center.y+62f)-half,0f);
            return dx*dx+dz*dz>=(TreeReservedRadius+.5f)*(TreeReservedRadius+.5f);
        }

        static bool RouteClear(float2 center,float half)
        {
            if(PortalLandingTour020.PointCount!=37||!PortalLandingTour020.TryPoint(0,out var previous,out _))return false;
            float radius=half+RouteBodyRadius;
            for(int point=1;point<PortalLandingTour020.PointCount;point++)
            {
                if(!PortalLandingTour020.TryPoint(point,out var next,out _))return false;
                if(SegmentIntersectsRect(new float2(previous.X,previous.Z),new float2(next.X,next.Z),
                    new float2(center.x-radius,center.y-radius),new float2(center.x+radius,center.y+radius)))return false;
                previous=next;
            }
            return true;
        }

        static bool SegmentIntersectsRect(float2 a,float2 b,float2 minimum,float2 maximum)
        {
            float enter=0f,exit=1f;float2 delta=b-a;
            return ClipAxis(a.x,delta.x,minimum.x,maximum.x,ref enter,ref exit)&&
                ClipAxis(a.y,delta.y,minimum.y,maximum.y,ref enter,ref exit);
        }

        static bool ClipAxis(float origin,float delta,float minimum,float maximum,ref float enter,ref float exit)
        {
            if(math.abs(delta)<1e-6f)return origin>=minimum&&origin<=maximum;
            float a=(minimum-origin)/delta,b=(maximum-origin)/delta;
            if(a>b){float swap=a;a=b;b=swap;}
            enter=math.max(enter,a);exit=math.min(exit,b);return enter<=exit;
        }

        static bool StationCapsulesClear(float2 center,float half)
        {
            for(int station=0;station<4;station++)
            {
                if(!TryStationCapsule(station,out float2 feet,out float radius))return false;
                if(!RectCircleClear(center,half,feet,radius+.1f))return false;
            }
            return true;
        }

        static bool TryStationCapsule(int index,out float2 feet,out float radius)
        {
            radius=ServiceCapsuleRadius;
            switch(index)
            {
                case 0:feet=new float2(-34f,-20f);return true;
                case 1:feet=new float2(-21.5317f,-11.27051f);return true;
                case 2:feet=new float2(17f,22f);return true;
                case 3:feet=new float2(-23f,20f);return true;
                default:feet=default;radius=0f;return false;
            }
        }

        static bool ExistingPlantersClear(float2 center,float half)
        {
            return RectRectClear(center,half,new float2(-22f,24.4f),new float2(.5f,.5f),PropMargin)&&
                RectRectClear(center,half,new float2(-38.4f,-15f),new float2(.5f,.5f),PropMargin);
        }

        static bool RectCircleClear(float2 center,float half,float2 obstacle,float radius)
        {
            float dx=math.max(math.abs(center.x-obstacle.x)-half,0f),dz=math.max(math.abs(center.y-obstacle.y)-half,0f);
            return dx*dx+dz*dz>radius*radius;
        }

        static bool RectRectClear(float2 center,float half,float2 obstacle,float2 obstacleHalf,float margin)
        {
            float dx=math.max(math.abs(center.x-obstacle.x)-half-obstacleHalf.x,0f);
            float dz=math.max(math.abs(center.y-obstacle.y)-half-obstacleHalf.y,0f);
            return dx*dx+dz*dz>margin*margin;
        }

        static bool OverlapsRect(float2 center,float half,float minX,float maxX,float minZ,float maxZ)
        {return center.x+half>=minX&&center.x-half<=maxX&&center.y+half>=minZ&&center.y-half<=maxZ;}

        static int EmitPatch<TSink>(ref TSink sink,in CampaignPlantCandidate020 candidate,int patch)
            where TSink:struct,ICampaignBuildingSink020
        {
            int written=0;quaternion basis=quaternion.RotateY(math.radians(candidate.Yaw));
            EmitGardenBox(ref sink,candidate,basis,ref written);

            int pattern=patch%3;
            if(pattern==0)EmitBushes(ref sink,candidate,basis,patch,ref written);
            else if(pattern==1)EmitGrass(ref sink,candidate,basis,patch,ref written);
            else EmitIvy(ref sink,candidate,basis,patch,ref written);
            EmitFlowers(ref sink,candidate,basis,patch,ref written);
            EmitVineCurtain(ref sink,candidate,basis,patch,ref written);
            return written;
        }

        static void EmitGardenBox<TSink>(ref TSink sink,in CampaignPlantCandidate020 candidate,quaternion basis,ref int written)
            where TSink:struct,ICampaignBuildingSink020
        {
            // The filled planter and four mitered timber sides sit directly on the accepted floor.
            Put(ref sink,candidate,basis,new float3(0f,SoilCenterY020,0f),new float3(SoilSide020,SoilHeight020,SoilSide020),Soil,ref written);
            for(int side=-1;side<=1;side+=2)
            {
                Put(ref sink,candidate,basis,new float3(0f,.12f,side*WallOffset),new float3(1.66f,.24f,WallThickness),BoxWood,ref written);
                Put(ref sink,candidate,basis,new float3(side*WallOffset,.12f,0f),new float3(WallThickness,.24f,1.66f),BoxWood,ref written);
            }
            // Four top planks and four raised corner joints form a readable rectangular rim.
            for(int side=-1;side<=1;side+=2)
            {
                Put(ref sink,candidate,basis,new float3(0f,.26f,side*WallOffset),new float3(1.54f,.04f,WallThickness),BoxTrim,ref written);
                Put(ref sink,candidate,basis,new float3(side*WallOffset,.26f,0f),new float3(WallThickness,.04f,1.54f),BoxTrim,ref written);
            }
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Put(ref sink,candidate,basis,new float3(x*.85f,CornerY,z*.85f),new float3(.16f,CornerHeight,.16f),BoxTrim,ref written);
            // Moss bands grow along every outside face and touch the ground at their lower edge.
            for(int side=-1;side<=1;side+=2)
            {
                Put(ref sink,candidate,basis,new float3(0f,.05f,side*.938f),new float3(1.10f,.10f,.024f),Moss,ref written);
                Put(ref sink,candidate,basis,new float3(side*.938f,.05f,0f),new float3(.024f,.10f,1.10f),Moss,ref written);
            }
        }

        static void EmitBushes<TSink>(ref TSink sink,in CampaignPlantCandidate020 candidate,quaternion basis,int patch,ref int written)
            where TSink:struct,ICampaignBuildingSink020
        {
            for(int item=0;item<3;item++)
            {
                float2 offset=BushOffset(item);float4 color=LeafColor(item+patch);
                Put(ref sink,candidate,basis,new float3(offset.x,.28f,offset.y),new float3(.44f,.44f,.40f),color,ref written);
                Put(ref sink,candidate,basis,new float3(offset.x,.54f,offset.y),new float3(.30f,.34f,.30f),LeafColor(item+patch+1),ref written);
            }
        }

        static float2 BushOffset(int index)
        {
            switch(index)
            {
                case 0:return new float2(0f,-.48f);
                case 1:return new float2(-.48f,.42f);
                default:return new float2(.48f,.42f);
            }
        }

        static void EmitGrass<TSink>(ref TSink sink,in CampaignPlantCandidate020 candidate,quaternion basis,int patch,ref int written)
            where TSink:struct,ICampaignBuildingSink020
        {
            for(int blade=0;blade<6;blade++)
            {
                float angle=blade*(math.PI*2f/6f)+patch*.17f;
                float3 offset=new float3(math.cos(angle)*.65f,0f,math.sin(angle)*.65f);
                float height=.42f+.36f*Noise((uint)(patch*23+blade+1));
                Put(ref sink,candidate,basis,offset+new float3(0f,.06f+height*.5f,0f),new float3(.075f,height,.075f),LeafColor(blade+patch),ref written,blade*22.5f);
            }
        }

        static void EmitIvy<TSink>(ref TSink sink,in CampaignPlantCandidate020 candidate,quaternion basis,int patch,ref int written)
            where TSink:struct,ICampaignBuildingSink020
        {
            for(int leaf=0;leaf<6;leaf++)
            {
                float x=-.70f+leaf*.28f,z=(leaf&1)==0?.60f:-.60f;
                Put(ref sink,candidate,basis,new float3(x,.14f,z),new float3(.27f,.16f,.22f),LeafColor(leaf+patch),ref written,(leaf&1)==0?12f:-12f);
            }
        }

        static void EmitFlowers<TSink>(ref TSink sink,in CampaignPlantCandidate020 candidate,quaternion basis,int patch,ref int written)
            where TSink:struct,ICampaignBuildingSink020
        {
            for(int flower=0;flower<3;flower++)
            {
                float2 offset=FlowerOffset(flower,patch);
                Put(ref sink,candidate,basis,new float3(offset.x,.435f,offset.y),new float3(.04f,.75f,.04f),Green3,ref written);
                Put(ref sink,candidate,basis,new float3(offset.x,.87f,offset.y),new float3(.14f,.12f,.14f),Flower,ref written);
            }
        }

        static float2 FlowerOffset(int index,int patch)
        {
            if(index==0)return new float2(-.78f,-.65f);
            if(index==1)return new float2(.78f,-.65f);
            return new float2((patch&1)==0?0f:.12f,.78f);
        }

        static void EmitVineCurtain<TSink>(ref TSink sink,in CampaignPlantCandidate020 candidate,quaternion basis,int patch,ref int written)
            where TSink:struct,ICampaignBuildingSink020
        {
            // One three-box curtain per bed descends from its rim to the floor on a rotating side.
            int side=patch&3;float3 stem,leafA,leafB;float3 stemSize=new float3(.045f,.28f,.045f),leafSize;
            switch(side)
            {
                case 0:stem=new float3(0f,.14f,-.94f);leafA=new float3(-.13f,.20f,-.94f);leafB=new float3(.13f,.08f,-.94f);
                    leafSize=new float3(.11f,.12f,.05f);break;
                case 1:stem=new float3(0f,.14f,.94f);leafA=new float3(.13f,.20f,.94f);leafB=new float3(-.13f,.08f,.94f);
                    leafSize=new float3(.11f,.12f,.05f);break;
                case 2:stem=new float3(-.94f,.14f,0f);leafA=new float3(-.94f,.20f,.13f);leafB=new float3(-.94f,.08f,-.13f);
                    leafSize=new float3(.05f,.12f,.11f);break;
                default:stem=new float3(.94f,.14f,0f);leafA=new float3(.94f,.20f,-.13f);leafB=new float3(.94f,.08f,.13f);
                    leafSize=new float3(.05f,.12f,.11f);break;
            }
            Put(ref sink,candidate,basis,stem,stemSize,Green3,ref written);
            Put(ref sink,candidate,basis,leafA,leafSize,LeafColor(patch+1),ref written);
            Put(ref sink,candidate,basis,leafB,leafSize,LeafColor(patch+2),ref written);
        }

        static void Put<TSink>(ref TSink sink,in CampaignPlantCandidate020 candidate,quaternion basis,float3 local,float3 size,float4 color,ref int written,float tilt=0f)
            where TSink:struct,ICampaignBuildingSink020
        {
            quaternion rotation=math.mul(basis,quaternion.RotateY(math.radians(tilt)));
            sink.Box(candidate.Center+math.rotate(basis,local),size,rotation,color,0f);written++;
        }

        static float4 LeafColor(int index)
        {
            switch(index&3){case 0:return Green0;case 1:return Green1;case 2:return Green2;default:return Green3;}
        }

        static uint Mix(uint value)
        {unchecked{value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;return value^(value>>16);}}
        static float Noise(uint value)=>((Mix(value)>>8)&65535u)/65535f;
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Eight fixed accepted 2m garden-bed candidates in world metres/Y-up. Planter walls/moss touch support; soil-rooted bases meet its .06m fill top; all boxes stay within each square and below1.05m.
// Array Formats: Eight kind8/key6000..6007 body-only collision records in the root semantic obstacle union, shared cold admission mask and eight world/generation/revision admission records; at most256 existing LandingDraw020 box commands,6144 vertices,9216 indices; no new mesh, texture, material, object, animation, or frame allocation.
// Dependencies/Pointers: Generic ICampaignBuildingSink020 and existing PortalLandingLayout/Contour, building anchors and37-point Nibwick route. Current four service capsule positions are pinned inputs. Caller supplies actual nine-sample native support proof; root owns world admission, integration, import, visual/performance acceptance.
