using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        const string CandleCrackleCue015 = "candle_soft_crackle015";
        AudioClip refugeBookOpen015, refugeBookClose015, refugePotBreak015, refugeCrystalBreak015;
        bool refugeSoundsPrepared015;
        float refugeNextBook015, refugeNextBreak015;
        bool RefugeAmbientAllowed015(string cue) => cue != CandleCrackleCue015 && cue != WindowDarkCue015 && cue != WindowBrightCue015 || GuideGestureObserved015;

        void PrepareRefugeSounds015()
        {
            if (refugeSoundsPrepared015) return;
            refugeSoundsPrepared015 = true;
            PrepareWindExtinguish020();
            refugeBookOpen015 = Resources.Load<AudioClip>("Audio/RefugeSound015/book_open015");
            refugeBookClose015 = Resources.Load<AudioClip>("Audio/RefugeSound015/book_close015");
            refugePotBreak015 = Resources.Load<AudioClip>("Audio/RefugeSound015/ceramic_pot_break015");
            refugeCrystalBreak015 = Resources.Load<AudioClip>("Audio/RefugeSound015/crystal_break015");
            AudioClip candle = Resources.Load<AudioClip>("Audio/RefugeSound015/candle_soft_crackle015");
            Prime(refugeBookOpen015); Prime(refugeBookClose015); Prime(refugePotBreak015); Prime(refugeCrystalBreak015); Prime(candle);
            namedClips[CandleCrackleCue015] = candle; namedNext[CandleCrackleCue015] = 0;
        }

        // Explicit real book open/close action only; no callback, replay queue, or music manipulation.
        public bool TryPlayBookSurface015(bool opening)
        {
            if (!isActiveAndEnabled || !CanPlayCue() || Time.unscaledTime < refugeNextBook015) return false;
            if (!PlayPrioritizedCue(opening ? refugeBookOpen015 : refugeBookClose015, ListenerPosition(), false, .045f, 1, .12f)) return false;
            refugeNextBook015 = Time.unscaledTime + .12f; return true;
        }

        // Caller already accepted/deduplicated the visible break; revision is not a global identity.
        public bool TryPlayRefugeBreak015(bool crystal, Vector3 position, uint revision)
        {
            if (!isActiveAndEnabled || !CanPlayCue() || session == null || !session.HasStarted || !session.SimulationActive ||
                session.IsDead || session.Victory || !Near(position, 30f) || Time.unscaledTime < refugeNextBreak015) return false;
            if (!PlayPrioritizedCue(crystal ? refugeCrystalBreak015 : refugePotBreak015, position, true, .065f, 1, .08f)) return false;
            refugeNextBreak015 = Time.unscaledTime + .08f; return true;
        }

        // Cold actor creation only. Existing32-record admission bounds simultaneous registered ambience.
        public bool RegisterCandleCrackle015(int id, Vector3 position, float gain = .025f, float range = 8f)
        {
            if (!initialized || !refugeSoundsPrepared015 || id == WindowDarkEmitter015 || id == WindowBrightEmitter015) return false;
            return SetAmbientEmitter(id, CandleCrackleCue015, position, true, range, gain);
        }
        public bool UpdateCandleCrackle015(int id, Vector3 position, float acceptedFuel, float gain = .025f)
        {
            if (!Finite(position) || float.IsNaN(acceptedFuel) || float.IsInfinity(acceptedFuel) || float.IsNaN(gain) ||
                float.IsInfinity(gain) || !ambientEmitters.TryGetValue(id, out var record) || record.Cue != CandleCrackleCue015) return false;
            record.Position = position; record.Gain = Mathf.Clamp(gain, 0f, .1f) * Mathf.Clamp01(acceptedFuel); return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Explicit accepted break/candle world anchors or actual bookUI/listener2D; no state/authority mutation.
// Array Formats: Five cold borrowed clips, two scalar one-shot cadences, existing4 protected cue and4 nearest ambient voices/32 records.
// Dependencies/Pointers: Actual accepted actor/UI owner supplies once-only calls/fuel. No hot Resources/delegates/new sources, no frozen38-bank/JS or music change; Unity fallback only for unmapped new one-shots.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: accepted existing refuge world cue anchors.
// Array Formats: one additional cold borrowed resource beside existing refuge sounds; original prioritized4voice pool reused.
// Dependencies/Pointers: resource bytes/import/listening pending root; no per-node/frame cue or new AudioSource, production38bank/web bridge unchanged.
