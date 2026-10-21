using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        UnityEngine.UI.Text compactGuideState020,instructionGuideState020;
        void BuildCompactGuideState020()
        {
            compactGuideState020=Label(tutorialPrompt,"CompactGuideState020","",12,Muted,TextAnchor.UpperCenter,
                new Vector2(-160,-40),new Vector2(300,20),new Vector2(.5f,1));
        }
        void RefreshGuideState020(TutorialDirector tutorial)
        {
            if(tutorial==null)return;
            if(compactGuideState020!=null)
            {
                bool show=tutorial.HasGuidePresentation015;
                if(compactGuideState020.gameObject.activeSelf!=show)compactGuideState020.gameObject.SetActive(show);
                if(show&&compactGuideState020.text!=tutorial.GuideCounterStatus020)compactGuideState020.text=tutorial.GuideCounterStatus020;
            }
            if(instructionGuideState020!=null&&currentPanel==JourneyPanel.TutorialInstruction&&instructionGuideState020.text!=tutorial.GuideCounterStatus020)
                instructionGuideState020.text=tutorial.GuideCounterStatus020;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted Canvas; compact counter/status occupies the left half of the second hint row.
// Array Formats: Two cold text references, immutable label references and scalar visibility comparisons only.
// Dependencies/Pointers: Existing guide state supplies all text; no Update formatting/new collection, no audio/progression/pointer action. Root verifies fitting/pixels/browser behavior.
