using System;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        WorldMode020 worldMode020;
        uint worldRevision020;
        bool worldPublicationReady020;
        LandingAdmission020 campaignHubAdmission020;
        Transform campaignHubPublishedRoot020;
        float campaignHubSpawnYaw020,campaignHubFarClip020;
        public WorldMode020 Mode020=>worldMode020;
        public uint WorldRevision020=>worldRevision020;
        public bool WorldPublished020=>worldPublicationReady020;
        public uint CampaignHubGeneration020=>CampaignHubActive020?campaignHubAdmission020.Generation:0;
        public float CampaignHubSpawnYaw020=>campaignHubSpawnYaw020;
        public float CampaignHubFarClip020=>campaignHubFarClip020;
        public bool CampaignHubActive020=>worldPublicationReady020&&worldMode020==WorldMode020.CampaignHub&&
            campaignHubPublishedRoot020!=null&&geometryRoot==campaignHubPublishedRoot020&&campaignHubPublishedRoot020.gameObject.activeInHierarchy&&
            PortalLandingPrototypeReady020&&PortalLandingLayout020.Active(campaignHubAdmission020)&&campaignHubAdmission020.Generation==PortalLandingPrototypeGeneration020&&
            transform.localToWorldMatrix==Matrix4x4.identity;
        public event Action<DungeonWorld> WorldModeChanged020;

        public bool CommitCampaignHub020(in LandingAdmission020 admission)
        {
            if(!built||worldRevision020==uint.MaxValue||!PortalLandingPrototypeReady020||
                !WorldModeRules020.InitialAdmission(admission,PortalLandingPrototypeGeneration020)||
                transform.localToWorldMatrix!=Matrix4x4.identity||!TryGetPortalLandingPrototype020(admission.Generation,out PortalLandingWorldSnapshot020 snapshot)||
                snapshot.Root==null||!snapshot.Root.IsChildOf(transform)||snapshot.Generation!=admission.Generation||
                !FiniteModePoint020(snapshot.SpawnFeet)||!FiniteModePoint020(snapshot.Shrine)||!FiniteModePoint020(snapshot.HomePortal)||
                !FiniteModeNumber020(snapshot.SpawnYaw)||snapshot.MapRooms==null||snapshot.MapRooms.Length<1||snapshot.MapRooms.Length>128||
                !FiniteModeNumber020(snapshot.MapBounds.x)||!FiniteModeNumber020(snapshot.MapBounds.y)||!FiniteModeNumber020(snapshot.MapBounds.width)||!FiniteModeNumber020(snapshot.MapBounds.height)||
                snapshot.MapBounds.width<=0f||snapshot.MapBounds.height<=0f)return false;
            // Snapshot publication is one cold main-thread transaction; no callback or yield sees partially replaced anchors.
            worldPublicationReady020=false;
            if(tutorialRoot!=null)tutorialRoot.gameObject.SetActive(false);
            if(arenaRoot!=null)arenaRoot.gameObject.SetActive(false);
            snapshot.Root.gameObject.SetActive(true);geometryRoot=snapshot.Root;
            PlayerSpawn=snapshot.SpawnFeet;ShrinePosition=snapshot.Shrine;PortalPosition=snapshot.HomePortal;
            EnemySpawns=Array.Empty<Vector3>();InvasionPortals=Array.Empty<Vector3>();TowerBuildPads=Array.Empty<Vector3>();LanePlatformPositions=Array.Empty<Vector3>();
            MapRooms=snapshot.MapRooms;MapBounds=snapshot.MapBounds;MapBoundary=Array.Empty<Vector2>();arenaLanes.Reset(0);
            campaignHubAdmission020=admission;campaignHubPublishedRoot020=snapshot.Root;campaignHubSpawnYaw020=snapshot.SpawnYaw;
            campaignHubFarClip020=Mathf.Clamp(snapshot.MapBounds.size.magnitude+16f,120f,260f);
            worldMode020=WorldMode020.CampaignHub;Physics.SyncTransforms();PublishWorldMode020();
            GameSession.Instance?.Audio?.RebuildAcousticWorld015(transform);return true;
        }
        void HideCampaignHubMode020()
        {
            HidePortalLandingPrototype020();campaignHubAdmission020=default;campaignHubPublishedRoot020=null;
            campaignHubSpawnYaw020=campaignHubFarClip020=0f;
        }
        void PublishWorldMode020()
        {
            if(worldRevision020==uint.MaxValue){worldMode020=WorldMode020.None;throw new InvalidOperationException("World mode revision is exhausted.");}
            worldRevision020++;worldPublicationReady020=true;if(worldMode020==WorldMode020.CampaignHub)BindCampaignAssets020();WorldModeChanged020?.Invoke(this);
        }
        void DisposeWorldMode020()
        {
            worldPublicationReady020=false;worldMode020=WorldMode020.None;campaignHubAdmission020=default;campaignHubPublishedRoot020=null;
            if(worldRevision020<uint.MaxValue)worldRevision020++;
            try{WorldModeChanged020?.Invoke(this);}
            finally{WorldModeChanged020=null;DisposePortalLandingPrototype020();}
        }
        static bool FiniteModeNumber020(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool FiniteModePoint020(Vector3 value)=>FiniteModeNumber020(value.x)&&FiniteModeNumber020(value.y)&&FiniteModeNumber020(value.z);
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Builder-validated world metres/Y-up with identity owning transform; borrowed map rectangles are drawing metadata only.
// Array Formats: One explicit mode/revision/admission/root lease and borrowed immutable builder snapshot; no new spatial container or endpoint grants.
// Dependencies/Pointers: Actual prototype Ready/generation/root/support own admission. Mode/anchors/map publish before the observer event. Tutorial/arena rebuilds restore their own data; atmosphere/NPC consumers hold independent positive-generation leases.
