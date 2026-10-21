using OdysseyOracle.Foundation.Presentation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        RectTransform mapDividerLine;
        readonly RectTransform[] refugeMapBadges=new RectTransform[4],refugeMapDots=new RectTransform[4],refugeMapLeaders=new RectTransform[4],partyMapBadges=new RectTransform[1];
        readonly Vector4[] refugeMapAnchors=new Vector4[4];
        bool refugeMapAnchorSnapshot,partyMapVisible;
        const string RefugeMapLegend="ANVIL · SPELLS · PRACTICE\nGREEN YOU · VIOLET PORTAL\nNORTH UP";
        const string RefugeMapLegendParty="ANVIL · SPELLS · PRACTICE\nGREEN YOU · VIOLET PORTAL\nCYAN FRIEND · NORTH UP";
        static Color StationMapColor(int index)=>index==0?new Color(.93f,.44f,.32f):index==1?new Color(.78f,.48f,1f):index==2?new Color(.30f,.73f,.96f):new Color(.95f,.75f,.32f);
        // Station icon kinds0..3 follow the unchanged actual anchor order.

        void BuildRefugeStationMapOverlay()
        {
            refugeMapAnchorSnapshot=false;partyMapVisible=false;
            for(int i=0;i<1;i++)
            {
                var badge=Rect(mapSurface,"AcceptedPartyPose"+i,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(10,10));
                Paint(badge,new Color(.02f,.07f,.07f,.95f));Border(badge,new Color(.38f,.95f,.94f),1);
                CreateMapIcon020(badge,4,Ivory,8);
                partyMapBadges[i]=badge;badge.gameObject.SetActive(false);
            }
            if(!session.World.IsTutorialRefuge||session.World.IsCoopArena)return;
            for(int i=0;i<4;i++)
            {
                Color color=StationMapColor(i);
                var leader=Rect(mapSurface,"StationLeader"+i,Vector2.zero,new Vector2(0,.5f),Vector2.zero,new Vector2(1,1));Paint(leader,color);
                var dot=Rect(mapSurface,"ExactStationAnchor"+i,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(3,3));Paint(dot,color);
                var badge=Rect(mapSurface,"InsetStationBadge"+i,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(16,16));
                Paint(badge,new Color(.02f,.028f,.025f,.98f));Border(badge,color,1);
                CreateMapIcon020(badge,i,color,12);
                refugeMapLeaders[i]=leader;refugeMapDots[i]=dot;refugeMapBadges[i]=badge;
                leader.gameObject.SetActive(false);dot.gameObject.SetActive(false);badge.gameObject.SetActive(false);
            }
            RefreshRefugeStationMapOverlay();
        }
        void ApplyRefugeMapGeometry(bool arena)
        {
            if(minimapRoot==null)return;
            float height=arena?295:307;minimapRoot.sizeDelta=new Vector2(229,height);
            if(mapDividerLine!=null)mapDividerLine.anchoredPosition=new Vector2(0,height*.5f-54f);
            mapLegend.fontSize=arena?9:11;mapLegend.alignment=arena?TextAnchor.MiddleCenter:TextAnchor.UpperLeft;
            mapLegend.rectTransform.sizeDelta=new Vector2(211,42);
            mapLegend.rectTransform.anchoredPosition=new Vector2(0,arena?5:8);
        }
        string CurrentRefugeMapLegend=>partyMapVisible?RefugeMapLegendParty:RefugeMapLegend;
        Vector4 CurrentStationMapAnchor(int index)
        {
            var world=session.World;
            if(index==0){var p=world.TutorialMonsterPracticePosition;return new Vector4(p.x,p.y,p.z,1);}
            if(index==2){var p=world.TutorialTowerPracticePosition;return new Vector4(p.x,p.y,p.z,1);}
            if(index==1&&world.TryGetRaykonInteractionTarget(out _,out Vector3 feet,out _))return new Vector4(feet.x,feet.y,feet.z,1);
            var smith=session.Shop!=null?session.Shop.Blacksmith:null;
            if(index==3&&smith!=null&&smith.TowerAnvilReady){var p=smith.TowerAnvilFeet;return new Vector4(p.x,p.y,p.z,1);}
            return Vector4.zero;
        }
        void RefreshRefugeStationMapOverlay()
        {
            if(session==null||session.World==null||!mapBuilt)return;
            bool tutorial=session.World.IsTutorialRefuge&&!session.World.IsCoopArena;
            if(tutorial&&refugeMapBadges[0]!=null)
            {
                bool changed=!refugeMapAnchorSnapshot;
                for(int i=0;i<4;i++){Vector4 next=CurrentStationMapAnchor(i);if(!next.Equals(refugeMapAnchors[i])){changed=true;refugeMapAnchors[i]=next;}}
                if(changed)
                {
                    RebuildRefugeStationLayout();
                }
            }
            int used=0;var rooms=session.Rooms;
            if(rooms!=null&&rooms.Connected)
            {
                var ids=rooms.ConnectedClientIds;
                for(int i=0;i<ids.Count&&i<2&&used<1;i++)
                {
                    ulong id=ids[i];if(id==rooms.LocalClientId||!rooms.IsCampaignMemberRegistered(id)||
                        !rooms.TryGetPlayerPose(id,out Vector3 point,out _)||!ContainsMapPoint(point))continue;
                    var badge=partyMapBadges[used++];if(badge==null)continue;
                    badge.anchoredPosition=MapPosition(point);if(!badge.gameObject.activeSelf)badge.gameObject.SetActive(true);
                }
            }
            for(int i=used;i<1;i++)if(partyMapBadges[i]!=null&&partyMapBadges[i].gameObject.activeSelf)partyMapBadges[i].gameObject.SetActive(false);
            bool visible=used>0;if(partyMapVisible!=visible){partyMapVisible=visible;RefreshMinimapContext(true);}
        }
        void RebuildRefugeStationLayout()
        {
            var actualRooms=session.World.MapRooms;
            if(actualRooms==null||actualRooms.Length<1||actualRooms.Length>32)return;
            NativeArray<float4> anchors=default,rectangles=default;NativeArray<MinimapLandmark32> output=default;JobHandle fence=default;
            try
            {
                anchors=new NativeArray<float4>(4,Allocator.TempJob);rectangles=new NativeArray<float4>(actualRooms.Length,Allocator.TempJob);
                output=new NativeArray<MinimapLandmark32>(4,Allocator.TempJob);
                for(int i=0;i<4;i++){var p=refugeMapAnchors[i];anchors[i]=new float4(p.x,p.y,p.z,p.w);}
                for(int i=0;i<actualRooms.Length;i++){var r=actualRooms[i];rectangles[i]=new float4(r.xMin,r.yMin,r.xMax,r.yMax);}
                fence=new MinimapLandmarkLayoutJob{Anchors=anchors,Rooms=rectangles,Results=output,
                    Bounds=new float4(mapBounds.xMin,mapBounds.yMin,mapBounds.xMax,mapBounds.yMax)}.Schedule();fence.Complete();
                for(int i=0;i<4;i++)
                {
                    var result=output[i];bool valid=result.Valid==1;var badge=refugeMapBadges[i];var dot=refugeMapDots[i];var leader=refugeMapLeaders[i];
                    badge.gameObject.SetActive(valid);dot.gameObject.SetActive(valid);leader.gameObject.SetActive(valid&&result.LeaderLength>.25f);
                    if(!valid)continue;
                    badge.anchoredPosition=new Vector2(result.Badge.x,result.Badge.y);dot.anchoredPosition=new Vector2(result.Anchor.x,result.Anchor.y);
                    leader.anchoredPosition=badge.anchoredPosition;leader.sizeDelta=new Vector2(result.LeaderLength,1);
                    leader.localRotation=Quaternion.Euler(0,0,result.LeaderDegrees);dot.SetAsLastSibling();badge.SetAsLastSibling();
                }
                refugeMapAnchorSnapshot=true;
            }
            finally{fence.Complete();if(output.IsCreated)output.Dispose();if(rectangles.IsCreated)rectangles.Dispose();if(anchors.IsCreated)anchors.Dispose();}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged authoritative WorldXZ->north-up182x186 map; inset badges retain separate actual anchor dots/leader, player rotation untouched.
// Array Formats: cold4 station views/snapshots+1 teammate view for actual2-player cap; TempJob float4[4],rooms[1..32],result32[4] only on readiness/anchor changes.
// Dependencies/Pointers: actual World Raykon/zone and ready Smith Anvil feet, accepted registered RoomConnection poses; caller completes job before reading/disposal. Existing MapPosition alone places live teammates. No actor, network, tutorial or save authority; Unity/browser/allocation acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing World/Canvas; one objective compass replaces floor-arrow publication, actual map legend retained.
// Array Formats: one bounded HUD/native store with fenced disposal; preserved original floor recipe stays inactive.
// Dependencies/Pointers: original tutorial/campaign/physics/input authorities unchanged; source-only integration/native/browser acceptance pending.
