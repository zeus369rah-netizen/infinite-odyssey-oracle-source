using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OracleWebAudioBridge015 webAudio015;
#endif
        // Existing GameSession calls SetListenerTransform once after cold Audio
        // initialization. The listener anchor belongs to this session lifetime.
        void InitializeWebAudio015(Transform listener)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(listener==null||!acousticReady015||webAudio015!=null)return;
            var bridge=gameObject.AddComponent<OracleWebAudioBridge015>();
            if(bridge.Initialize(this,listener,AcousticOwnerRevision015))webAudio015=bridge;
            else Destroy(bridge); // Unready/unsupported keeps the native backend.
#endif
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing stable world listener anchor; browser boundary alone reflects Z.
// Array Formats: One cold cached bridge and existing eight voice leases/catalog; no per-frame component or Resource creation.
// Dependencies/Pointers: Session initializes audio then listener once; positive browser admission alone suppresses Unity playback. Bridge owns its separate context/DOM and teardown, no trusted gesture bypass.
