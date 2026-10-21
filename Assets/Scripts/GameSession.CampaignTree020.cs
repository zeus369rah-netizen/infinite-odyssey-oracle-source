using UnityEngine;
using Unity.Mathematics;
namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        TreeLifeLease020 campaignTree020;
        GameObject campaignTreeSolid020;
        uint campaignTreeGeneration020,campaignTreeRevision020;
        float campaignTreeClock020;
        bool campaignTreeAttempted020,campaignTreeRetireFailed020;
        public bool CampaignTreeReady020=>campaignTree020!=null&&campaignTree020.Ready;
        void TickCampaignTree020()
        {
            if(!CampaignHubActive020||World==null){RetireCampaignTree020();return;}
            uint generation=World.CampaignHubGeneration020,revision=World.WorldRevision020;
            if(campaignTreeGeneration020!=generation||campaignTreeRevision020!=revision)
            {RetireCampaignTree020();if(campaignTree020!=null)return;campaignTreeGeneration020=generation;campaignTreeRevision020=revision;}
            if(!campaignTreeAttempted020)
            {
                campaignTreeAttempted020=true;
                // One cold support admission in the actual published region; never infer a floor from map rectangles.
                var anchor=PortalLandingContour020.TreeFeet;Vector3 feet=new Vector3(anchor.X,anchor.Y,anchor.Z);
                if(!Physics.Raycast(feet+Vector3.up*2,Vector3.down,out RaycastHit hit,4,~0,QueryTriggerInteraction.Ignore)||
                    hit.collider==null||!hit.collider.transform.IsChildOf(World.transform)||Mathf.Abs(hit.point.y-feet.y)>.02f||hit.normal.y<.95f)return;
                feet.y=hit.point.y;
                var blockers=new Collider[16];
                int count=Physics.OverlapCapsuleNonAlloc(feet+Vector3.up*3.25f,feet+Vector3.up*4.75f,3.25f,blockers,~0,QueryTriggerInteraction.Ignore);
                if(count>=blockers.Length)return;
                for(int i=0;i<count;i++)if(blockers[i]!=hit.collider)return;
                campaignTree020=new TreeLifeLease020();
                var placement=new TreeLifePlacement020{Feet=new float3(feet.x,feet.y,feet.z),FinalHeight=42,ReservedRootRadius=4.5f,
                    Generation=generation,GeometryRevision=revision,Seed=0x4c494645u,Supported=1,Reserved=1};
                if(!campaignTree020.Prepare(World.transform,placement)){RetireCampaignTree020();campaignTreeGeneration020=generation;campaignTreeRevision020=revision;campaignTreeAttempted020=true;return;}
                campaignTreeSolid020=new GameObject("One Tree of Life solid trunk020");campaignTreeSolid020.transform.SetParent(World.transform,false);
                campaignTreeSolid020.transform.position=feet;var solid=campaignTreeSolid020.AddComponent<CapsuleCollider>();solid.radius=3.25f;solid.height=8;solid.center=Vector3.up*4;
                Physics.SyncTransforms();campaignTree020.SetVisible(generation,revision,true);
            }
            if(campaignTree020==null||!campaignTree020.Ready)return;
            bool moving=IsPlaying&&!Paused&&Application.isFocused&&!OracleBookAccessibility.PrefersReducedMotion;
            if(moving)campaignTreeClock020=Mathf.Repeat(campaignTreeClock020+Mathf.Clamp(Time.unscaledDeltaTime,0,.15f),1200);
            campaignTree020.SetHeldClock(campaignTreeClock020,OracleBookAccessibility.PrefersReducedMotion?0:.35f);
        }
        void RetireCampaignTree020()
        {
            if(campaignTreeRetireFailed020)return;
            if(campaignTreeSolid020!=null)campaignTreeSolid020.SetActive(false);
            if(campaignTree020!=null)
            {
                try{campaignTree020.SetVisible(campaignTreeGeneration020,campaignTreeRevision020,false);campaignTree020.Dispose();campaignTree020=null;}
                catch(System.Exception error){if(!campaignTreeRetireFailed020)Debug.LogError("Tree resource retirement retained: "+error.Message);campaignTreeRetireFailed020=true;return;}
            }
            if(campaignTreeSolid020!=null)Destroy(campaignTreeSolid020);campaignTreeSolid020=null;
            campaignTreeGeneration020=campaignTreeRevision020=0;campaignTreeClock020=0;campaignTreeAttempted020=campaignTreeRetireFailed020=false;
        }
        internal void RetryCampaignTreeRetirement020(){campaignTreeRetireFailed020=false;RetireCampaignTree020();}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual published unit World, supported lowest-floor anchor(0,-6,-62),42metre final tree and one3.25radius8metre trunk collider.
// Array Formats: One generation/revision-owned three-LOD mesh lease, one collider node, one held scalar clock;16-entry overlap array exists only during cold support admission.
// Dependencies/Pointers: Existing controller remains movement authority. Root numeric keepout leaves3.5metres to the party table and1.5metres to the south wall; actual support/clearance fails closed. Existing region retirement owns teardown; failed native disposal retains ownership and prevents replacement. No per-leaf Objects, saved quest grants, new lights or frame heap allocation.
