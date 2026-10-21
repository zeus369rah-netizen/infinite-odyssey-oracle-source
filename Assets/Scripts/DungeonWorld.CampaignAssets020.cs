// PSEUDOCODE: prepare one optional catalogue lease inside existing floor bootstrap; retain old base on failure; bind after actual publication and retire without discarding failed ownership.
using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        CampaignAssetLease020 campaignAssets020;
        public string CampaignAssetStatus020 {get;private set;}="Not prepared";
        void PrepareCampaignAssets020(Transform candidate,BoxCollider[] floors,uint generation)
        {
            if(CampaignAssetLease020.HasPending){CampaignAssetStatus020="Catalogue cleanup pending; legacy surface retained";return;}
            if(campaignAssets020!=null)throw new InvalidOperationException("Catalogue candidate already owned");
            campaignAssets020=new CampaignAssetLease020();
            if(campaignAssets020.Prepare(this,candidate,floors,generation)){CampaignAssetStatus020="Pilot support prepared";return;}
            CampaignAssetStatus020=campaignAssets020.Failure;
            RetireCampaignAssets020();
        }
        int CampaignAssetFloorIndex020=>campaignAssets020!=null&&campaignAssets020.Prepared?campaignAssets020.FloorIndex:-1;
        void RebindCampaignAssetGeneration020(uint generation)
        {if(campaignAssets020!=null&&!campaignAssets020.RebindGeneration(generation))RetireCampaignAssets020();}
        void PrepareCampaignAssetMask020(MeshFilter filter,NativeArray<LandingVertex020> vertices,NativeArray<int> indices,int vertexCount,int indexCount,int span,ref JobHandle fence)
        {
            if(campaignAssets020==null||!campaignAssets020.Prepared)return;
            try{campaignAssets020.PrepareMaskedBase(filter,vertices,indices,vertexCount,indexCount,span,ref fence);}
            catch(Exception error){CampaignAssetStatus020="Catalogue mask rejected: "+error.Message;RetireCampaignAssets020();}
        }
        void BindCampaignAssets020()
        {
            if(campaignAssets020==null)return;
            try{if(campaignAssets020.Bind()){CampaignAssetStatus020="Catalogue pilot bound";return;}CampaignAssetStatus020="Catalogue binding rejected; original surface retained";}
            catch(Exception error){CampaignAssetStatus020="Catalogue binding failed: "+error.Message;}
            RetireCampaignAssets020();
        }
        internal void TickCampaignAssets020(GameSession owner,PortalLandingAtmosphere020 atmosphere)
        {
            if(campaignAssets020==null)return;
            if(owner==null||!ReferenceEquals(owner.World,this)||atmosphere==null||!atmosphere.Bound020){campaignAssets020.Hide();return;}
            campaignAssets020.Tick(owner.Player!=null?owner.Player.ViewCamera:null,atmosphere.PresentationClock020,
                !owner.IsPlaying||owner.Paused||!Application.isFocused,OracleBookAccessibility.PrefersReducedMotion,PassiveWindField015);
        }
        void HideCampaignAssets020(){if(campaignAssets020==null)return;try{campaignAssets020.Hide();}catch(Exception error){CampaignAssetStatus020=error.Message;RetireCampaignAssets020();}}
        internal void HideCampaignAssetPresentation020()=>HideCampaignAssets020();
        void RetireCampaignAssets020()
        {
            if(campaignAssets020==null)return;
            var owner=campaignAssets020;
            try{owner.Dispose();campaignAssets020=null;}
            catch(Exception error){CampaignAssetLease020.Retain(owner);campaignAssets020=null;CampaignAssetStatus020="Catalogue cleanup retained: "+error.Message;}
        }
        public bool TryGetCampaignAssetDiagnostics020(out CampaignAssetDiagnostics020 snapshot)
        {snapshot=default;if(campaignAssets020==null)return false;snapshot=campaignAssets020.Diagnostics;return true;}
        public static bool RetryCampaignAssetCleanup020(out string failure)=>CampaignAssetLease020.RetryPending(out failure);
#if UNITY_EDITOR
        public bool TryBeginCampaignAssetCapture020(Camera camera,out ulong ticket)
        {ticket=0;return campaignAssets020!=null&&campaignAssets020.BeginCapture(camera,out ticket);}
        public void EndCampaignAssetCapture020(ulong ticket){if(campaignAssets020!=null)campaignAssets020.EndCapture(ticket);}
#endif
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing inactive/published campaign root and exact current generation; no alternate floor/body publisher.
// Array Formats: One optional catalogue lease and numeric diagnostic value; existing231collider objects unchanged.
// Dependencies/Pointers: Cold rejection retains legacy base binding; failed cleanup moves into a static explicit-retry owner. Session/World hooks keep one camera and actual revision authority.
