using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        bool altMouseHeld020,altMouseOwner020,exitPending020;
        int exitGeneration020;
        uint exitRevision020;
        string exitError020="";
        public bool ExitPending020=>exitPending020;
        public uint ExitRevision020=>exitRevision020;
        public string ExitError020=>exitError020;
        bool AltPressed020=>Keyboard.current!=null&&(Keyboard.current.leftAltKey.isPressed||Keyboard.current.rightAltKey.isPressed);

        // Preserve only an already visible builder through canonical Alt and browser re-capture.
        public bool BuildPickerRetained020
        {
            get
            {
                if(!Application.isFocused||!HasStarted||!WorldControlReady020||Victory||IsDead||IsDowned||SoloRestartHoldsSimulation||
                    !string.IsNullOrEmpty(FailureReason)||Loading!=null&&Loading.IsLoading||Opening!=null&&(Opening.IsTransitioning||Opening.IsIntroPlaying)||
                    Hud==null||!Hud.CanShowMouse015||!Hud.CompactBuildPickerVisible015||Builder==null||!Builder.IsPlacing)return false;
                return LaneBuildPreviewRules020.Retain(IsPlaying,Paused,AltPressed020||altMouseHeld020,CursorFree015,Cursor.lockState==CursorLockMode.Locked,
                    AwaitingBrowserPointerCapture,Time.frameCount<=pointerCaptureGuardFrame);
            }
        }
        public bool BuildPickerPointer020=>BuildPickerRetained020&&!Paused&&CursorFree015&&altMouseOwner020&&AltPressed020;

        void UpdateMenuInput020()
        {
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame&&!cursorMenu015.SuppressPauseEscape020(Time.frameCount))
            {
                if(Hud!=null&&Hud.TryEscapeMenu015()) { }
                else if(HasStarted&&!Victory&&!IsDead)Pause();
            }
            if(AltPressed020)
            {
                altMouseHeld020=true;
                if(!altMouseOwner020&&!CursorFree015&&!Paused&&TryShowMouse015())altMouseOwner020=true;
            }
            else if(altMouseHeld020)
            {
                altMouseHeld020=false;bool owned=altMouseOwner020;altMouseOwner020=false;
                if(owned&&CursorFree015&&!Paused&&Application.isFocused&&Hud!=null&&Hud.CanShowMouse015)Resume();
            }
        }
        void CancelAltMouse020(){altMouseHeld020=altMouseOwner020=false;}
        void CancelExitRequest020(){exitGeneration020++;exitPending020=false;exitError020="";exitRevision020++;}

        public async void ExitGame020()
        {
            if(exitPending020||idleShutdownStarted||idleDestroyed)return;
            exitPending020=true;exitError020="";exitRevision020++;int generation=++exitGeneration020;
            Pause();var rooms=Rooms;
            try
            {
                Task<string> cleanup=rooms!=null?rooms.LeaveRoomAsync():Task.FromResult<string>(null);
                int roomGeneration=rooms!=null?rooms.InactivityLifecycleGeneration:0;
                string failure=await cleanup;
                if(this==null||Instance!=this||generation!=exitGeneration020)return;
                if(!ReferenceEquals(Rooms,rooms)||!ReferenceEquals(rooms,null)&&
                    (!rooms.InactivityOwnerAlive||rooms.InactivityLifecycleGeneration!=roomGeneration||rooms.Connected||rooms.Busy))
                    throw new InvalidOperationException("The game connection changed. Exit was cancelled.");
                if(failure!=null)throw new InvalidOperationException(failure);
                PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
                if(OracleExit020_Ready(generation,gameObject.name)==0)throw new InvalidOperationException("The website exit bridge is unavailable. Reload the matching game page.");
#elif !UNITY_EDITOR
                Application.Quit();
#else
                // Editor verification may inspect readiness; it must never quit the user's Editor.
                exitPending020=false;exitRevision020++;
#endif
            }
            catch(Exception error)
            {
                if(this==null||generation!=exitGeneration020)return;
                exitPending020=false;exitError020=error.Message;exitRevision020++;
                Debug.LogWarning("Game exit did not finish: "+error.Message);
            }
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int OracleExit020_Ready(int generation,string ownerName);
#endif
        public void BrowserExitFailure020(string value)
        {
            if(this==null||Instance!=this||!exitPending020||!int.TryParse(value,
                System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int generation)||
                generation<=0||generation!=exitGeneration020)return;
            exitPending020=false;
            exitError020="The game could not close. Try again, or return to the lobby.";
            exitRevision020++;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Local real InputSystem edges, existing menu frames and owned connection identity; no world writes.
// Array Formats: Two Alt flags, bounded pause frame state, one cold async exit command/generation/revision/error.
// Dependencies/Pointers: Existing Pause/Resume and trusted browser capture retain authority. Existing room cleanup and preference flush precede public Quit; changed/disabled/restarted owners cancel stale completion. Root verifies actual browser/native behavior.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Alt mouse-release and trusted browser re-capture/guard interval for one already visible picker.
// Array Formats: Read-only scalar predicate; no new pointer owner, capture request, state store, hot string or event.
// Dependencies/Pointers: Existing GameSession owns capture/pause. Focus/modal/world/terminal checks fail closed. Pointer scope permits local selection only; world submissions still require genuine IsPlaying and locked cursor.
