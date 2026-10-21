using System.Runtime.InteropServices;
using UnityEngine;
namespace OdysseyOracle
{
    public static class GuideAudioAdmission015
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int OracleGuide015_AudioState();
#endif
        public static int ReadState()
        {
            if(!Application.isFocused)return 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            return OracleGuide015_AudioState();
#else
            return 7; // Native UI path has no browser unlock; focus/volume/pause still rechecked by the sole narrator.
#endif
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Read-only browser/native focus/gesture/audio-context admission; no world coordinates.
// Array Formats: Three scalar bits:1focused-visible,2real sticky userActivation,4installed Unity audio context running.
// Dependencies/Pointers: No context creation/resume, event spoofing, focus/cursor/Terms or saved consent writes. Native/browser audio execution remains root verification.
