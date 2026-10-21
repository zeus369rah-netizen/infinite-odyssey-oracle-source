using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using OdysseyOracle.Foundation.World;
#if UNITY_EDITOR
using LaneBuilderReload015=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using LaneBuilderReload015=System.Action;
#endif

namespace OdysseyOracle
{
    public sealed partial class ShrinePlacementBuilder
    {
        const string CantBuild015="Can’t build here—choose a flat block beside your lane.";
        static readonly string[] LaneReasons015={"",CantBuild015+" Building is unavailable.",CantBuild015+" Reopen the builder: context changed.",CantBuild015+" Outside the two-metre road-edge band.",
            CantBuild015+" This is a road or reserved path.",CantBuild015+" Water is not buildable.",CantBuild015+" Lava is not buildable.",CantBuild015+" The complete base needs flat, supported land.",
            CantBuild015+" A body, wall, prop or reserved approach blocks this block.",CantBuild015+" Stand within 25 metres.",CantBuild015+" Four shared towers are already active.",
            CantBuild015+" No placement tokens remain.",CantBuild015+" Only the owner can change this tower.",CantBuild015+" This request is stale.",CantBuild015+" Saved tower art is unavailable."};
        LaneBuildHighlightBatch015 laneBatch015;
        LaneBuilderReload015 laneBuilderReload015;bool laneBuilderSubscribed015;
        NativeArray<RaycastCommand> laneRay015;NativeArray<RaycastHit> laneRayHit015;JobHandle laneRayReader015;
        uint laneOpen015,laneRequested015,laneSemantic015;int laneEpoch015=-1,laneLocked015=-1,laneOwned015,laneOwnedRevision015,laneAimTower015,laneAimRevision015;
        Vector3 lanePoint015,laneMarker015,laneOwnedPoint015;bool laneHit015,laneLegal015,laneOpening015,laneWaiting015;
        string laneRun015="",laneChooseHint015="",lanePlaceHint015="",laneTowerHint015="";int laneBinding015=-100;
        double laneNextProbe015,laneNextGrid015;
        bool laneSnap020=true,laneDismissed020,laneWasAvailable020,laneHasFloor020;float laneFloor020;
        int laneWheelFrame020=-1;
        public bool LaneSnap020=>laneSnap020;
        public bool LanePickerRetained020=>Available&&Application.isFocused&&!AbilityBindings.IsRebinding&&!GameActionBindings.IsRebinding&&
            (session.IsPlaying||session.BuildPickerRetained020);
        bool LaneChoiceAvailable020=>LaneInputAvailable015||Available&&Application.isFocused&&!AbilityBindings.IsRebinding&&!GameActionBindings.IsRebinding&&session.BuildPickerPointer020;
        public bool LaneBuildContextValid015=>Available&&ModelsReady&&laneOpen015!=0&&laneLocked015>=0&&laneRun015==session.Defense.ForgeRunId&&
            laneEpoch015==session.Defense.CurrentLaneBuildEpoch015&&laneSemantic015==session.World.LaneBuildSemantic015&&(session.World.AdmissionLaneMask&(1<<laneLocked015))!=0;
        bool LaneInputAvailable015=>Available&&session.IsPlaying&&Application.isFocused&&!AbilityBindings.IsRebinding&&!GameActionBindings.IsRebinding&&Cursor.lockState==CursorLockMode.Locked;
        public bool LaneBuildWaiting015=>laneWaiting015;
        public int SelectedTowerType015=>selectedElement;
        bool laneRemoveSelected015;
        public bool LaneRemoveSelected015=>laneRemoveSelected015;
        const string LaneRemoveHint015="REMOVE OWN TOWER | No refund. Interact confirms; Escape closes.";
        string laneFeedback015="";
        const string LaneNoConfirmation015="No confirmation received. The tower and token display will update from the host. Reopen the builder before trying again.";
        const string LaneCancelled015="Build request cancelled locally. Any accepted tower and token changes will update from the host.";
        public string LaneBuildFeedback015=>laneFeedback015;
        string LaneHint015(string fallback)=>laneFeedback015.Length!=0?laneFeedback015:fallback;
        void SetLaneFeedback015(string text){laneFeedback015=text;if(text.Length!=0)Hint=text;}
        internal void OnLaneRequestEnded015(bool timedOut)
        {CancelPlacement();SetLaneFeedback015(timedOut?LaneNoConfirmation015:LaneCancelled015);}
        public bool LaneOwnedTowerActions015=>LaneBuildContextValid015&&laneOwned015>0;
        public int LaneLockedRoad015=>laneLocked015;
        public uint LaneContextNonce015=>laneOpen015;
        internal static string LaneReasonText015(LaneBuildReason015 reason)=>LaneReasons015[(int)reason<LaneReasons015.Length?(int)reason:1];
        void InitializeLaneBuilding015()
        {
            if(laneBatch015!=null)return;
            try
            {
                laneBatch015=new LaneBuildHighlightBatch015();
                laneRay015=new NativeArray<RaycastCommand>(1,Allocator.Persistent);laneRayHit015=new NativeArray<RaycastHit>(1,Allocator.Persistent);
                laneBuilderReload015=DisposeLaneBuilding015;LightPoolLifecycle.SubscribeBeforeReload(laneBuilderReload015);laneBuilderSubscribed015=true;
            }
            catch{DisposeLaneBuilding015();throw;}
        }
        bool UpdateLaneBuilding015()
        {
            // Local cancellation precedes pending retry, probe, rendering and world submission.
            bool cancelEdge=Application.isFocused&&(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame||Mouse.current!=null&&Mouse.current.rightButton.wasPressedThisFrame);
            if(cancelEdge){CancelPlacement();if(Available)laneWasAvailable020=true;return Available;}
            if(session!=null&&session.Defense!=null)
            {
                bool cancel=!LanePickerRetained020;
                session.Defense.TickLaneRequest015(cancel);
            }
            if(!Available){if(IsPlacing||laneRequested015!=0||laneOpen015!=0)CancelPlacement();laneWasAvailable020=false;laneBatch015?.Clear();return false;}
            if(!laneWasAvailable020){laneWasAvailable020=true;laneDismissed020=false;}
            if(laneBatch015==null||!laneBatch015.Ready){Hint=LaneReasonText015(LaneBuildReason015.Model);return true;}
            if(laneOpen015!=0&&!LaneBuildContextValid015)CancelPlacement();
            if(!LanePickerRetained020)
            {if(IsPlacing||laneRequested015!=0||laneOpen015!=0)CancelPlacement();if(ghost!=null)ghost.SetActive(false);return true;}
            int binding=Mathf.Max(AbilityBindings.LastChangeFrame,GameActionBindings.LastChangeFrame);
            if(laneBinding015!=binding)
            {laneBinding015=binding;string key=GameActionBindings.GetLabel("Interact");
                laneChooseHint015="GREEN FLOOR | ["+key+"] BUILD";lanePlaceHint015="Scroll chooses | ["+key+"] Place | RMB/Escape cancel | Hold Alt: logos / snap";
                laneTowerHint015="["+key+"] Owned tower: replace or remove";}
            if(IsPlacing&&LaneBuildContextValid015&&!laneWaiting015&&Mouse.current!=null&&
                LaneBuildPreviewRules020.TakeWheel(ref laneWheelFrame020,Time.frameCount,Mouse.current.scroll.ReadValue().y,LaneChoiceAvailable020,out int step))
            {
                int count=LaneOwnedTowerActions015?5:4,choice=LaneBuildPreviewRules020.Choice(laneRemoveSelected015?4:selectedElement,step,count);
                if(choice==4)TrySelectLaneRemove020();else TrySelectLaneType015(choice);
            }
            // Pointer UI changes only local choices. Capture must finish before world input.
            if(!LaneInputAvailable015){if(ghost!=null)ghost.SetActive(false);return true;}
            bool pressed=Keyboard.current!=null&&Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame;
            if(laneDismissed020&&!IsPlacing){if(pressed&&!laneWaiting015)TryOpenLaneBuilder015();return true;}
            bool claimed=session.FieldSmith!=null&&session.FieldSmith.ClaimsInteract||session.Revival!=null&&session.Revival.IsHelping;
            double now=Time.realtimeSinceStartupAsDouble;
            if(now>=laneNextProbe015||pressed)
            {
                laneNextProbe015=now+.1d;ProbeLanePoint015(now>=laneNextGrid015);if(now>=laneNextGrid015)laneNextGrid015=now+.5d;
            }
            if(claimed){if(IsPlacing)CancelPlacement();laneBatch015.Clear();if(ghost!=null)ghost.SetActive(false);return true;}
            if(laneRemoveSelected015&&LaneOwnedTowerActions015)Hint=LaneHint015(LaneRemoveHint015);
            bool preview=IsPlacing&&LaneBuildContextValid015&&!laneRemoveSelected015;
            bool budget=session.Defense.LocalBuildTokens>0&&session.Defense.PlacedCount-(laneOwned015>0?1:0)<session.Defense.ActiveTowerCapacity&&session.Defense.LocalCanBuildLaneType015(selectedElement);
            float diameter=session.World.LaneBuildEnvelope015(selectedElement,out float radius,out _)?radius*2:LaneBuildMath015.SoleRadius*2;
            laneBatch015.Draw(preview&&laneHasFloor020,(preview||!IsPlacing&&!laneDismissed020)&&laneHit015,laneMarker015,laneLegal015,diameter,budget);
            if(!IsPlacing)
            {if(ghost!=null)ghost.SetActive(false);if(pressed&&!laneWaiting015)TryOpenLaneBuilder015();return true;}
            if(ghost!=null)
            {
                ghost.transform.position=lanePoint015;ghost.transform.rotation=Quaternion.Euler(0,LaneBuildMath015.RoadFacingYaw(new float3(lanePoint015.x,lanePoint015.y,lanePoint015.z),laneLocked015),0);
                ghost.SetActive(preview&&laneHit015&&laneLegal015);
            }
            if(pressed&&Time.frameCount>suppressThroughFrame&&!laneWaiting015)
            {
                if(laneRemoveSelected015){if(LaneOwnedTowerActions015)TryRemoveOwnedLaneTower015();}
                else if(laneHit015&&laneLegal015)TrySubmitLaneBuild015(selectedElement,lanePoint015);
            }
            return true;
        }
        void ProbeLanePoint015(bool enumerate)
        {
            if(enumerate)laneBatch015.Clear();
            laneHit015=laneLegal015=laneHasFloor020=false;laneAimTower015=laneAimRevision015=0;Hint=LaneHint015("");
            if(session.Player==null||session.Player.ViewCamera==null||session.Rooms==null||!session.Rooms.TryGetPlayerPose(session.Rooms.LocalClientId,out Vector3 feet,out _)||
                !session.World.TryGetLaneBuildFloor020(out laneFloor020))return;
            laneHasFloor020=true;
            int lane=LaneBuildContextValid015?laneLocked015:LaneBuildMath015.NearestLane(new float3(feet.x,feet.y,feet.z),session.World.AdmissionLaneMask);
            // Borrow the same World predicate/results independent of camera aim.
            if(enumerate&&IsPlacing&&LaneBuildContextValid015)
            {
                session.World.EvaluateLaneBuild015(lanePoint015,feet,lane,selectedElement,laneOwned015,true,out _);
                laneBatch015.Publish(session.World.LaneBuildHighlights015,session.World.LaneBuildHighlightCount015,laneFloor020);
            }
            var eye=session.Player.ViewCamera.transform;
            laneAimTower015=session.World.PickOwnedLaneTower015(eye.position,eye.forward);
            if(laneAimTower015>0&&session.Defense.TryGetLocalOwnedTower015(laneAimTower015,out _,out laneAimRevision015)&&!IsPlacing){Hint=LaneHint015(laneTowerHint015);return;}
            if(!LaneBuildPreviewRules020.TryFloorRay(new float3(eye.position.x,eye.position.y,eye.position.z),new float3(eye.forward.x,eye.forward.y,eye.forward.z),laneFloor020,LaneBuildMath015.Reach,out float3 surface,out _)||
                !LaneBuildPreviewRules020.TryRoot(surface,laneSnap020,out float3 p))return;
            laneHit015=true;lanePoint015=new Vector3(p.x,p.y,p.z);laneMarker015=new Vector3(p.x,laneFloor020,p.z);
            laneRay015[0]=new RaycastCommand(eye.position,eye.forward,new QueryParameters(~(1<<2),false,QueryTriggerInteraction.Ignore,false),LaneBuildMath015.Reach);
            laneRayReader015=RaycastCommand.ScheduleBatch(laneRay015,laneRayHit015,1);laneRayReader015.Complete();var hit=laneRayHit015[0];
            LaneBuildReason015 reason=LaneBuildReason015.Uneven;
            // Elevated/side/occluding hits never become legal projected floor roots.
            if(hit.collider!=null&&LaneBuildPreviewRules020.Surface(hit.point.y,hit.normal.y,laneFloor020))
                laneLegal015=session.World.EvaluateLaneBuild015(lanePoint015,feet,lane,selectedElement,laneOwned015,false,out reason);
            if(session.Defense.LocalBuildTokens<=0){laneLegal015=false;reason=LaneBuildReason015.Tokens;}
            if(session.Defense.PlacedCount-(laneOwned015>0?1:0)>=session.Defense.ActiveTowerCapacity){laneLegal015=false;reason=LaneBuildReason015.Capacity;}
            if(!session.Defense.LocalCanBuildLaneType015(selectedElement)){laneLegal015=false;reason=LaneBuildReason015.Unavailable;}
            bool showReason=IsPlacing||LaneBuildMath015.InBand(p,lane);
            Hint=LaneHint015(showReason?(laneLegal015?(IsPlacing?lanePlaceHint015:laneChooseHint015):LaneReasonText015(reason)):"");
        }
        public bool TryOpenLaneBuilder015()
        {
            if(!Available||!ModelsReady||laneWaiting015||laneBatch015==null||!laneBatch015.Ready||session.Hud==null||!session.IsPlaying||!Application.isFocused||AbilityBindings.IsRebinding||GameActionBindings.IsRebinding||Cursor.lockState!=CursorLockMode.Locked||
                session.FieldSmith!=null&&session.FieldSmith.ClaimsInteract||session.Revival!=null&&session.Revival.IsHelping)return false;
            ProbeLanePoint015(false); // E/touch activation rechecks the current aimed owned tower, never the previous poll's target.
            if(!session.Rooms.TryGetPlayerPose(session.Rooms.LocalClientId,out Vector3 feet,out _))return false;
            int lane=LaneBuildMath015.NearestLane(new float3(feet.x,feet.y,feet.z),session.World.AdmissionLaneMask);if(lane<0)return false;
            CloseLaneContext015();SetLaneFeedback015("");laneRequested015=session.Defense.NextLaneBuildNonce015();if(laneRequested015==0)return false;
            laneRun015=session.Defense.ForgeRunId;laneLocked015=lane;laneWaiting015=true;laneOpening015=true;laneDismissed020=false;laneNextGrid015=laneNextProbe015=0;
            laneOwned015=laneAimTower015;laneOwnedRevision015=laneAimRevision015;
            // Local preview/input ownership begins now; no lease, token or tower is accepted here.
            IsPlacing=true;validPreview=false;suppressThroughFrame=Time.frameCount+1;
            if(!session.Defense.RequestLaneOpen015(laneRequested015,lane,laneOwned015,laneOwnedRevision015)){CancelPlacement();return false;}
            session.Hud.ShowBuildMenu();laneOpening015=false;return true;
        }
        internal void ApplyLaneResponse015(LaneBuildFrame64 frame)
        {
            if(frame.Kind==LaneBuildMath015.Context)
            {
                if(frame.Nonce!=laneRequested015||laneRun015!=session.Defense.ForgeRunId)return;laneWaiting015=false;
                if(frame.Result!=0){CancelPlacement();SetLaneFeedback015(LaneReasonText015((LaneBuildReason015)frame.Result));return;}
                SetLaneFeedback015("");
                laneOpen015=frame.Context;laneEpoch015=frame.Epoch;laneLocked015=frame.Lane;laneSemantic015=frame.Semantic;laneOwned015=frame.TowerId;laneOwnedRevision015=frame.TowerRevision;
                if(laneOwned015>0)session.Defense.TryGetLocalOwnedTower015(laneOwned015,out laneOwnedPoint015,out _);
                TrySelectLaneType015(selectedElement);
            }
            else if(frame.Kind==LaneBuildMath015.Receipt&&frame.Context==laneOpen015)
            {
                laneWaiting015=false;if(frame.Result==0){laneOpen015=laneRequested015=0;SetLaneFeedback015("");CancelPlacement();session.Hud?.HideBuildMenu();}else SetLaneFeedback015(LaneReasonText015((LaneBuildReason015)frame.Result));
            }
        }
        bool TrySelectLaneType015(int element)
        {
            if(!LaneChoiceAvailable020||!LaneBuildContextValid015||element<0||element>=4||laneWaiting015)return false;
            if(towerModelPreview==null||!towerModelPreview.SetSelectedType(element))return false;selectedElement=element;
            laneRemoveSelected015=false;
            IsPlacing=true;validPreview=false;suppressThroughFrame=Time.frameCount+1;laneNextGrid015=laneNextProbe015=0;Hint=lanePlaceHint015;return true;
        }
        public bool TryToggleLaneSnap020()
        {
            if(!LaneChoiceAvailable020||!LaneBuildContextValid015||laneWaiting015)return false;
            laneSnap020=!laneSnap020;laneNextGrid015=laneNextProbe015=0;suppressThroughFrame=Time.frameCount+1;return true;
        }
        public bool TrySelectLaneRemove020()
        {
            if(!LaneChoiceAvailable020||!LaneOwnedTowerActions015||laneWaiting015)return false;
            laneRemoveSelected015=true;suppressThroughFrame=Time.frameCount+1;return true;
        }
        public bool TrySubmitLaneBuild015(int element,Vector3 point)
        {
            if(!LaneInputAvailable015||!LaneBuildContextValid015||!IsPlacing||laneWaiting015||element!=selectedElement)return false;
            uint kind=laneOwned015>0?LaneBuildMath015.Replace:LaneBuildMath015.Build;SetLaneFeedback015("");laneWaiting015=true;
            if(!session.Defense.RequestLaneAction015(kind,laneOpen015,laneLocked015,laneSemantic015,point,element,laneOwned015,laneOwnedRevision015)){laneWaiting015=false;return false;}return true;
        }
        public bool TryRemoveOwnedLaneTower015()
        {
            if(!LaneInputAvailable015||!LaneOwnedTowerActions015||laneWaiting015)return false;SetLaneFeedback015("");laneWaiting015=true;
            if(!session.Defense.RequestLaneAction015(LaneBuildMath015.Remove,laneOpen015,laneLocked015,laneSemantic015,laneOwnedPoint015,0,laneOwned015,laneOwnedRevision015)){laneWaiting015=false;return false;}return true;
        }
        void CloseLaneContext015()
        {
            laneRemoveSelected015=false;laneDismissed020=true;laneHit015=laneLegal015=laneHasFloor020=false;
            session?.Defense?.ClearLaneRequest015();
            uint context=laneOpen015!=0?laneOpen015:laneRequested015;int lane=laneLocked015;
            uint semantic=laneSemantic015!=0?laneSemantic015:session!=null&&session.World!=null?session.World.LaneBuildSemantic015:0;
            laneOpen015=laneRequested015=laneSemantic015=0;laneOwned015=laneOwnedRevision015=0;laneEpoch015=laneLocked015=-1;laneWaiting015=laneOpening015=false;laneRun015="";
            laneBatch015?.Clear();if(context!=0&&session!=null&&session.Defense!=null)session.Defense.RequestLaneClose015(context,lane,semantic);
        }
        bool CanonicalLanePosition015(Vector3 point,out Vector3 canonical)
        {canonical=point;return session!=null&&session.World!=null&&session.World.LaneBuildSemantic015!=0&&LaneBuildMath015.Canonical(new float3(point.x,point.y,point.z),out _);}
        internal bool TryReplaceLaneTower015(int oldId,int newId,int type,Vector3 point,int tier,int revision,int lane)=>TryInstallLaneTower015(oldId,newId,type,point,tier,revision,lane);
        internal bool TryInstallLaneTower015(int oldId,int newId,int type,Vector3 point,int tier,int revision,int lane)
        {
            PlacedElementShrine old=null;
            if(!ModelsReady||type<0||type>=4||lane<0||lane>7||oldId>0&&(!placed.TryGetValue(oldId,out old)||old==null||old.IsDestroyed)||oldId==0&&placed.Count>=MaximumBuildings||placed.ContainsKey(newId)||!CanonicalLanePosition015(point,out _))return false;
            GameObject candidate=null;bool suspended=false,registeredNew=false;
            try
            {
                candidate=new GameObject(TowerNames[type]+" replacement");candidate.SetActive(false);candidate.transform.SetParent(session.World.transform,false);candidate.transform.position=point;
                candidate.transform.rotation=Quaternion.Euler(0,LaneBuildMath015.RoadFacingYaw(new float3(point.x,point.y,point.z),lane),0);
                var tower=candidate.AddComponent<PlacedElementShrine>();tower.Initialize(session,newId,type,green,gold,elementMaterials[type],towerModelLibrary,tier,revision,true);
                if(old!=null){old.gameObject.SetActive(false);suspended=true;placed.Remove(oldId);}placed.Add(newId,tower);registeredNew=true;candidate.SetActive(true);
                if(!PlacedElementShrine.TryGetRegistered(newId,out var accepted)||accepted!=tower||session.World.XpUnknownTowerRegistration015||session.World.XpRejectedTowerCount015!=0)throw new System.InvalidOperationException("Dormant tower activation failed.");
                if(old!=null){RemoveWorkshop015(oldId);Destroy(old.gameObject);}return true;
            }
            catch
            {
                if(candidate!=null){candidate.GetComponent<SavedTowerModelPresentation>()?.OnAcceptedDestroyed();candidate.SetActive(false);}if(registeredNew)placed.Remove(newId);if(suspended){placed[oldId]=old;old.gameObject.SetActive(true);}if(candidate!=null)Destroy(candidate);return false;
            }
        }
        private void OnDisable()
        {
            ClearWorkshop015();CancelPlacement(); // Canonical owner clears pending before best-effort non-spending Close.
            laneBatch015?.Clear();padHighlights?.Apply(padStates,false);
        }
        void DisposeLaneBuilding015()
        {session?.Defense?.ClearLaneRequest015();laneRayReader015.Complete();if(laneBuilderSubscribed015){LightPoolLifecycle.UnsubscribeBeforeReload(laneBuilderReload015);laneBuilderSubscribed015=false;}if(laneRay015.IsCreated)laneRay015.Dispose();if(laneRayHit015.IsCreated)laneRayHit015.Dispose();laneBatch015?.Dispose();laneBatch015=null;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual camera-centre ray and accepted feet, .25m canonical roots on exactY0; locked nearest active road until close, original saved model pivots unchanged.
// Array Formats: One persistent batched ray/hit, fixed8192 instanced indicator store; cached key/reason text and64B context/actions, no per-cell Objects or repeated-frame strings.
// Dependencies/Pointers: Builder owns preview/resources only; host supplies accepted lease/transaction. Dormant replacement is inactive/unregistered until swap with rollback. Normal E focus/NPC/revive/modal gates preserved; native/browser/input acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current authenticated run/epoch/context and monotonic real-time pending request ages; placement/world coordinates unchanged.
// Array Formats: Immutable Frame64, fixed Pending96 scalar state and one cached Frame64 per existing peer; no per-frame heap container or ABI change.
// Dependencies/Pointers: Bounded same-nonce delivery, exact host replay and response-controlled UI only. Root alone imports/native-tests; frozen original packet remains unchanged.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current active Builder lifetime and existing run/epoch/nonce identities; no world or input coordinate changes.
// Array Formats: Existing Frame64/Pending96 plus one borrowed local Builder identity reference; no ABI/native/container allocation changes.
// Dependencies/Pointers: Canonical disable cancels pending/context and clears visuals; inactive/replaced lifetime cannot receive UI callbacks. Root alone imports/native-tests this tiny guarded layer.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing locked-cursor picker and host-owned tower context; no new pose, support or permission path.
// Array Formats: One local bool selects fifth owned-only Remove action; selectedElement remains0..3, exact existing Frame64 request/replay ABI unchanged.
// Dependencies/Pointers: Scroll only selects, E invokes canonical Remove with pending guard. Context close resets selection; no refund/new nonce/auto restoration or pointer relaxation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted metre/Y-up/XZ support roots and HUD Canvas; cosmetic floor boxes do not establish build permission.
// Array Formats: Existing fixed8192 preview records, one ray job, four cached original tower choices, unchanged Frame64 pending/replay transaction.
// Dependencies/Pointers: Local picker owns no pause/cursor/economy; accepted host result alone closes successful transaction. Normal input/native/Web rendering remain separate acceptance gates.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up/XZ canonical placement roots and local active-play input lifetime; no terrain or host permission changes.
// Array Formats: Existing fixed preview records, scalar context/request state and cached HUD controls; no new runtime store or hot allocation.
// Dependencies/Pointers: Local commands require the current play/input owner. Highlight count is invalidated behind its existing job fence before refresh; host acceptance remains authoritative.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Registered foundation plane for cosmetic grid/footprint, integer-centred1m cells, existing Y0/.25m host roots and road-facing pivots.
// Array Formats: Existing one persistent ray and8192 World results; scalar snap/dismiss/floor/wheel-frame state; no per-cell Objects or hot allocation.
// Dependencies/Pointers: Existing World evaluates full footprint independent of aim. Alt retains local picker only; E/build/remove still require IsPlaying+locked cursor and exact host lease/pending/cost/replay path. Close clears preview; Campaign and actor pools unchanged.
