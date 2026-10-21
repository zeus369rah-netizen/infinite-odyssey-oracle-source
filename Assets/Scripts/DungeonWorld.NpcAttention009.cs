using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        void TickRaykonAttention009()
        {
            if(!IsTutorialRefuge||IsCoopArena||refugeRaykon==null||!Application.isFocused)return;
            var owner=GameSession.Instance;
            if(owner==null||!owner.HasStarted||owner.IsDead||owner.IsDowned||owner.Victory||owner.Player==null||owner.Player.ViewCamera==null)return;
            bool talking=owner.RefugeActivities!=null&&owner.Hud!=null&&owner.Hud.IsRaykonMentorVisible(owner.RefugeActivities);
            if(owner.Paused&&!talking)return;
            refugeRaykon.TickAttention009(owner.Player.ViewCamera.transform.position,Time.unscaledDeltaTime);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing supported tutorial-refuge Raykon and one actual local player's world eye.
// Array Formats: No new world buffers or NPC queries; one explicit cached lease call at existing World update boundary.
// Dependencies/Pointers: Own accepted mentor modal may animate while paused; unrelated pause/focus loss freezes; no raycast/search loop, guide ownership, economy, authority or room/environment change.
