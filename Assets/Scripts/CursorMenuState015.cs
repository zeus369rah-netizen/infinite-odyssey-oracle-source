namespace OdysseyOracle
{
    internal struct CursorMenuState015
    {
        public bool Active { get; private set; }
        int releaseFrame,pauseFrame020;bool pauseFrameValid020;
        public bool Release(int frame)
        {
            if(Active)return false;
            Active=true;releaseFrame=frame;return true;
        }
        public bool SuppressReleaseEscape(int frame)=>Active&&unchecked((uint)(frame-releaseFrame))<=1u;
        public void RecordPause020(int frame){pauseFrame020=frame;pauseFrameValid020=true;}
        public bool SuppressPauseEscape020(int frame)=>pauseFrameValid020&&unchecked((uint)(frame-pauseFrame020))<=1u;
        public void Reset(){Active=false;releaseFrame=0;pauseFrame020=0;pauseFrameValid020=false;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Main-thread input frame identity only; no world, DOM or permission state.
// Array Formats: One inline bool/int value; unsigned modular frame delta, no allocations or callbacks.
// Dependencies/Pointers: GameSession alone releases/resets; existing menu back and browser gesture/capture authority remain unchanged.
