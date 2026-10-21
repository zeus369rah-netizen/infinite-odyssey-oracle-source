using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        HeldFireIntent015 localHeld015;
        uint localHeldSequence015;
        bool heldRequest015;
        int heldRoute015; // 0 local,1 campaign,2 shared lab; cancellation never migrates routes.
        public static int ReadPhysicalElementEdges015()
        {
            var keyboard=Keyboard.current;if(keyboard==null)return 0;
            int mask=0;for(int e=0;e<4;e++)if(keyboard[AbilityBindings.GetKey(e)].wasPressedThisFrame)mask|=1<<e;
            return mask;
        }
        public bool HeldFireContext015
        {
            get
            {
                var s=GameSession.Instance;
                return Application.isFocused&&s!=null&&s.IsPlaying&&s.CombatUnlocked&&Mouse.current!=null&&Mouse.current.leftButton.isPressed&&
                    Cursor.lockState==CursorLockMode.Locked&&!AbilityBindings.IsRebinding&&Time.frameCount>AbilityBindings.LastChangeFrame&&
                    (s.Builder==null||!s.Builder.IsPlacing)&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject());
            }
        }
        public bool TrySelectHeldElements015(int freshMask)
        {
            if(!HeldFireContext015||!ConfirmedSelection.ValidMask(freshMask))return false;
            CancelHeldFire015();var s=GameSession.Instance;
            if(s.SharedTrainingLab!=null&&s.SharedTrainingLab.ContextActive)
            {heldRoute015=2;heldRequest015=s.SharedTrainingLab.RequestHeldFire015(freshMask);return heldRequest015;}
            if(s.CoopMode&&s.Defense!=null&&s.Defense.Running)
            {heldRoute015=1;heldRequest015=s.Defense.RequestHeldFire015(freshMask);return heldRequest015;}
            if(localHeldSequence015==uint.MaxValue)return false; // fail closed for a local lifecycle.
            uint seq=++localHeldSequence015;
            if(!localHeld015.Begin(freshMask,seq,abilityClock)||!RapidCastRules015.FreshSelection(ref selection,freshMask))return false;
            autoSelectionWait.NotifyInput(abilityClock);SelectedSpell=selection.PreviewElement;
            s.Progression?.SetSelectedElementDirect(SelectedSpell);RefreshSelectionStatus();
            TickHeldFire015(true);return true;
        }
        void TickHeldFire015(bool held)
        {
            if(!held){CancelHeldFire015();return;}
            var s=GameSession.Instance;
            if(heldRequest015)
            {
                if(s==null||heldRoute015==1&&(!s.CoopMode||s.Defense==null||!s.Defense.Running)||
                    heldRoute015==2&&(s.SharedTrainingLab==null||!s.SharedTrainingLab.ContextActive))CancelHeldFire015();
                return;
            }
            if(!localHeld015.Active)return;
            if(!localHeld015.Valid(selection.ConfirmedMask,localHeldSequence015,abilityClock)){localHeld015.Clear();return;}
            if(!Playing||!selection.Ready(abilityClock,primaryReadyAt,elementReadyAt))return;
            localHeld015.Clear();
            if(TryCastConfirmed(false))
            {selection.Clear();autoSelectionWait.Clear();RefreshSelectionStatus();}
        }
        void CancelHeldFire015()
        {
            localHeld015.Clear();
            if(!heldRequest015)return;
            heldRequest015=false;var s=GameSession.Instance;int route=heldRoute015;heldRoute015=0;
            if(route==1)s?.Defense?.CancelHeldFire015();else if(route==2)s?.SharedTrainingLab?.CancelHeldFire015();
        }
        void ResetHeldFire015(){localHeld015.Clear();heldRequest015=false;heldRoute015=0;localHeldSequence015=0;}
        void OnApplicationFocus(bool focused){if(!focused)CancelHeldFire015();}
        void OnApplicationPause(bool paused){if(paused)CancelHeldFire015();}

        // Bounded presentation/pool adapter, not a target/spatial kernel. Three free
        // slots are reserved before any Launch; ordinary active flights are not recycled.
        public bool TryLaunchThree015(Vector3 origin,Vector3 aim,int mask,float d0,float d1,float d2,float d3,int firstShotId,int statusMask)
        {
            if(!Playing||!initialized||SpellIngredientRules.Count(mask)!=3||!FinitePosition(origin)||!FiniteDirection(aim)||
                firstShotId<0||firstShotId>int.MaxValue-2||!ValidScale015(d0)||!ValidScale015(d1)||!ValidScale015(d2)||!ValidScale015(d3))return false;
            if(projectiles.Count>48)return false;
            ulong occupied=0;
            for(int i=0;i<projectiles.Count;i++)
            {
                if(projectiles[i]==null||projectiles[i].Flying)occupied|=1ul<<i;
            }
            if(!RapidCastRules015.ReserveThree(occupied,projectiles.Count,projectileCursor,out int a,out int b,out int c))return false;
            Vector3 direction=aim.normalized;
            try
            {
                LaunchReserved015(a,RapidCastRules015.ElementAt(mask,0),origin,direction,d0,d1,d2,d3,firstShotId,statusMask);
                LaunchReserved015(b,RapidCastRules015.ElementAt(mask,1),origin,direction,d0,d1,d2,d3,firstShotId==0?0:firstShotId+1,statusMask);
                LaunchReserved015(c,RapidCastRules015.ElementAt(mask,2),origin,direction,d0,d1,d2,d3,firstShotId==0?0:firstShotId+2,statusMask);
            }
            catch
            {
                // Only previously inactive slots belong to this transaction. No hit
                // callback runs in Launch/OnEnable; collision belongs to later Update.
                projectiles[a].Retire();projectiles[b].Retire();projectiles[c].Retire();return false;
            }
            projectileCursor=(c+1)%projectiles.Count;return true;
        }
        static bool ValidScale015(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x)&&x>0f;
        void LaunchReserved015(int slot,int element,Vector3 origin,Vector3 aim,float d0,float d1,float d2,float d3,int id,int status)
        {
            float damage=element==0?d0:element==1?d1:element==2?d2:d3;
            float speed=element==3?32f:element==2?17f:740f/UnitsPerMetre;
            projectiles[slot].Launch((OracleProjectile.Kind)((int)OracleProjectile.Kind.Fire+element),origin,aim,speed,MissileRange,
                element==2?.19f:.11f,elementMaterials[element],damage,0,id,false,statusMask:status&0xFFF,ingredientMask:1<<element);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.
