using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class PlacedElementShrine
    {
        // Cold accepted model/tier/lifetime boundaries. No new collision or gameplay ownership.
        bool RegisterXpBody015()
        {
            if(session==null||session.World==null||IsDestroyed||!gameObject.activeInHierarchy)return false;
            Bounds envelope=default;
            if(towerModel!=null)
            {
                if(TowerModelClearanceProfiles.TryEnvelope015(Element,towerModel.AcceptedTier,out float radius,out float lower,out float upper)&&
                    transform.lossyScale==Vector3.one&&transform.up==Vector3.up)
                {
                    Vector3 p=transform.position;
                    envelope.SetMinMax(p+new Vector3(-radius,lower,-radius),p+new Vector3(radius,upper,radius));
                }
                // Missing template profile or unsupported root basis registers an invalid box and disables merging.
            }
            else if(healthVisuals!=null)
            {
                bool found=false;
                for(int i=0;i<healthVisuals.Length;i++)
                {
                    Renderer renderer=healthVisuals[i];if(renderer==null||!renderer.enabled)continue;
                    if(!found){envelope=renderer.bounds;found=true;}else envelope.Encapsulate(renderer.bounds);
                }
                // Original fallback crystal pulse is <=12%; .3m per side exceeds its bounded displacement.
                if(found)envelope.Expand(.6f);
            }
            return session.World.TryRegisterXpTower015(EntityId.ToULong(GetEntityId()),transform,envelope,true);
        }
        internal static bool RebuildXpBodies015(DungeonWorld world,out int expected,out int accepted)
        {
            expected=accepted=0;var current=GameSession.Instance;
            if(world==null||current==null||current.World!=world||registered.Length!=MaximumRegisteredTowers)return false;
            bool valid=true;
            for(int i=0;i<registered.Length;i++)
            {
                var tower=registered[i];if(tower==null||!tower.gameObject.activeInHierarchy||tower.IsDestroyed)continue;
                expected++;bool duplicate=false;
                for(int j=0;j<i;j++)if(registered[j]==tower){duplicate=true;break;}
                if(duplicate||tower.session!=current||tower.session.World!=world){valid=false;continue;}
                if(tower.RegisterXpBody015())accepted++;else valid=false;
            }
            return valid&&accepted==expected;
        }
        void RemoveXpBody015()
        {if(session!=null&&session.World!=null)session.World.RegisterXpTower015(EntityId.ToULong(GetEntityId()),null,default,false);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Canonical unscaled Y-up tower root; complete recipe recoil/core/rotor yaw envelopes in world metres. Collider-free tower bodies remain solid for XP clearance.
// Array Formats: Existing cold profile bands -> one fixed World tower proxy; no per-frame renderer lookup or new Collider.
// Dependencies/Pointers: Existing initialize/tier/enable/disable/destruction acoustic hook points also register/remove this independent XP body. Missing profile or unsupported root fails closed; cosmetic outgoing projectiles are not structural solids.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world tower envelopes, canonical unscaled Y-up roots and current identity/lifetime authority.
// Array Formats: Eight accepted proxies plus eight bounded rejected ulong IDs, scalar unknown/current counts; cold complete maximum-four accepted registry reconstruction.
// Dependencies/Pointers: Only matching successful registration/removal clears an identified rejection. Invalid-ID/overflow UNKNOWN clears only after validated canonical cold inventory; unsupported/incomplete geometry remains0. Native jobs/XP claims/economy unchanged; root native recovery proof pending.
