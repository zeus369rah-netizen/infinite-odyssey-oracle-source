using UnityEngine;

namespace OdysseyOracle
{
    // PSEUDOCODE: enter only after the published campaign atmosphere; reserve a
    // monotonic session epoch; prepare once for this exact world/root revision;
    // capture the real local controller, feed the shared held clock and actual
    // capsule to the GPU owner; retire coherently before allowing replacement.
    public sealed partial class GameSession
    {
        CampaignGardenFoliageLease020 campaignFoliage020;
        DungeonWorld campaignFoliageWorld020;
        Transform campaignFoliageWorldRoot020;
        OraclePlayer campaignFoliagePlayer020;
        CharacterController campaignFoliageController020;
        uint campaignFoliageGeneration020,campaignFoliageWorldRevision020,campaignFoliageGeometryRevision020;
        ulong campaignFoliageEpochHighWater020,campaignFoliageSequence020;
        int campaignFoliageRetiredFrame020=-1;
        bool campaignFoliageAttempted020,campaignFoliageRetireFailed020;
        void TickCampaignFoliage020()
        {
            if(!CampaignHubActive020||World==null||campaignAtmosphere020==null||!campaignAtmosphere020.Bound020||
                !World.TryGetPortalLandingPrototype020(World.CampaignHubGeneration020,out var snapshot)||snapshot.Root==null)
            {RetireCampaignFoliage020();return;}
            uint generation=World.CampaignHubGeneration020,revision=World.WorldRevision020,geometry=CampaignLandingObstacleSet020.Revision;
            if(!ReferenceEquals(campaignFoliageWorld020,World)||campaignFoliageWorldRoot020!=snapshot.Root||
                campaignFoliageGeneration020!=generation||campaignFoliageWorldRevision020!=revision||campaignFoliageGeometryRevision020!=geometry)
            {
                RetireCampaignFoliage020();if(campaignFoliage020!=null)return;
                campaignFoliageWorld020=World;campaignFoliageWorldRoot020=snapshot.Root;campaignFoliageGeneration020=generation;
                campaignFoliageWorldRevision020=revision;campaignFoliageGeometryRevision020=geometry;campaignFoliageAttempted020=false;
            }
            if(campaignFoliageRetireFailed020)return;
            if(!campaignFoliageAttempted020)
            {
                if(Time.frameCount<=campaignFoliageRetiredFrame020||campaignFoliageEpochHighWater020==ulong.MaxValue)return;
                campaignFoliageAttempted020=true;campaignFoliageEpochHighWater020++;campaignFoliageSequence020=0;
                campaignFoliage020=new CampaignGardenFoliageLease020();
                if(!campaignFoliage020.Prepare020(this,World,campaignFoliageEpochHighWater020,geometry))
                {
                    if(campaignFoliage020.CleanupBlocked020){campaignFoliageRetireFailed020=true;Debug.LogError("Foliage cleanup retained: "+campaignFoliage020.Failure020);return;}
                    RetireCampaignFoliage020();campaignFoliageAttempted020=true;return;
                }
            }
            if(campaignFoliage020==null||!campaignFoliage020.Ready020)return;
            if(campaignFoliagePlayer020!=Player)
            {
                // Only the cold actual-player replacement branch performs lookup.
                campaignFoliagePlayer020=Player;
                campaignFoliageController020=Player!=null?Player.GetComponent<CharacterController>():null;
            }
            Vector4 brush=Vector4.zero;Vector2 vertical=Vector2.zero;
            if(Player!=null&&campaignFoliageController020!=null&&campaignFoliageController020.enabled&&Player.gameObject.activeInHierarchy&&!IsDead)
            {
                Vector3 feet=Player.transform.position,scale=Player.transform.lossyScale;
                float radius=campaignFoliageController020.radius,height=campaignFoliageController020.height;
                if(float.IsFinite(feet.x)&&float.IsFinite(feet.y)&&float.IsFinite(feet.z)&&(scale-Vector3.one).sqrMagnitude<1e-6f&&
                    radius>0f&&radius<=.5f&&height>=radius*2f&&height<=2f)
                {brush=new Vector4(feet.x,feet.z,radius,1f);vertical=new Vector2(feet.y,height);}
            }
            if(campaignFoliageSequence020==ulong.MaxValue){RetireCampaignFoliage020();return;}campaignFoliageSequence020++;
            // Current offline campaign has exactly one admitted local player; slot1 is capacity, never a synthetic copy.
            var scope=campaignFoliage020.Scope020;
            float natureWind=TickCampaignNatureBinding020(scope,brush,vertical);
            campaignFoliage020.TickPresentation020(scope,campaignAtmosphere020.PresentationClock020,natureWind,
                !OracleBookAccessibility.PrefersReducedMotion,!IsPlaying||Paused||!Application.isFocused,
                Graphics!=null?Graphics.ParticleQuality:0,brush,vertical,Vector4.zero,Vector2.zero,campaignFoliageSequence020);
        }
        void RetireCampaignFoliage020()
        {
            if(campaignFoliage020!=null)campaignFoliage020.Hide020();
            if(!RetireCampaignNatureBinding020())return;
            if(campaignFoliage020==null)return;
            if(campaignFoliageRetireFailed020||campaignFoliage020.CleanupBlocked020){campaignFoliageRetireFailed020=true;return;}
            if(!campaignFoliage020.TryDispose020(out var failure))
            {campaignFoliageRetireFailed020=true;Debug.LogError("Foliage retirement retained: "+failure);return;}
            campaignFoliageRetiredFrame020=Mathf.Max(campaignFoliageRetiredFrame020,campaignFoliage020.RetiredFrame020);
            campaignFoliage020=null;campaignFoliageController020=null;campaignFoliagePlayer020=null;
        }
        internal void RetryCampaignFoliageRetirement020()
        {
            if(!RetryCampaignNatureBindingRetirement020())return;
            if(campaignFoliage020==null){campaignFoliageRetireFailed020=false;campaignFoliageAttempted020=false;return;}
            campaignFoliage020.Hide020();
            if(!campaignFoliage020.TryDispose020(out var failure)){campaignFoliageRetireFailed020=true;Debug.LogError("Foliage retry retained: "+failure);return;}
            campaignFoliageRetiredFrame020=Mathf.Max(campaignFoliageRetiredFrame020,campaignFoliage020.RetiredFrame020);
            campaignFoliage020=null;campaignFoliageRetireFailed020=false;campaignFoliageAttempted020=false;campaignFoliageController020=null;campaignFoliagePlayer020=null;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current admitted campaign World/root and actual local CharacterController feet/radius/height; slot2 stays inactive until an authoritative real-player adapter exists.
// Array Formats: One retained foliage lease, monotonic epoch/sequence and cached player/controller references. Active ticks allocate no managed/native arrays, geometry or Objects and only update bounded scalar shader state.
// Dependencies/Pointers: Hook runs after the existing atmosphere tick and reads its held4096s clock; no independent visual clock. Retire before atmosphere/tree invalidation; cleanup failure preserves the owner and blocks replacement until explicit cold Retry. Root must bind the separate contact worker/audio dispatcher using cold copied contacts and exact same scope.

