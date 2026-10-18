using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    // Main-thread boundary only. The DOM bridge records intent; it never asks
    // for lock outside a trusted canvas/button gesture and never focuses a window.
    internal static class OracleBrowserPointerCapture
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void OracleSetPointerCaptureWanted(int wanted);
#endif
        public static void SetWanted(bool wanted)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OracleSetPointerCaptureWanted(wanted ? 1 : 0);
#endif
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: browser canvas, not OS desktop or world space.
// Array Formats: scalar capture intent; no owned buffers or frame-loop heap allocations.
// Dependencies/Pointers: GameSession calls the matching template bridge on its main thread; no second input owner.
