using System;

namespace OdysseyOracle
{
    public sealed partial class TutorialDirector
    {
        [Serializable]
        public struct Rapid150Readback
        {
            public bool Active, Finished, CompletedThroughPractice, NovaTrialOwned, NovaImpactAccepted;
            public int Lesson, GuideIndex, TeachingStep, InitialCasts, InitialResolutions;
            public float SuccessHoldRemaining;
            public string Heading, Instruction, Caption, AudioClip;
        }

        // Cold diagnostic readback. No lesson, clock, HP, recovery or reward setter
        // is exposed; acceptance must still use the actual production event flow.
        public Rapid150Readback CaptureRapid150Readback()
        {
            return new Rapid150Readback
            {
                Active=active,Finished=Finished,CompletedThroughPractice=CompletedThroughPractice,
                Lesson=(int)CurrentLesson,GuideIndex=guideIndex,
                TeachingStep=novaTeachingStep,NovaTrialOwned=novaTrialOwned,
                NovaImpactAccepted=novaImpactAccepted,InitialCasts=novaInitialCastCount,
                InitialResolutions=novaInitialResolutionCount,SuccessHoldRemaining=novaSuccessHoldRemaining,
                Heading=Heading,Instruction=Instruction,Caption=Subtitle,
                AudioClip=voice!=null&&voice.clip!=null?voice.clip.name:""
            };
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: cold read-only tutorial presentation/owned-trial state, existing Unity world unchanged.
// Array Formats: one value snapshot with borrowed strings; no per-frame allocation or writable gameplay state.
// Dependencies/Pointers: native diagnostics observe production tutorial events; no private reflection or completion bypass.
