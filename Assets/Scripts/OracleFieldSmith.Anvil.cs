using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    public sealed partial class OracleFieldSmith
    {
        int anvilLookFrame=-1;
        bool anvilLook;
        const string AnvilGuide="TOWER ANVIL\nThe four tier-one tower blueprints are included in a connected campaign. Opening the anvil records the Smith visit after the host confirms your reach; no purchase is required.\n\nEarn Forge charge during the stage. Return here after stage one or two to buy an optional tower upgrade using carried campaign charge. Upgrade purchases stay locked before stage one and throughout the arena.\n\nThis anvil never spends your permanent town wallet. Talk to the Smith himself to browse the separate solo town shop.";
        bool AnvilContext=>initialized&&session!=null&&session.HasStarted&&session.World!=null&&session.World.IsTutorialRefuge&&
            !session.World.IsCoopArena&&!session.IsDead&&!session.IsDowned&&!session.Victory&&
            (session.Defense==null||!session.Defense.Running)&&session.Player!=null&&session.Shop!=null&&session.Shop.Blacksmith!=null&&
            session.Shop.Blacksmith.TowerAnvilReady;
        // TownShop reads this claim before its own E poll: only the original anvil
        // collider belongs to FieldSmith; the original NPC body retains town shop input.
        public bool ClaimsAnvilInteraction=>AnvilContext&&LookingAtOriginalAnvil();
        bool CanOpenAnvilGuidance=>!ForgeActive&&ClaimsAnvilInteraction&&session.IsPlaying&&Application.isFocused&&
            Cursor.lockState==CursorLockMode.Locked&&!AbilityBindings.IsRebinding&&!GameActionBindings.IsRebinding&&
            (session.Builder==null||!session.Builder.IsPlacing)&&(session.Revival==null||!session.Revival.IsHelping)&&
            !session.Shop.Visible&&!Visible;
        bool LookingAtOriginalAnvil()
        {
            if(anvilLookFrame==Time.frameCount)return anvilLook;
            anvilLookFrame=Time.frameCount;anvilLook=false;
            var original=session.Shop.Blacksmith;
            Vector3 delta=session.Player.transform.position-original.TowerAnvilFeet;
            if(Mathf.Abs(delta.y)>=1.5f||delta.x*delta.x+delta.z*delta.z>ShopReach*ShopReach||session.Player.ViewCamera==null)return false;
            Transform eye=session.Player.ViewCamera.transform;
            anvilLook=Physics.Raycast(eye.position,eye.forward,out RaycastHit hit,LookReach,~(1<<2),QueryTriggerInteraction.Ignore)&&
                original.OwnsTowerAnvilCollider(hit.collider);return anvilLook;
        }
        bool TryOpenAnvilGuidance()
        {
            if(Time.frameCount==openedFrame||!CanOpenAnvilGuidance)return false;
            if(!session.Hud.TryShowRefugeNpcConversation(this,"Tower Anvil · Between-stage Forge",AnvilGuide))return false;
            openedFrame=Time.frameCount;session.Audio?.PlayUi();return true;
        }
        void TickAnvilGuidance()
        {
            if(session.Hud.IsRefugeNpcConversationVisible(this))
            {
                if(!AnvilContext||!session.Paused)HideAnvilGuidance();
                return;
            }
            if(CanOpenAnvilGuidance&&Keyboard.current!=null&&Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame)
                TryOpenAnvilGuidance();
        }
        void HideAnvilGuidance(){if(session!=null&&session.Hud!=null)session.Hud.HideRefugeNpcConversation(this);}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing grounded original anvil local(1.10,0,.12) -> world metres/Y-up; actual collider gates the sole interaction.
// Array Formats: Static guide text and borrowed references; one bounded main-thread ray, no per-frame text construction/native allocations.
// Dependencies/Pointers: FieldSmith alone polls anvil E; original TownShop body remains separate. Existing adapter/host phase fence alone spends charge; guidance writes no account/save/quest state.
