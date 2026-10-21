using System;

namespace OdysseyOracle
{
    public static class CampaignWaveDeadline015
    {
        public static double AdmissionCutoff(int globalWave,double waveStartedAt)
        {
            Validate(globalWave,waveStartedAt);
            double scheduled=waveStartedAt+CampaignStagePacing.WaveSeconds(globalWave)+CampaignStagePacing.WaveGraceSeconds;
            double latest=CampaignStagePacing.MaximumStageSeconds-CampaignTravel015.TargetSeconds(globalWave)-CampaignStagePacing.WaveGraceSeconds;
            return Math.Min(scheduled,latest);
        }
        public static double ClearDeadline(int globalWave,double waveStartedAt,double lastAcceptedAt)
        {
            Validate(globalWave,waveStartedAt);
            if(!Finite(lastAcceptedAt)||lastAcceptedAt<waveStartedAt||lastAcceptedAt>AdmissionCutoff(globalWave,waveStartedAt))
                throw new ArgumentOutOfRangeException(nameof(lastAcceptedAt));
            double minimum=waveStartedAt+CampaignStagePacing.WaveSeconds(globalWave);
            double allowance=lastAcceptedAt+CampaignTravel015.TargetSeconds(globalWave)+CampaignStagePacing.WaveGraceSeconds;
            // Eligibility guarantees this bound; never clip a newly accepted actor's allowance.
            double deadline=Math.Max(minimum,allowance);
            if(deadline>CampaignStagePacing.MaximumStageSeconds)throw new InvalidOperationException("No bounded accepted-wave deadline.");
            return deadline;
        }
        static void Validate(int globalWave,double waveStartedAt)
        {
            // A valid late clear may schedule the next intermission beyond the cap.
            // It has no eligible admission interval and must fail, rather than throw.
            if(!CampaignBalance015.ValidWave(globalWave)||!Finite(waveStartedAt)||waveStartedAt<0d||
                waveStartedAt>CampaignStagePacing.MaximumStageSeconds+CampaignStagePacing.InterWaveSeconds)
                throw new ArgumentOutOfRangeException();
        }
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual accepted wave start/last admission in host milestone simulation seconds;295second hard cap retained.
// Array Formats: Scalar bounded deadline functions; no Unity Object access, allocation, arrays or native resources.
// Dependencies/Pointers: Existing wave windows/grace and integrated20/10/40 travel targets; caller must refuse admissions beyond cutoff before pool acquisition, never shorten accepted transit/grace.
