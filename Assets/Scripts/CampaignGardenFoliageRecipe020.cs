// PSEUDOCODE:
// Validate the complete16-anchor lease and exact floor-support mask before touching any output buffer.
// Derive eight deterministic clump roots per anchor; emit all128 stable contact records once, independent of visual LOD.
// Build near/medium/far geometry into six fixed upper/lower sector buffers with a hard16-quad clump bound.
// Keep every root fixed, carry only a per-vertex bend weight, and fail closed on stale identity, non-finite data or capacity mismatch.
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct CampaignGardenFoliageAnchor020
    {
        public ulong WorldToken;
        public uint Generation,WorldRevision,GeometryRevision,FoliageRevision;
        public uint AnchorKey,MortonXZ,Seed;
        public float3 SupportedFeet;
        public float YawDegrees;
        public float2 HalfExtents;
        public float MaximumHeight,RootOffsetY;
        public ushort SupportMask;
        public byte StyleId,Sector,PatchKind,Supported;
    }

    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct CampaignGardenFoliageVertex020
    {
        public float3 Position,Normal;
        public float4 Color;
        public float2 UV;
        public float4 Anchor;
    }

    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct CampaignGardenPlantContact020
    {
        public ulong PlantId;
        public uint AnchorKey,Ordinal;
        public float3 RootWorld;
        public float Radius,Height;
        public uint MortonXZ;
    }

    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct CampaignGardenFoliageSectorBounds020
    {
        public float3 Minimum,Maximum;
        public byte Sector;
    }

    public static class CampaignGardenFoliageRecipe020
    {
        public const int AnchorCount=16,ClumpsPerAnchor=8,ContactCount=128;
        public const int LodCount=3,SectorCount=2,MeshBufferCount=LodCount*SectorCount;
        public const int QuadsPerClump=16,VerticesPerClump=64,IndicesPerClump=96;
        public const int MaximumVerticesPerMesh=4096,MaximumIndicesPerMesh=12288;
        public const int MaximumVertexCount=MeshBufferCount*MaximumVerticesPerMesh;
        public const int MaximumIndexCount=MeshBufferCount*MaximumIndicesPerMesh;
        public const ushort FullSupportMask=0x1ff;
        public const float MaximumTotalDisplacement=.10f,MaximumClumpRadius=.30f;
        public const float BedHeightCap=.93f,CornerHeightCap=.72f,BedRootOffset=.06f;
        public const byte BedPatch=0,CornerPatch=1;

        public static bool TryTemplate(uint anchorKey,out CampaignGardenFoliageAnchor020 value)
        {
            value=default;
            int index;
            float3 feet;
            float yaw;
            byte style,sector,kind;
            uint seed;
            float height,rootOffset;
            if(anchorKey>=6000u&&anchorKey<6008u)
            {
                index=(int)(anchorKey-6000u);kind=BedPatch;sector=(byte)(index<4?0:1);
                switch(index)
                {
                    case 0:feet=new float3(-19.9f,0f,22.5f);yaw=0f;style=1;break;
                    case 1:feet=new float3(-30.1f,0f,22.5f);yaw=0f;style=0;break;
                    case 2:feet=new float3(-36.8f,0f,-13.3f);yaw=-90f;style=2;break;
                    case 3:feet=new float3(-36.8f,0f,-22.7f);yaw=-90f;style=3;break;
                    case 4:feet=new float3(4.59619f,-6f,-57.40381f);yaw=0f;style=1;break;
                    case 5:feet=new float3(-4.59619f,-6f,-57.40381f);yaw=0f;style=2;break;
                    case 6:feet=new float3(-4.59619f,-6f,-66.59619f);yaw=0f;style=0;break;
                    default:feet=new float3(4.59619f,-6f,-66.59619f);yaw=0f;style=3;break;
                }
                seed=0x31A0u+(uint)index;height=BedHeightCap;rootOffset=BedRootOffset;
            }
            else if(anchorKey>=6100u&&anchorKey<6108u)
            {
                index=(int)(anchorKey-6100u);kind=CornerPatch;sector=(byte)(index<4?0:1);
                switch(index)
                {
                    case 0:feet=new float3(-56.9f,0f,-28.9f);yaw=0f;style=0;break;
                    case 1:feet=new float3(41.1f,0f,-32.9f);yaw=0f;style=1;break;
                    case 2:feet=new float3(-60.9f,0f,-10.9f);yaw=0f;style=0;break;
                    case 3:feet=new float3(60.9f,0f,2.9f);yaw=180f;style=0;break;
                    case 4:feet=new float3(-18.9f,-6f,-82.9f);yaw=0f;style=1;break;
                    case 5:feet=new float3(30.9f,-6f,-78.9f);yaw=-90f;style=0;break;
                    case 6:feet=new float3(38.9f,-6f,-72.9f);yaw=-90f;style=2;break;
                    default:feet=new float3(-28.9f,-6f,-45.1f);yaw=90f;style=3;break;
                }
                seed=4411904u+(uint)index*127u;height=CornerHeightCap;rootOffset=0f;
            }
            else return false;

            if(!TryMortonQuarter(feet.x,feet.z,out uint morton))return false;
            value=new CampaignGardenFoliageAnchor020
            {
                AnchorKey=anchorKey,MortonXZ=morton,Seed=seed,SupportedFeet=feet,YawDegrees=yaw,
                HalfExtents=new float2(1f,1f),MaximumHeight=height,RootOffsetY=rootOffset,
                StyleId=style,Sector=sector,PatchKind=kind,Supported=0,SupportMask=0
            };
            return true;
        }

        public static bool TryValidateAnchorSet(NativeArray<CampaignGardenFoliageAnchor020> anchors,
            ulong worldToken,uint generation,uint worldRevision,uint geometryRevision,uint foliageRevision)
        {
            if(!anchors.IsCreated||anchors.Length!=AnchorCount||worldToken==0||generation==0||worldRevision==0||
                geometryRevision==0||foliageRevision==0)return false;
            for(int i=0;i<AnchorCount;i++)
            {
                uint expectedKey=i<8?6000u+(uint)i:6100u+(uint)(i-8);
                var a=anchors[i];
                if(a.AnchorKey!=expectedKey||!TryTemplate(expectedKey,out var template))return false;
                if(a.WorldToken!=worldToken||a.Generation!=generation||a.WorldRevision!=worldRevision||
                    a.GeometryRevision!=geometryRevision||a.FoliageRevision!=foliageRevision||
                    a.SupportMask!=FullSupportMask||a.Supported!=1)return false;
                if(a.MortonXZ!=template.MortonXZ||a.Seed!=template.Seed||a.StyleId!=template.StyleId||
                    a.Sector!=template.Sector||a.PatchKind!=template.PatchKind)return false;
                if(!math.all(math.isfinite(a.SupportedFeet))||!math.all(math.isfinite(a.HalfExtents))||
                    !math.isfinite(a.YawDegrees)||!math.isfinite(a.MaximumHeight)||!math.isfinite(a.RootOffsetY))return false;
                if(math.distance(a.SupportedFeet,template.SupportedFeet)>.001f||
                    math.abs(a.YawDegrees-template.YawDegrees)>.001f||
                    math.distance(a.HalfExtents,template.HalfExtents)>.001f||
                    math.abs(a.MaximumHeight-template.MaximumHeight)>.001f||
                    math.abs(a.RootOffsetY-template.RootOffsetY)>.001f)return false;
                if(a.MaximumHeight>(a.PatchKind==BedPatch?BedHeightCap:CornerHeightCap)||
                    a.HalfExtents.x<.9f||a.HalfExtents.y<.9f||a.HalfExtents.x>1f||a.HalfExtents.y>1f)return false;
            }
            return true;
        }

        public static bool TrySectorBounds(NativeArray<CampaignGardenFoliageAnchor020> anchors,
            ulong worldToken,uint generation,uint worldRevision,uint geometryRevision,uint foliageRevision,
            byte sector,out CampaignGardenFoliageSectorBounds020 bounds)
        {
            bounds=default;
            if(sector>=SectorCount||!TryValidateAnchorSet(anchors,worldToken,generation,worldRevision,geometryRevision,foliageRevision))return false;
            float3 minimum=new float3(float.MaxValue,float.MaxValue,float.MaxValue);
            float3 maximum=new float3(float.MinValue,float.MinValue,float.MinValue);
            int found=0;
            for(int i=0;i<AnchorCount;i++)
            {
                var a=anchors[i];if(a.Sector!=sector)continue;found++;
                float sweep=MaximumTotalDisplacement;
                float3 lo=a.SupportedFeet-new float3(a.HalfExtents.x+sweep,0f,a.HalfExtents.y+sweep);
                float3 hi=a.SupportedFeet+new float3(a.HalfExtents.x+sweep,a.MaximumHeight,a.HalfExtents.y+sweep);
                minimum=math.min(minimum,lo);maximum=math.max(maximum,hi);
            }
            if(found!=8||!math.all(math.isfinite(minimum))||!math.all(math.isfinite(maximum)))return false;
            bounds=new CampaignGardenFoliageSectorBounds020{Minimum=minimum,Maximum=maximum,Sector=sector};
            return true;
        }

        public static bool TryPlantId(uint anchorKey,uint ordinal,out ulong plantId)
        {
            plantId=0;
            if(ordinal>=ClumpsPerAnchor||!((anchorKey>=6000u&&anchorKey<6008u)||(anchorKey>=6100u&&anchorKey<6108u)))return false;
            plantId=((ulong)anchorKey<<32)|(ordinal+1u);
            return plantId!=0;
        }

        public static bool TryMortonQuarter(float x,float z,out uint key)
        {
            key=0;if(!math.isfinite(x)||!math.isfinite(z)||x< -128f||x>=128f||z< -128f||z>=128f)return false;
            uint a=(uint)((x+128f)*4f),b=(uint)((z+128f)*4f);
            for(int bit=0;bit<10;bit++)key|=((a>>bit)&1u)<<(bit*2)|((b>>bit)&1u)<<(bit*2+1);
            return true;
        }

        public static bool TryContactMorton2m(float x,float z,out uint key)
        {
            key=0;if(!math.isfinite(x)||!math.isfinite(z)||x< -128f||x>=128f||z< -128f||z>=128f)return false;
            int cellX=(int)math.floor(x*.5f)+64,cellZ=(int)math.floor(z*.5f)+64;
            if(cellX<0||cellX>=128||cellZ<0||cellZ>=128)return false;
            uint a=(uint)cellX,b=(uint)cellZ;
            for(int bit=0;bit<7;bit++)key|=((a>>bit)&1u)<<(bit*2)|((b>>bit)&1u)<<(bit*2+1);
            return true;
        }

        public static float ClumpHeight(byte style,byte patchKind,uint ordinal)
        {
            if(style==0)return .41f;
            if(style==1)return patchKind==BedPatch?.785f:.585f;
            if(style==2)
            {
                if((ordinal&3u)==3u)return patchKind==BedPatch?.83f:.64f;
                return patchKind==BedPatch?.705f:.515f;
            }
            return .42f;
        }

        public static bool TryClumpOffset(byte patchKind,uint ordinal,out float2 offset)
        {
            offset=default;if(ordinal>=ClumpsPerAnchor)return false;
            if(patchKind==BedPatch)
            {
                switch(ordinal)
                {
                    case 0:offset=new float2(-.42f,-.28f);break;
                    case 1:offset=new float2(-.14f,-.28f);break;
                    case 2:offset=new float2(.14f,-.28f);break;
                    case 3:offset=new float2(.42f,-.28f);break;
                    case 4:offset=new float2(-.42f,.28f);break;
                    case 5:offset=new float2(-.14f,.28f);break;
                    case 6:offset=new float2(.14f,.28f);break;
                    default:offset=new float2(.42f,.28f);break;
                }
                return true;
            }
            if(patchKind==CornerPatch)
            {
                switch(ordinal)
                {
                    case 0:offset=new float2(-.55f,-.55f);break;
                    case 1:offset=new float2(-.15f,-.55f);break;
                    case 2:offset=new float2(.25f,-.55f);break;
                    case 3:offset=new float2(.60f,-.55f);break;
                    case 4:offset=new float2(-.55f,-.15f);break;
                    case 5:offset=new float2(-.55f,.25f);break;
                    case 6:offset=new float2(-.55f,.60f);break;
                    default:offset=new float2(-.20f,-.20f);break;
                }
                return true;
            }
            return false;
        }

        public static bool IsVisibleAtLod(byte patchKind,int lod,uint ordinal)
        {
            if(lod==0)return ordinal<ClumpsPerAnchor;
            if(lod==1)return ordinal<ClumpsPerAnchor&&(ordinal&1u)==0u;
            if(lod!=2)return false;
            return patchKind==CornerPatch?(ordinal==3u||ordinal==6u):(ordinal==0u||ordinal==7u);
        }
    }

    [BurstCompile]
    public struct CampaignGardenFoliageBuildJob020:IJob
    {
        [ReadOnly]public NativeArray<CampaignGardenFoliageAnchor020> Anchors;
        public NativeArray<CampaignGardenFoliageVertex020> Vertices;
        public NativeArray<int> Indices,MeshVertexCounts,MeshIndexCounts,Failure;
        public NativeArray<CampaignGardenPlantContact020> Contacts;
        public ulong WorldToken;
        public uint Generation,WorldRevision,GeometryRevision,FoliageRevision;

        const int InvalidBuffers=1,InvalidAnchors=2,Overflow=3,NonFinite=4;

        public void Execute()
        {
            if(!Failure.IsCreated||Failure.Length!=1)return;
            Failure[0]=InvalidBuffers;
            if(!Anchors.IsCreated||!Vertices.IsCreated||!Indices.IsCreated||!MeshVertexCounts.IsCreated||
                !MeshIndexCounts.IsCreated||!Contacts.IsCreated||Anchors.Length!=CampaignGardenFoliageRecipe020.AnchorCount||
                Vertices.Length!=CampaignGardenFoliageRecipe020.MaximumVertexCount||
                Indices.Length!=CampaignGardenFoliageRecipe020.MaximumIndexCount||
                MeshVertexCounts.Length!=CampaignGardenFoliageRecipe020.MeshBufferCount||
                MeshIndexCounts.Length!=CampaignGardenFoliageRecipe020.MeshBufferCount||
                Contacts.Length!=CampaignGardenFoliageRecipe020.ContactCount)return;

            for(int i=0;i<CampaignGardenFoliageRecipe020.MeshBufferCount;i++)
            {MeshVertexCounts[i]=0;MeshIndexCounts[i]=0;}
            for(int i=0;i<CampaignGardenFoliageRecipe020.ContactCount;i++)Contacts[i]=default;
            if(!CampaignGardenFoliageRecipe020.TryValidateAnchorSet(Anchors,WorldToken,Generation,WorldRevision,GeometryRevision,FoliageRevision))
            {Failure[0]=InvalidAnchors;return;}
            Failure[0]=0;

            for(int anchorIndex=0;anchorIndex<CampaignGardenFoliageRecipe020.AnchorCount;anchorIndex++)
            {
                var anchor=Anchors[anchorIndex];
                for(uint ordinal=0;ordinal<CampaignGardenFoliageRecipe020.ClumpsPerAnchor;ordinal++)
                {
                    if(!CampaignGardenFoliageRecipe020.TryClumpOffset(anchor.PatchKind,ordinal,out float2 localOffset)||
                        !CampaignGardenFoliageRecipe020.TryPlantId(anchor.AnchorKey,ordinal,out ulong plantId))
                    {Failure[0]=InvalidAnchors;return;}
                    float3 root=WorldRoot(anchor,localOffset);
                    float height=CampaignGardenFoliageRecipe020.ClumpHeight(anchor.StyleId,anchor.PatchKind,ordinal);
                    if(!math.all(math.isfinite(root))||!math.isfinite(height)||height<=0f||
                        height+anchor.RootOffsetY>anchor.MaximumHeight+.001f||
                        !CampaignGardenFoliageRecipe020.TryContactMorton2m(root.x,root.z,out uint contactMorton))
                    {Failure[0]=NonFinite;return;}
                    Contacts[anchorIndex*CampaignGardenFoliageRecipe020.ClumpsPerAnchor+(int)ordinal]=
                        new CampaignGardenPlantContact020{PlantId=plantId,AnchorKey=anchor.AnchorKey,Ordinal=ordinal,
                            RootWorld=root,Radius=CampaignGardenFoliageRecipe020.MaximumClumpRadius,Height=height,MortonXZ=contactMorton};
                }

                for(int lod=0;lod<CampaignGardenFoliageRecipe020.LodCount;lod++)
                for(uint ordinal=0;ordinal<CampaignGardenFoliageRecipe020.ClumpsPerAnchor;ordinal++)
                {
                    if(!CampaignGardenFoliageRecipe020.IsVisibleAtLod(anchor.PatchKind,lod,ordinal))continue;
                    int output=lod*CampaignGardenFoliageRecipe020.SectorCount+anchor.Sector;
                    int vertexStart=MeshVertexCounts[output],indexStart=MeshIndexCounts[output];
                    if(vertexStart+CampaignGardenFoliageRecipe020.VerticesPerClump>CampaignGardenFoliageRecipe020.MaximumVerticesPerMesh||
                        indexStart+CampaignGardenFoliageRecipe020.IndicesPerClump>CampaignGardenFoliageRecipe020.MaximumIndicesPerMesh)
                    {Failure[0]=Overflow;return;}
                    if(!CampaignGardenFoliageRecipe020.TryClumpOffset(anchor.PatchKind,ordinal,out float2 localOffset))
                    {Failure[0]=InvalidAnchors;return;}
                    int quad=0;
                    float3 root=WorldRoot(anchor,localOffset);
                    EmitClump(output,vertexStart,indexStart,anchor,root,ordinal,ref quad);
                    if(Failure[0]!=0)return;
                    if(quad!=CampaignGardenFoliageRecipe020.QuadsPerClump)
                    {Failure[0]=Overflow;return;}
                    MeshVertexCounts[output]=vertexStart+CampaignGardenFoliageRecipe020.VerticesPerClump;
                    MeshIndexCounts[output]=indexStart+CampaignGardenFoliageRecipe020.IndicesPerClump;
                }
            }
            Failure[0]=0;
        }

        static float3 WorldRoot(in CampaignGardenFoliageAnchor020 anchor,float2 local)
        {
            float angle=math.radians(anchor.YawDegrees),s=math.sin(angle),c=math.cos(angle);
            return anchor.SupportedFeet+new float3(local.x*c+local.y*s,anchor.RootOffsetY,-local.x*s+local.y*c);
        }

        float3 WorldPoint(in CampaignGardenFoliageAnchor020 anchor,float3 root,float3 point)
        {
            float angle=math.radians(anchor.YawDegrees),s=math.sin(angle),c=math.cos(angle);
            return root+new float3(point.x*c+point.z*s,point.y,-point.x*s+point.z*c);
        }

        float3 WorldNormal(in CampaignGardenFoliageAnchor020 anchor,float3 normal)
        {
            float angle=math.radians(anchor.YawDegrees),s=math.sin(angle),c=math.cos(angle);
            return new float3(normal.x*c+normal.z*s,normal.y,-normal.x*s+normal.z*c);
        }

        void EmitClump(int output,int vertexStart,int indexStart,in CampaignGardenFoliageAnchor020 anchor,
            float3 root,uint ordinal,ref int quad)
        {
            if(anchor.StyleId==0)
            {
                AddBox(output,vertexStart,indexStart,anchor,root,new float3(0f,.14f,0f),new float3(.34f,.28f,.30f),Green0,ref quad);
                for(int frond=0;frond<4;frond++)AddFernFrond(output,vertexStart,indexStart,anchor,root,frond*.5f*math.PI,frond,ref quad);
                for(int leaf=0;leaf<6;leaf++)
                    AddLeaf(output,vertexStart,indexStart,anchor,root,leaf*(math.PI/3f),.08f+(leaf&1)*.06f,
                        .10f+(leaf%3)*.02f,(leaf&1)==0?Green2:Green3,ref quad);
            }
            else if(anchor.StyleId==1||anchor.StyleId==2)
            {
                bool cyan=anchor.StyleId==2;
                float head=cyan?(anchor.PatchKind==CampaignGardenFoliageRecipe020.BedPatch?.67f:.48f):
                    (anchor.PatchKind==CampaignGardenFoliageRecipe020.BedPatch?.75f:.55f);
                float stemTop=head-.08f;
                AddQuad(output,vertexStart,indexStart,anchor,root,new float3(-.018f,0f,0f),new float3(.018f,0f,0f),
                    new float3(.018f,stemTop,0f),new float3(-.018f,stemTop,0f),new float3(0f,0f,1f),StemColor,ref quad);
                AddQuad(output,vertexStart,indexStart,anchor,root,new float3(0f,0f,-.018f),new float3(0f,0f,.018f),
                    new float3(0f,stemTop,.018f),new float3(0f,stemTop,-.018f),new float3(1f,0f,0f),StemColor,ref quad);
                AddLeaf(output,vertexStart,indexStart,anchor,root,ordinal*.81f,.36f,.17f,LeafColor(anchor.StyleId,ordinal),ref quad);
                AddLeaf(output,vertexStart,indexStart,anchor,root,ordinal*.81f+math.PI,.47f,.15f,LeafColor(anchor.StyleId,ordinal+1u),ref quad);
                if(cyan)AddCyanBloom(output,vertexStart,indexStart,anchor,root,ordinal,head,ref quad);
                else AddNormalBloom(output,vertexStart,indexStart,anchor,root,ordinal,head,ref quad);
                AddBox(output,vertexStart,indexStart,anchor,root,new float3(0f,head,0f),
                    cyan?new float3(.06f,.06f,.06f):new float3(.07f,.07f,.07f),
                    cyan?CyanCore:NormalCore(ordinal),ref quad);
            }
            else
            {
                AddBox(output,vertexStart,indexStart,anchor,root,new float3(-.045f,.11f,0f),new float3(.22f,.22f,.22f),Crimson0,ref quad);
                AddBox(output,vertexStart,indexStart,anchor,root,new float3(.055f,.32f,.015f),new float3(.20f,.19f,.19f),Crimson1,ref quad);
                for(int tendril=0;tendril<4;tendril++)AddDroop(output,vertexStart,indexStart,anchor,root,tendril*math.PI*.5f,ref quad);
            }
        }

        void AddNormalBloom(int output,int vertexStart,int indexStart,in CampaignGardenFoliageAnchor020 anchor,
            float3 root,uint ordinal,float head,ref int quad)
        {
            float4 petal=NormalPetal(ordinal);
            for(int i=0;i<6;i++)
            {
                float angle=i*(math.PI/3f)+(ordinal&3u)*.08f;
                float2 dir=new float2(math.cos(angle),math.sin(angle)),side=new float2(-dir.y,dir.x);
                float2 inner=dir*.018f,outer=dir*.115f;
                float3 p0=new float3(inner.x,head-.035f,inner.y);
                float3 p1=new float3(outer.x+side.x*.026f,head-.040f,outer.y+side.y*.026f);
                float3 p2=new float3(dir.x*.145f,head+.006f,dir.y*.145f);
                float3 p3=new float3(outer.x-side.x*.026f,head-.040f,outer.y-side.y*.026f);
                AddQuad(output,vertexStart,indexStart,anchor,root,p0,p1,p2,p3,new float3(side.x,0f,side.y),petal,ref quad);
            }
        }

        void AddCyanBloom(int output,int vertexStart,int indexStart,in CampaignGardenFoliageAnchor020 anchor,
            float3 root,uint ordinal,float head,ref int quad)
        {
            uint variant=ordinal&3u;
            for(int i=0;i<6;i++)
            {
                float angle=i*(math.PI/3f)+(ordinal&3u)*.06f;
                float2 dir=new float2(math.cos(angle),math.sin(angle)),side=new float2(-dir.y,dir.x);
                float3 p0,p1,p2,p3;float4 color=CyanPetal;
                if(variant==1u)
                {
                    float2 inner=dir*.055f,outer=dir*.125f;
                    p0=new float3(inner.x,head+.015f,inner.y);
                    p1=new float3(outer.x+side.x*.025f,head-.08f,outer.y+side.y*.025f);
                    p2=new float3(outer.x,head-.14f,outer.y);
                    p3=new float3(outer.x-side.x*.025f,head-.08f,outer.y-side.y*.025f);
                }
                else if(variant==3u)
                {
                    p0=new float3(dir.x*.025f,head-.03f,dir.y*.025f);
                    p1=new float3(dir.x*.07f+side.x*.015f,head+.02f,dir.y*.07f+side.y*.015f);
                    p2=new float3(dir.x*.105f,head+.16f,dir.y*.105f);
                    p3=new float3(dir.x*.07f-side.x*.015f,head+.02f,dir.y*.07f-side.y*.015f);
                    if(i==1||i==4)color=MagentaSpike;
                }
                else
                {
                    float2 inner=dir*.018f,outer=dir*.115f;
                    p0=new float3(inner.x,head-.035f,inner.y);
                    p1=new float3(outer.x+side.x*.028f,head-.02f,outer.y+side.y*.028f);
                    p2=new float3(dir.x*.145f,head+.025f,dir.y*.145f);
                    p3=new float3(outer.x-side.x*.028f,head-.02f,outer.y-side.y*.028f);
                }
                AddQuad(output,vertexStart,indexStart,anchor,root,p0,p1,p2,p3,new float3(side.x,0f,side.y),color,ref quad);
            }
        }

        void AddLeaf(int output,int vertexStart,int indexStart,in CampaignGardenFoliageAnchor020 anchor,
            float3 root,float angle,float startY,float length,float4 color,ref int quad)
        {
            float2 dir=new float2(math.cos(angle),math.sin(angle)),side=new float2(-dir.y,dir.x);
            float2 start=dir*.035f,tip=dir*length;
            float3 p0=new float3(start.x-side.x*.022f,startY,start.y-side.y*.022f);
            float3 p1=new float3(start.x+side.x*.022f,startY,start.y+side.y*.022f);
            float3 p2=new float3(tip.x+side.x*.009f,startY+.035f,tip.y+side.y*.009f);
            float3 p3=new float3(tip.x-side.x*.009f,startY+.035f,tip.y-side.y*.009f);
            AddQuad(output,vertexStart,indexStart,anchor,root,p0,p1,p2,p3,new float3(side.x,.25f,side.y),color,ref quad);
        }

        void AddFernFrond(int output,int vertexStart,int indexStart,in CampaignGardenFoliageAnchor020 anchor,
            float3 root,float angle,int leaflet,ref int quad)
        {
            float2 dir=new float2(math.cos(angle),math.sin(angle)),side=new float2(-dir.y,dir.x);
            float2 basePoint=dir*.025f,midPoint=dir*.14f,tip=dir*.265f;
            float3 p0=new float3(basePoint.x-side.x*.028f,.12f,basePoint.y-side.y*.028f);
            float3 p1=new float3(midPoint.x+side.x*.024f,.26f,midPoint.y+side.y*.024f);
            float3 p2=new float3(tip.x,.41f,tip.y);
            float3 p3=new float3(midPoint.x-side.x*.024f,.26f,midPoint.y-side.y*.024f);
            AddQuad(output,vertexStart,indexStart,anchor,root,p0,p1,p2,p3,
                new float3(side.x,.45f,side.y),(leaflet&1)==0?Green2:Green3,ref quad);
        }

        void AddDroop(int output,int vertexStart,int indexStart,in CampaignGardenFoliageAnchor020 anchor,
            float3 root,float angle,ref int quad)
        {
            float2 dir=new float2(math.cos(angle),math.sin(angle)),side=new float2(-dir.y,dir.x);
            float2 start=dir*.07f,tip=dir*.22f;
            float3 p0=new float3(start.x-side.x*.034f,.40f,start.y-side.y*.034f);
            float3 p1=new float3(start.x+side.x*.034f,.40f,start.y+side.y*.034f);
            float3 p2=new float3(tip.x+side.x*.012f,.12f,tip.y+side.y*.012f);
            float3 p3=new float3(tip.x-side.x*.012f,.12f,tip.y-side.y*.012f);
            AddQuad(output,vertexStart,indexStart,anchor,root,p0,p1,p2,p3,new float3(side.x,.2f,side.y),Crimson2,ref quad);
        }

        void AddBox(int output,int vertexStart,int indexStart,in CampaignGardenFoliageAnchor020 anchor,
            float3 root,float3 center,float3 size,float4 color,ref int quad)
        {
            float3 h=size*.5f;
            for(int face=0;face<6;face++)
            {
                GetFace(face,h,center,out float3 p0,out float3 p1,out float3 p2,out float3 p3,out float3 normal);
                AddQuad(output,vertexStart,indexStart,anchor,root,p0,p1,p2,p3,normal,color,ref quad);
            }
        }

        static void GetFace(int face,float3 h,float3 c,out float3 p0,out float3 p1,out float3 p2,out float3 p3,out float3 normal)
        {
            switch(face)
            {
                case 0:normal=new float3(1,0,0);p0=c+new float3(h.x,-h.y,-h.z);p1=c+new float3(h.x,h.y,-h.z);p2=c+new float3(h.x,h.y,h.z);p3=c+new float3(h.x,-h.y,h.z);break;
                case 1:normal=new float3(-1,0,0);p0=c+new float3(-h.x,-h.y,h.z);p1=c+new float3(-h.x,h.y,h.z);p2=c+new float3(-h.x,h.y,-h.z);p3=c+new float3(-h.x,-h.y,-h.z);break;
                case 2:normal=new float3(0,1,0);p0=c+new float3(-h.x,h.y,-h.z);p1=c+new float3(-h.x,h.y,h.z);p2=c+new float3(h.x,h.y,h.z);p3=c+new float3(h.x,h.y,-h.z);break;
                case 3:normal=new float3(0,-1,0);p0=c+new float3(-h.x,-h.y,h.z);p1=c+new float3(-h.x,-h.y,-h.z);p2=c+new float3(h.x,-h.y,-h.z);p3=c+new float3(h.x,-h.y,h.z);break;
                case 4:normal=new float3(0,0,1);p0=c+new float3(h.x,-h.y,h.z);p1=c+new float3(h.x,h.y,h.z);p2=c+new float3(-h.x,h.y,h.z);p3=c+new float3(-h.x,-h.y,h.z);break;
                default:normal=new float3(0,0,-1);p0=c+new float3(-h.x,-h.y,-h.z);p1=c+new float3(-h.x,h.y,-h.z);p2=c+new float3(h.x,h.y,-h.z);p3=c+new float3(h.x,-h.y,-h.z);break;
            }
        }

        void AddQuad(int output,int vertexStart,int indexStart,in CampaignGardenFoliageAnchor020 anchor,
            float3 root,float3 p0,float3 p1,float3 p2,float3 p3,float3 normal,float4 color,ref int quad)
        {
            if(quad>=CampaignGardenFoliageRecipe020.QuadsPerClump){Failure[0]=Overflow;return;}
            float3 w0=WorldPoint(anchor,root,p0),w1=WorldPoint(anchor,root,p1),w2=WorldPoint(anchor,root,p2),w3=WorldPoint(anchor,root,p3);
            if(!math.all(math.isfinite(w0))||!math.all(math.isfinite(w1))||!math.all(math.isfinite(w2))||
                !math.all(math.isfinite(w3))||!math.all(math.isfinite(color))||!math.all(math.isfinite(normal)))
            {Failure[0]=NonFinite;return;}
            float3 n=math.normalizesafe(WorldNormal(anchor,normal),new float3(0f,1f,0f));
            int v=output*CampaignGardenFoliageRecipe020.MaximumVerticesPerMesh+vertexStart+quad*4;
            int ix=output*CampaignGardenFoliageRecipe020.MaximumIndicesPerMesh+indexStart+quad*6;
            Vertices[v]=MakeVertex(w0,n,color,root,p0.y);Vertices[v+1]=MakeVertex(w1,n,color,root,p1.y);
            Vertices[v+2]=MakeVertex(w2,n,color,root,p2.y);Vertices[v+3]=MakeVertex(w3,n,color,root,p3.y);
            Indices[ix]=vertexStart+quad*4;Indices[ix+1]=vertexStart+quad*4+1;Indices[ix+2]=vertexStart+quad*4+2;
            Indices[ix+3]=vertexStart+quad*4;Indices[ix+4]=vertexStart+quad*4+2;Indices[ix+5]=vertexStart+quad*4+3;
            quad++;
        }

        static CampaignGardenFoliageVertex020 MakeVertex(float3 position,float3 normal,float4 color,float3 root,float localY)
        {
            return new CampaignGardenFoliageVertex020
            {
                Position=position,Normal=normal,Color=color,UV=new float2(-1f,0f),
                Anchor=new float4(root,math.saturate((localY-.035f)*1.5f))
            };
        }

        static float4 LeafColor(byte style,uint index)
        {
            if(style==0)return (index&1u)==0u?Green2:Green3;
            if(style==1)return GreenStem;
            if(style==2)return CyanLeaf;
            return Crimson1;
        }

        static float4 NormalPetal(uint ordinal)
        {
            switch(ordinal&3u)
            {
                case 0:return PoppyRed;
                case 1:return Marigold;
                case 2:return DaisyCream;
                default:return Violet;
            }
        }

        static float4 NormalCore(uint ordinal)=>((ordinal&3u)==1u)?WarmCore:new float4(.78f,.60f,.20f,1f);

        static readonly float4 Green0=new float4(.10f,.28f,.07f,1f);
        static readonly float4 Green1=new float4(.20f,.43f,.12f,1f);
        static readonly float4 Green2=new float4(.33f,.57f,.18f,1f);
        static readonly float4 Green3=new float4(.07f,.22f,.10f,1f);
        static readonly float4 GreenStem=new float4(.11f,.34f,.10f,1f);
        static readonly float4 PoppyRed=new float4(.80f,.10f,.13f,1f);
        static readonly float4 Marigold=new float4(.98f,.71f,.10f,1f);
        static readonly float4 DaisyCream=new float4(.95f,.91f,.73f,1f);
        static readonly float4 Violet=new float4(.49f,.25f,.69f,1f);
        static readonly float4 WarmCore=new float4(.95f,.54f,.07f,1f);
        static readonly float4 CyanLeaf=new float4(.10f,.53f,.48f,1f);
        static readonly float4 CyanPetal=new float4(.20f,.83f,.83f,1f);
        static readonly float4 CyanCore=new float4(.68f,.98f,.85f,1f);
        static readonly float4 MagentaSpike=new float4(.91f,.19f,.58f,1f);
        static readonly float4 Crimson0=new float4(.35f,.035f,.065f,1f);
        static readonly float4 Crimson1=new float4(.61f,.065f,.11f,1f);
        static readonly float4 Crimson2=new float4(.79f,.12f,.20f,1f);
        static readonly float4 StemColor=new float4(.10f,.31f,.09f,1f);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World metres/Y-up; each root is supported floor or admitted planter soil-top, with yaw-rotated local clump offsets and vertices.
// Array Formats: 16 revision-bound anchor structs; six fixed NativeArray mesh buffers (4096 vertices/12288 indices each); 128 stable 40B plant contacts; 64B vertex ABI.
// Dependencies/Pointers: Root populates all identity fields and nine-hit native support before scheduling. Parent binds two opaque sector renderers, three cold LODs, held wind clock and at most two player capsule uniforms; no per-plant GameObject or collider.
